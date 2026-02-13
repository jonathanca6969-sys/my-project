namespace TurretApp.Models;

public class SbrToolInfo
{
    public double StripperLand { get; set; }
    public double DiePen { get; set; }
    public double MaxSBR { get; set; }
    public double MinSBR { get; set; }
    public double OAL { get; set; }
}

public class ShearFactorEntry
{
    public double Thickness { get; set; }
    public double Factor { get; set; }
}

public class ShearTable
{
    public double Depth { get; set; }
    public ShearFactorEntry[] Factors { get; set; } = [];
}

public static class ToolData
{
    public static readonly Dictionary<string, Dictionary<string, SbrToolInfo>> SbrTools = new()
    {
        ["Thick Turret"] = new()
        {
            ["Ultra (A)"] = new() { StripperLand = 3.99, DiePen = 3, MaxSBR = 18.8, MinSBR = 8.13, OAL = 107.82 },
            ["Ultra (B)"] = new() { StripperLand = 3.99, DiePen = 3, MaxSBR = 18.8, MinSBR = 8.13, OAL = 100.51 },
            ["Ultra/Original/Inch (C)"] = new() { StripperLand = 5.0, DiePen = 3, MaxSBR = 25.53, MinSBR = 8.13, OAL = 96.16 },
            ["Ultra/Original/Inch (D)"] = new() { StripperLand = 5.0, DiePen = 3, MaxSBR = 25.53, MinSBR = 8.13, OAL = 84.15 },
            ["Ultra/Original/Inch (E)"] = new() { StripperLand = 5.0, DiePen = 3, MaxSBR = 26.54, MinSBR = 8.13, OAL = 85.17 },
            ["MXC (C)"] = new() { StripperLand = 6.1, DiePen = 3, MaxSBR = 25.53, MinSBR = 8.13, OAL = 59.94 },
            ["MXC (D)"] = new() { StripperLand = 8.0, DiePen = 3, MaxSBR = 25.53, MinSBR = 8.13, OAL = 59.94 },
            ["MXC (E)"] = new() { StripperLand = 8.0, DiePen = 3, MaxSBR = 26.54, MinSBR = 8.13, OAL = 59.94 },
        },
        ["Trumpf"] = new()
        {
            ["Size 0 (74mm Standard)"] = new() { StripperLand = 3.18, DiePen = 1, MaxSBR = 13.5, MinSBR = 4.6, OAL = 59.87 },
            ["Size 0 (77mm Long)"] = new() { StripperLand = 3.18, DiePen = 1, MaxSBR = 16.5, MinSBR = 7.6, OAL = 62.87 },
            ["Size 1 (74mm Standard)"] = new() { StripperLand = 3.18, DiePen = 1, MaxSBR = 13.46, MinSBR = 4.57, OAL = 73.94 },
        },
    };

    public static readonly Dictionary<string, ShearTable> ShearTables = new()
    {
        ["Mate (1.5mm)"] = new()
        {
            Depth = 1.5,
            Factors = [
                new() { Thickness = 1.2, Factor = 0.50 }, new() { Thickness = 1.5, Factor = 0.50 },
                new() { Thickness = 1.9, Factor = 0.58 }, new() { Thickness = 2.7, Factor = 0.72 },
                new() { Thickness = 3.0, Factor = 0.75 }, new() { Thickness = 3.4, Factor = 0.78 },
                new() { Thickness = 4.2, Factor = 0.83 }, new() { Thickness = 4.8, Factor = 0.86 },
                new() { Thickness = 6.4, Factor = 0.90 },
            ]
        },
        ["Muratec/Wilson (2.4mm)"] = new()
        {
            Depth = 2.4,
            Factors = [
                new() { Thickness = 1.2, Factor = 0.50 }, new() { Thickness = 1.6, Factor = 0.50 },
                new() { Thickness = 2.0, Factor = 0.50 }, new() { Thickness = 2.3, Factor = 0.50 },
                new() { Thickness = 3.0, Factor = 0.61 }, new() { Thickness = 3.2, Factor = 0.63 },
                new() { Thickness = 4.0, Factor = 0.71 }, new() { Thickness = 4.5, Factor = 0.75 },
                new() { Thickness = 6.3, Factor = 0.83 }, new() { Thickness = 9.5, Factor = 0.90 },
            ]
        },
        ["None (Flat)"] = new() { Depth = 0, Factors = [] },
    };
}
