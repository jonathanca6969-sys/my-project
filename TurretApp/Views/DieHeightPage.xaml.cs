using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TurretApp.Models;
using TurretApp.Calculators;

namespace TurretApp.Views;

public partial class DieHeightPage : Page
{
    // Die specs: NewHeight, MinHeight, GrindLimit (mm)
    private static readonly (double NewHeight, double MinHeight, double GrindLimit)[] DieSpecs =
    {
        (20.000, 18.800, 1.200),  // A - 12.7mm
        (20.000, 18.600, 1.400),  // B - 24mm
        (20.000, 18.600, 1.400),  // C - 31mm
        (30.000, 28.000, 2.000),  // D - 89mm
    };

    // Common shim sizes in mm
    private static readonly double[] ShimSizes = { 0.050, 0.100, 0.150, 0.200, 0.250, 0.300, 0.500, 1.000 };

    public DieHeightPage()
    {
        InitializeComponent();
        CboDieType.SelectedIndex = 0;
    }

    private void OnInputChanged(object sender, EventArgs e)
    {
        if (CboDieType == null || TxtMeasured == null) return;

        int dieIndex = CboDieType.SelectedIndex;
        if (dieIndex < 0 || dieIndex >= DieSpecs.Length) return;

        var spec = DieSpecs[dieIndex];

        // Update spec display
        TxtNewHeight.Text = $"{spec.NewHeight:F3} mm";
        TxtMinHeight.Text = $"{spec.MinHeight:F3} mm";
        TxtGrindLimit.Text = $"{spec.GrindLimit:F3} mm";

        if (!double.TryParse(TxtMeasured.Text, out double measured))
        {
            ClearResults();
            TxtStatus.Text = "> Enter a valid measured die height...";
            return;
        }

        double.TryParse(TxtExistingShims.Text, out double existingShims);

        // Actual die height without any existing shims
        double actualDieHeight = measured;

        // Amount removed from original
        double removed = spec.NewHeight - actualDieHeight;
        if (removed < 0) removed = 0;

        // Life left percentage
        double totalGrindable = spec.GrindLimit;
        double lifeLeft = totalGrindable > 0 ? Math.Max(0, (totalGrindable - removed) / totalGrindable * 100.0) : 0;

        // Shim required to bring back to new height
        double shimRequired = spec.NewHeight - actualDieHeight - existingShims;
        if (shimRequired < 0) shimRequired = 0;

        // Final height with shims
        double finalHeight = actualDieHeight + existingShims + shimRequired;

        // Update result displays
        TxtRemoved.Text = $"{removed:F3}";
        TxtRemoved.Foreground = removed > spec.GrindLimit * 0.8
            ? (Brush)FindResource("BadBrush")
            : removed > spec.GrindLimit * 0.5
                ? (Brush)FindResource("WarnBrush")
                : (Brush)FindResource("GoodBrush");

        TxtLifeLeft.Text = $"{lifeLeft:F1}%";
        TxtLifeLeft.Foreground = lifeLeft < 20
            ? (Brush)FindResource("BadBrush")
            : lifeLeft < 50
                ? (Brush)FindResource("WarnBrush")
                : (Brush)FindResource("GoodBrush");

        TxtShimRequired.Text = $"{shimRequired:F3}";
        TxtShimRequired.Foreground = shimRequired > 0
            ? (Brush)FindResource("AccentBrightBrush")
            : (Brush)FindResource("TextBrush");

        TxtFinalHeight.Text = $"{finalHeight:F3}";

        // Calculate shim combination
        string combo = CalculateShimCombo(shimRequired);
        TxtShimCombo.Text = shimRequired <= 0.001
            ? "No shims needed - die is at spec or has sufficient existing shims."
            : combo;

        // Status
        if (actualDieHeight < spec.MinHeight)
        {
            TxtStatus.Text = $"> WARNING: Die is below minimum height ({spec.MinHeight:F3} mm). REPLACE DIE.";
            TxtStatus.Foreground = (Brush)FindResource("BadBrush");
        }
        else if (lifeLeft < 20)
        {
            TxtStatus.Text = $"> CAUTION: Die nearing end of life ({lifeLeft:F1}% remaining).";
            TxtStatus.Foreground = (Brush)FindResource("WarnBrush");
        }
        else
        {
            TxtStatus.Text = $"> Calculation complete. Die has {lifeLeft:F1}% grind life remaining.";
            TxtStatus.Foreground = (Brush)FindResource("GoodBrush");
        }
    }

    private string CalculateShimCombo(double target)
    {
        if (target <= 0.001) return "None required";

        // Greedy approach from largest to smallest shim
        var combo = new System.Collections.Generic.List<string>();
        double remaining = target;

        for (int i = ShimSizes.Length - 1; i >= 0; i--)
        {
            int count = (int)(remaining / ShimSizes[i]);
            if (count > 0)
            {
                combo.Add($"{count}x {ShimSizes[i]:F3}mm");
                remaining -= count * ShimSizes[i];
            }
        }

        if (remaining > 0.005)
        {
            combo.Add($"+ {remaining:F3}mm (custom)");
        }

        return combo.Count > 0 ? string.Join("  +  ", combo) : $"{target:F3}mm (custom shim)";
    }

    private void ClearResults()
    {
        TxtRemoved.Text = "--";
        TxtLifeLeft.Text = "--";
        TxtShimRequired.Text = "--";
        TxtFinalHeight.Text = "--";
        TxtShimCombo.Text = "Select die type and enter measured height to calculate...";
        TxtRemoved.Foreground = (Brush)FindResource("TextBrush");
        TxtLifeLeft.Foreground = (Brush)FindResource("TextBrush");
        TxtShimRequired.Foreground = (Brush)FindResource("TextBrush");
        TxtStatus.Foreground = (Brush)FindResource("TextMutedBrush");
    }
}
