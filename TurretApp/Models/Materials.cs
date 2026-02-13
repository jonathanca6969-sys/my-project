namespace TurretApp.Models;

public class MaterialGrade
{
    public string Name { get; set; } = "";
    public double Shear { get; set; } // Kgf/mm²
}

public class ClearanceBracket
{
    public double MaxThickness { get; set; }
    public double Piercing { get; set; } // percentage
    public double Blanking { get; set; } // percentage
}

public class EuromacRange
{
    public double Min { get; set; }
    public double Rec { get; set; }
    public double Max { get; set; }
}

public static class Materials
{
    public static readonly Dictionary<string, MaterialGrade[]> Categories = new()
    {
        ["Aluminium"] = [
            new() { Name = "5005 Soft (O)", Shear = 7.1 },
            new() { Name = "5005 H32", Shear = 8.5 },
            new() { Name = "5005 H34", Shear = 9.9 },
            new() { Name = "6061-T6", Shear = 21.0 },
        ],
        ["Steel"] = [
            new() { Name = "Mild Steel (CR)", Shear = 43.0 },
            new() { Name = "Mild Steel (HR)", Shear = 50.0 },
            new() { Name = "Stainless 304", Shear = 52.0 },
            new() { Name = "Stainless 316", Shear = 55.0 },
        ],
        ["Other"] = [
            new() { Name = "Copper (Rolled)", Shear = 30.0 },
            new() { Name = "Brass (Soft)", Shear = 24.1 },
        ],
    };

    public static readonly Dictionary<string, ClearanceBracket[]> TextbookData = new()
    {
        ["Aluminium"] = [
            new() { MaxThickness = 2.5, Piercing = 15, Blanking = 15 },
            new() { MaxThickness = 5.0, Piercing = 20, Blanking = 15 },
            new() { MaxThickness = double.PositiveInfinity, Piercing = 25, Blanking = 20 },
        ],
        ["Mild Steel"] = [
            new() { MaxThickness = 3.0, Piercing = 20, Blanking = 15 },
            new() { MaxThickness = 6.0, Piercing = 25, Blanking = 20 },
            new() { MaxThickness = double.PositiveInfinity, Piercing = 30, Blanking = 20 },
        ],
        ["Stainless Steel"] = [
            new() { MaxThickness = 1.5, Piercing = 20, Blanking = 15 },
            new() { MaxThickness = 2.8, Piercing = 25, Blanking = 20 },
            new() { MaxThickness = 4.0, Piercing = 30, Blanking = 20 },
            new() { MaxThickness = double.PositiveInfinity, Piercing = 35, Blanking = 25 },
        ],
        ["Copper/Brass"] = [
            new() { MaxThickness = 2.0, Piercing = 8, Blanking = 8 },
            new() { MaxThickness = 4.0, Piercing = 12, Blanking = 10 },
            new() { MaxThickness = double.PositiveInfinity, Piercing = 16, Blanking = 14 },
        ],
    };

    public static readonly Dictionary<string, EuromacRange> EuromacData = new()
    {
        ["Aluminium"] = new() { Min = 5, Rec = 10, Max = 15 },
        ["Mild Steel"] = new() { Min = 10, Rec = 15, Max = 20 },
        ["Stainless Steel"] = new() { Min = 15, Rec = 20, Max = 25 },
        ["Copper/Brass"] = new() { Min = 6, Rec = 11, Max = 16 },
    };
}
