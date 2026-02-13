using System.Text.Json.Serialization;

namespace TurretApp.Models;

public class InventoryTool
{
    public int Id { get; set; }
    public string Station { get; set; } = ""; // A, B, C, D
    public string ToolType { get; set; } = ""; // Round Punch, Square Die, Cluster Round Punch, etc.
    public string Category { get; set; } = ""; // Punch, Die, Cluster
    public string Shape { get; set; } = ""; // Round, Square, Obround, Rectangle, Hexagon, Special, Forming
    public string Size { get; set; } = ""; // e.g. "3.2", "5 x 20", "16 pins 3.2mm"
    public double? Clearance { get; set; } // mm, null if N/A (punches)
    public int Quantity { get; set; } = 1;
    public string Condition { get; set; } = "OK"; // New, Good, OK, Poor, Broken Pin, Ruined, Bad
    public bool NeedsSharpen { get; set; }
    public string Notes { get; set; } = "";

    // Tracking fields
    public double? MeasuredHeight { get; set; }
    public double? CurrentShims { get; set; }

    [JsonIgnore]
    public string DisplayName => $"{Station} | {ToolType} {Size}" + (Clearance.HasValue ? $" (clr:{Clearance})" : "");

    [JsonIgnore]
    public string ConditionColor => Condition switch
    {
        "New" => "#44ccff",
        "Good" => "#44ff44",
        "OK" => "#55ff88",
        "Poor" => "#ffdd44",
        "Broken Pin" or "Ruined" or "Bad" => "#ff5555",
        _ => "#55ff88"
    };
}

public class TurretStation
{
    public int StationNumber { get; set; }
    public string StationType { get; set; } = ""; // A, B, C, D
    public int? MountedPunchId { get; set; }
    public int? MountedDieId { get; set; }
    public double RotationDegrees { get; set; } // indexing stations only (odd: 1,3,5)
}

public class EmpiricalEntry
{
    public int Id { get; set; }
    public string Material { get; set; } = "";
    public double Thickness { get; set; }
    public string Operation { get; set; } = "";
    public double ClearanceMM { get; set; }
    public double ClearancePct { get; set; }
    public string Notes { get; set; } = "";
    public bool Verified { get; set; }
}

public class NoteEntry
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
}
