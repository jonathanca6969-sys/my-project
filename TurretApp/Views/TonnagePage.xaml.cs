using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace TurretApp.Views;

public partial class TonnagePage : Page
{
    private static readonly (string Name, double ShearMPa)[] MaterialList =
    [
        ("Mild Steel (CRS)",    345),
        ("High Tensile Steel",  500),
        ("304 Stainless",       480),
        ("316 Stainless",       580),
        ("5005 Aluminium",      121),
        ("6061 Aluminium",      193),
        ("Copper",              245),
        ("Brass",               310),
        ("Galvanised Steel",    345),
    ];

    private const double MildSteelRef = 345.0;
    private const double MetricTonneDivisor = 9806.65;
    private const double ShortTonDivisor = 8896.44;

    private static readonly (string Label, double Factor)[] ShearTypes =
    [
        ("Flat (None)", 1.00),
        ("1\u00B0 Shear", 0.75),
        ("2\u00B0 Shear", 0.60),
        ("3\u00B0 Shear", 0.50),
        ("Roof/Whisper", 0.55),
    ];

    private static readonly (string Label, double Factor)[] ConditionTypes =
    [
        ("Sharp", 1.00),
        ("Worn",  1.10),
        ("Dull",  1.25),
    ];

    private static readonly string[] EngagementStages = ["ALL", "FIRST TO", "SECOND TO", "THIRD TO"];
    private static readonly string[] PunchShapes = ["Round", "Square", "Rectangle", "Oblong", "Hexagon", "Special"];

    private readonly List<PunchRowControls> _punchRows = [];
    private double _cachedBarWidth;

    public TonnagePage()
    {
        InitializeComponent();
        foreach (var (name, _) in MaterialList)
            CboMaterial.Items.Add(new ComboBoxItem { Content = name });
        CboMaterial.SelectedIndex = 0;
        AddPunchRow();
    }

    private void OnAddPunch(object sender, RoutedEventArgs e) => AddPunchRow();

    private void OnGlobalInputChanged(object sender, EventArgs e)
    {
        UpdateMultiplierDisplay();
        Recalculate();
    }

    private void OnCapacityBarSizeChanged(object sender, SizeChangedEventArgs e)
    {
        _cachedBarWidth = e.NewSize.Width;
        Recalculate();
    }

    private void UpdateMultiplierDisplay()
    {
        if (CboMaterial.SelectedIndex < 0) return;
        double shear = MaterialList[CboMaterial.SelectedIndex].ShearMPa;
        TxtMultiplier.Text = $"{shear / MildSteelRef:F2}x";
    }

    // ── Punch Row Builder ──────────────────────────────────────────

