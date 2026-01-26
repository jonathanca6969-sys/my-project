const EventEmitter = require('events');
const path = require('path');
const fs = require('fs');
const os = require('os');
const chokidar = require('chokidar');
const writeFileAtomic = require('write-file-atomic');

const RETRY_MAX = 5;
const RETRY_BASE_DELAY = 100;
const HEALTH_CHECK_INTERVAL = 30000;

class DataService extends EventEmitter {
  constructor(dataPath, localConfig) {
    super();
    this.dataPath = dataPath;
    this.localConfig = localConfig;
    this.machineName = os.hostname();

    // Local paths (per-machine)
    this.settingsPath = path.join(dataPath, 'settings.json');
    this.statePath = path.join(dataPath, 'state.json');
    this.backupPath = path.join(dataPath, 'backups');
    this.localFallbackPath = path.join(dataPath, 'shop_data_local.json');

    // Shared file state
    this.uncPath = localConfig.get('uncPath') || '';
    this.filePath = null;
    this.connected = false;
    this.watcher = null;
    this.healthCheckTimer = null;
    this.cache = this._getEmptyData();

    // Write serialization: chain promises to prevent concurrent writes
    this._writeQueue = Promise.resolve();

    this._initialize();
  }

  // ==================== Initialization ====================

  async _initialize() {
    // Load local fallback first (always available)
    this._loadLocalFallback();

    // Attempt shared connection if path is configured
    if (this.uncPath) {
      await this._tryConnect();
    }

    // Start health monitoring
    this._startHealthCheck();
  }

  async _tryConnect() {
    if (!this.uncPath) return;

    this.filePath = path.join(this.uncPath, 'shop_data.json');

    try {
      await this._readSharedFile();
      this.connected = true;
      this._startWatcher();
      this.emit('connection:restored');
    } catch (err) {
      console.error('DataService: Failed to connect to shared path:', err.message);
      this.connected = false;
      this.filePath = null;
    }
  }

  // ==================== Connection & Status ====================

  async testConnection(testPath) {
    const targetPath = testPath || this.uncPath;
    if (!targetPath) {
      return { success: false, error: 'No path specified' };
    }

    const start = Date.now();
    const testFile = path.join(targetPath, `.connection_test_${this.machineName}`);

    try {
      // Step 1: Check path exists and is accessible
      await fs.promises.access(targetPath, fs.constants.R_OK | fs.constants.W_OK);

      // Step 2: Write test file
      const testContent = `connection_test_${Date.now()}`;
      await fs.promises.writeFile(testFile, testContent, 'utf-8');

      // Step 3: Read it back
      const readBack = await fs.promises.readFile(testFile, 'utf-8');
      if (readBack !== testContent) {
        return { success: false, error: 'Read-back verification failed', latencyMs: Date.now() - start };
      }

      // Step 4: Delete test file
      await fs.promises.unlink(testFile);

      return { success: true, latencyMs: Date.now() - start };
    } catch (err) {
      // Clean up test file if it was created
      try { await fs.promises.unlink(testFile); } catch (_) { /* ignore */ }

      let error = err.message;
      if (err.code === 'ENOENT') error = 'Path does not exist or is not accessible';
      if (err.code === 'EPERM' || err.code === 'EACCES') error = 'Permission denied - check share permissions';
      if (err.code === 'ETIMEDOUT') error = 'Connection timed out - server may be offline';

      return { success: false, error, latencyMs: Date.now() - start };
    }
  }

  async setUncPath(newPath) {
    // Validate format
    if (newPath && !newPath.startsWith('\\\\') && !newPath.startsWith('//')) {
      return { success: false, error: 'Path must be a UNC path (start with \\\\ or //)' };
    }

    // Test connection if path provided
    if (newPath) {
      const result = await this.testConnection(newPath);
      if (!result.success) {
        return result;
      }
    }

    // Stop current watcher
    this._stopWatcher();

    // Update config
    this.uncPath = newPath || '';
    this.localConfig.set('uncPath', this.uncPath);

    if (this.uncPath) {
      this.filePath = path.join(this.uncPath, 'shop_data.json');
      // Connect and sync
      try {
        await this._readSharedFile();
        this.connected = true;
        this._startWatcher();
        this.emit('connection:restored');
        return { success: true };
      } catch (err) {
        this.connected = false;
        return { success: false, error: 'Connected but failed to read shared file: ' + err.message };
      }
    } else {
      this.filePath = null;
      this.connected = false;
      return { success: true };
    }
  }

