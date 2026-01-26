const Database = require('better-sqlite3');
const path = require('path');
const fs = require('fs');

class Migration {
  /**
   * Check if the old SQLite database exists and has data worth migrating.
   */
  static checkForData(dataPath) {
    const dbPath = path.join(dataPath, 'tooling.db');

    if (!fs.existsSync(dbPath)) {
      return { hasData: false, counts: { empirical: 0, notes: 0, tools: 0 } };
    }

    let db;
    try {
      db = new Database(dbPath, { readonly: true });

      const empirical = db.prepare('SELECT COUNT(*) as count FROM empirical_data').get();
      const notes = db.prepare('SELECT COUNT(*) as count FROM notes').get();
      const tools = db.prepare('SELECT COUNT(*) as count FROM tools').get();

      const counts = {
        empirical: empirical.count,
        notes: notes.count,
        tools: tools.count
      };

      return {
        hasData: counts.empirical > 0 || counts.notes > 0 || counts.tools > 0,
        counts
      };
    } catch (err) {
      console.error('Migration: Error checking SQLite data:', err.message);
      return { hasData: false, error: err.message, counts: { empirical: 0, notes: 0, tools: 0 } };
    } finally {
      if (db) db.close();
    }
  }

  /**
   * Migrate all data from SQLite to the JSON-based DataService.
   * Idempotent — uses deterministic IDs so re-running skips already-migrated records.
   */
  static async migrateToJson(dataPath, dataService) {
    const dbPath = path.join(dataPath, 'tooling.db');

    if (!fs.existsSync(dbPath)) {
      return { success: false, error: 'SQLite database not found' };
    }

    let db;
    try {
      db = new Database(dbPath, { readonly: true });
      const migrated = { empirical: 0, notes: 0, tools: 0 };

      // Build a set of existing IDs to skip duplicates
      const existingIds = new Set();
      for (const e of dataService.cache.empiricalData || []) existingIds.add(e.id);
      for (const page of Object.keys(dataService.cache.notes || {})) {
        for (const n of dataService.cache.notes[page] || []) existingIds.add(n.id);
      }
      for (const t of dataService.cache.tools || []) existingIds.add(t.id);

      // --- Empirical Data ---
      const empiricalRows = db.prepare(`
        SELECT id, material, thickness, operation,
               clearance_mm as clearanceMM, clearance_pct as clearancePct,
               notes, verified, created_at, updated_at
        FROM empirical_data
      `).all();

      for (const row of empiricalRows) {
        const newId = `emp_migrated_${row.id}`;
        if (existingIds.has(newId)) continue;

        dataService.cache.empiricalData.push({
          id: newId,
          material: row.material,
          thickness: row.thickness,
          operation: row.operation,
          clearanceMM: row.clearanceMM,
          clearancePct: row.clearancePct,
          notes: row.notes || '',
          verified: row.verified,
          createdAt: row.created_at || new Date().toISOString(),
          updatedAt: row.updated_at || new Date().toISOString(),
          createdBy: 'migrated'
        });
        migrated.empirical++;
      }

      // --- Notes ---
      const noteRows = db.prepare(`
        SELECT id, page, title, content, created_at, updated_at
        FROM notes
      `).all();

      for (const row of noteRows) {
        const newId = `note_migrated_${row.id}`;
        if (existingIds.has(newId)) continue;

        const page = row.page;
        if (!dataService.cache.notes[page]) {
          dataService.cache.notes[page] = [];
        }

        dataService.cache.notes[page].push({
          id: newId,
          title: row.title,
          content: row.content || '',
          createdAt: row.created_at || new Date().toISOString(),
          updatedAt: row.updated_at || new Date().toISOString(),
          createdBy: 'migrated'
        });
        migrated.notes++;
      }

      // --- Tools ---
      const toolRows = db.prepare('SELECT * FROM tools').all();

      for (const row of toolRows) {
        const newId = `tool_migrated_${row.id}`;
        if (existingIds.has(newId)) continue;

        dataService.cache.tools.push({
          id: newId,
          toolType: row.tool_type,
          station: row.station,
          shape: row.shape,
          sizeSmall: row.size_small,
          sizeLarge: row.size_large,
          cornerRadius: row.corner_radius,
          serialNumber: row.serial_number,
          location: row.location,
          currentSBR: row.current_sbr,
          grindCount: row.grind_count || 0,
          status: row.status || 'active',
          purchaseDate: row.purchase_date,
          lastGrindDate: row.last_grind_date,
          notes: row.notes || '',
          createdAt: row.created_at || new Date().toISOString(),
          updatedAt: row.updated_at || new Date().toISOString()
        });
        migrated.tools++;
      }

      // --- Tool Usage Log ---
      let usageCount = 0;
      try {
        const usageRows = db.prepare('SELECT * FROM tool_usage_log').all();
        const existingLogIds = new Set(
          (dataService.cache.toolUsageLog || []).map(l => l.id)
        );

        for (const row of usageRows) {
          const newId = `log_migrated_${row.id}`;
          if (existingLogIds.has(newId)) continue;

          if (!dataService.cache.toolUsageLog) dataService.cache.toolUsageLog = [];
          dataService.cache.toolUsageLog.push({
            id: newId,
            toolId: `tool_migrated_${row.tool_id}`,
            action: row.action,
            details: row.details || '',
            loggedAt: row.logged_at || new Date().toISOString()
          });
          usageCount++;
        }
      } catch (_) {
        // tool_usage_log might not exist or be empty
      }

      // Write everything
      await dataService._writeSharedFile(dataService.cache);

      return {
        success: true,
        migrated,
        usageLogs: usageCount
      };
    } catch (err) {
      console.error('Migration: Error migrating data:', err.message);
      return { success: false, error: err.message };
    } finally {
      if (db) db.close();
    }
  }
}

module.exports = Migration;
