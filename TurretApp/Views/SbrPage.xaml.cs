using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TurretApp.Models;
using TurretApp.Calculators;

namespace TurretApp.Views;

public partial class SbrPage : Page
{
    // Tool style specs: (NewOAL, NewSBR, MaxGrind, MinSbrFactor)
    // MinSbrFactor: minimum SBR = thickness * factor
    private static readonly Dictionary<string, (double OAL, double SBR, double MaxGrind, double MinFactor)> ToolSpecs = new()
    {
        { "MateStd",    (40.100, 3.000, 5.000, 1.5) },
        { "MateUltra",  (40.100, 3.500, 6.000, 1.5) },
        { "EuroStd",    (46.000, 3.000, 5.000, 1.5) },
        { "EuroThick",  (59.900, 4.000, 8.000, 2.0) },
    };

    public SbrPage()
    {
        InitializeComponent();
        CboToolStyle.SelectedIndex = 0;
        CboToolType.SelectedIndex = 0;
    }

    private void OnInputChanged(object sender, EventArgs e)
    {
        if (CboToolStyle == null || TxtMatThickness == null || TxtActualSbr == null) return;

        var styleItem = CboToolStyle.SelectedItem as ComboBoxItem;
        if (styleItem == null) return;

        string styleTag = styleItem.Tag?.ToString() ?? "MateStd";
        var spec = ToolSpecs.GetValueOrDefault(styleTag, ToolSpecs["MateStd"]);

        // Update tool specs display
        TxtSpecOal.Text = $"{spec.OAL:F3} mm";
        TxtSpecSbr.Text = $"{spec.SBR:F3} mm";
        TxtSpecMaxGrind.Text = $"{spec.MaxGrind:F3} mm";
        TxtSpecMinFactor.Text = $"{spec.MinFactor:F1}x thickness";

        if (!double.TryParse(TxtMatThickness.Text, out double thickness) || thickness <= 0)
        {
            ClearResults();
            return;
        }

        // Minimum SBR required = thickness * factor
        double minSbrRequired = thickness * spec.MinFactor;
        TxtMinSbrRequired.Text = $"{minSbrRequired:F3} mm";
        TxtMinSbrFormula.Text = $"= {thickness:F3} x {spec.MinFactor:F1}";

        // Max grind life = amount that can be ground from new SBR before reaching min SBR
        // But also capped by the max grind from OAL perspective
        double sbrBasedMaxGrind = spec.SBR - minSbrRequired;
        double effectiveMaxGrind = Math.Min(spec.MaxGrind, sbrBasedMaxGrind);
        if (effectiveMaxGrind < 0) effectiveMaxGrind = 0;

        TxtMaxGrindLife.Text = $"{effectiveMaxGrind:F3} mm";
        TxtMinOal.Text = $"{(spec.OAL - spec.MaxGrind):F3} mm";

        if (!double.TryParse(TxtActualSbr.Text, out double actualSbr) || actualSbr <= 0)
        {
            TxtGrindLife.Text = "--";
            TxtGrindLifePct.Text = "Enter actual SBR measurement";
            GrindLifeBar.Width = 0;
            UpdateStatus("AWAITING SBR MEASUREMENT", "TextMutedBrush", "BgTertiaryBrush");
            return;
        }

        // Remaining grind life = actual SBR - min SBR required
        double remainingGrind = actualSbr - minSbrRequired;
        if (remainingGrind < 0) remainingGrind = 0;

        TxtGrindLife.Text = $"{remainingGrind:F3} mm";

        // Percentage of total grind life remaining
        double grindPct = effectiveMaxGrind > 0 ? (remainingGrind / effectiveMaxGrind) * 100.0 : 0;
        grindPct = Math.Clamp(grindPct, 0, 100);
        TxtGrindLifePct.Text = $"{grindPct:F1}% of grind life remaining";

        // Progress bar (max width ~300 for the panel)
        double barMaxWidth = 300;
        GrindLifeBar.Width = barMaxWidth * grindPct / 100.0;

        // Color code grind life bar
        if (grindPct > 50)
        {
            GrindLifeBar.Background = (Brush)FindResource("GoodBrush");
            TxtGrindLife.Foreground = (Brush)FindResource("GoodBrush");
        }
        else if (grindPct > 20)
        {
            GrindLifeBar.Background = (Brush)FindResource("WarnBrush");
            TxtGrindLife.Foreground = (Brush)FindResource("WarnBrush");
        }
        else
        {
            GrindLifeBar.Background = (Brush)FindResource("BadBrush");
            TxtGrindLife.Foreground = (Brush)FindResource("BadBrush");
        }

        // Status badge
        if (actualSbr < minSbrRequired)
        {
            UpdateStatus("FAIL - BELOW MINIMUM SBR", "BadBrush", null);
            StatusBadge.Background = new SolidColorBrush(Color.FromArgb(50, 255, 60, 60));
        }
        else if (grindPct < 20)
        {
            UpdateStatus("WARNING - LOW GRIND LIFE", "WarnBrush", null);
            StatusBadge.Background = new SolidColorBrush(Color.FromArgb(50, 255, 200, 60));
        }
        else
        {
            UpdateStatus("PASS - SBR WITHIN SPEC", "GoodBrush", null);
            StatusBadge.Background = new SolidColorBrush(Color.FromArgb(50, 60, 255, 120));
        }
    }

    private void UpdateStatus(string text, string foregroundResource, string? backgroundResource)
    {
        TxtStatusBadge.Text = text;
        TxtStatusBadge.Foreground = (Brush)FindResource(foregroundResource);
        if (backgroundResource != null)
            StatusBadge.Background = (Brush)FindResource(backgroundResource);
    }

    private void ClearResults()
    {
        TxtMinSbrRequired.Text = "--";
        TxtMinSbrFormula.Text = "";
        TxtGrindLife.Text = "--";
        TxtGrindLife.Foreground = (Brush)FindResource("AccentBrightBrush");
        TxtGrindLifePct.Text = "";
        GrindLifeBar.Width = 0;
        TxtMaxGrindLife.Text = "--";
        TxtMinOal.Text = "--";
        UpdateStatus("AWAITING INPUT", "TextMutedBrush", "BgTertiaryBrush");
    }
}