  getStatus() {
    return {
      connected: this.connected,
      uncPath: this.uncPath,
      lastSync: this.cache._meta ? this.cache._meta.lastModified : null,
      machineName: this.machineName
    };
  }

  // ==================== File I/O ====================

  async _readSharedFile() {
    if (!this.filePath) {
      throw new Error('No shared file path configured');
    }

    try {
      const raw = await fs.promises.readFile(this.filePath, 'utf-8');
      const data = JSON.parse(raw);

      if (!data._meta || !data._meta.version) {
        throw new Error('Invalid shop_data.json: missing _meta.version');
      }

      this.cache = data;
      this.connected = true;
      this._saveLocalFallback(data);
      return data;
    } catch (err) {
      if (err.code === 'ENOENT') {
        // File doesn't exist yet — create it
        const empty = this._getEmptyData();
        await this._atomicWriteWithRetry(this.filePath, empty);
        this.cache = empty;
        this.connected = true;
        this._saveLocalFallback(empty);
        return empty;
      }
      if (err instanceof SyntaxError) {
        // Corrupt JSON — do NOT overwrite, emit error, use local fallback
        console.error('DataService: Corrupt JSON in shared file:', err.message);
        this.emit('error', { type: 'corrupt_json', message: err.message });
        this.connected = false;
        throw err;
      }
      // Network/permission errors
      this.connected = false;
      throw err;
    }
  }

  async _writeSharedFile(data) {
    // Serialize writes through queue
    this._writeQueue = this._writeQueue.then(async () => {
      // Update metadata
      data._meta.lastModified = new Date().toISOString();
      data._meta.lastModifiedBy = this.machineName;

      // Always save to local fallback
      this._saveLocalFallback(data);

      if (!this.filePath || !this.connected) {
        // Local-only mode
        this.cache = data;
        return { success: true, local: true };
      }

      try {
        await this._atomicWriteWithRetry(this.filePath, data);
        this.cache = data;
        this.emit('data:saved');
        return { success: true };
      } catch (err) {
        console.error('DataService: Write to shared file failed after retries:', err.message);
        this.connected = false;
        this.cache = data;
        this.emit('write:failed', { error: err.message });
        this.emit('connection:lost');
        return { success: false, error: err.message, savedLocally: true };
      }
    });

    return this._writeQueue;
  }

  async _atomicWriteWithRetry(filePath, data, maxRetries = RETRY_MAX, delayMs = RETRY_BASE_DELAY) {
    let lastError = null;
    const content = JSON.stringify(data, null, 2);

    for (let attempt = 0; attempt < maxRetries; attempt++) {
      try {
        await writeFileAtomic(filePath, content, { encoding: 'utf-8' });
        return;
      } catch (err) {
        lastError = err;

        if (err.code === 'ENOSPC') {
          throw err; // Disk full — retrying won't help
        }
        if (err.code === 'ETIMEDOUT') {
          this.connected = false;
          throw err; // Network gone — retrying won't help quickly
        }

        // EBUSY, EPERM, EACCES — file locked by another machine, retry
        const waitMs = delayMs * (attempt + 1);
        await new Promise(resolve => setTimeout(resolve, waitMs));
      }
    }

    throw lastError;
  }

  // ==================== File Watching ====================

