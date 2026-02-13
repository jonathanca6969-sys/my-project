using System;
using System.IO;
using System.Text.Json;
using System.Windows;
using TurretApp.Models;

namespace TurretApp;

public partial class App : Application
{
    public static AppState State { get; set; } = new();
    private static readonly string DataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appdata.json");
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        LoadState();
        ApplyTheme(State.CurrentTheme);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        SaveState();
        base.OnExit(e);
    }

    public static void ApplyTheme(string themeName)
    {
        string file = themeName == "Fluromac" ? "Themes/FluromacTheme.xaml" : "Themes/TerminalTheme.xaml";
        var dict = new ResourceDictionary
        {
            Source = new Uri(file, UriKind.Relative)
        };
        Current.Resources.MergedDictionaries.Clear();
        Current.Resources.MergedDictionaries.Add(dict);
    }

    public static void LoadState()
    {
        try
        {
            if (File.Exists(DataPath))
            {
                var json = File.ReadAllText(DataPath);
                State = JsonSerializer.Deserialize<AppState>(json, JsonOpts) ?? new AppState();
            }
        }
        catch
        {
            State = new AppState();
        }

        if (State.EmpiricalData.Count == 0)
        {
            State.EmpiricalData =
            [
                new() { Id = 1, Material = "Aluminium", Thickness = 3.0, Operation = "Slitting", ClearanceMM = 0.4, ClearancePct = 13.3, Notes = "60x5 slitter - good results", Verified = true },
                new() { Id = 2, Material = "Stainless Steel", Thickness = 0.9, Operation = "Piercing", ClearanceMM = 0.12, ClearancePct = 13.3, Notes = "Clean holes, good strip", Verified = true },
                new() { Id = 3, Material = "Stainless Steel", Thickness = 1.2, Operation = "Piercing", ClearanceMM = 0.225, ClearancePct = 18.8, Notes = "Range 0.2-0.25mm tested", Verified = true },
                new() { Id = 4, Material = "Aluminium", Thickness = 3.0, Operation = "Piercing", ClearanceMM = 0.6, ClearancePct = 20, Notes = "3.2mm pins, cluster tool", Verified = true },
            ];
        }

        if (State.ToolInventory.Count == 0)
            SeedToolInventory();
    }

    public static void SaveState()
    {
        try
        {
            var json = JsonSerializer.Serialize(State, JsonOpts);
            File.WriteAllText(DataPath, json);
        }
        catch { /* silently fail on save errors */ }
    }

    private static void SeedToolInventory()
    {
        int id = 1;
        void Add(string station, string toolType, string size, double? clearance, int qty, string condition, bool sharpen, string notes = "")
        {
            string category = toolType.Contains("Cluster") ? "Cluster"
                : toolType.Contains("Punch") || toolType.Contains("Forming") ? "Punch" : "Die";
            string shape = toolType.Contains("Round") ? "Round"
                : toolType.Contains("Square") ? "Square"
                : toolType.Contains("Obround") || toolType.Contains("Oblong") || toolType.Contains("Oval") ? "Obround"
                : toolType.Contains("Rectangle") ? "Rectangle"
                : toolType.Contains("Hexagon") || toolType.Contains("Hex") ? "Hexagon"
                : toolType.Contains("Trapezoid") ? "Trapezoid"
                : "Special";

            for (int i = 0; i < qty; i++)
            {
                State.ToolInventory.Add(new InventoryTool
                {
                    Id = id++, Station = station, ToolType = toolType, Category = category,
                    Shape = shape, Size = size, Clearance = clearance, Quantity = 1,
                    Condition = condition, NeedsSharpen = sharpen, Notes = notes,
                });
            }
        }

        // Punches
        Add("A","Round Punch","2.3",null,1,"Good",false);
        Add("A","Round Punch","3.0",null,1,"Good",false);
        Add("A","Round Punch","3.2",null,1,"Good",false);
        Add("A","Round Punch","3.4",null,1,"Good",true);
        Add("A","Round Punch","4.1",null,1,"Good",false);
        Add("A","Round Punch","8.0",null,1,"OK",true);
        Add("A","Round Punch","9.0",null,1,"OK",true);
        Add("A","Round Punch","10.0",null,1,"OK",true);
        Add("B","Round Punch","9.5",null,1,"OK",true);
        Add("B","Round Punch","13.0",null,1,"OK",true);
        Add("B","Round Punch","14.0",null,1,"Good",false);
        Add("B","Round Punch","15.0",null,1,"Good",false);
        Add("B","Round Punch","19.0",null,1,"Good",true);
        Add("B","Round Punch","22.0",null,1,"Good",false);
        Add("B","Round Punch (Centre Insert)","19.0",null,1,"OK",true);
        Add("A","Square Punch","5.0",null,1,"OK",true);
        Add("B","Square Punch","10.0",null,3,"OK",true);
        Add("B","Square Punch","15.0",null,1,"Poor",true);
        Add("B","Square Punch","15.0",null,1,"Broken Pin",true);
        Add("B","Obround Punch","5 x 20",null,1,"Good",true);
        Add("B","Obround Punch","5 x 24",null,1,"OK",true);
        Add("B","Obround Punch","6 x 20",null,1,"Good",false);
        Add("B","Obround Punch","6.5 x 12",null,1,"OK",true);
        Add("B","Obround Punch","7 x 21",null,1,"Good",false);
        Add("B","Obround Punch","8 x 13",null,2,"OK",true);
        Add("B","Obround Punch","10 x 15",null,1,"OK",true);
        Add("B","Obround Punch","13 x 15",null,1,"OK",true);
        Add("B","Rectangle Punch","3 x 12",null,1,"Good",true);
        Add("B","Rectangle Punch","5 x 20",null,1,"Good",true);
        Add("B","Rectangle Punch","6.5 x 12",null,2,"OK",true);
        Add("C","Rectangle Punch","12 x 25",null,1,"OK",true);
        Add("C","Rectangle Punch (Centre Insert)","12 x 25",null,2,"OK",true);
        Add("B","Hexagon Punch","6.1",null,1,"Good",true);
        Add("B","Hexagon Punch","9.5",null,1,"OK",true);
        Add("B","Hexagon Punch","11.2",null,2,"Good",true);
        Add("B","Hexagon Punch","11.2",null,1,"New",false);
        Add("B","Special T-Radius Sq Edge","15 approx",null,1,"Good",false);
        Add("B","Special T-Radius Rnd Edge","10 approx",null,1,"Good",false);
        Add("D","Round Punch","30.0",null,1,"Good",true);
        Add("D","Oblong Punch","50 x 10",null,1,"OK",true);
        Add("D","Rectangle Punch","5 x 60",null,3,"OK",true);
        Add("D","Square Punch","30 x 30",null,1,"OK",true);
        Add("D","Square Punch","50 x 50",null,1,"OK",true);
        Add("D","Forming Tool (Stamp Up)","-",null,1,"Good",false);
        Add("D","Forming Tool (Round Form)","-",null,1,"Good",false);

        // Cluster Punches
        Add("C","Cluster Punch","1.6 (12 pins)",null,1,"Good",false);
        Add("D","Cluster Round Punch","4 pins 10mm (40x54)",null,1,"OK",false);
        Add("D","Cluster Round Punch","4 pins 12mm",null,1,"OK",false);
        Add("D","Cluster Round Punch","16 pins 7mm",null,1,"OK",true,"Repin");
        Add("D","Cluster Round Punch","16 pins 3.2mm",null,1,"OK",true);
        Add("D","Cluster Oblong Punch","50 x 50 (3 slots)",null,1,"OK",true);
        Add("D","Cluster Oblong Punch","45 x 54.9 (6 slots)",null,1,"OK",true);
        Add("D","Cluster Oblong Punch","25 x 25 x 2 (2 slots)",null,1,"OK",true);
        Add("D","Cluster Oblong Punch","45 x 55 (4+6 slots)",null,1,"OK",true);
        Add("D","Cluster Ob+Rnd Punch","21 x 62 x 6",null,1,"Good",false);
        Add("D","Cluster Ob+Rnd Punch","21 x 62.9 x 7",null,1,"OK",true);

        // Cluster Dies
        Add("C","Cluster Round Die","3 x 3 (9mm total?)",0.12,1,"OK",false);
        Add("C","Cluster Round Die","3 x 3 (9mm total?)",0.2,1,"OK",false);
        Add("C","Cluster Round Die","1.6 to 18 (2.3+?)",null,1,"Good",true);
        Add("C","Cluster Round Die","2.3+ (9 pins?)",null,1,"Good",true);
        Add("D","Cluster Oblong Die","25 x 5 (3 slots)",0.6,1,"Ruined",false);
        Add("D","Cluster Oblong Die","25 x 5 (3 slots)",0.25,1,"OK",true);
        Add("D","Cluster Oblong Die","5 x 25 (3 slots)",0.25,2,"Bad",false,"Remove 2mm");
        Add("D","Cluster Oblong Die","50 x 10 (3 slots)",0.6,1,"OK",true);
        Add("D","Cluster Oblong Die","10 x 50 (1 slot)",0.6,1,"OK",true);
        Add("D","Cluster Round Die","33 x 28.9",0.2,1,"OK",true);
        Add("D","Cluster Round Die","33 x 28.9",0.6,1,"OK",true);
        Add("D","Cluster Round Die","50.5",0.38,1,"OK",true);
        Add("D","Cluster Round Die","50.5",0.6,1,"OK",true);
        Add("D","Cluster Round Die","21",0.4,1,"OK",true);
        Add("D","Cluster Round Die","21",0.6,2,"OK",true);
        Add("D","Cluster Round Die","55.5 x 50",0.6,1,"OK",true);
        Add("D","Cluster Round Die","52.7 x 43.4",0.6,1,"OK",true);
        Add("D","Cluster Round Die","47.3 x 75.5",0.2,1,"OK",true);
        Add("D","Cluster Round Die","47.3 x 75.5",0.4,1,"OK",true);
        Add("D","Cluster Round Die","47.3 x 75.5",0.6,1,"OK",true);
        Add("D","Cluster Round Die","58 x 42",0.6,1,"Poor",true);
        Add("D","Cluster Ob+Rnd Die","64.3 x 22.3",0.8,1,"Good",false);
        Add("D","Cluster Ob+Rnd Die","64.3 x 22.3",0.8,1,"Good",true);
        Add("D","Cluster Hex Die","61.5 x 33",0.6,1,"Good",false);

        // Single Dies - A Station
        Add("A","Round Die","2-3",0.4,1,"Good",false);
        Add("A","Round Die","3.0",0.12,1,"OK",true);
        Add("A","Round Die","3.0",0.2,1,"OK",true);
        Add("A","Round Die","3.0",0.3,1,"OK",true);
        Add("A","Round Die","3.2",0.5,1,"OK",true);
        Add("A","Round Die","3.2",0.6,1,"OK",true);
        Add("A","Round Die","4.0",0.6,1,"OK",true);
        Add("A","Round Die","5.0",0.12,1,"OK",true);
        Add("A","Round Die","5.0",0.3,1,"OK",true);
        Add("A","Round Die","5.0",0.38,1,"OK",true);
        Add("A","Round Die","5.0",0.6,1,"OK",true);
        Add("A","Round Die","7.1",0.3,2,"OK",true);
        Add("A","Round Die","7.1",0.4,1,"OK",true);
        Add("A","Round Die","7.1",0.6,1,"OK",true);
        Add("A","Round Die","8.0",0.12,1,"OK",true);
        Add("A","Round Die","8.0",0.2,2,"OK",true);
        Add("A","Round Die","8.0",0.3,1,"OK",true);
        Add("A","Round Die","8.0",0.6,2,"OK",true);
        Add("A","Round Die","10.0",0.12,1,"OK",true);
        Add("A","Round Die","10.0",0.2,2,"OK",true);
        Add("A","Round Die","10.0",0.3,1,"OK",true);
        Add("A","Round Die","10.0",0.4,1,"OK",true);
        Add("A","Round Die","10.0",0.6,2,"OK",true);
        Add("A","Round Die","12.0",0.12,1,"OK",true);
        Add("A","Round Die","12.0",0.2,1,"OK",true);
        Add("A","Round Die","12.0",0.6,1,"OK",true);
        Add("A","Square Die","5.0",0.12,1,"Good",true);
        Add("A","Square Die","5.0",0.2,1,"Good",true);
        Add("A","Square Die","5.0",0.3,1,"Good",true);
        Add("A","Square Die","5.0",0.6,1,"Good",true);

        // Single Dies - B Station
        Add("B","Round Die","9.0",0.3,1,"OK",true);
        Add("B","Round Die","9.0",0.6,1,"OK",true);
        Add("B","Round Die","13.0",0.3,1,"OK",true);
        Add("B","Round Die","13.0",0.4,1,"OK",true);
        Add("B","Round Die","13.0",0.6,2,"OK",true);
        Add("B","Round Die","16.0",0.12,1,"OK",false);
        Add("B","Round Die","16.0",0.2,1,"OK",false);
        Add("B","Round Die","16.0",0.4,1,"OK",false);
        Add("B","Round Die","16.0",0.6,1,"OK",false);
        Add("B","Round Die","19.0",0.6,1,"Good",false);
        Add("B","Round Die","19.0",0.8,2,"Good",false);
        Add("B","Round Die","20.0",0.12,1,"OK",true);
        Add("B","Round Die","20.0",0.2,1,"OK",true);
        Add("B","Round Die","20.0",0.6,1,"OK",true);
        Add("B","Round Die","22.0",0.2,1,"Good",true);
        Add("B","Square Die","10 x 10",0.38,2,"OK",false);
        Add("B","Square Die","10 x 10",0.4,1,"OK",false);
        Add("B","Square Die","15 x 15",0.12,3,"OK",false);
        Add("B","Square Die","15 x 15",0.2,2,"OK",false);
        Add("B","Square Die","15 x 15",0.3,2,"OK",false);
        Add("B","Square Die","15 x 15",0.4,1,"OK",false);
        Add("B","Square Die","15 x 15",0.5,1,"OK",false);
        Add("B","Square Die","15 x 15",0.6,4,"OK",false);
        Add("B","Oblong Die","5 x 24",0.3,1,"Good",true);
        Add("B","Oblong Die","5 x 24",0.4,1,"Good",true);
        Add("B","Oblong Die","5 x 24",0.6,1,"Good",true);
        Add("B","Oblong Die","6 x 10",0.6,1,"Good",true);
        Add("B","Oblong Die","6 x 20",0.12,1,"Good",true);
        Add("B","Oblong Die","6 x 20",0.2,1,"Good",true);
        Add("B","Oblong Die","6 x 20",0.3,1,"Good",true);
        Add("B","Oblong Die","6 x 20",0.6,2,"Good",true);
        Add("B","Oblong Die","6.5 x 12",0.3,1,"Good",false);
        Add("B","Oblong Die","6.5 x 12",0.6,1,"Good",false);
        Add("B","Oblong Die","7 x 21",0.3,1,"Good",false);
        Add("B","Oblong Die","7 x 21",0.6,1,"Good",false);
        Add("B","Oblong Die","7 x 21",0.8,1,"Good",false);
        Add("B","Oblong Die","8 x 13",0.3,2,"Poor",true,"Not Good");
        Add("B","Oblong Die","8 x 13",4.6,1,"Good",false);
        Add("B","Oblong Die","10 x 15",0.3,1,"OK",true);
        Add("B","Oblong Die","13 x 15",0.3,1,"Good",false);
        Add("B","Rectangle Die","3 x 12",0.3,3,"OK",true);
        Add("B","Rectangle Die","5 x 20",0.12,2,"OK",true,"Artifacts");
        Add("B","Rectangle Die","5 x 20",0.2,1,"OK",true,"Artifacts");
        Add("B","Rectangle Die","5 x 20",0.3,2,"OK",true,"Artifacts");
        Add("B","Rectangle Die","5 x 20",0.4,1,"OK",true,"Artifacts");
        Add("B","Rectangle Die","5 x 20",0.5,1,"OK",true,"Artifacts");
        Add("B","Rectangle Die","5 x 20",0.6,2,"OK",true,"Artifacts");
        Add("B","Rectangle Die","6.5 x 12",0.3,1,"Good",true);
        Add("B","Rectangle Die","7 x 10",0.3,1,"OK",true);
        Add("B","Rectangle Die","7 x 10",0.6,1,"OK",true);
        Add("B","Rectangle Die","7 x 10",4.5,1,"OK",true);
        Add("B","Trapezoid Die","4 x 17",0.6,1,"OK",true);
        Add("B","Hexagon Die","6.1",0.2,1,"Good",true);
        Add("B","Hexagon Die","9.5",0.2,1,"Good",false);
        Add("B","Hexagon Die","9.5",0.3,2,"Good",false);
        Add("B","Hexagon Die","9.5",0.6,2,"Good",false);
        Add("B","Hexagon Die","11.2 x 12.9",0.3,1,"Good",false);
        Add("B","Hexagon Die","11.2 x 12.9",0.38,1,"Good",false);
        Add("B","Hexagon Die","11.2 x 12.9",0.4,1,"Good",false);
        Add("B","Hexagon Die","11.2 x 12.9",0.6,3,"Good",false);
        Add("B","Special T-Rad Sq Edge","15 approx",0.12,1,"OK",false);
        Add("B","Special T-Rad Sq Edge","15 approx",0.2,1,"OK",false);
        Add("B","Special T-Rad Sq Edge","15 approx",0.3,1,"OK",false);
        Add("B","Special T-Rad Sq Edge","15 approx",0.6,1,"OK",false);
        Add("B","Round Edge Die","11 x 11 x 4?",0.6,1,"Good",false);

        // Single Dies - C/D Station
        Add("C","Rectangle Die","12 x 25",0.3,1,"OK",false);
        Add("C","Rectangle Die","12 x 25",0.4,1,"OK",false);
        Add("C","Rectangle Die","12 x 25",0.6,1,"OK",false);
        Add("D","Rectangle Die","5 x 60",0.12,1,"OK",true);
        Add("D","Rectangle Die","5 x 60",0.4,2,"OK",true);
        Add("D","Square Die","30 x 30",0.4,1,"OK",true);
        Add("D","Square Die","50 x 50",0.4,1,"OK",true);
        Add("D","Square De Burring Die","30 x 30",null,1,"Good",false);
        Add("D","Round Form Die","-",null,1,"Good",false);
        Add("D","Die (Unknown Type)","25.4 x 88.5",0.6,1,"Good",true);
        Add("D","Die (Unknown Type)","53.9 x 58.3",0.6,1,"Good",true);
        Add("D","Round Single Die","30",0.3,1,"Good",true);
        Add("D","Round Single Die","30",0.6,1,"Good",true);
    }
}
