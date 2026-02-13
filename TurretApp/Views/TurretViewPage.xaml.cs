using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;
using TurretApp.Models;

namespace TurretApp.Views;

public partial class TurretViewPage : Page
{
    // ══════════════════════════════════════════════════════════════
    //  MACHINE-SPECIFIC: Euromac MTX 12-30
    // ══════════════════════════════════════════════════════════════

    private static readonly int[] Multi6 = [101, 102, 103, 104, 105, 106];
    private static readonly int[] Multi10 = [401, 402, 403, 404, 405, 406, 407, 408, 409, 410];
    private static readonly int[] Singles = [2, 3, 5, 6];
    private static readonly HashSet<int> AllNums = [..Multi6, ..Singles, ..Multi10];

    // ══════════════════════════════════════════════════════════════
    //  MAIN CANVAS LAYOUT (1400×1400)
    // ══════════════════════════════════════════════════════════════

    private const double Cx = 700, Cy = 700;
    private const double OrbitR = 420;
    private const double HubR = 68;
    private static readonly double[] MainR = [200, 130, 130, 230, 130, 130];
    private const double Sub6R = 46, Sub6Orbit = 118;
    private const double Sub10R = 36, Sub10BR = 56, Sub10Orbit = 150;

    private static readonly double[] Ang =
    [
        120 * Math.PI / 180,  60 * Math.PI / 180,  0,
        300 * Math.PI / 180, 240 * Math.PI / 180, 180 * Math.PI / 180,
    ];

    // ══════════════════════════════════════════════════════════════
    //  ZOOM CANVAS LAYOUT (1200×1200)
    // ══════════════════════════════════════════════════════════════

    private const double Zc = 600;            // zoom canvas center
    private const double ZoomMultiR = 400;    // multi-tool housing radius in zoom
    private const double ZoomSubR = 70;       // sub-station radius in zoom L1 (A)
    private const double ZoomSubBR = 100;     // sub-station radius in zoom L1 (B: 401, 406)
    private const double ZoomSubOrbit = 265;  // sub-station orbit in zoom L1
    private const double ZoomSingleR = 330;   // D station radius in zoom
    private const double ZoomDetailR = 370;   // sub-station radius in zoom L2

    // ══════════════════════════════════════════════════════════════
    //  STATE
    // ══════════════════════════════════════════════════════════════

    private int _sel = -1;           // selected station (for detail panel)
    private int _zoomStation = -1;   // main station (1-6) being zoomed
    private int _zoomSub = -1;       // sub-station in level-2 zoom
    private Canvas _dc = null!;      // current draw-target canvas

    // Hit-test targets for zoom L1 sub-stations
    private readonly List<(int Num, double X, double Y, double R)> _zoomHits = [];

    // ══════════════════════════════════════════════════════════════
    //  ANIMATION STATE
    // ══════════════════════════════════════════════════════════════

    private int _animPhase;          // 0=stopped, 1-4=cycle phases
    private double _animMtxAngle;    // cumulative MTX orbit rotation (degrees)
    private double _animIdxAngle;    // cumulative indexing station rotation (degrees)
    private double _animMtxSpeed;    // degrees per tick
    private double _animIdxSpeed;    // degrees per tick
    private DispatcherTimer? _animTimer;

    // ══════════════════════════════════════════════════════════════
    //  INIT
    // ══════════════════════════════════════════════════════════════

    public TurretViewPage()
    {
        InitializeComponent();
        MigrateStations();
        Render();
        Unloaded += (_, _) => StopAnim();
    }

    private static string StnType(int n) => n switch
    {
        >= 101 and <= 106 => "B",
        401 or 406 => "B",
        >= 402 and <= 410 => "A",
        _ => "D"
    };

    private static bool IsSub(int n) => n >= 100;
    private static int MainStn(int n) => n >= 100 ? n / 100 : n;
    private static int Slot(int n) => n >= 100 ? n % 100 : 0;
    private static bool IsMulti(int main) => main == 1 || main == 4;

    // Odd main stations are indexing (1, 3, 5)
    private static bool IsIdx(int sel)
    {
        int main = sel < 100 ? sel : sel / 100;
        return main % 2 == 1;
    }

    // Get/set rotation for a station. Multi-6 rotation stored on stn 101.
    private double GetRotation(int sel)
    {
        int key = sel >= 101 && sel <= 106 ? 101 : sel;
        return Find(key)?.RotationDegrees ?? 0;
    }

    private void SetRotation(int sel, double deg)
    {
        int key = sel >= 101 && sel <= 106 ? 101 : sel;
        var stn = Find(key);
        if (stn != null) stn.RotationDegrees = deg;
        App.SaveState();
        CloseZoom();
        Render();
    }

    private void MigrateStations()
    {
        var have = App.State.TurretStations.Select(s => s.StationNumber).ToHashSet();
        if (AllNums.SetEquals(have)) return;
        App.State.TurretStations.Clear();
        foreach (int n in AllNums.Order())
            App.State.TurretStations.Add(new TurretStation
            { StationNumber = n, StationType = StnType(n) });
        App.SaveState();
    }

    private TurretStation? Find(int num) =>
        App.State.TurretStations.FirstOrDefault(s => s.StationNumber == num);

    private InventoryTool? Tool(int? id) =>
        id.HasValue ? App.State.ToolInventory.FirstOrDefault(t => t.Id == id.Value) : null;

    // ══════════════════════════════════════════════════════════════
    //  RENDER (main turret + detail panel)
    // ══════════════════════════════════════════════════════════════

    private void Render()
    {
        _dc = TurretCanvas;
        DrawTurret();
        ShowDetail();
    }

