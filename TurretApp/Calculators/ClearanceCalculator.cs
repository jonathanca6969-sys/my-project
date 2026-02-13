using TurretApp.Models;

namespace TurretApp.Calculators;

public class ClearanceResult
{
    public double TextbookPct { get; set; }
    public double TextbookMM { get; set; }
    public EuromacRange Euromac { get; set; } = new();
    public EmpiricalEntry? EmpiricalMatch { get; set; }
}

public static class ClearanceCalculator
{
    public static ClearanceResult Calculate(string material, double thickness, string operation, List<EmpiricalEntry> empiricalData)
    {
        var brackets = Materials.TextbookData.GetValueOrDefault(material)
                    ?? Materials.TextbookData["Mild Steel"];

        double textbookPct = 20;
        foreach (var bracket in brackets)
        {
            if (thickness <= bracket.MaxThickness)
            {
                textbookPct = operation == "Blanking" ? bracket.Blanking : bracket.Piercing;
                break;
            }
        }

        double textbookMM = thickness * textbookPct / 100.0;

        var euromac = Materials.EuromacData.GetValueOrDefault(material)
                   ?? Materials.EuromacData["Mild Steel"];

        var empiricalMatch = empiricalData.FirstOrDefault(e =>
            e.Material == material &&
            Math.Abs(e.Thickness - thickness) < 0.5 &&
            (e.Operation == operation || e.Operation == "All"));

        return new ClearanceResult
        {
            TextbookPct = textbookPct,
            TextbookMM = textbookMM,
            Euromac = euromac,
            EmpiricalMatch = empiricalMatch,
        };
    }
}
