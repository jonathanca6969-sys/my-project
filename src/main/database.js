const Database = require('better-sqlite3');
const path = require('path');
const fs = require('fs');

const SCHEMA_VERSION = 1;

class AppDatabase {
  constructor(dataPath) {
    this.dataPath = dataPath;
    this.dbPath = path.join(dataPath, 'tooling.db');
    this.settingsPath = path.join(dataPath, 'settings.json');
    this.statePath = path.join(dataPath, 'state.json');
    this.backupPath = path.join(dataPath, 'backups');

    this.db = new Database(this.dbPath);
    this.db.pragma('journal_mode = WAL'); // Better performance
    this.initializeSchema();
  }

  initializeSchema() {
    // Schema versioning table
    this.db.exec(`
      CREATE TABLE IF NOT EXISTS schema_version (
        version INTEGER PRIMARY KEY
      )
    `);

    const versionRow = this.db.prepare('SELECT version FROM schema_version').get();
    const currentVersion = versionRow ? versionRow.version : 0;

    if (currentVersion < SCHEMA_VERSION) {
      this.migrate(currentVersion);
    }
  }

  migrate(fromVersion) {
    // Migration v0 -> v1: Initial schema
    if (fromVersion < 1) {
      this.db.exec(`
        -- Empirical clearance data
        CREATE TABLE IF NOT EXISTS empirical_data (
          id INTEGER PRIMARY KEY AUTOINCREMENT,
          material TEXT NOT NULL,
          thickness REAL NOT NULL,
          operation TEXT NOT NULL,
          clearance_mm REAL NOT NULL,
          clearance_pct REAL NOT NULL,
          notes TEXT DEFAULT '',
          verified INTEGER DEFAULT 0,
          created_at TEXT DEFAULT (datetime('now')),
          updated_at TEXT DEFAULT (datetime('now'))
        );

        -- Notes for each calculator page
        CREATE TABLE IF NOT EXISTS notes (
          id INTEGER PRIMARY KEY AUTOINCREMENT,
          page TEXT NOT NULL,
          title TEXT NOT NULL,
          content TEXT DEFAULT '',
          created_at TEXT DEFAULT (datetime('now')),
          updated_at TEXT DEFAULT (datetime('now'))
        );

        -- Tool inventory
        CREATE TABLE IF NOT EXISTS tools (
          id INTEGER PRIMARY KEY AUTOINCREMENT,
          tool_type TEXT NOT NULL,
          station TEXT,
          shape TEXT,
          size_small REAL,
          size_large REAL,
          corner_radius REAL,
          serial_number TEXT,
          location TEXT,
          current_sbr REAL,
          grind_count INTEGER DEFAULT 0,
          status TEXT DEFAULT 'active',
          purchase_date TEXT,
          last_grind_date TEXT,
          notes TEXT DEFAULT '',
          created_at TEXT DEFAULT (datetime('now')),
          updated_at TEXT DEFAULT (datetime('now'))
        );

        -- Tool usage log
        CREATE TABLE IF NOT EXISTS tool_usage_log (
          id INTEGER PRIMARY KEY AUTOINCREMENT,
          tool_id INTEGER NOT NULL,
          action TEXT NOT NULL,
          details TEXT,
          logged_at TEXT DEFAULT (datetime('now')),
          FOREIGN KEY (tool_id) REFERENCES tools(id) ON DELETE CASCADE
        );

        -- Create indexes
        CREATE INDEX IF NOT EXISTS idx_empirical_material ON empirical_data(material);
        CREATE INDEX IF NOT EXISTS idx_empirical_operation ON empirical_data(operation);
        CREATE INDEX IF NOT EXISTS idx_notes_page ON notes(page);
        CREATE INDEX IF NOT EXISTS idx_tools_type ON tools(tool_type);
        CREATE INDEX IF NOT EXISTS idx_tools_status ON tools(status);
        CREATE INDEX IF NOT EXISTS idx_usage_tool ON tool_usage_log(tool_id);
      `);

      this.db.prepare('INSERT OR REPLACE INTO schema_version (version) VALUES (?)').run(1);
    }

    // Future migrations go here:
    // if (fromVersion < 2) { ... }
  }