    private void DrawTurret()
    {
        TurretCanvas.Children.Clear();
        Ring(Cx, Cy, OrbitR + 30, Br("BorderBrush"), 1.5, 0.2);

        double mtxRad = _animMtxAngle * Math.PI / 180;

        for (int i = 0; i < 6; i++)
        {
            int stn = i + 1;
            double a = Ang[i] - mtxRad;
            double sx = Cx + OrbitR * Math.Cos(a);
            double sy = Cy - OrbitR * Math.Sin(a);

            Spoke(Cx, Cy, sx, sy);

            if (stn == 1)      DrawMulti(sx, sy, Multi6, Sub6R, Sub6Orbit, 1);
            else if (stn == 4) DrawMulti(sx, sy, Multi10, Sub10R, Sub10Orbit, 4);
            else               DrawSingle(sx, sy, stn);

            // External label
            double lr = OrbitR + MainR[i] + 20;
            double lx = Cx + lr * Math.Cos(a);
            double ly = Cy - lr * Math.Sin(a);
            string lbl = stn switch
            {
                1 => "STN 1\nMULTI-6 [B]\nIDX",
                4 => "STN 4\nMULTI-10 [A/B]\nROUND ONLY",
                _ => $"STN {stn} [D]" + (stn % 2 == 1 ? "\nIDX" : "")
            };
            Label(lx, ly, lbl, 22, Br("AccentBrush"), true);
        }

        Brush hubStroke = _animPhase > 0 ? Br("AccentBrush") : Br("BorderBrush");
        var hub = Ring(Cx, Cy, HubR, hubStroke, 2, 1, Br("BgSecondaryBrush"));
        hub.Cursor = Cursors.Hand;
        hub.MouseLeftButtonDown += OnHubClick;
        Label(Cx, Cy, "MTX", 26,
            _animPhase > 0 ? Br("AccentBrightBrush") : Br("TextMutedBrush"), true, false);
    }

    private void DrawMulti(double cx, double cy, int[] slots, double subR, double subOrbit, int mainStn)
    {
        double r = mainStn == 1 ? MainR[0] : MainR[3];

        // Housing circle (clickable)
        var housing = Ring(cx, cy, r, Br("BorderBrush"), 1.5, 1, Br("BgTertiaryBrush"));
        housing.Cursor = Cursors.Hand;
        housing.Tag = mainStn;
        housing.MouseLeftButtonDown += OnSlotClick;

        // Rotation offset (indexing stations only) + animation
        double rotRad = mainStn == 1 ? (GetRotation(101) + _animIdxAngle) * Math.PI / 180 : 0;

        for (int j = 0; j < slots.Length; j++)
        {
            double sa = 2 * Math.PI * j / slots.Length - Math.PI / 2 + rotRad;
            double sx = cx + subOrbit * Math.Cos(sa);
            double sy = cy + subOrbit * Math.Sin(sa);
            // 401 & 406 are B stations — draw larger than the A slots
            double slotR = (slots[j] == 401 || slots[j] == 406) ? Sub10BR : subR;
            DrawSlot(sx, sy, slotR, slots[j]);
        }

        // Rotation reference marker (line from center toward slot 1 position)
        if (mainStn == 1 && _animPhase == 0)
        {
            double refAngle = -Math.PI / 2 + rotRad;
            double rx = cx + (r - 4) * Math.Cos(refAngle);
            double ry = cy + (r - 4) * Math.Sin(refAngle);
            _dc.Children.Add(new Line
            {
                X1 = cx + 15 * Math.Cos(refAngle), Y1 = cy + 15 * Math.Sin(refAngle),
                X2 = rx, Y2 = ry,
                Stroke = Br("AccentDimBrush"), StrokeThickness = 1.5, Opacity = 0.6,
            });
        }

        Label(cx, cy, $"{mainStn}\u00B70", 22, Br("TextMutedBrush"), true);
    }

    private void DrawSingle(double cx, double cy, int stn)
    {
        DrawSlot(cx, cy, MainR[stn - 1], stn);
    }

    private void DrawSlot(double cx, double cy, double r, int num)
    {
        var stn = Find(num);
        bool has = stn?.MountedPunchId != null || stn?.MountedDieId != null;
        bool sel = num == _sel;
        Color cc = CondColor(stn);

        Brush fill = has ? new SolidColorBrush(Color.FromArgb(0x35, cc.R, cc.G, cc.B))
                         : Br("BgSecondaryBrush");
        Brush stroke = sel ? Br("AccentBrightBrush")
                     : has ? new SolidColorBrush(cc) : Br("BorderBrush");

        var el = new Ellipse
        {
            Width = r * 2, Height = r * 2,
            Fill = fill, Stroke = stroke,
            StrokeThickness = sel ? 3 : has ? 2.5 : 1.5,
            Cursor = Cursors.Hand, Tag = num,
        };
        if (sel)
            el.Effect = new DropShadowEffect
            {
                Color = (Color)FindResource("AccentBrightColor"),
                BlurRadius = 20, ShadowDepth = 0, Opacity = 0.6
            };

        el.MouseLeftButtonDown += OnSlotClick;

        Canvas.SetLeft(el, cx - r);
        Canvas.SetTop(el, cy - r);
        _dc.Children.Add(el);

        // Orientation marker for indexing D stations
        if (!IsSub(num) && num % 2 == 1)
        {
            double rot = (GetRotation(num) + _animIdxAngle) * Math.PI / 180;
            double ma = -Math.PI / 2 + rot;
            double tipX = cx + (r - 4) * Math.Cos(ma);
            double tipY = cy + (r - 4) * Math.Sin(ma);
            double b1X = cx + (r - 26) * Math.Cos(ma - 0.2);
            double b1Y = cy + (r - 26) * Math.Sin(ma - 0.2);
            double b2X = cx + (r - 26) * Math.Cos(ma + 0.2);
            double b2Y = cy + (r - 26) * Math.Sin(ma + 0.2);
            var arrow = new Polygon
            {
                Points = [new Point(tipX, tipY), new Point(b1X, b1Y), new Point(b2X, b2Y)],
                Fill = Br("AccentBrush"), IsHitTestVisible = false,
            };
            _dc.Children.Add(arrow);
        }

        // Interior text
        if (IsSub(num))
        {
            Label(cx, cy - 10, Slot(num).ToString(), r > 28 ? 26 : 18,
                has ? Brushes.White : Br("TextMutedBrush"), true, false);
            Label(cx, cy + 14, StnType(num), 15,
                has ? Brushes.White : Br("TextMutedBrush"), false, false);
        }
        else
        {
            if (has && stn != null)
            {
                var p = Tool(stn.MountedPunchId);
                var d = Tool(stn.MountedDieId);
                var lines = new List<string>();
                if (p != null) { lines.Add($"P:{p.Shape}"); lines.Add($"  {p.Size}"); }
                if (d != null) lines.Add($"D:{d.Size}");
                Label(cx, cy, string.Join("\n", lines), 20, Brushes.White, false, false);
            }
            else
                Label(cx, cy, "EMPTY", 22, Br("TextMutedBrush"), false, false);
        }
    }