  _startWatcher() {
    if (!this.filePath) return;
    this._stopWatcher();

    this.watcher = chokidar.watch(this.filePath, {
      persistent: true,
      usePolling: true,           // Required for UNC/network paths
      interval: 1000,
      binaryInterval: 2000,
      awaitWriteFinish: {
        stabilityThreshold: 300,
        pollInterval: 100
      }
    });

    this.watcher.on('change', async () => {
      try {
        const raw = await fs.promises.readFile(this.filePath, 'utf-8');
        const data = JSON.parse(raw);

        // Ignore our own writes
        if (data._meta && data._meta.lastModifiedBy === this.machineName) {
          return;
        }

        this.cache = data;
        this._saveLocalFallback(data);
        this.emit('data:externalChange', data);
      } catch (err) {
        if (err instanceof SyntaxError) {
          // File still being written — ignore, awaitWriteFinish should prevent this
          return;
        }
        console.error('DataService: Error reading changed file:', err.message);
      }
    });

    this.watcher.on('unlink', () => {
      // File was deleted — recreate from cache
      console.warn('DataService: Shared file was deleted, recreating from cache');
      this._atomicWriteWithRetry(this.filePath, this.cache).catch(err => {
        console.error('DataService: Failed to recreate shared file:', err.message);
      });
    });

    this.watcher.on('error', (err) => {
      console.error('DataService: Watcher error:', err.message);
      if (err.code === 'EPERM' || err.code === 'EACCES' || err.code === 'ENOENT') {
        this.connected = false;
        this.emit('connection:lost');
      }
    });
  }

  _stopWatcher() {
    if (this.watcher) {
      this.watcher.close().catch(() => {});
      this.watcher = null;
    }
  }

  // ==================== Local Fallback ====================

  _loadLocalFallback() {
    try {
      if (fs.existsSync(this.localFallbackPath)) {
        const raw = fs.readFileSync(this.localFallbackPath, 'utf-8');
        const data = JSON.parse(raw);
        if (data._meta && data._meta.version) {
          this.cache = data;
          return;
        }
      }
    } catch (err) {
      console.error('DataService: Error loading local fallback:', err.message);
    }
    this.cache = this._getEmptyData();
  }

  _saveLocalFallback(data) {
    try {
      fs.writeFileSync(this.localFallbackPath, JSON.stringify(data, null, 2), 'utf-8');
    } catch (err) {
      console.error('DataService: Error saving local fallback:', err.message);
    }
  }

  async _syncLocalToShared() {
    if (!this.filePath) return { added: 0, updated: 0 };

    try {
      const sharedRaw = await fs.promises.readFile(this.filePath, 'utf-8');
      const shared = JSON.parse(sharedRaw);
      const local = this.cache;
      let added = 0;
      let updated = 0;

      // Merge empirical data
      const merged = this._mergeArrays(shared.empiricalData || [], local.empiricalData || []);
      added += merged.added;
      updated += merged.updated;
      shared.empiricalData = merged.result;

      // Merge notes per page
      const pages = ['dieHeight', 'clearance', 'tonnage', 'sbr'];
      for (const page of pages) {
        const m = this._mergeArrays(
          (shared.notes && shared.notes[page]) || [],
          (local.notes && local.notes[page]) || []
        );
        added += m.added;
        updated += m.updated;
        if (!shared.notes) shared.notes = {};
        shared.notes[page] = m.result;
      }

      // Merge tools
      const toolsMerge = this._mergeArrays(shared.tools || [], local.tools || []);
      added += toolsMerge.added;
      updated += toolsMerge.updated;
      shared.tools = toolsMerge.result;

      // Merge usage log (append-only, deduplicate by id)
      const logIds = new Set((shared.toolUsageLog || []).map(l => l.id));
      for (const entry of (local.toolUsageLog || [])) {
        if (!logIds.has(entry.id)) {
          shared.toolUsageLog = shared.toolUsageLog || [];
          shared.toolUsageLog.push(entry);
          added++;
        }
      }

      // Write merged result
      await this._writeSharedFile(shared);
      this.localConfig.set('lastSuccessfulSync', new Date().toISOString());

      const stats = { added, updated };
      this.emit('sync:completed', stats);
      return stats;
    } catch (err) {
      console.error('DataService: Sync failed:', err.message);
      return { added: 0, updated: 0, error: err.message };
    }
  }

  _mergeArrays(sharedArr, localArr) {
    const map = new Map();
    let added = 0;
    let updated = 0;

    // Index shared by id
    for (const item of sharedArr) {
      map.set(item.id, item);
    }

    // Merge local into shared
    for (const item of localArr) {
      const existing = map.get(item.id);
      if (!existing) {
        // New record from local
        map.set(item.id, item);
        added++;
      } else {
        // Both exist — last-write-wins by updatedAt
        const existingTime = new Date(existing.updatedAt || existing.createdAt || 0).getTime();
        const localTime = new Date(item.updatedAt || item.createdAt || 0).getTime();
        if (localTime > existingTime) {
          map.set(item.id, item);
          updated++;
        }
      }
    }

    return { result: Array.from(map.values()), added, updated };
  }

