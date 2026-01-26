const { app, BrowserWindow, ipcMain, dialog } = require('electron');
const path = require('path');
const fs = require('fs');
const DataService = require('./data-service');
const localConfig = require('./local-store');

// Keep a global reference of the window object to prevent garbage collection
let mainWindow = null;
let dataService = null;

// Get the correct path for user data (works in both dev and production)
function getUserDataPath() {
  return app.getPath('userData');
}

// Get the correct path for app resources
function getResourcePath(relativePath) {
  if (app.isPackaged) {
    return path.join(process.resourcesPath, relativePath);
  }
  return path.join(__dirname, '..', '..', relativePath);
}

// Ensure data directories exist
function ensureDataDirectories() {
  const userDataPath = getUserDataPath();
  const dirs = [
    path.join(userDataPath, 'data'),
    path.join(userDataPath, 'data', 'backups')
  ];

  dirs.forEach(dir => {
    if (!fs.existsSync(dir)) {
      fs.mkdirSync(dir, { recursive: true });
    }
  });

  return path.join(userDataPath, 'data');
}

function createWindow() {
  mainWindow = new BrowserWindow({
    width: 1400,
    height: 900,
    minWidth: 1000,
    minHeight: 700,
    backgroundColor: '#050805',
    show: false, // Don't show until ready
    webPreferences: {
      preload: path.join(__dirname, '..', 'preload', 'preload.js'),
      contextIsolation: true,
      nodeIntegration: false,
      sandbox: false // Required for native modules via preload
    }
  });

  // Load the main HTML file directly
  mainWindow.loadFile(path.join(__dirname, '..', '..', 'index.html'));

  // Show window when ready to prevent visual flash
  mainWindow.once('ready-to-show', () => {
    mainWindow.show();
  });

  // Open DevTools in development
  if (!app.isPackaged) {
    mainWindow.webContents.openDevTools({ mode: 'detach' });
  }

  mainWindow.on('closed', () => {
    mainWindow = null;
  });
}

// Forward DataService events to renderer
function setupEventForwarding() {
  dataService.on('data:externalChange', (data) => {
    if (mainWindow && !mainWindow.isDestroyed()) {
      mainWindow.webContents.send('shared:dataChanged', data);
    }
  });

  dataService.on('connection:lost', () => {
    if (mainWindow && !mainWindow.isDestroyed()) {
      mainWindow.webContents.send('shared:connectionLost');
    }
  });

  dataService.on('connection:restored', () => {
    if (mainWindow && !mainWindow.isDestroyed()) {
      mainWindow.webContents.send('shared:connectionRestored');
    }
  });

  dataService.on('sync:completed', (stats) => {
    if (mainWindow && !mainWindow.isDestroyed()) {
      mainWindow.webContents.send('shared:syncCompleted', stats);
    }
  });

  dataService.on('write:failed', (error) => {
    if (mainWindow && !mainWindow.isDestroyed()) {
      mainWindow.webContents.send('shared:writeFailed', error);
    }
  });

  dataService.on('error', (err) => {
    if (mainWindow && !mainWindow.isDestroyed()) {
      mainWindow.webContents.send('shared:error', err);
    }
  });
}