    // ══════════════════════════════════════════════════════════════
    //  ZOOM — LEVEL 1 (station overview)
    // ══════════════════════════════════════════════════════════════

    private void ShowZoom()
    {
        TurretBox.Effect = new BlurEffect { Radius = 5 };
        ZoomOverlay.Background = new SolidColorBrush(Color.FromArgb(0xBB, 0x05, 0x08, 0x05));
        ZoomOverlay.Visibility = Visibility.Visible;
        _zoomSub = -1;
        _zoomHits.Clear();
        _dc = ZoomCanvas;

        if (IsMulti(_zoomStation))
            DrawZoomMulti();
        else
            DrawZoomSingle();
    }

    private void DrawZoomSingle()
    {
        ZoomCanvas.Children.Clear();
        int stn = _zoomStation;
        var station = Find(stn);

        // Header
        Label(Zc, 44, $"STATION {stn}  [D]", 38, Br("AccentBrightBrush"), true);
        bool idx = stn % 2 == 1;
        Label(Zc, 90, (idx ? "INDEXING" : "FIXED") + "  \u00B7  ADAPTER COMPATIBLE",
            22, Br("TextMutedBrush"), false);

        // Rotation info for indexing stations
        if (idx)
        {
            double rotDeg = GetRotation(stn);
            Label(Zc, 122, $"ROTATION: {rotDeg}\u00B0", 22, Br("AccentBrush"), true);
        }

        // Large circle
        double circleY = Zc + 40;
        var cc = CondColor(station);
        bool has = station?.MountedPunchId != null || station?.MountedDieId != null;
        Brush cStroke = has ? new SolidColorBrush(cc) : Br("BorderBrush");

        var circle = Ring(Zc, circleY, ZoomSingleR, cStroke, 3, 1, Br("BgSecondaryBrush"));
        circle.Effect = new DropShadowEffect
        {
            Color = has ? cc : (Color)FindResource("AccentBrightColor"),
            BlurRadius = 25, ShadowDepth = 0, Opacity = 0.4
        };

        // Orientation marker for indexing D stations
        if (idx)
        {
            double rot = GetRotation(stn) * Math.PI / 180;
            double ma = -Math.PI / 2 + rot;
            double tipDist = ZoomSingleR - 8;
            double baseDist = ZoomSingleR - 55;
            double tipX = Zc + tipDist * Math.Cos(ma);
            double tipY = circleY + tipDist * Math.Sin(ma);
            double b1X = Zc + baseDist * Math.Cos(ma - 0.15);
            double b1Y = circleY + baseDist * Math.Sin(ma - 0.15);
            double b2X = Zc + baseDist * Math.Cos(ma + 0.15);
            double b2Y = circleY + baseDist * Math.Sin(ma + 0.15);
            _dc.Children.Add(new Polygon
            {
                Points = [new Point(tipX, tipY), new Point(b1X, b1Y), new Point(b2X, b2Y)],
                Fill = Br("AccentBrush"), IsHitTestVisible = false,
            });
        }

        // Tool details inside
        if (has && station != null)
        {
            double ty = Zc - 60;
            var p = Tool(station.MountedPunchId);
            var d = Tool(station.MountedDieId);

            if (p != null)
            {
                Label(Zc, ty, "PUNCH", 22, Br("TextMutedBrush"), true);
                Label(Zc, ty + 32, $"{p.ToolType} {p.Size}", 30, Br("TextBrush"), true);
                Label(Zc, ty + 72, p.Condition, 24,
                    new SolidColorBrush(ParseHex(p.ConditionColor)), true);
                ty += 120;
            }
            if (d != null)
            {
                Label(Zc, ty, "DIE", 22, Br("TextMutedBrush"), true);
                string dTxt = $"{d.ToolType} {d.Size}";
                if (d.Clearance.HasValue) dTxt += $"  clr:{d.Clearance}mm";
                Label(Zc, ty + 32, dTxt, 30, Br("TextBrush"), true);
                Label(Zc, ty + 72, d.Condition, 24,
                    new SolidColorBrush(ParseHex(d.ConditionColor)), true);
            }
        }
        else
        {
            Label(Zc, Zc + 30, "EMPTY", 40, Br("TextMutedBrush"), true);
        }

        Label(Zc, 1150, "CLICK TO SELECT  \u00B7  CLICK BACKGROUND TO CLOSE", 20, Br("AccentDimBrush"), true);
    }