    private void AddPunchRow()
    {
        var row = new PunchRowControls();

        var border = new Border
        {
            Style = (Style)FindResource("SectionPanel"),
            Margin = new Thickness(0, 0, 0, 8),
        };

        var grid = new Grid();

        // Proportional columns matching the screenshot layout:
        // Shape(1.1*) | Diameter(2*) | PinQty(0.8*) | Stage(1.2*) | Offset(0.8*) | Shear(1.2*) | Condition(0.9*) | Tonnage(1*) | Delete(Auto)
        // With 6px gaps between each
        var colDefs = new (double stars, bool isStar)[]
        {
            (1.1, true),  // 0: shape
            (6, false),   // 1: gap
            (2.0, true),  // 2: diameter
            (6, false),   // 3: gap
            (0.8, true),  // 4: pin qty
            (6, false),   // 5: gap
            (1.2, true),  // 6: engagement stage
            (6, false),   // 7: gap
            (0.8, true),  // 8: height offset
            (6, false),   // 9: gap
            (1.2, true),  // 10: shear type
            (6, false),   // 11: gap
            (0.9, true),  // 12: condition
            (6, false),   // 13: gap
            (1.0, true),  // 14: tonnage
            (6, false),   // 15: gap
            (30, false),  // 16: delete
        };

        foreach (var (val, isStar) in colDefs)
            grid.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = isStar ? new GridLength(val, GridUnitType.Star) : new GridLength(val, GridUnitType.Pixel)
            });

        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // labels
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // controls

        // Labels (row 0)
        SetLabel(grid, 0, 0, "PUNCH SHAPE");
        SetLabel(grid, 0, 2, "DIAMETER");
        SetLabel(grid, 0, 4, "PIN QUANTITY");
        SetLabel(grid, 0, 6, "ENGAGEMENT STAGE");
        SetLabel(grid, 0, 8, "HEIGHT OFFSET");
        SetLabel(grid, 0, 10, "SHEAR TYPE");
        SetLabel(grid, 0, 12, "CONDITION");

        // Controls (row 1)
        row.CboShape = MakeCombo(PunchShapes, 0);
        row.CboShape.SelectionChanged += (_, _) => Recalculate();
        SetCell(grid, row.CboShape, 1, 0);

        row.TxtDiameter = MakeTextBox("0");
        row.TxtDiameter.TextChanged += (_, _) => Recalculate();
        SetCell(grid, row.TxtDiameter, 1, 2);

        row.TxtPinQty = MakeTextBox("1");
        row.TxtPinQty.TextChanged += (_, _) => Recalculate();
        SetCell(grid, row.TxtPinQty, 1, 4);

        row.CboStage = MakeCombo(EngagementStages, 0);
        row.CboStage.SelectionChanged += (_, _) => Recalculate();
        SetCell(grid, row.CboStage, 1, 6);

        row.TxtHeightOffset = MakeTextBox("0");
        row.TxtHeightOffset.TextChanged += (_, _) => Recalculate();
        SetCell(grid, row.TxtHeightOffset, 1, 8);

        row.CboShear = MakeCombo(ShearTypes.Select(s => s.Label).ToArray(), 0);
        row.CboShear.SelectionChanged += (_, _) => Recalculate();
        SetCell(grid, row.CboShear, 1, 10);

        row.CboCondition = MakeCombo(ConditionTypes.Select(c => c.Label).ToArray(), 0);
        row.CboCondition.SelectionChanged += (_, _) => Recalculate();
        SetCell(grid, row.CboCondition, 1, 12);

        // Tonnage display - green bordered box
        var tonnageBorder = new Border
        {
            Background = (Brush)FindResource("BgInputBrush"),
            BorderBrush = (Brush)FindResource("AccentBrush"),
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(6),
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        row.TxtTonnage = new TextBlock
        {
            Text = "0.00",
            FontSize = 18,
            FontWeight = FontWeights.Bold,
            Foreground = (Brush)FindResource("AccentBrightBrush"),
            FontFamily = (FontFamily)FindResource("TerminalFont"),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        tonnageBorder.Child = row.TxtTonnage;
        SetCell(grid, tonnageBorder, 1, 14);

        // Delete button - red circle
        var btnDelete = new Button
        {
            Content = "\u00D7",
            Style = (Style)FindResource("DeleteCircleButton"),
            VerticalAlignment = VerticalAlignment.Center,
        };
        btnDelete.Click += (_, _) => RemovePunchRow(row, border);
        SetCell(grid, btnDelete, 1, 16);

        border.Child = grid;
        row.Container = border;
        _punchRows.Add(row);
        PunchRowsPanel.Children.Add(border);
        Recalculate();
    }

    private void RemovePunchRow(PunchRowControls row, Border container)
    {
        _punchRows.Remove(row);
        PunchRowsPanel.Children.Remove(container);
        Recalculate();
    }

    // ── Calculation ────────────────────────────────────────────────

    private void Recalculate()
    {
        if (CboMaterial == null || TxtThickness == null || TxtMachineCapacity == null || CboUnits == null) return;
        if (CboMaterial.SelectedIndex < 0) return;

        double shearMPa = MaterialList[CboMaterial.SelectedIndex].ShearMPa;

        if (!double.TryParse(TxtThickness.Text, out double thickness) || thickness <= 0)
        {
            ClearResults();
            return;
        }

        bool isMetric = (CboUnits.SelectedItem as ComboBoxItem)?.Tag?.ToString() == "MT";
        double divisor = isMetric ? MetricTonneDivisor : ShortTonDivisor;
        TxtUnitLabel.Text = isMetric ? "METRIC TONNES" : "SHORT TONS";

        var stageGroups = new Dictionary<string, double>();

        foreach (var row in _punchRows)
        {
            double tonnage = CalcRowTonnage(row, shearMPa, thickness, divisor);
            row.TxtTonnage.Text = $"{tonnage:F2}";

            string stage = GetSelectedText(row.CboStage) ?? "ALL";
            stageGroups.TryAdd(stage, 0);
            stageGroups[stage] += tonnage;
        }

        // Peak load: max across stagger groups (ALL adds to every group)
        double peakLoad;
        if (stageGroups.Count == 0)
            peakLoad = 0;
        else if (stageGroups.Count == 1 && stageGroups.ContainsKey("ALL"))
            peakLoad = stageGroups["ALL"];
        else
        {
            double allBase = stageGroups.GetValueOrDefault("ALL", 0);
            var staged = stageGroups.Where(kv => kv.Key != "ALL").ToList();
            peakLoad = staged.Count == 0 ? allBase : staged.Max(kv => kv.Value + allBase);
        }

        double.TryParse(TxtMachineCapacity.Text, out double capacity);
        if (capacity <= 0) capacity = 20;
        double pct = (peakLoad / capacity) * 100.0;

        TxtPeakLoad.Text = $"{peakLoad:F2}";
        TxtCapacityPct.Text = $"{pct:F1}% OF MACHINE CAPACITY";

        if (pct > 100)
        {
            TxtPeakLoad.Foreground = (Brush)FindResource("BadBrush");
            TxtCapacityPct.Foreground = (Brush)FindResource("BadBrush");
        }
        else if (pct > 80)
        {
            TxtPeakLoad.Foreground = (Brush)FindResource("WarnBrush");
            TxtCapacityPct.Foreground = (Brush)FindResource("WarnBrush");
        }
        else
        {
            TxtPeakLoad.Foreground = (Brush)FindResource("AccentBrightBrush");
            TxtCapacityPct.Foreground = (Brush)FindResource("AccentBrush");
        }

        UpdateCapacityBar(pct);
    }

    private double CalcRowTonnage(PunchRowControls row, double shearMPa, double thickness, double divisor)
    {
        string shape = GetSelectedText(row.CboShape) ?? "Round";
        if (!double.TryParse(row.TxtDiameter.Text, out double dim) || dim <= 0) return 0;
        if (!int.TryParse(row.TxtPinQty.Text, out int pins) || pins < 1) pins = 1;

        double perimeter = shape switch
        {
            "Round" => Math.PI * dim,
            "Square" => 4.0 * dim,
            "Rectangle" => 4.0 * dim,
            "Oblong" => Math.PI * (dim / 2.0) + 2.0 * dim,
            "Hexagon" => 6.0 * (dim / Math.Sqrt(3)),
            "Special" => dim,
            _ => Math.PI * dim,
        };

        int shearIdx = row.CboShear.SelectedIndex;
        double shearFactor = shearIdx >= 0 && shearIdx < ShearTypes.Length ? ShearTypes[shearIdx].Factor : 1.0;

        int condIdx = row.CboCondition.SelectedIndex;
        double condFactor = condIdx >= 0 && condIdx < ConditionTypes.Length ? ConditionTypes[condIdx].Factor : 1.0;

        double forceN = perimeter * pins * thickness * shearMPa;
        return (forceN / divisor) * shearFactor * condFactor;
    }

    private void UpdateCapacityBar(double pct)
    {
        double barWidth = _cachedBarWidth > 0 ? _cachedBarWidth : 600;
        double maxPct = 130.0;
        double markerPos = (100.0 / maxPct) * barWidth;
        double fillWidth = Math.Clamp(pct / maxPct, 0, 1) * barWidth;

        CapacityBar.Width = fillWidth;
        CapacityMarker.Margin = new Thickness(markerPos, 0, 0, 0);

        CapacityBar.Background = pct > 100
            ? (Brush)FindResource("BadBrush")
            : pct > 80
                ? (Brush)FindResource("WarnBrush")
                : (Brush)FindResource("AccentBrush");
    }

    private void ClearResults()
    {
        TxtPeakLoad.Text = "0.00";
        TxtPeakLoad.Foreground = (Brush)FindResource("AccentBrightBrush");
        TxtCapacityPct.Text = "0.0% OF MACHINE CAPACITY";
        TxtCapacityPct.Foreground = (Brush)FindResource("AccentBrush");
        CapacityBar.Width = 0;
        foreach (var row in _punchRows)
            row.TxtTonnage.Text = "0.00";
    }

    // ── Helpers ─────────────────────────────────────────────────────

    private static string? GetSelectedText(ComboBox cbo) =>
        (cbo.SelectedItem as ComboBoxItem)?.Content?.ToString();

    private static void SetCell(Grid grid, UIElement element, int row, int col)
    {
        Grid.SetRow(element, row);
        Grid.SetColumn(element, col);
        grid.Children.Add(element);
    }

    private static void SetLabel(Grid grid, int row, int col, string text)
    {
        var tb = new TextBlock
        {
            Text = text,
            FontSize = 10,
            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#33bb66")!),
            FontFamily = new FontFamily("Consolas"),
            Margin = new Thickness(0, 0, 0, 4),
        };
        SetCell(grid, tb, row, col);
    }

    private static ComboBox MakeCombo(string[] items, int selectedIndex)
    {
        var cbo = new ComboBox { FontSize = 12, Padding = new Thickness(6, 4, 6, 4) };
        foreach (var item in items)
            cbo.Items.Add(new ComboBoxItem { Content = item });
        cbo.SelectedIndex = selectedIndex;
        return cbo;
    }

    private static TextBox MakeTextBox(string text) => new()
    {
        Text = text,
        FontSize = 14,
        Padding = new Thickness(6, 4, 6, 4),
        HorizontalContentAlignment = HorizontalAlignment.Center,
    };

    private class PunchRowControls
    {
        public Border Container { get; set; } = null!;
        public ComboBox CboShape { get; set; } = null!;
        public TextBox TxtDiameter { get; set; } = null!;
        public TextBox TxtPinQty { get; set; } = null!;
        public ComboBox CboStage { get; set; } = null!;
        public TextBox TxtHeightOffset { get; set; } = null!;
        public ComboBox CboShear { get; set; } = null!;
        public ComboBox CboCondition { get; set; } = null!;
        public TextBlock TxtTonnage { get; set; } = null!;
    }
}