  // ==================== Health Check ====================

  _startHealthCheck() {
    this._stopHealthCheck();
    this.healthCheckTimer = setInterval(async () => {
      if (!this.uncPath) return;

      if (this.connected) {
        // Verify we're still connected
        try {
          await fs.promises.access(this.uncPath, fs.constants.R_OK | fs.constants.W_OK);
        } catch (_) {
          this.connected = false;
          this._stopWatcher();
          this.emit('connection:lost');
        }
      } else {
        // Try to reconnect
        try {
          await fs.promises.access(this.uncPath, fs.constants.R_OK | fs.constants.W_OK);
          this.filePath = path.join(this.uncPath, 'shop_data.json');
          await this._readSharedFile();
          this.connected = true;
          this._startWatcher();
          await this._syncLocalToShared();
          this.emit('connection:restored');
        } catch (_) {
          // Still disconnected
        }
      }
    }, HEALTH_CHECK_INTERVAL);
  }

  _stopHealthCheck() {
    if (this.healthCheckTimer) {
      clearInterval(this.healthCheckTimer);
      this.healthCheckTimer = null;
    }
  }

  // ==================== ID Generation ====================

  _generateId(prefix) {
    const hex = Math.random().toString(16).substring(2, 8);
    return `${prefix}_${Date.now()}_${hex}`;
  }

  // ==================== Empty Data Template ====================

  _getEmptyData() {
    return {
      _meta: {
        version: 1,
        lastModified: new Date().toISOString(),
        lastModifiedBy: this.machineName,
        appVersion: '1.0.0'
      },
      empiricalData: [],
      notes: {
        dieHeight: [],
        clearance: [],
        tonnage: [],
        sbr: []
      },
      tools: [],
      toolUsageLog: []
    };
  }

  // ==================== Empirical Data CRUD ====================

  getAllEmpiricalData() {
    return (this.cache.empiricalData || []).slice().sort((a, b) => {
      if (a.material !== b.material) return a.material.localeCompare(b.material);
      return a.thickness - b.thickness;
    });
  }

  async addEmpiricalEntry(entry) {
    const id = this._generateId('emp');
    const now = new Date().toISOString();
    const record = {
      id,
      material: entry.material,
      thickness: entry.thickness,
      operation: entry.operation,
      clearanceMM: entry.clearanceMM,
      clearancePct: entry.clearancePct,
      notes: entry.notes || '',
      verified: entry.verified ? 1 : 0,
      createdAt: now,
      updatedAt: now,
      createdBy: this.machineName
    };
    this.cache.empiricalData.push(record);
    await this._writeSharedFile(this.cache);
    return { success: true, id };
  }

  async updateEmpiricalEntry(id, entry) {
    const idx = this.cache.empiricalData.findIndex(e => e.id === id || e.id === Number(id));
    if (idx === -1) return { success: false, error: 'Entry not found' };

    const existing = this.cache.empiricalData[idx];
    this.cache.empiricalData[idx] = {
      ...existing,
      material: entry.material,
      thickness: entry.thickness,
      operation: entry.operation,
      clearanceMM: entry.clearanceMM,
      clearancePct: entry.clearancePct,
      notes: entry.notes || '',
      verified: entry.verified ? 1 : 0,
      updatedAt: new Date().toISOString()
    };
    await this._writeSharedFile(this.cache);
    return { success: true };
  }

  async deleteEmpiricalEntry(id) {
    this.cache.empiricalData = this.cache.empiricalData.filter(e => e.id !== id && e.id !== Number(id));
    await this._writeSharedFile(this.cache);
    return { success: true };
  }

  // ==================== Notes CRUD ====================

  getNotes(page) {
    const notes = (this.cache.notes && this.cache.notes[page]) || [];
    return notes.slice().sort((a, b) => {
      const ta = new Date(b.createdAt || 0).getTime();
      const tb = new Date(a.createdAt || 0).getTime();
      return ta - tb;
    });
  }

