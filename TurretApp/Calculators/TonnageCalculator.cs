using TurretApp.Models;

namespace TurretApp.Calculators;

public class PunchInput
{
    public int Id { get; set; }
    public string Shape { get; set; } = "Round";
    public double SmallerDim { get; set; }
    public double LargerDim { get; set; }
    public double CornerRadius { get; set; }
    public int Quantity { get; set; } = 1;
}

public class PunchResult
{
    public int Id { get; set; }
    public string Shape { get; set; } = "";
    public double Dimension { get; set; }
    public int Quantity { get; set; }
    public double Perimeter { get; set; }
    public double SingleTonnage { get; set; }
    public double TonnageWithShear { get; set; }
    public double TypeTotalTonnage { get; set; }
}

public class TonnageResult
{
    public List<PunchResult> PunchResults { get; set; } = [];
    public int TotalPins { get; set; }
    public double ShearFactor { get; set; }
    public double TonnageReduction { get; set; }
    public double TotalTonnage { get; set; }
    public double PeakTonnage { get; set; }
    public int PinsInPeakGroup { get; set; }
    public double CapacityUsed { get; set; }
    public string Status { get; set; } = "";
    public double ShearStrength { get; set; }
}

public static class TonnageCalculator
{
    public static double CalculatePerimeter(PunchInput punch)
    {
        double smaller = punch.SmallerDim;
        double larger = punch.LargerDim > 0 ? punch.LargerDim : smaller;
        double radius = punch.CornerRadius;

        return punch.Shape switch
        {
            "Round" => Math.PI * smaller,
            "Rectangle" => radius > 0 && radius < Math.Min(smaller, larger) / 2.0
                ? (2 * (larger - 2 * radius)) + (2 * (smaller - 2 * radius)) + (2 * Math.PI * radius)
                : 2 * (smaller + larger),
            "Square" => 4 * smaller,
            "Oval" or "Obround" => (2 * (larger - smaller)) + (Math.PI * smaller),
            "Hexagon" => 6 * (smaller / Math.Sqrt(3)),
            _ => 0,
        };
    }

    public static double GetShearFactor(double t, string tableKey)
    {
        if (tableKey == "None (Flat)") return 1.0;
        if (!ToolData.ShearTables.TryGetValue(tableKey, out var table) || t <= 0) return 1.0;
        if (t <= table.Depth) return 0.50;

        var factors = table.Factors;
        for (int i = 0; i < factors.Length; i++)
        {
            if (t <= factors[i].Thickness)
            {
                if (i == 0) return factors[0].Factor;
                var prev = factors[i - 1];
                var curr = factors[i];
                return prev.Factor + ((t - prev.Thickness) / (curr.Thickness - prev.Thickness)) * (curr.Factor - prev.Factor);
            }
        }

        return Math.Min((table.Depth / t) + 0.5, 1.0);
    }

    public static TonnageResult? Calculate(List<PunchInput> punches, double thickness, double shearStrength,
        string shearTableKey, double machineCapacity, double staggerHeight, int pinsPerGroup)
    {
        if (thickness <= 0 || shearStrength <= 0) return null;

        var punchResults = new List<PunchResult>();
        double shearFactor = GetShearFactor(thickness, shearTableKey);

        foreach (var punch in punches)
        {
            double perimeter = CalculatePerimeter(punch);
            if (perimeter <= 0 || punch.Quantity <= 0) continue;

            double forceKgf = perimeter * thickness * shearStrength;
            double singleTonnage = forceKgf / 1000.0;
            double tonnageWithShear = singleTonnage * shearFactor;

            punchResults.Add(new PunchResult
            {
                Id = punch.Id,
                Shape = punch.Shape,
                Dimension = punch.SmallerDim,
                Quantity = punch.Quantity,
                Perimeter = perimeter,
                SingleTonnage = singleTonnage,
                TonnageWithShear = tonnageWithShear,
                TypeTotalTonnage = tonnageWithShear * punch.Quantity,
            });
        }

        if (punchResults.Count == 0) return null;

        int totalPins = punchResults.Sum(p => p.Quantity);
        double totalTonnage = punchResults.Sum(p => p.TypeTotalTonnage);

        int pinsInPeakGroup = (staggerHeight > 0 && pinsPerGroup > 0 && pinsPerGroup < totalPins)
            ? pinsPerGroup : totalPins;
        double peakTonnage = (staggerHeight > 0 && pinsPerGroup > 0 && pinsPerGroup < totalPins)
            ? totalTonnage * ((double)pinsPerGroup / totalPins) : totalTonnage;
        double capacityUsed = (peakTonnage / machineCapacity) * 100;

        string status = capacityUsed switch
        {
            <= 60 => "GOOD",
            <= 80 => "ACCEPTABLE",
            <= 100 => "AT LIMIT",
            _ => "EXCEEDS",
        };

        return new TonnageResult
        {
            PunchResults = punchResults,
            TotalPins = totalPins,
            ShearFactor = shearFactor,
            TonnageReduction = (1 - shearFactor) * 100,
            TotalTonnage = totalTonnage,
            PeakTonnage = peakTonnage,
            PinsInPeakGroup = pinsInPeakGroup,
            CapacityUsed = capacityUsed,
            Status = status,
            ShearStrength = shearStrength,
        };
    }
}