    private void DrawZoomMulti()
    {
        ZoomCanvas.Children.Clear();
        _zoomHits.Clear();

        int mainStn = _zoomStation;
        int[] slots = mainStn == 1 ? Multi6 : Multi10;
        string name = mainStn == 1 ? "MULTI-6" : "MULTI-10";
        string info = mainStn == 1 ? "B STATION  \u00B7  IDX  \u00B7  ALL SHAPES"
                                   : "A/B STATION  \u00B7  ROUND ONLY";

        // Header
        Label(Zc, 40, $"STN {mainStn}  \u2014  {name}", 38, Br("AccentBrightBrush"), true);
        Label(Zc, 86, info, 22, Br("TextMutedBrush"), false);

        // Housing circle
        Ring(Zc, Zc + 30, ZoomMultiR, Br("BorderBrush"), 2.5, 1, Br("BgTertiaryBrush"));

        // Rotation
        double rotDeg = mainStn == 1 ? GetRotation(101) : 0;
        double rotRad = rotDeg * Math.PI / 180;
        if (mainStn == 1)
            Label(Zc, 118, $"ROTATION: {rotDeg}\u00B0", 22, Br("AccentBrush"), true);

        // Reference marker line
        if (mainStn == 1)
        {
            double refA = -Math.PI / 2 + rotRad;
            double rx = Zc + (ZoomMultiR - 8) * Math.Cos(refA);
            double ry = (Zc + 30) + (ZoomMultiR - 8) * Math.Sin(refA);
            _dc.Children.Add(new Line
            {
                X1 = Zc + 30 * Math.Cos(refA), Y1 = (Zc + 30) + 30 * Math.Sin(refA),
                X2 = rx, Y2 = ry,
                Stroke = Br("AccentDimBrush"), StrokeThickness = 3, Opacity = 0.5,
            });
        }

        // Sub-stations
        for (int j = 0; j < slots.Length; j++)
        {
            double sa = 2 * Math.PI * j / slots.Length - Math.PI / 2 + rotRad;
            double sx = Zc + ZoomSubOrbit * Math.Cos(sa);
            double sy = (Zc + 30) + ZoomSubOrbit * Math.Sin(sa);

            bool isB = slots[j] == 401 || slots[j] == 406;
            double sr = mainStn == 4 && isB ? ZoomSubBR : ZoomSubR;

            _zoomHits.Add((slots[j], sx, sy, sr));

            var stn = Find(slots[j]);
            bool has = stn?.MountedPunchId != null || stn?.MountedDieId != null;
            var cc = CondColor(stn);
            Brush fill = has ? new SolidColorBrush(Color.FromArgb(0x40, cc.R, cc.G, cc.B))
                             : Br("BgSecondaryBrush");
            Brush stroke = has ? new SolidColorBrush(cc) : Br("BorderBrush");

            var el = Ring(sx, sy, sr, stroke, has ? 2.5 : 1.5, 1, fill);
            el.Cursor = Cursors.Hand;

            // Slot number + type
            int slot = Slot(slots[j]);
            Label(sx, sy - 14, slot.ToString(), isB ? 32 : 28,
                has ? Brushes.White : Br("TextMutedBrush"), true, false);
            Label(sx, sy + 16, StnType(slots[j]), isB ? 22 : 18,
                has ? Brushes.White : Br("TextMutedBrush"), false, false);

            // Brief tool info below circle
            if (has && stn != null)
            {
                var p = Tool(stn.MountedPunchId);
                if (p != null)
                    Label(sx, sy + sr + 18, $"{p.Shape} {p.Size}", 16,
                        Br("TextSecondaryBrush"), false, false);
            }
        }

        // Center label
        Label(Zc, Zc + 30, $"{mainStn}\u00B70", 24, Br("TextMutedBrush"), true);
        Label(Zc, 1150, "CLICK SUB-STATION TO ZOOM  \u00B7  CLICK BACKGROUND TO CLOSE", 20, Br("AccentDimBrush"), true);
    }

    // ══════════════════════════════════════════════════════════════
    //  ZOOM — LEVEL 2 (sub-station detail, the inception layer)
    // ══════════════════════════════════════════════════════════════