  async addNote(page, note) {
    const id = this._generateId('note');
    const now = new Date().toISOString();
    const record = {
      id,
      title: note.title,
      content: note.content || '',
      createdAt: now,
      updatedAt: now,
      createdBy: this.machineName
    };

    if (!this.cache.notes) this.cache.notes = {};
    if (!this.cache.notes[page]) this.cache.notes[page] = [];
    this.cache.notes[page].push(record);

    await this._writeSharedFile(this.cache);
    return { success: true, id };
  }

  async updateNote(id, note) {
    if (!this.cache.notes) return { success: false, error: 'Note not found' };

    for (const page of Object.keys(this.cache.notes)) {
      const idx = this.cache.notes[page].findIndex(n => n.id === id || n.id === Number(id));
      if (idx !== -1) {
        const existing = this.cache.notes[page][idx];
        this.cache.notes[page][idx] = {
          ...existing,
          title: note.title,
          content: note.content || '',
          updatedAt: new Date().toISOString()
        };
        await this._writeSharedFile(this.cache);
        return { success: true };
      }
    }
    return { success: false, error: 'Note not found' };
  }

  async deleteNote(id) {
    if (!this.cache.notes) return { success: true };

    for (const page of Object.keys(this.cache.notes)) {
      this.cache.notes[page] = this.cache.notes[page].filter(n => n.id !== id && n.id !== Number(id));
    }
    await this._writeSharedFile(this.cache);
    return { success: true };
  }

  // ==================== Tools CRUD ====================

  getAllTools() {
    return (this.cache.tools || []).slice().sort((a, b) => {
      if (a.toolType !== b.toolType) return (a.toolType || '').localeCompare(b.toolType || '');
      return (a.station || '').localeCompare(b.station || '');
    });
  }

  async addTool(tool) {
    const id = this._generateId('tool');
    const now = new Date().toISOString();
    const record = {
      id,
      toolType: tool.toolType,
      station: tool.station,
      shape: tool.shape,
      sizeSmall: tool.sizeSmall,
      sizeLarge: tool.sizeLarge,
      cornerRadius: tool.cornerRadius,
      serialNumber: tool.serialNumber,
      location: tool.location,
      currentSBR: tool.currentSBR,
      grindCount: tool.grindCount || 0,
      status: tool.status || 'active',
      purchaseDate: tool.purchaseDate,
      lastGrindDate: tool.lastGrindDate,
      notes: tool.notes || '',
      createdAt: now,
      updatedAt: now
    };
    this.cache.tools.push(record);
    await this._writeSharedFile(this.cache);
    return { success: true, id };
  }

  async updateTool(id, tool) {
    const idx = this.cache.tools.findIndex(t => t.id === id || t.id === Number(id));
    if (idx === -1) return { success: false, error: 'Tool not found' };

    const existing = this.cache.tools[idx];
    this.cache.tools[idx] = {
      ...existing,
      toolType: tool.toolType,
      station: tool.station,
      shape: tool.shape,
      sizeSmall: tool.sizeSmall,
      sizeLarge: tool.sizeLarge,
      cornerRadius: tool.cornerRadius,
      serialNumber: tool.serialNumber,
      location: tool.location,
      currentSBR: tool.currentSBR,
      grindCount: tool.grindCount,
      status: tool.status,
      purchaseDate: tool.purchaseDate,
      lastGrindDate: tool.lastGrindDate,
      notes: tool.notes || '',
      updatedAt: new Date().toISOString()
    };
    await this._writeSharedFile(this.cache);
    return { success: true };
  }

  async deleteTool(id) {
    this.cache.tools = this.cache.tools.filter(t => t.id !== id && t.id !== Number(id));
    // Also remove usage log entries for this tool
    this.cache.toolUsageLog = (this.cache.toolUsageLog || []).filter(l => l.toolId !== id);
    await this._writeSharedFile(this.cache);
    return { success: true };
  }

  async logToolUsage(toolId, action, details) {
    const id = this._generateId('log');
    const record = {
      id,
      toolId,
      action,
      details: details || '',
      loggedAt: new Date().toISOString()
    };
    if (!this.cache.toolUsageLog) this.cache.toolUsageLog = [];
    this.cache.toolUsageLog.push(record);
    await this._writeSharedFile(this.cache);
  }

  // ==================== Settings (LOCAL only) ====================

