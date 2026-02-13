using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using TurretApp.Models;
using TurretApp.Calculators;

namespace TurretApp.Views;

public partial class ClearancePage : Page
{
    // Textbook clearance percentages per material (% of thickness, per side)
    private static readonly Dictionary<string, double> TextbookClearance = new()
    {
        { "MildSteel",  12.5 },
        { "SS304",      15.0 },
        { "SS316",      15.0 },
        { "AL5052",     10.0 },
        { "AL6061",     10.0 },
        { "Copper",      8.0 },
        { "Brass",       8.0 },
        { "Galvanized", 12.5 },
    };

    // Empirical adjustments (shop-proven, typically tighter or wider than textbook)
    private static readonly Dictionary<string, double> EmpiricalClearance = new()
    {
        { "MildSteel",  15.0 },
        { "SS304",      17.5 },
        { "SS316",      18.0 },
        { "AL5052",     12.0 },
        { "AL6061",     12.0 },
        { "Copper",     10.0 },
        { "Brass",      10.0 },
        { "Galvanized", 15.0 },
    };

    // Operation multipliers
    private static readonly Dictionary<string, double> OperationMultiplier = new()
    {
        { "Standard", 1.0 },
        { "Nibble",   1.25 },  // nibbling needs more clearance
        { "Louver",   0.75 },  // forms typically need tighter
    };

    public class EmpiricalRow
    {
        public string Material { get; set; } = "";
        public string Thickness { get; set; } = "";
        public string ClearancePct { get; set; } = "";
        public string Notes { get; set; } = "";
    }

    private readonly ObservableCollection<EmpiricalRow> _empiricalData = new();

    public ClearancePage()
    {
        InitializeComponent();
        CboMaterial.SelectedIndex = 0;
        CboOperation.SelectedIndex = 0;
        DgEmpirical.ItemsSource = _empiricalData;

        // Seed with a couple example rows
        _empiricalData.Add(new EmpiricalRow { Material = "Mild Steel", Thickness = "1.500", ClearancePct = "15.0", Notes = "Standard round punch, verified 2024" });
        _empiricalData.Add(new EmpiricalRow { Material = "SS 304", Thickness = "2.000", ClearancePct = "17.5", Notes = "Slight burr at 15%, increased" });
    }

    private void OnInputChanged(object sender, EventArgs e)
    {
        if (CboMaterial == null || TxtThickness == null || CboOperation == null) return;

        var matItem = CboMaterial.SelectedItem as ComboBoxItem;
        var opItem = CboOperation.SelectedItem as ComboBoxItem;
        if (matItem == null || opItem == null) return;

        string matTag = matItem.Tag?.ToString() ?? "MildSteel";
        string opTag = opItem.Tag?.ToString() ?? "Standard";

        if (!double.TryParse(TxtThickness.Text, out double thickness) || thickness <= 0)
        {
            ClearResults();
            return;
        }

        double textbookPct = TextbookClearance.GetValueOrDefault(matTag, 12.5);
        double empiricalPct = EmpiricalClearance.GetValueOrDefault(matTag, 15.0);
        double opMult = OperationMultiplier.GetValueOrDefault(opTag, 1.0);

        // Apply operation multiplier
        double adjTextbookPct = textbookPct * opMult;
        double adjEmpiricalPct = empiricalPct * opMult;

        // Per-side clearance = thickness * (percentage / 100)
        double textbookPerSide = thickness * (adjTextbookPct / 100.0);
        double empiricalPerSide = thickness * (adjEmpiricalPct / 100.0);

        // Total (diameter clearance) = 2 * per-side
        double textbookTotal = textbookPerSide * 2.0;
        double empiricalTotal = empiricalPerSide * 2.0;

        TxtTextbookPct.Text = $"{adjTextbookPct:F1}%";
        TxtTextbookPerSide.Text = $"{textbookPerSide:F4} mm";
        TxtTextbookTotal.Text = $"{textbookTotal:F4} mm";

        TxtEmpiricalPct.Text = $"{adjEmpiricalPct:F1}%";
        TxtEmpiricalPerSide.Text = $"{empiricalPerSide:F4} mm";
        TxtEmpiricalTotal.Text = $"{empiricalTotal:F4} mm";
    }

    private void ClearResults()
    {
        TxtTextbookPct.Text = "--";
        TxtTextbookPerSide.Text = "--";
        TxtTextbookTotal.Text = "--";
        TxtEmpiricalPct.Text = "--";
        TxtEmpiricalPerSide.Text = "--";
        TxtEmpiricalTotal.Text = "--";
    }

    private void OnAddRow(object sender, RoutedEventArgs e)
    {
        var matItem = CboMaterial.SelectedItem as ComboBoxItem;
        string matName = matItem?.Content?.ToString() ?? "Unknown";
        string thickness = TxtThickness.Text;

        _empiricalData.Add(new EmpiricalRow
        {
            Material = matName,
            Thickness = thickness,
            ClearancePct = "",
            Notes = "New entry"
        });
    }

    private void OnDeleteRow(object sender, RoutedEventArgs e)
    {
        if (DgEmpirical.SelectedItem is EmpiricalRow row)
        {
            _empiricalData.Remove(row);
        }
    }
}
