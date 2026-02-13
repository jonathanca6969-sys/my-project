namespace TurretApp.Models;

public class DieTypeInfo
{
    public string Name { get; set; } = "";
    public double NewHeight { get; set; }
    public double GrindLife { get; set; }
    public double MinHeight { get; set; }
    public string ShimPart { get; set; } = "";
    public double[] Shims { get; set; } = [];
    public double MaxOvershim { get; set; }
}

public static class DieTypes
{
    public static readonly Dictionary<string, DieTypeInfo> All = new()
    {
        ["12.7x20"] = new() { Name = "12.7 A STATION", NewHeight = 20.00, GrindLife = 1.50, MinHeight = 18.50, ShimPart = "MATE01130", Shims = [0.51, 0.20, 0.15, 0.10], MaxOvershim = 0.05 },
        ["24x24"] = new() { Name = "24 B STATION", NewHeight = 24.00, GrindLife = 1.50, MinHeight = 22.50, ShimPart = "MSFZ", Shims = [0.51, 0.20, 0.15, 0.10], MaxOvershim = 0.05 },
        ["31.7x30.15"] = new() { Name = "31 C STATION", NewHeight = 30.15, GrindLife = 3.18, MinHeight = 27.00, ShimPart = "MSAB", Shims = [1.20, 0.80, 0.40], MaxOvershim = 0.07 },
        ["Dx30.15"] = new() { Name = "89 D STATION", NewHeight = 30.15, GrindLife = 3.18, MinHeight = 27.00, ShimPart = "MSAD", Shims = [1.20, 0.80, 0.40], MaxOvershim = 0.07 },
    };
}