    private void DrawZoomL2(int subNum)
    {
        ZoomCanvas.Children.Clear();
        ZoomOverlay.Background = new SolidColorBrush(Color.FromArgb(0xDD, 0x03, 0x05, 0x03));

        var stn = Find(subNum);
        int main = MainStn(subNum);
        int slot = Slot(subNum);
        string mName = main == 1 ? "MULTI-6" : "MULTI-10";

        // Header
        Label(Zc, 38, $"STATION {subNum}", 38, Br("AccentBrightBrush"), true);
        Label(Zc, 84, $"{mName}  \u2192  SLOT {slot}  [{StnType(subNum)}]", 26,
            Br("TextSecondaryBrush"), false);
        if (main == 4)
            Label(Zc, 120, "ROUND PUNCHES ONLY", 22, Br("WarnBrush"), false);
        if (main == 1)
        {
            double rotDeg = GetRotation(101);
            Label(Zc, 120, $"INDEXING  \u00B7  ALL SHAPES  \u00B7  {rotDeg}\u00B0", 22, Br("AccentDimBrush"), false);
        }

        // Large detail circle
        var cc = CondColor(stn);
        bool has = stn?.MountedPunchId != null || stn?.MountedDieId != null;
        Brush cStroke = has ? new SolidColorBrush(cc) : Br("BorderBrush");

        var circle = Ring(Zc, Zc + 30, ZoomDetailR, cStroke, 3.5, 1, Br("BgSecondaryBrush"));
        circle.Effect = new DropShadowEffect
        {
            Color = has ? cc : (Color)FindResource("AccentBrightColor"),
            BlurRadius = 30, ShadowDepth = 0, Opacity = 0.5
        };

        // Tool details
        if (has && stn != null)
        {
            double ty = Zc - 80;
            var p = Tool(stn.MountedPunchId);
            var d = Tool(stn.MountedDieId);

            if (p != null)
            {
                Label(Zc, ty, "PUNCH", 22, Br("TextMutedBrush"), true);
                Label(Zc, ty + 32, $"{p.ToolType} {p.Size}", 32, Br("TextBrush"), true);
                Label(Zc, ty + 74, p.Condition, 26,
                    new SolidColorBrush(ParseHex(p.ConditionColor)), true);
                ty += 130;
            }
            if (d != null)
            {
                Label(Zc, ty, "DIE", 22, Br("TextMutedBrush"), true);
                string dTxt = $"{d.ToolType} {d.Size}";
                if (d.Clearance.HasValue) dTxt += $"  clr:{d.Clearance}mm";
                Label(Zc, ty + 32, dTxt, 32, Br("TextBrush"), true);
                Label(Zc, ty + 74, d.Condition, 26,
                    new SolidColorBrush(ParseHex(d.ConditionColor)), true);
            }
        }
        else
        {
            Label(Zc, Zc + 20, "EMPTY", 44, Br("TextMutedBrush"), true);
        }

        // Mini multi-tool indicator (bottom-right corner showing which slot is zoomed)
        int[] slots = main == 1 ? Multi6 : Multi10;
        double miniCx = 1050, miniCy = 1050, miniR = 85, miniSubR = 18;
        double miniOrbit = 52;
        Ring(miniCx, miniCy, miniR, Br("BorderBrush"), 1, 0.4);
        for (int j = 0; j < slots.Length; j++)
        {
            double sa = 2 * Math.PI * j / slots.Length - Math.PI / 2;
            double sx = miniCx + miniOrbit * Math.Cos(sa);
            double sy = miniCy + miniOrbit * Math.Sin(sa);
            bool isThis = slots[j] == subNum;
            Ring(sx, sy, miniSubR, isThis ? Br("AccentBrightBrush") : Br("BorderBrush"),
                isThis ? 2 : 1, isThis ? 1 : 0.4,
                isThis ? Br("AccentDimBrush") : null);
        }

        Label(Zc, 1150, "CLICK TO SELECT  \u00B7  CLICK BACKGROUND TO CLOSE", 20, Br("AccentDimBrush"), true);
    }

    // ══════════════════════════════════════════════════════════════
    //  ZOOM EVENTS (click-driven)
    // ══════════════════════════════════════════════════════════════

    private void OnZoomClick(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;

        if (_zoomSub > 0)
        {
            // Level 2: click selects this sub-station
            _sel = _zoomSub;
            CloseZoom();
            Render();
            return;
        }

        if (IsMulti(_zoomStation))
        {
            // Level 1 multi-tool: click sub-station → drill to L2, click background → close
            var pos = e.GetPosition(ZoomCanvas);
            int hit = HitTestZoom(pos);
            if (hit > 0)
            {
                _zoomSub = hit;
                _dc = ZoomCanvas;
                DrawZoomL2(hit);
                return;
            }
        }
        else if (_zoomStation > 0)
        {
            // Level 1 D station: click selects it
            _sel = _zoomStation;
            CloseZoom();
            Render();
            return;
        }

        // Clicked background → close zoom
        CloseZoom();
        _dc = TurretCanvas;
    }

    private void CloseZoom()
    {
        _zoomStation = -1;
        _zoomSub = -1;
        _zoomHits.Clear();
        TurretBox.Effect = null;
        ZoomOverlay.Visibility = Visibility.Collapsed;
    }

    private int HitTestZoom(Point pos)
    {
        foreach (var (num, x, y, r) in _zoomHits)
        {
            double dx = pos.X - x, dy = pos.Y - y;
            if (dx * dx + dy * dy <= r * r) return num;
        }
        return -1;
    }

    // ══════════════════════════════════════════════════════════════
    //  DETAIL PANEL
    // ══════════════════════════════════════════════════════════════