  loadSettings() {
    try {
      if (fs.existsSync(this.settingsPath)) {
        const data = fs.readFileSync(this.settingsPath, 'utf-8');
        return JSON.parse(data);
      }
    } catch (err) {
      console.error('Error loading settings:', err);
    }
    return this.getDefaultSettings();
  }

  saveSettings(settings) {
    try {
      const data = JSON.stringify(settings, null, 2);
      fs.writeFileSync(this.settingsPath, data, 'utf-8');
      return { success: true };
    } catch (err) {
      console.error('Error saving settings:', err);
      return { success: false, error: err.message };
    }
  }

  getDefaultSettings() {
    return {
      version: 1,
      theme: 'dark',
      units: 'metric',
      defaultMaterial: 'Aluminium',
      autoSave: true,
      autoSaveInterval: 30000,
      lastBackup: null
    };
  }

  // ==================== State (LOCAL only) ====================

  loadState() {
    try {
      if (fs.existsSync(this.statePath)) {
        const data = fs.readFileSync(this.statePath, 'utf-8');
        return JSON.parse(data);
      }
    } catch (err) {
      console.error('Error loading state:', err);
    }
    return null;
  }

  saveState(state) {
    try {
      const data = JSON.stringify(state, null, 2);
      fs.writeFileSync(this.statePath, data, 'utf-8');
      return { success: true };
    } catch (err) {
      console.error('Error saving state:', err);
      return { success: false, error: err.message };
    }
  }

  // ==================== Backup/Restore ====================

  createBackup() {
    const timestamp = new Date().toISOString().replace(/[:.]/g, '-');
    const backupName = `backup-${timestamp}`;
    const backupDir = path.join(this.backupPath, backupName);

    try {
      fs.mkdirSync(backupDir, { recursive: true });

      // Backup shared data (from cache)
      fs.writeFileSync(
        path.join(backupDir, 'shop_data.json'),
        JSON.stringify(this.cache, null, 2),
        'utf-8'
      );

      // Backup local settings
      if (fs.existsSync(this.settingsPath)) {
        fs.copyFileSync(this.settingsPath, path.join(backupDir, 'settings.json'));
      }
      if (fs.existsSync(this.statePath)) {
        fs.copyFileSync(this.statePath, path.join(backupDir, 'state.json'));
      }

      return { success: true, name: backupName };
    } catch (err) {
      console.error('Backup failed:', err);
      return { success: false, error: err.message };
    }
  }

  listBackups() {
    try {
      if (!fs.existsSync(this.backupPath)) return [];
      return fs.readdirSync(this.backupPath, { withFileTypes: true })
        .filter(d => d.isDirectory() && d.name.startsWith('backup-'))
        .map(d => ({
          name: d.name,
          date: d.name.replace('backup-', '').replace(/-/g, ':').substring(0, 19)
        }))
        .sort((a, b) => b.name.localeCompare(a.name));
    } catch (err) {
      console.error('Error listing backups:', err);
      return [];
    }
  }

  async restoreBackup(backupName) {
    const backupDir = path.join(this.backupPath, backupName);
    if (!fs.existsSync(backupDir)) {
      return { success: false, error: 'Backup not found' };
    }

    try {
      // Restore shared data
      const shopDataPath = path.join(backupDir, 'shop_data.json');
      if (fs.existsSync(shopDataPath)) {
        const raw = fs.readFileSync(shopDataPath, 'utf-8');
        const data = JSON.parse(raw);
        this.cache = data;
        await this._writeSharedFile(data);
      }

      // Restore local settings
      const backupSettingsPath = path.join(backupDir, 'settings.json');
      if (fs.existsSync(backupSettingsPath)) {
        fs.copyFileSync(backupSettingsPath, this.settingsPath);
      }

      const backupStatePath = path.join(backupDir, 'state.json');
      if (fs.existsSync(backupStatePath)) {
        fs.copyFileSync(backupStatePath, this.statePath);
      }

      return { success: true };
    } catch (err) {
      console.error('Restore failed:', err);
      return { success: false, error: err.message };
    }
  }

  // ==================== Cleanup ====================

  async close() {
    this._stopWatcher();
    this._stopHealthCheck();
    // Final save to local fallback
    this._saveLocalFallback(this.cache);
  }
}

module.exports = DataService;