// Initialize data service and IPC handlers
function initializeApp() {
  const dataPath = ensureDataDirectories();
  dataService = new DataService(dataPath, localConfig);
  setupEventForwarding();

  // ==================== Settings handlers ====================

  ipcMain.handle('settings:load', async () => {
    return dataService.loadSettings();
  });

  ipcMain.handle('settings:save', async (event, settings) => {
    return dataService.saveSettings(settings);
  });

  // ==================== State handlers (calculator state) ====================

  ipcMain.handle('state:load', async () => {
    return dataService.loadState();
  });

  ipcMain.handle('state:save', async (event, state) => {
    return dataService.saveState(state);
  });

  // ==================== Empirical data handlers ====================

  ipcMain.handle('empirical:getAll', async () => {
    return dataService.getAllEmpiricalData();
  });

  ipcMain.handle('empirical:add', async (event, entry) => {
    return dataService.addEmpiricalEntry(entry);
  });

  ipcMain.handle('empirical:update', async (event, id, entry) => {
    return dataService.updateEmpiricalEntry(id, entry);
  });

  ipcMain.handle('empirical:delete', async (event, id) => {
    return dataService.deleteEmpiricalEntry(id);
  });

  // ==================== Notes handlers ====================

  ipcMain.handle('notes:getAll', async (event, page) => {
    return dataService.getNotes(page);
  });

  ipcMain.handle('notes:add', async (event, page, note) => {
    return dataService.addNote(page, note);
  });

  ipcMain.handle('notes:update', async (event, id, note) => {
    return dataService.updateNote(id, note);
  });

  ipcMain.handle('notes:delete', async (event, id) => {
    return dataService.deleteNote(id);
  });

  // ==================== Tool inventory handlers ====================

  ipcMain.handle('tools:getAll', async () => {
    return dataService.getAllTools();
  });

  ipcMain.handle('tools:add', async (event, tool) => {
    return dataService.addTool(tool);
  });

  ipcMain.handle('tools:update', async (event, id, tool) => {
    return dataService.updateTool(id, tool);
  });

  ipcMain.handle('tools:delete', async (event, id) => {
    return dataService.deleteTool(id);
  });

  // ==================== Export handlers ====================

  ipcMain.handle('export:csv', async (event, data, defaultFilename) => {
    const result = await dialog.showSaveDialog(mainWindow, {
      defaultPath: defaultFilename,
      filters: [
        { name: 'CSV Files', extensions: ['csv'] },
        { name: 'All Files', extensions: ['*'] }
      ]
    });

    if (!result.canceled && result.filePath) {
      fs.writeFileSync(result.filePath, data, 'utf-8');
      return { success: true, path: result.filePath };
    }
    return { success: false, canceled: true };
  });

  ipcMain.handle('import:csv', async () => {
    const result = await dialog.showOpenDialog(mainWindow, {
      filters: [
        { name: 'CSV Files', extensions: ['csv'] },
        { name: 'All Files', extensions: ['*'] }
      ],
      properties: ['openFile']
    });

    if (!result.canceled && result.filePaths.length > 0) {
      const content = fs.readFileSync(result.filePaths[0], 'utf-8');
      return { success: true, content, path: result.filePaths[0] };
    }
    return { success: false, canceled: true };
  });

  // ==================== Backup handlers ====================

  ipcMain.handle('backup:create', async () => {
    return dataService.createBackup();
  });

  ipcMain.handle('backup:list', async () => {
    return dataService.listBackups();
  });

  ipcMain.handle('backup:restore', async (event, backupName) => {
    return dataService.restoreBackup(backupName);
  });

  // ==================== Network / Shared Brain handlers ====================

  ipcMain.handle('network:getStatus', async () => {
    return dataService.getStatus();
  });

  ipcMain.handle('network:testConnection', async (event, testPath) => {
    return dataService.testConnection(testPath || undefined);
  });

  ipcMain.handle('network:setUncPath', async (event, uncPath) => {
    return dataService.setUncPath(uncPath);
  });

  ipcMain.handle('network:getConfig', async () => {
    return {
      uncPath: localConfig.get('uncPath'),
      autoConnect: localConfig.get('autoConnect'),
      machineAlias: localConfig.get('machineAlias'),
      pollingIntervalMs: localConfig.get('pollingIntervalMs')
    };
  });

  ipcMain.handle('network:saveConfig', async (event, config) => {
    if (config.autoConnect !== undefined) localConfig.set('autoConnect', config.autoConnect);
    if (config.machineAlias !== undefined) localConfig.set('machineAlias', config.machineAlias);
    if (config.pollingIntervalMs !== undefined) localConfig.set('pollingIntervalMs', config.pollingIntervalMs);
    return { success: true };
  });

  ipcMain.handle('network:forceSync', async () => {
    return dataService._syncLocalToShared();
  });

  // ==================== Migration handlers ====================

  ipcMain.handle('migration:check', async () => {
    const Migration = require('./migration');
    return Migration.checkForData(dataPath);
  });

  ipcMain.handle('migration:run', async () => {
    const Migration = require('./migration');
    return Migration.migrateToJson(dataPath, dataService);
  });

  // ==================== App info ====================

  ipcMain.handle('app:getVersion', async () => {
    return app.getVersion();
  });

  ipcMain.handle('app:getPath', async (event, name) => {
    return app.getPath(name);
  });
}

// App lifecycle
app.whenReady().then(() => {
  initializeApp();
  createWindow();

  app.on('activate', () => {
    if (BrowserWindow.getAllWindows().length === 0) {
      createWindow();
    }
  });
});

app.on('window-all-closed', () => {
  if (dataService) {
    dataService.close();
  }
  if (process.platform !== 'darwin') {
    app.quit();
  }
});

// Handle uncaught exceptions
process.on('uncaughtException', (error) => {
  console.error('Uncaught Exception:', error);
});