    private void ShowDetail()
    {
        DetailPanel.Children.Clear();

        if (_sel < 0)
        {
            DText("SELECT A STATION", 28, Br("TextMutedBrush"), true, new(0, 40, 0, 0));
            DText("Click any station or sub-station\non the turret diagram.", 20,
                Br("TextMutedBrush"), false, new(0, 12, 0, 0));
            return;
        }

        var stn = Find(_sel);
        if (stn == null) return;

        if (IsSub(_sel))
        {
            int main = MainStn(_sel);
            int slot = Slot(_sel);
            string mName = main == 1 ? "MULTI-6" : "MULTI-10";
            DText($"STATION {_sel}", 30, Br("AccentBrightBrush"), true);
            DText($"{mName}  \u2192  SLOT {slot}  [{stn.StationType}]", 20,
                Br("TextSecondaryBrush"), false);
            if (main == 1)
                DText("INDEXING  \u00B7  ALL SHAPES", 18, Br("AccentDimBrush"), false, new(0, 2, 0, 0));
            if (main == 4)
                DText("ROUND PUNCHES ONLY", 18, Br("WarnBrush"), false, new(0, 2, 0, 0));
        }
        else
        {
            DText($"STATION {_sel}  [D]", 30, Br("AccentBrightBrush"), true);
            bool idx = _sel % 2 == 1;
            DText(idx ? "INDEXING" : "FIXED", 18,
                idx ? Br("AccentDimBrush") : Br("TextMutedBrush"), false, new(0, 2, 0, 0));
            DText("ADAPTER COMPATIBLE (A/B/C \u2192 D)", 16,
                Br("TextMutedBrush"), false, new(0, 2, 0, 0));
        }

        // Rotation controls for indexing stations
        if (IsIdx(_sel))
        {
            DSep();
            DText("ROTATION", 24, Br("TextBrush"), true);
            double curRot = GetRotation(_sel);
            DText($"Current: {curRot}\u00B0", 20, Br("TextSecondaryBrush"), false, new(0, 4, 0, 6));

            var presetRow = new WrapPanel { Margin = new Thickness(0, 0, 0, 8) };
            foreach (double deg in new[] { 0, 90, 180, 270 })
            {
                var rb = MkBtn($"{deg}\u00B0", "NavButton");
                rb.Opacity = Math.Abs(curRot - deg) < 0.01 ? 1.0 : 0.5;
                rb.Margin = new Thickness(0, 0, 10, 8);
                rb.Padding = new Thickness(18, 8, 18, 8);
                rb.FontSize = 20;
                double cap = deg;
                rb.Click += (_, _) => SetRotation(_sel, cap);
                presetRow.Children.Add(rb);
            }
            DetailPanel.Children.Add(presetRow);

            var customRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
            var tbx = new TextBox
            {
                Width = 100, Text = curRot.ToString("F0"),
                FontFamily = (FontFamily)FindResource("TerminalFont"),
                FontSize = 20, VerticalContentAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0),
            };
            var setBtn = MkBtn("SET", "NavButton");
            setBtn.Padding = new Thickness(18, 8, 18, 8);
            setBtn.FontSize = 20;
            setBtn.Click += (_, _) =>
            {
                if (double.TryParse(tbx.Text, out double d))
                    SetRotation(_sel, d % 360);
            };
            var degLabel = new TextBlock
            {
                Text = "\u00B0", FontSize = 22, Foreground = Br("TextMutedBrush"),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0),
            };
            customRow.Children.Add(tbx);
            customRow.Children.Add(degLabel);
            customRow.Children.Add(setBtn);
            DetailPanel.Children.Add(customRow);
        }

        DSep();
        DText("MOUNTED", 24, Br("TextBrush"), true);

        var punch = Tool(stn.MountedPunchId);
        var die = Tool(stn.MountedDieId);

        if (punch != null)
        {
            DText($"PUN  {punch.ToolType} {punch.Size}", 20,
                Br("TextSecondaryBrush"), false, new(0, 6, 0, 0));
            DText($"       {punch.Condition}", 18,
                new SolidColorBrush(ParseHex(punch.ConditionColor)), false);
        }
        else
            DText("PUN  -- empty --", 20, Br("TextMutedBrush"), false, new(0, 6, 0, 0));

        if (die != null)
        {
            DText($"DIE  {die.ToolType} {die.Size}", 20,
                Br("TextSecondaryBrush"), false, new(0, 6, 0, 0));
            DText($"       {die.Condition}", 18,
                new SolidColorBrush(ParseHex(die.ConditionColor)), false);
            if (die.Clearance.HasValue)
                DText($"CLEARANCE  {die.Clearance}mm", 22, Br("AccentBrightBrush"), true, new(0, 8, 0, 4));
        }
        else
            DText("DIE  -- empty --", 20, Br("TextMutedBrush"), false, new(0, 6, 0, 0));

        if (die?.MeasuredHeight != null)
        {
            double shims = die.CurrentShims ?? 0;
            double finalH = die.MeasuredHeight.Value + shims;
            double spec = stn.StationType == "D" ? 30.0 : 20.0;
            double diff = spec - finalH;
            DText($"HEIGHT  {finalH:F3}mm  ({(diff >= 0 ? "-" : "+")}{Math.Abs(diff):F3})", 18,
                Math.Abs(diff) < 0.05 ? Br("GoodBrush") : Br("WarnBrush"),
                false, new(0, 6, 0, 0));
        }

        DSep();
        DText("ASSIGN TOOLS", 24, Br("TextBrush"), true);

        // Build lists for this station
        var stationPunches = App.State.ToolInventory
            .Where(t => IsSinglePunch(t) || IsClusterPunch(t))
            .Where(t => stn.StationType == "D" || t.Station == stn.StationType)
            .ToList();
        if (_sel >= 401 && _sel <= 410)
            stationPunches = stationPunches.Where(t => t.Shape == "Round").ToList();

        var stationDies = App.State.ToolInventory
            .Where(t => IsDieTool(t))
            .Where(t => stn.StationType == "D" || t.Station == stn.StationType)
            .ToList();

        var mountedPunch = Tool(stn.MountedPunchId);
        var mountedDie = Tool(stn.MountedDieId);
        bool _linking = false;

        // ── 1. SHAPE ──
        DText("Shape:", 20, Br("TextSecondaryBrush"), false, new(0, 8, 0, 4));
        var cboShape = new ComboBox { FontSize = 18, Margin = new Thickness(0, 0, 0, 10) };
        cboShape.Items.Add(new ComboBoxItem { Content = "(all shapes)", Tag = "" });
        int shapeSelIdx = 0; int si = 1;
        foreach (var shape in stationPunches.Select(t => t.Shape).Distinct().OrderBy(s => s))
        {
            cboShape.Items.Add(new ComboBoxItem { Content = shape, Tag = shape });
            if (mountedPunch != null && mountedPunch.Shape == shape) shapeSelIdx = si;
            si++;
        }
        DetailPanel.Children.Add(cboShape);

        // ── 2. PUNCH ──
        DText("Punch:", 20, Br("TextSecondaryBrush"), false, new(0, 4, 0, 4));
        var cboPunch = new ComboBox { FontSize = 18, Margin = new Thickness(0, 0, 0, 10) };
        DetailPanel.Children.Add(cboPunch);

        // ── 3. DIE ──
        var lblDie = new TextBlock
        {
            Text = "Die:", FontSize = 20, Foreground = Br("TextSecondaryBrush"),
            FontFamily = (FontFamily)FindResource("TerminalFont"),
            Margin = new Thickness(0, 4, 0, 4),
        };
        DetailPanel.Children.Add(lblDie);
        var cboDie = new ComboBox { FontSize = 18, Margin = new Thickness(0, 0, 0, 16) };
        DetailPanel.Children.Add(cboDie);

        // ── Fill punch list from selected shape ──
        void FillPunchList()
        {
            cboPunch.Items.Clear();
            cboPunch.Items.Add(new ComboBoxItem { Content = "(none)", Tag = -1 });
            string shape = cboShape.SelectedItem is ComboBoxItem { Tag: string s } ? s : "";
            var punches = (shape == "" ? stationPunches : stationPunches.Where(t => t.Shape == shape))
                .OrderBy(t => IsClusterPunch(t) ? 1 : 0).ThenBy(t => t.Size);

            int selIdx = 0, idx = 1;
            bool addedSep = false;
            foreach (var t in punches)
            {
                if (!addedSep && IsClusterPunch(t))
                {
                    cboPunch.Items.Add(new ComboBoxItem { Content = "── CLUSTER ──", IsEnabled = false, Tag = -1 });
                    addedSep = true; idx++;
                }
                cboPunch.Items.Add(new ComboBoxItem
                    { Content = $"{t.ToolType} {t.Size} [{t.Condition}]", Tag = t.Id });
                if (stn.MountedPunchId.HasValue && t.Id == stn.MountedPunchId.Value) selIdx = idx;
                idx++;
            }
            cboPunch.SelectedIndex = selIdx;
        }

        // ── Fill die list from selected punch ──
        void FillDieList()
        {
            cboDie.Items.Clear();
            cboDie.Items.Add(new ComboBoxItem { Content = "(none)", Tag = -1 });

            var selPunch = cboPunch.SelectedItem is ComboBoxItem { Tag: int pid } && pid > 0
                ? App.State.ToolInventory.FirstOrDefault(t => t.Id == pid) : null;

            IEnumerable<InventoryTool> dies;
            bool clearanceOnly = false;

            if (selPunch != null && IsSinglePunch(selPunch))
            {
                // Single punch → matching dies by shape+size (varying clearances)
                dies = stationDies.Where(t => !t.ToolType.Contains("Cluster")
                    && t.Shape == selPunch.Shape && t.Size == selPunch.Size);
                lblDie.Text = "Die Clearance:";
                clearanceOnly = true;
            }
            else if (selPunch != null && IsClusterPunch(selPunch))
            {
                // Cluster punch → show cluster dies
                dies = stationDies.Where(t => t.ToolType.Contains("Cluster"));
                lblDie.Text = "Cluster Die:";
            }
            else
            {
                // No punch → show all dies
                dies = stationDies;
                lblDie.Text = "Die:";
            }

            int selIdx = 0, idx = 1;
            foreach (var t in dies.OrderBy(t => t.Clearance ?? 999).ThenBy(t => t.Size))
            {
                string clr = t.Clearance.HasValue ? $"clr:{t.Clearance}mm" : "";
                string display = clearanceOnly
                    ? $"{clr} [{t.Condition}]"
                    : $"{t.ToolType} {t.Size} {clr} [{t.Condition}]";
                cboDie.Items.Add(new ComboBoxItem { Content = display, Tag = t.Id });
                if (stn.MountedDieId.HasValue && t.Id == stn.MountedDieId.Value) selIdx = idx;
                idx++;
            }
            cboDie.SelectedIndex = selIdx;
        }

        // ── Cascade: shape → punch → die (one direction, no loops) ──
        cboShape.SelectionChanged += (_, _) =>
        {
            if (_linking) return;
            _linking = true;
            FillPunchList();
            FillDieList();
            _linking = false;
        };
        cboPunch.SelectionChanged += (_, _) =>
        {
            if (_linking) return;
            _linking = true;
            FillDieList();
            _linking = false;
        };

        // ── Initial fill ──
        _linking = true;
        cboShape.SelectedIndex = shapeSelIdx;
        FillPunchList();
        FillDieList();
        _linking = false;

        // ── Mount / Clear ──
        var row = new WrapPanel { Margin = new Thickness(0, 0, 0, 8) };
        var btnMount = MkBtn("MOUNT", "NavButton");
        btnMount.Margin = new Thickness(0, 0, 8, 0);
        btnMount.Click += (_, _) =>
        {
            int pid = cboPunch.SelectedItem is ComboBoxItem pi && pi.Tag is int p ? p : -1;
            int did = cboDie.SelectedItem is ComboBoxItem di && di.Tag is int d ? d : -1;
            stn.MountedPunchId = pid > 0 ? pid : null;
            stn.MountedDieId = did > 0 ? did : null;
            App.SaveState();
            Render();
        };
        var btnClear = MkBtn("CLEAR", "DangerButton");
        btnClear.Click += (_, _) =>
        {
            stn.MountedPunchId = null;
            stn.MountedDieId = null;
            App.SaveState();
            Render();
        };
        row.Children.Add(btnMount);
        row.Children.Add(btnClear);
        DetailPanel.Children.Add(row);
    }

    // Determine tool role from ToolType string, not Category
    private static bool IsSinglePunch(InventoryTool t) =>
        t.ToolType.Contains("Punch") && !t.ToolType.Contains("Cluster");
    private static bool IsClusterPunch(InventoryTool t) =>
        t.ToolType.Contains("Cluster") && t.ToolType.Contains("Punch");
    private static bool IsDieTool(InventoryTool t) =>
        t.ToolType.Contains("Die");

    // ══════════════════════════════════════════════════════════════
    //  EVENTS
    // ══════════════════════════════════════════════════════════════

    private void OnSlotClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: int num }) return;
        e.Handled = true;

        // Stop animation if running
        if (_animPhase > 0) { ResetAnim(); return; }

        if (num >= 100 || !IsMulti(num))
        {
            // Sub-station or single D station: select directly → show settings
            _sel = num;
            Render();
        }
        else
        {
            // Multi-tool housing (1 or 4): zoom to pick sub-station
            _zoomStation = num;
            ShowZoom();
        }
    }

    // ══════════════════════════════════════════════════════════════
    //  HUB ANIMATION (5-click cycle)
    // ══════════════════════════════════════════════════════════════

    private void OnHubClick(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        CloseZoom();

        _animPhase++;
        switch (_animPhase)
        {
            case 1: // MTX clockwise
                _animMtxSpeed = 1.5;
                _animIdxSpeed = 0;
                StartAnim();
                break;
            case 2: // + indexing stations counter-clockwise
                _animIdxSpeed = 2.0;
                break;
            case 3: // reverse MTX (now counter-clockwise)
                _animMtxSpeed = -1.5;
                break;
            case 4: // reverse indexing (now clockwise)
                _animIdxSpeed = -2.0;
                break;
            default: // stop and reset
                ResetAnim();
                break;
        }
    }

    private void StartAnim()
    {
        if (_animTimer != null) return;
        _animTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(25) };
        _animTimer.Tick += OnAnimTick;
        _animTimer.Start();
    }

    private void StopAnim()
    {
        _animTimer?.Stop();
        _animTimer = null;
    }

    private void ResetAnim()
    {
        StopAnim();
        _animPhase = 0;
        _animMtxAngle = 0;
        _animIdxAngle = 0;
        _animMtxSpeed = 0;
        _animIdxSpeed = 0;
        DrawTurret();
    }

    private void OnAnimTick(object? sender, EventArgs e)
    {
        _animMtxAngle = (_animMtxAngle + _animMtxSpeed) % 360;
        _animIdxAngle = (_animIdxAngle + _animIdxSpeed) % 360;
        _dc = TurretCanvas;
        DrawTurret();
    }

    // ══════════════════════════════════════════════════════════════
    //  CANVAS PRIMITIVES (draw to _dc)
    // ══════════════════════════════════════════════════════════════

    private Ellipse Ring(double cx, double cy, double r, Brush stroke, double sw, double opacity,
        Brush? fill = null)
    {
        var el = new Ellipse
        {
            Width = r * 2, Height = r * 2,
            Stroke = stroke, StrokeThickness = sw,
            Fill = fill ?? Brushes.Transparent, Opacity = opacity,
        };
        Canvas.SetLeft(el, cx - r);
        Canvas.SetTop(el, cy - r);
        _dc.Children.Add(el);
        return el;
    }

    private void Spoke(double x1, double y1, double x2, double y2)
    {
        _dc.Children.Add(new Line
        {
            X1 = x1, Y1 = y1, X2 = x2, Y2 = y2,
            Stroke = Br("BorderBrush"), StrokeThickness = 1, Opacity = 0.2,
            StrokeDashArray = [4, 3],
        });
    }

    private void Label(double cx, double cy, string text, double size, Brush fg, bool bold,
        bool hitTest = true)
    {
        var tb = new TextBlock
        {
            Text = text, FontSize = size,
            FontWeight = bold ? FontWeights.Bold : FontWeights.Normal,
            Foreground = fg,
            FontFamily = (FontFamily)FindResource("TerminalFont"),
            TextAlignment = TextAlignment.Center,
            IsHitTestVisible = hitTest,
        };
        tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Canvas.SetLeft(tb, cx - tb.DesiredSize.Width / 2);
        Canvas.SetTop(tb, cy - tb.DesiredSize.Height / 2);
        _dc.Children.Add(tb);
    }

    // ══════════════════════════════════════════════════════════════
    //  DETAIL PANEL HELPERS
    // ══════════════════════════════════════════════════════════════

    private void DText(string text, double size, Brush fg, bool bold, Thickness? margin = null)
    {
        DetailPanel.Children.Add(new TextBlock
        {
            Text = text, FontSize = size,
            FontWeight = bold ? FontWeights.Bold : FontWeights.Normal,
            Foreground = fg,
            FontFamily = (FontFamily)FindResource("TerminalFont"),
            Margin = margin ?? new Thickness(0, 2, 0, 2),
            TextWrapping = TextWrapping.Wrap,
        });
    }

    private void DSep() => DetailPanel.Children.Add(new Border
    {
        Height = 1, Background = Br("BorderBrush"), Margin = new Thickness(0, 10, 0, 10),
    });

    private Button MkBtn(string content, string styleKey) => new()
    {
        Content = content, Style = (Style)FindResource(styleKey),
        Padding = new Thickness(20, 10, 20, 10), FontSize = 18,
    };

    // ══════════════════════════════════════════════════════════════
    //  COLOR HELPERS
    // ══════════════════════════════════════════════════════════════

    private Brush Br(string key) => (Brush)FindResource(key);

    private static Color ParseHex(string hex) =>
        (Color)ColorConverter.ConvertFromString(hex)!;

    private static Color CondColor(TurretStation? stn)
    {
        if (stn == null) return ParseHex("#55ff88");
        var ids = new[] { stn.MountedPunchId, stn.MountedDieId }
            .Where(i => i.HasValue).Select(i => i!.Value);
        Color worst = ParseHex("#55ff88");
        foreach (var id in ids)
        {
            var tool = App.State.ToolInventory.FirstOrDefault(t => t.Id == id);
            if (tool == null) continue;
            if (tool.Condition is "Broken Pin" or "Ruined" or "Bad") return ParseHex("#ff5555");
            if (tool.Condition == "Poor") worst = ParseHex("#ffdd44");
        }
        return worst;
    }
}
