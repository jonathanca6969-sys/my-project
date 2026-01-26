const { app, BrowserWindow, ipcMain, dialog } = require('electron');
const path = require('path');
const fs = require('fs');
const Database = require('./database');

// Keep a global reference of the window object to prevent garbage collection
let mainWindow = null;
let db = null;

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
      sandbox: false // Required for better-sqlite3 via preload
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

// Initialize database and IPC handlers
function initializeApp() {
  const dataPath = ensureDataDirectories();
  db = new Database(dataPath);

  // Settings handlers
  ipcMain.handle('settings:load', async () => {
    return db.loadSettings();
  });

  ipcMain.handle('settings:save', async (event, settings) => {
    return db.saveSettings(settings);
  });

  // State handlers (calculator state)
  ipcMain.handle('state:load', async () => {
    return db.loadState();
  });

  ipcMain.handle('state:save', async (event, state) => {
    return db.saveState(state);
  });

  // Empirical data handlers
  ipcMain.handle('empirical:getAll', async () => {
    return db.getAllEmpiricalData();
  });

  ipcMain.handle('empirical:add', async (event, entry) => {
    return db.addEmpiricalEntry(entry);
  });

  ipcMain.handle('empirical:update', async (event, id, entry) => {
    return db.updateEmpiricalEntry(id, entry);
  });

  ipcMain.handle('empirical:delete', async (event, id) => {
    return db.deleteEmpiricalEntry(id);
  });

  // Notes handlers
  ipcMain.handle('notes:getAll', async (event, page) => {
    return db.getNotes(page);
  });

  ipcMain.handle('notes:add', async (event, page, note) => {
    return db.addNote(page, note);
  });

  ipcMain.handle('notes:update', async (event, id, note) => {
    return db.updateNote(id, note);
  });

  ipcMain.handle('notes:delete', async (event, id) => {
    return db.deleteNote(id);
  });

  // Tool inventory handlers
  ipcMain.handle('tools:getAll', async () => {
    return db.getAllTools();
  });

  ipcMain.handle('tools:add', async (event, tool) => {
    return db.addTool(tool);
  });

  ipcMain.handle('tools:update', async (event, id, tool) => {
    return db.updateTool(id, tool);
  });

  ipcMain.handle('tools:delete', async (event, id) => {
    return db.deleteTool(id);
  });

  // Export handlers
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

  // Backup handlers
  ipcMain.handle('backup:create', async () => {
    return db.createBackup();
  });

  ipcMain.handle('backup:list', async () => {
    return db.listBackups();
  });

  ipcMain.handle('backup:restore', async (event, backupName) => {
    return db.restoreBackup(backupName);
  });

  // App info
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
  if (db) {
    db.close();
  }
  if (process.platform !== 'darwin') {
    app.quit();
  }
});

// Handle uncaught exceptions
process.on('uncaughtException', (error) => {
  console.error('Uncaught Exception:', error);
});