  // ==================== Settings (JSON) ====================

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
      autoSaveInterval: 30000, // 30 seconds
      lastBackup: null
    };
  }

  // ==================== State (JSON) ====================

  loadState() {
    try {
      if (fs.existsSync(this.statePath)) {
        const data = fs.readFileSync(this.statePath, 'utf-8');
        return JSON.parse(data);
      }
    } catch (err) {
      console.error('Error loading state:', err);
    }
    return null; // Return null to indicate using defaults
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

  // ==================== Empirical Data (SQLite) ====================

  getAllEmpiricalData() {
    return this.db.prepare(`
      SELECT id, material, thickness, operation, clearance_mm as clearanceMM,
             clearance_pct as clearancePct, notes, verified,
             created_at as createdAt, updated_at as updatedAt
      FROM empirical_data
      ORDER BY material, thickness
    `).all();
  }

  addEmpiricalEntry(entry) {
    const stmt = this.db.prepare(`
      INSERT INTO empirical_data (material, thickness, operation, clearance_mm, clearance_pct, notes, verified)
      VALUES (?, ?, ?, ?, ?, ?, ?)
    `);
    const result = stmt.run(
      entry.material,
      entry.thickness,
      entry.operation,
      entry.clearanceMM,
      entry.clearancePct,
      entry.notes || '',
      entry.verified ? 1 : 0
    );
    return { success: true, id: result.lastInsertRowid };
  }

  updateEmpiricalEntry(id, entry) {
    const stmt = this.db.prepare(`
      UPDATE empirical_data
      SET material = ?, thickness = ?, operation = ?, clearance_mm = ?,
          clearance_pct = ?, notes = ?, verified = ?, updated_at = datetime('now')
      WHERE id = ?
    `);
    stmt.run(
      entry.material,
      entry.thickness,
      entry.operation,
      entry.clearanceMM,
      entry.clearancePct,
      entry.notes || '',
      entry.verified ? 1 : 0,
      id
    );
    return { success: true };
  }

  deleteEmpiricalEntry(id) {
    this.db.prepare('DELETE FROM empirical_data WHERE id = ?').run(id);
    return { success: true };
  }

  // ==================== Notes (SQLite) ====================

  getNotes(page) {
    return this.db.prepare(`
      SELECT id, page, title, content, created_at as createdAt, updated_at as updatedAt
      FROM notes
      WHERE page = ?
      ORDER BY created_at DESC
    `).all(page);
  }

  addNote(page, note) {
    const stmt = this.db.prepare(`
      INSERT INTO notes (page, title, content) VALUES (?, ?, ?)
    `);
    const result = stmt.run(page, note.title, note.content || '');
    return { success: true, id: result.lastInsertRowid };
  }

  updateNote(id, note) {
    const stmt = this.db.prepare(`
      UPDATE notes SET title = ?, content = ?, updated_at = datetime('now') WHERE id = ?
    `);
    stmt.run(note.title, note.content || '', id);
    return { success: true };
  }

  deleteNote(id) {
    this.db.prepare('DELETE FROM notes WHERE id = ?').run(id);
    return { success: true };
  }

  // ==================== Tools (SQLite) ====================

  getAllTools() {
    return this.db.prepare(`
      SELECT * FROM tools ORDER BY tool_type, station
    `).all();
  }

  addTool(tool) {
    const stmt = this.db.prepare(`
      INSERT INTO tools (tool_type, station, shape, size_small, size_large, corner_radius,
                         serial_number, location, current_sbr, grind_count, status,
                         purchase_date, last_grind_date, notes)
      VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
    `);
    const result = stmt.run(
      tool.toolType,
      tool.station,
      tool.shape,
      tool.sizeSmall,
      tool.sizeLarge,
      tool.cornerRadius,
      tool.serialNumber,
      tool.location,
      tool.currentSBR,
      tool.grindCount || 0,
      tool.status || 'active',
      tool.purchaseDate,
      tool.lastGrindDate,
      tool.notes || ''
    );
    return { success: true, id: result.lastInsertRowid };
  }

  updateTool(id, tool) {
    const stmt = this.db.prepare(`
      UPDATE tools
      SET tool_type = ?, station = ?, shape = ?, size_small = ?, size_large = ?,
          corner_radius = ?, serial_number = ?, location = ?, current_sbr = ?,
          grind_count = ?, status = ?, purchase_date = ?, last_grind_date = ?,
          notes = ?, updated_at = datetime('now')
      WHERE id = ?
    `);
    stmt.run(
      tool.toolType,
      tool.station,
      tool.shape,
      tool.sizeSmall,
      tool.sizeLarge,
      tool.cornerRadius,
      tool.serialNumber,
      tool.location,
      tool.currentSBR,
      tool.grindCount,
      tool.status,
      tool.purchaseDate,
      tool.lastGrindDate,
      tool.notes || '',
      id
    );
    return { success: true };
  }

  deleteTool(id) {
    this.db.prepare('DELETE FROM tools WHERE id = ?').run(id);
    return { success: true };
  }

  logToolUsage(toolId, action, details) {
    const stmt = this.db.prepare(`
      INSERT INTO tool_usage_log (tool_id, action, details) VALUES (?, ?, ?)
    `);
    stmt.run(toolId, action, details || '');
  }

  // ==================== Backup/Restore ====================

  createBackup() {
    const timestamp = new Date().toISOString().replace(/[:.]/g, '-');
    const backupName = `backup-${timestamp}`;
    const backupDir = path.join(this.backupPath, backupName);

    try {
      fs.mkdirSync(backupDir, { recursive: true });

      // Backup database
      this.db.backup(path.join(backupDir, 'tooling.db'));

      // Backup JSON files
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
      if (!fs.existsSync(this.backupPath)) {
        return [];
      }
      const dirs = fs.readdirSync(this.backupPath, { withFileTypes: true })
        .filter(d => d.isDirectory() && d.name.startsWith('backup-'))
        .map(d => ({
          name: d.name,
          date: d.name.replace('backup-', '').replace(/-/g, ':').substring(0, 19)
        }))
        .sort((a, b) => b.name.localeCompare(a.name));
      return dirs;
    } catch (err) {
      console.error('Error listing backups:', err);
      return [];
    }
  }

  restoreBackup(backupName) {
    const backupDir = path.join(this.backupPath, backupName);

    if (!fs.existsSync(backupDir)) {
      return { success: false, error: 'Backup not found' };
    }

    try {
      // Close current database
      this.db.close();

      // Restore files
      const backupDbPath = path.join(backupDir, 'tooling.db');
      if (fs.existsSync(backupDbPath)) {
        fs.copyFileSync(backupDbPath, this.dbPath);
      }

      const backupSettingsPath = path.join(backupDir, 'settings.json');
      if (fs.existsSync(backupSettingsPath)) {
        fs.copyFileSync(backupSettingsPath, this.settingsPath);
      }

      const backupStatePath = path.join(backupDir, 'state.json');
      if (fs.existsSync(backupStatePath)) {
        fs.copyFileSync(backupStatePath, this.statePath);
      }

      // Reopen database
      this.db = new Database(this.dbPath);
      this.db.pragma('journal_mode = WAL');

      return { success: true };
    } catch (err) {
      console.error('Restore failed:', err);
      // Try to reopen database
      this.db = new Database(this.dbPath);
      return { success: false, error: err.message };
    }
  }

  close() {
    if (this.db) {
      this.db.close();
    }
  }
}

module.exports = AppDatabase;
