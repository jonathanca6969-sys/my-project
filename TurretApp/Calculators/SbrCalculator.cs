using TurretApp.Models;

namespace TurretApp.Calculators;

public class SbrResult
{
    public double MinSBRRequired { get; set; }
    public double RemainingGrindLife { get; set; }
    public double MaxGrindLife { get; set; }
    public double MinOAL { get; set; }
    public double GrindLifePercent { get; set; }
    public string Status { get; set; } = "";
    public string StatusDesc { get; set; } = "";
}

public static class SbrCalculator
{
    private const double SafetyMargin = 1.0;

    public static SbrResult? Calculate(SbrToolInfo toolData, double materialThickness, double actualSBR)
    {
        double minSBRRequired = toolData.StripperLand + materialThickness + toolData.DiePen + SafetyMargin;
        double remainingGrindLife = actualSBR - minSBRRequired;
        double maxGrindLife = toolData.MaxSBR - minSBRRequired;
        double minOAL = toolData.OAL - (toolData.MaxSBR - minSBRRequired);
        double grindLifePercent = maxGrindLife > 0 ? (remainingGrindLife / maxGrindLife) * 100 : 0;

        string status, statusDesc;
        if (remainingGrindLife >= 3)
        {
            status = "GOOD";
            statusDesc = "Adequate grind life";
        }
        else if (remainingGrindLife >= 1)
        {
            status = "PLAN";
            statusDesc = "Schedule replacement";
        }
        else
        {
            status = "REPLACE";
            statusDesc = "Insufficient grind life";
        }

        return new SbrResult
        {
            MinSBRRequired = minSBRRequired,
            RemainingGrindLife = remainingGrindLife,
            MaxGrindLife = maxGrindLife,
            MinOAL = minOAL,
            GrindLifePercent = grindLifePercent,
            Status = status,
            StatusDesc = statusDesc,
        };
    }
}
