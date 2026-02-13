namespace TurretApp.Models;

public class AppState
{
    public List<InventoryTool> ToolInventory { get; set; } = [];
    public List<EmpiricalEntry> EmpiricalData { get; set; } = [];
    public List<TurretStation> TurretStations { get; set; } = [];

    // Theme
    public string CurrentTheme { get; set; } = "Green";

    // Notes per module
    public List<NoteEntry> DieHeightNotes { get; set; } = [];
    public List<NoteEntry> ClearanceNotes { get; set; } = [];
    public List<NoteEntry> TonnageNotes { get; set; } = [];
    public List<NoteEntry> SbrNotes { get; set; } = [];
    public List<NoteEntry> ToolLibraryNotes { get; set; } = [];
    public List<NoteEntry> TurretViewNotes { get; set; } = [];
}
