const { contextBridge, ipcRenderer } = require('electron');

// Expose a safe API to the renderer process
contextBridge.exposeInMainWorld('electronAPI', {
  // Settings
  settings: {
    load: () => ipcRenderer.invoke('settings:load'),
    save: (settings) => ipcRenderer.invoke('settings:save', settings)
  },

  // Calculator state
  state: {
    load: () => ipcRenderer.invoke('state:load'),
    save: (state) => ipcRenderer.invoke('state:save', state)
  },

  // Empirical data
  empirical: {
    getAll: () => ipcRenderer.invoke('empirical:getAll'),
    add: (entry) => ipcRenderer.invoke('empirical:add', entry),
    update: (id, entry) => ipcRenderer.invoke('empirical:update', id, entry),
    delete: (id) => ipcRenderer.invoke('empirical:delete', id)
  },

  // Notes
  notes: {
    getAll: (page) => ipcRenderer.invoke('notes:getAll', page),
    add: (page, note) => ipcRenderer.invoke('notes:add', page, note),
    update: (id, note) => ipcRenderer.invoke('notes:update', id, note),
    delete: (id) => ipcRenderer.invoke('notes:delete', id)
  },

  // Tool inventory
  tools: {
    getAll: () => ipcRenderer.invoke('tools:getAll'),
    add: (tool) => ipcRenderer.invoke('tools:add', tool),
    update: (id, tool) => ipcRenderer.invoke('tools:update', id, tool),
    delete: (id) => ipcRenderer.invoke('tools:delete', id)
  },

  // Import/Export
  export: {
    csv: (data, defaultFilename) => ipcRenderer.invoke('export:csv', data, defaultFilename)
  },

  import: {
    csv: () => ipcRenderer.invoke('import:csv')
  },

  // Backup/Restore
  backup: {
    create: () => ipcRenderer.invoke('backup:create'),
    list: () => ipcRenderer.invoke('backup:list'),
    restore: (backupName) => ipcRenderer.invoke('backup:restore', backupName)
  },

  // Network / Shared Brain
  network: {
    getStatus: () => ipcRenderer.invoke('network:getStatus'),
    testConnection: (path) => ipcRenderer.invoke('network:testConnection', path),
    setUncPath: (path) => ipcRenderer.invoke('network:setUncPath', path),
    getConfig: () => ipcRenderer.invoke('network:getConfig'),
    saveConfig: (config) => ipcRenderer.invoke('network:saveConfig', config),
    forceSync: () => ipcRenderer.invoke('network:forceSync'),

    // Push events from main process
    onDataChanged: (callback) => ipcRenderer.on('shared:dataChanged', (event, data) => callback(data)),
    onConnectionLost: (callback) => ipcRenderer.on('shared:connectionLost', () => callback()),
    onConnectionRestored: (callback) => ipcRenderer.on('shared:connectionRestored', () => callback()),
    onSyncCompleted: (callback) => ipcRenderer.on('shared:syncCompleted', (event, stats) => callback(stats)),
    onWriteFailed: (callback) => ipcRenderer.on('shared:writeFailed', (event, error) => callback(error)),
    onError: (callback) => ipcRenderer.on('shared:error', (event, err) => callback(err)),

    // Cleanup
    removeAllListeners: () => {
      ipcRenderer.removeAllListeners('shared:dataChanged');
      ipcRenderer.removeAllListeners('shared:connectionLost');
      ipcRenderer.removeAllListeners('shared:connectionRestored');
      ipcRenderer.removeAllListeners('shared:syncCompleted');
      ipcRenderer.removeAllListeners('shared:writeFailed');
      ipcRenderer.removeAllListeners('shared:error');
    }
  },

  // Migration (SQLite to JSON)
  migration: {
    check: () => ipcRenderer.invoke('migration:check'),
    run: () => ipcRenderer.invoke('migration:run')
  },

  // App info
  app: {
    getVersion: () => ipcRenderer.invoke('app:getVersion'),
    getPath: (name) => ipcRenderer.invoke('app:getPath', name)
  }
});

// Notify renderer when DOM is ready
window.addEventListener('DOMContentLoaded', () => {
  console.log('Preload script loaded successfully');
});
