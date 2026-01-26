const path = require('path');
const fs = require('fs');
const { app } = require('electron');

const CONFIG_NAME = 'shared-brain-config.json';

const DEFAULTS = {
  uncPath: '',
  autoConnect: true,
  machineAlias: '',
  syncOnStartup: true,
  pollingIntervalMs: 1000,
  lastSuccessfulSync: null
};

class LocalStore {
  constructor() {
    this._configPath = path.join(app.getPath('userData'), CONFIG_NAME);
    this._data = this._load();
  }

  _load() {
    try {
      if (fs.existsSync(this._configPath)) {
        const raw = fs.readFileSync(this._configPath, 'utf-8');
        return { ...DEFAULTS, ...JSON.parse(raw) };
      }
    } catch (err) {
      console.error('LocalStore: Error loading config:', err.message);
    }
    return { ...DEFAULTS };
  }

  _save() {
    try {
      fs.writeFileSync(this._configPath, JSON.stringify(this._data, null, 2), 'utf-8');
    } catch (err) {
      console.error('LocalStore: Error saving config:', err.message);
    }
  }

  get(key) {
    return this._data[key] !== undefined ? this._data[key] : DEFAULTS[key];
  }

  set(key, value) {
    this._data[key] = value;
    this._save();
  }

  getAll() {
    return { ...this._data };
  }
}

module.exports = new LocalStore();
