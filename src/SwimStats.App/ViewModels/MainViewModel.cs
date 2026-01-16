using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using OxyPlot.Legends;
using Microsoft.Extensions.DependencyInjection;
using SwimStats.Core.Interfaces;
using SwimStats.Core.Models;
using SwimStats.Data;
using SwimStats.Data.Services;
using System.Collections.ObjectModel;
using System.Windows;
using SwimStats.App.Models;
using SwimStats.App.Controls;
using SwimStats.App.Resources;
using SwimStats.App.Services;

namespace SwimStats.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    public ObservableCollection<SelectableSwimmer> SelectableSwimmers { get; } = new();
    public ObservableCollection<LocalizedStroke> Strokes { get; } = new();
    public ObservableCollection<int> Distances { get; } = new();
    public ObservableCollection<PersonalRecordViewModel> PersonalRecords { get; } = new();

    [ObservableProperty]
    private Swimmer? selectedSwimmer;

    [ObservableProperty]
    private LocalizedStroke? selectedStroke;

    [ObservableProperty]
    private int? selectedDistance;

    [ObservableProperty]
    private PlotModel? plotModel;

    [ObservableProperty]
    private int importProgress;

    [ObservableProperty]
    private string importStatus = "";

    [ObservableProperty]
    private bool isImporting;
    
    [ObservableProperty]
    private PersonalRecordViewModel? selectedPersonalRecord;
    
    private bool _isInitializing = true;

    public MainViewModel()
    {
        // Load swimmers from database on startup
        LoadInitialDataAsync();

        Strokes.Add(new LocalizedStroke(Stroke.Freestyle));
        Strokes.Add(new LocalizedStroke(Stroke.Breaststroke));
        Strokes.Add(new LocalizedStroke(Stroke.Backstroke));
        Strokes.Add(new LocalizedStroke(Stroke.Butterfly));
        Strokes.Add(new LocalizedStroke(Stroke.IM));

        // Add all common swimming distances
        Distances.Add(50);
        Distances.Add(100);
        Distances.Add(200);
        Distances.Add(400);
        Distances.Add(800);
        Distances.Add(1500);

        // Load saved settings
        var settings = AppSettings.Instance;
        
        // Restore selected stroke
        if (settings.SelectedStroke != null && Enum.TryParse<Stroke>(settings.SelectedStroke, out var savedStroke))
        {
            SelectedStroke = Strokes.FirstOrDefault(s => s.Stroke == savedStroke) ?? Strokes.FirstOrDefault();
        }
        else
        {
            SelectedStroke = Strokes.FirstOrDefault();
        }
        
        // Restore selected distance
        SelectedDistance = settings.SelectedDistance ?? Distances.FirstOrDefault();
        
        // Start background import on startup
        _ = Task.Run(async () => await AutoImportDataAsync());
    }

    private async void LoadInitialDataAsync()
    {
        if (App.Services == null) return;

        using var scope = App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SwimStatsDbContext>();
        
        var swimmers = await db.Swimmers.ToListAsync();
        var settings = AppSettings.Instance;
        
        foreach (var s in swimmers)
        {
            var selectableSwimmer = new SelectableSwimmer(s);
            
            // Restore selection from settings
            selectableSwimmer.IsSelected = settings.SelectedSwimmerIds.Contains(s.Id);
            
            selectableSwimmer.PropertyChanged += (sender, e) =>
            {
                if (e.PropertyName == nameof(SelectableSwimmer.IsSelected))
                {
                    SaveSelections();
                    BuildChart();
                    LoadPersonalRecords();
                }
            };
            SelectableSwimmers.Add(selectableSwimmer);
        }
        
        // Data is loaded, now build chart and load records
        _isInitializing = false;
        BuildChart();
        LoadPersonalRecords();
    }

    private async Task AutoImportDataAsync()
    {
        try
        {
            if (App.Services == null) return;

            using var scope = App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<SwimStatsDbContext>();
            
            // Check if we already have data - skip auto-import if we do
            var existingSwimmerCount = await db.Swimmers.CountAsync();
            if (existingSwimmerCount > 0)
            {
                // Data already exists, skip auto-import
                return;
            }

            // No delay needed - start immediately in background
            IsImporting = true;
            ImportProgress = 0;
            var loc = LocalizationManager.Instance;
            ImportStatus = loc["StartingImport"];
            
            // Create importer with progress callback
            var importer = new SwimTrackImporter(db, (current, total, status) =>
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    ImportProgress = total > 0 ? (int)((double)current / total * 100) : 0;
                    ImportStatus = status;
                });
            });

            // Use the specific page with swimmer personal times
            var url = "https://www.swimtrack.nl/ez-pc/perstijden.php";

            ImportStatus = loc["ImportingSwimmers"];
            var swimmerCount = await importer.ImportSwimmersAsync(url);
            
            ImportStatus = loc["ImportingResults"];
            var resultCount = await importer.ImportResultsAsync(url);

            IsImporting = false;
            ImportProgress = 0;
            ImportStatus = "";

            // Refresh the UI silently (no message box for auto-import)
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                await RefreshDataAsync();
            });
        }
        catch
        {
            // Silently fail for background import
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                IsImporting = false;
                ImportProgress = 0;
                ImportStatus = "";
            });
        }
    }

    [RelayCommand]
    private async Task ClearData()
    {
        if (App.Services == null) return;

        var loc = LocalizationManager.Instance;
        var result = MessageBox.Show(
            loc["ClearDataConfirmMessage"],
            loc["ClearDataConfirmTitle"],
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            using var scope = App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<SwimStatsDbContext>();
            
            // Delete all data
            await db.Database.ExecuteSqlRawAsync("DELETE FROM Results");
            await db.Database.ExecuteSqlRawAsync("DELETE FROM Events");
            await db.Database.ExecuteSqlRawAsync("DELETE FROM Swimmers");
            
            MessageBox.Show(loc["ClearDataCompleteMessage"], loc["ClearDataCompleteTitle"], MessageBoxButton.OK, MessageBoxImage.Information);

            // Refresh the UI
            await RefreshDataAsync();
            BuildChart();
        }
        catch (Exception ex)
        {
            MessageBox.Show(string.Format(loc["ClearDataErrorMessage"], ex.Message), loc["ImportErrorTitle"], MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task ImportData()
    {
        try
        {
            var loc = LocalizationManager.Instance;
            
            if (App.Services == null)
            {
                MessageBox.Show("Services not initialized", loc["ImportErrorTitle"], MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var result = MessageBox.Show(
                loc["ImportConfirmMessage"],
                loc["ImportConfirmTitle"],
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
            {
                return;
            }

            IsImporting = true;
            ImportProgress = 0;
            ImportStatus = loc["StartingImport"];

            using var scope = App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<SwimStatsDbContext>();
            
            // Create importer with progress callback
            var importer = new SwimTrackImporter(db, (current, total, status) =>
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    ImportProgress = total > 0 ? (int)((double)current / total * 100) : 0;
                    ImportStatus = status;
                });
            });

            // Use the specific page with swimmer personal times
            var url = "https://www.swimtrack.nl/ez-pc/perstijden.php";

            ImportStatus = loc["ImportingSwimmers"];
            var swimmerCount = await importer.ImportSwimmersAsync(url);
            
            ImportStatus = loc["ImportingResults"];
            var resultCount = await importer.ImportResultsAsync(url);

            IsImporting = false;
            ImportProgress = 0;
            ImportStatus = "";

            MessageBox.Show(
                string.Format(loc["ImportCompleteMessage"], swimmerCount, resultCount),
                loc["ImportCompleteTitle"], 
                MessageBoxButton.OK, 
                MessageBoxImage.Information);

            // Refresh the UI
            await RefreshDataAsync();
        }
        catch (Exception ex)
        {
            var loc = LocalizationManager.Instance;
            IsImporting = false;
            ImportProgress = 0;
            ImportStatus = "";
            MessageBox.Show(
                string.Format(loc["ImportErrorMessage"], ex.Message, ex.InnerException?.Message ?? ""), 
                loc["ImportErrorTitle"], 
                MessageBoxButton.OK, 
                MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void ClearSwimmerSelections()
    {
        // Unselect all swimmers so user can quickly reset selections
        foreach (var s in SelectableSwimmers)
        {
            s.IsSelected = false;
        }

        // Persist and refresh views
        SaveSelections();
        BuildChart();
        LoadPersonalRecords();
    }

    private async Task RefreshDataAsync()
    {
        if (App.Services == null) return;

        using var scope = App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SwimStatsDbContext>();
        
        // Save current selections before refresh
        var settings = AppSettings.Instance;
        var selectedSwimmerIds = SelectableSwimmers
            .Where(s => s.IsSelected)
            .Select(s => s.Swimmer.Id)
            .ToHashSet();
        
        // Reload swimmers from database
        var swimmers = await db.Swimmers.ToListAsync();
        SelectableSwimmers.Clear();
        foreach (var s in swimmers)
        {
            var selectableSwimmer = new SelectableSwimmer(s);
            
            // Restore previous selection state
            selectableSwimmer.IsSelected = selectedSwimmerIds.Contains(s.Id);
            
            selectableSwimmer.PropertyChanged += (sender, e) =>
            {
                if (e.PropertyName == nameof(SelectableSwimmer.IsSelected))
                {
                    SaveSelections();
                    BuildChart();
                    LoadPersonalRecords();
                }
            };
            SelectableSwimmers.Add(selectableSwimmer);
        }

        BuildChart();
    }

    private void SaveSelections()
    {
        var settings = AppSettings.Instance;
        
        // Save selected swimmers
        settings.SelectedSwimmerIds = SelectableSwimmers
            .Where(s => s.IsSelected)
            .Select(s => s.Swimmer.Id)
            .ToList();
        
        // Save selected stroke
        settings.SelectedStroke = SelectedStroke?.Stroke.ToString();
        
        // Save selected distance
        settings.SelectedDistance = SelectedDistance;
        
        settings.Save();
    }

    partial void OnSelectedSwimmerChanged(Swimmer? value)
    {
        if (!_isInitializing)
            BuildChart();
    }

    partial void OnSelectedStrokeChanged(LocalizedStroke? value)
    {
        if (_isInitializing) return;
        SaveSelections();
        BuildChart();
        LoadPersonalRecords();
    }

    partial void OnSelectedDistanceChanged(int? value)
    {
        if (_isInitializing) return;
        SaveSelections();
        BuildChart();
        LoadPersonalRecords();
    }

    partial void OnSelectedPersonalRecordChanged(PersonalRecordViewModel? value)
    {
        // Rebuild chart to highlight the selected swimmer's line
        BuildChart();
    }

    public void RefreshChart()
    {
        BuildChart();
        LoadPersonalRecords();
    }

    private async void BuildChart()
    {
        var loc = LocalizationManager.Instance;
        var pm = new PlotModel 
        { 
            Title = loc["ChartTitle"],
            TitleFontSize = 16,
            TitleFontWeight = OxyPlot.FontWeights.Bold,
            Background = OxyColors.White,
            PlotAreaBorderColor = OxyColor.FromRgb(200, 200, 200),
            PlotAreaBorderThickness = new OxyThickness(1),
            Padding = new OxyThickness(10)
        };
        
        // Enable zoom and pan functionality
        pm.IsLegendVisible = true;
        
        // Add legend with modern styling
        var legend = new Legend
        {
            LegendTitle = loc["LegendTitle"],
            LegendPosition = LegendPosition.RightTop,
            LegendPlacement = LegendPlacement.Outside,
            LegendOrientation = LegendOrientation.Vertical,
            LegendBorder = OxyColor.FromRgb(224, 224, 224),
            LegendBorderThickness = 1,
            LegendBackground = OxyColor.FromRgb(250, 250, 250),
            LegendTextColor = OxyColor.FromRgb(51, 51, 51),
            LegendFontSize = 11,
            LegendPadding = 8,
            LegendMargin = 10
        };
        pm.Legends.Add(legend);
        
        // Modern date axis with zoom/pan enabled
        pm.Axes.Add(new DateTimeAxis 
        { 
            Position = AxisPosition.Bottom, 
            StringFormat = "MMM yyyy",
            AxislineColor = OxyColor.FromRgb(150, 150, 150),
            AxislineThickness = 1,
            MajorGridlineStyle = LineStyle.Solid,
            MajorGridlineColor = OxyColor.FromRgb(240, 240, 240),
            MinorGridlineStyle = LineStyle.Dot,
            MinorGridlineColor = OxyColor.FromRgb(245, 245, 245),
            TextColor = OxyColor.FromRgb(85, 85, 85),
            FontSize = 11,
            // Enable zooming and panning
            IsPanEnabled = true,
            IsZoomEnabled = true,
            MinimumPadding = 0.05,
            MaximumPadding = 0.05
        });
        
        // Custom axis formatter for swim times with modern styling
        var timeAxis = new LinearAxis 
        { 
            Position = AxisPosition.Left, 
            Title = loc["TimeAxisTitle"],
            LabelFormatter = FormatSwimTime,
            Key = "TimeAxis",
            AxislineColor = OxyColor.FromRgb(150, 150, 150),
            AxislineThickness = 1,
            MajorGridlineStyle = LineStyle.Solid,
            MajorGridlineColor = OxyColor.FromRgb(240, 240, 240),
            MinorGridlineStyle = LineStyle.Dot,
            MinorGridlineColor = OxyColor.FromRgb(245, 245, 245),
            TextColor = OxyColor.FromRgb(85, 85, 85),
            FontSize = 11,
            TitleFontSize = 12,
            TitleFontWeight = 600,
            // Enable zooming and panning
            IsPanEnabled = true,
            IsZoomEnabled = true,
            MinimumPadding = 0.05,
            MaximumPadding = 0.05
        };
        pm.Axes.Add(timeAxis);

        // Get selected swimmers from checkboxes
        var selectedSwimmers = SelectableSwimmers.Where(s => s.IsSelected).Select(s => s.Swimmer).ToList();

        if (SelectedStroke != null && SelectedDistance != null && App.Services != null)
        {
            try
            {
                using var scope = App.Services.CreateScope();
                var resultService = scope.ServiceProvider.GetRequiredService<IResultService>();
                var db = scope.ServiceProvider.GetRequiredService<SwimStatsDbContext>();

                // First, get the date range from selected swimmers
                DateTime? minDate = null;
                DateTime? maxDate = null;

                if (selectedSwimmers.Any())
                {
                    var selectedSwimmerIds = selectedSwimmers.Select(s => s.Id).ToList();
                    var selectedSwimmerResults = await db.Results
                        .Include(r => r.Event)
                        .Where(r => selectedSwimmerIds.Contains(r.SwimmerId) &&
                                   r.Event!.Stroke == SelectedStroke.Stroke && 
                                   r.Event.DistanceMeters == SelectedDistance.Value)
                        .ToListAsync();

                    if (selectedSwimmerResults.Any())
                    {
                        minDate = selectedSwimmerResults.Min(r => r.Date);
                        maxDate = selectedSwimmerResults.Max(r => r.Date);
                    }
                }

                // Add club statistics only if we have a date range from selected swimmers
                if (minDate.HasValue && maxDate.HasValue)
                {
                    var allClubResults = await db.Results
                        .Include(r => r.Event)
                        .Where(r => r.Event!.Stroke == SelectedStroke.Stroke && 
                                   r.Event.DistanceMeters == SelectedDistance.Value &&
                                   r.Date >= minDate.Value &&
                                   r.Date <= maxDate.Value)
                        .OrderBy(r => r.Date)
                        .ToListAsync();

                    if (allClubResults.Any())
                    {
                        // Group by date and calculate statistics
                        var statsByDate = allClubResults
                            .GroupBy(r => r.Date)
                            .OrderBy(g => g.Key)
                            .Select(g => new
                            {
                                Date = g.Key,
                                Min = g.Min(r => r.TimeSeconds),
                                Median = CalculateMedian(g.Select(r => r.TimeSeconds).ToList()),
                                Max = g.Max(r => r.TimeSeconds)
                            })
                            .ToList();

                        // Create cumulative best (rolling minimum) - using color-blind friendly colors
                        var cumulativeMin = new LineSeries
                        {
                            Title = loc["ClubBest"],
                            Color = OxyColor.FromRgb(0, 158, 115),  // Bluish Green (color-blind safe)
                            StrokeThickness = 2.5,
                            LineStyle = LineStyle.Dash,
                            MarkerType = MarkerType.None
                        };

                        var cumulativeMedian = new LineSeries
                        {
                            Title = loc["ClubMedian"],
                            Color = OxyColor.FromRgb(230, 159, 0),  // Orange (color-blind safe)
                            StrokeThickness = 2.5,
                            LineStyle = LineStyle.Dot,
                            MarkerType = MarkerType.None
                        };

                        var cumulativeMax = new LineSeries
                        {
                            Title = loc["ClubSlowest"],
                            Color = OxyColor.FromRgb(213, 94, 0),   // Vermillion (color-blind safe)
                            StrokeThickness = 2.5,
                            LineStyle = LineStyle.DashDot,
                            MarkerType = MarkerType.None
                        };

                        double runningMin = double.MaxValue;
                        double runningMax = double.MinValue;

                        foreach (var stat in statsByDate)
                        {
                            runningMin = Math.Min(runningMin, stat.Min);
                            runningMax = Math.Max(runningMax, stat.Max);

                            var dateValue = DateTimeAxis.ToDouble(stat.Date);
                            cumulativeMin.Points.Add(new DataPoint(dateValue, runningMin));
                            cumulativeMedian.Points.Add(new DataPoint(dateValue, stat.Median));
                            cumulativeMax.Points.Add(new DataPoint(dateValue, runningMax));
                        }

                        pm.Series.Add(cumulativeMin);
                        pm.Series.Add(cumulativeMedian);
                        pm.Series.Add(cumulativeMax);
                    }
                }

                // Add individual swimmer data if any selected
                if (selectedSwimmers.Any())
                {
                    // Color-blind friendly palette (Wong 2011 + Tol)
                    // Designed to be distinguishable for all types of color blindness
                    var colorPalette = new[]
                    {
                        OxyColor.FromRgb(0, 114, 178),      // Blue
                        OxyColor.FromRgb(230, 159, 0),      // Orange
                        OxyColor.FromRgb(0, 158, 115),      // Bluish Green
                        OxyColor.FromRgb(204, 121, 167),    // Reddish Purple
                        OxyColor.FromRgb(86, 180, 233),     // Sky Blue
                        OxyColor.FromRgb(213, 94, 0),       // Vermillion
                        OxyColor.FromRgb(240, 228, 66),     // Yellow
                        OxyColor.FromRgb(0, 0, 0),          // Black
                        OxyColor.FromRgb(117, 112, 179),    // Purple
                        OxyColor.FromRgb(102, 102, 102)     // Gray
                    };
                    
                    int colorIndex = 0;
                    foreach (var swimmer in selectedSwimmers)
                    {
                        var results = await resultService.GetResultsAsync(swimmer.Id, SelectedStroke.Stroke, SelectedDistance);
                        
                        if (results.Any())
                        {
                            // Check if this swimmer is selected in the personal records table
                            bool isHighlighted = SelectedPersonalRecord != null && 
                                                SelectedPersonalRecord.SwimmerName == swimmer.Name;
                            
                            var series = new SwimTimeSeries 
                            { 
                                Title = swimmer.Name,
                                Color = colorPalette[colorIndex % colorPalette.Length],
                                MarkerType = MarkerType.Circle,
                                MarkerSize = isHighlighted ? 7 : 5,
                                StrokeThickness = isHighlighted ? 4.0 : 2.5,
                                CanTrackerInterpolatePoints = false,
                                MarkerFill = colorPalette[colorIndex % colorPalette.Length],
                                MarkerStroke = isHighlighted ? colorPalette[colorIndex % colorPalette.Length] : OxyColors.White,
                                MarkerStrokeThickness = isHighlighted ? 2 : 1
                            };
                            
                            // If highlighted, add a shadow effect by adding the series twice
                            if (isHighlighted)
                            {
                                // Add shadow series first (slightly offset and lighter)
                                var shadowSeries = new LineSeries
                                {
                                    Title = null, // Don't show in legend
                                    Color = OxyColor.FromAColor(80, colorPalette[colorIndex % colorPalette.Length]),
                                    StrokeThickness = 6.0,
                                    LineStyle = LineStyle.Solid,
                                    MarkerType = MarkerType.None
                                };
                                
                                foreach (var r in results)
                                {
                                    shadowSeries.Points.Add(new DataPoint(DateTimeAxis.ToDouble(r.Date), r.TimeSeconds));
                                }
                                
                                pm.Series.Add(shadowSeries);
                            }
                            
                            foreach (var r in results)
                            {
                                var dataPoint = new DataPoint(DateTimeAxis.ToDouble(r.Date), r.TimeSeconds);
                                series.Points.Add(dataPoint);
                            }
                            
                            pm.Series.Add(series);
                            colorIndex++;
                        }
                    }
                }
            }
            catch
            {
                // If query fails, show empty chart
            }
        }

        PlotModel = pm;
    }

    private void LoadPersonalRecords()
    {
        PersonalRecords.Clear();
        
        if (App.Services == null) return;
        if (SelectedStroke == null || SelectedDistance == null) return;
        
        var selectedSwimmers = SelectableSwimmers.Where(s => s.IsSelected).ToList();
        if (!selectedSwimmers.Any()) return;

        try
        {
            using var scope = App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<SwimStatsDbContext>();
            var loc = LocalizationManager.Instance;

            var currentStroke = SelectedStroke.Stroke;
            var currentDistance = SelectedDistance.Value;

            // Define seasons (Sept 1 to Aug 31)
            var today = DateTime.Today;
            var currentSeasonStart = new DateTime(
                today.Month >= 9 ? today.Year : today.Year - 1, 
                9, 1);
            var currentSeasonEnd = currentSeasonStart.AddYears(1).AddDays(-1);
            
            var previousSeasonStart = currentSeasonStart.AddYears(-1);
            var previousSeasonEnd = currentSeasonStart.AddDays(-1);

            // Get swimmer IDs
            var swimmerIds = selectedSwimmers.Select(s => s.Swimmer.Id).ToList();

            // Single query to get all results for selected swimmers/stroke/distance
            var allResults = db.Results
                .Include(r => r.Event)
                .Where(r => swimmerIds.Contains(r.SwimmerId) && 
                           r.Event!.Stroke == currentStroke && 
                           r.Event.DistanceMeters == currentDistance)
                .OrderBy(r => r.SwimmerId)
                .ThenBy(r => r.TimeSeconds)
                .ToList();

            // Group by swimmer
            var resultsBySwimmer = allResults.GroupBy(r => r.SwimmerId);

            foreach (var swimmerGroup in resultsBySwimmer)
            {
                var swimmer = selectedSwimmers.First(s => s.Swimmer.Id == swimmerGroup.Key);
                var swimmerResults = swimmerGroup.ToList();
                
                if (!swimmerResults.Any()) continue;

                // All-time best
                var bestResult = swimmerResults.First();

                // Current season best (Sept 1 - Aug 31)
                var seasonalBest = swimmerResults
                    .Where(r => r.Date >= currentSeasonStart && r.Date <= currentSeasonEnd)
                    .OrderBy(r => r.TimeSeconds)
                    .FirstOrDefault();

                // Previous season best
                var previousSeasonalBest = swimmerResults
                    .Where(r => r.Date >= previousSeasonStart && r.Date <= previousSeasonEnd)
                    .OrderBy(r => r.TimeSeconds)
                    .FirstOrDefault();

                PersonalRecords.Add(new PersonalRecordViewModel
                {
                    SwimmerName = swimmer.Name,
                    StrokeName = loc[$"Stroke_{currentStroke}"],
                    Distance = currentDistance,
                    BestTime = bestResult.TimeSeconds,
                    BestTimeFormatted = FormatSwimTime(bestResult.TimeSeconds),
                    Date = bestResult.Date,
                    SeasonalBest = seasonalBest?.TimeSeconds,
                    SeasonalBestFormatted = seasonalBest != null ? FormatSwimTime(seasonalBest.TimeSeconds) : "-",
                    SeasonalBestDate = seasonalBest?.Date,
                    PreviousSeasonalBest = previousSeasonalBest?.TimeSeconds,
                    PreviousSeasonalBestFormatted = previousSeasonalBest != null ? FormatSwimTime(previousSeasonalBest.TimeSeconds) : "-",
                    PreviousSeasonalBestDate = previousSeasonalBest?.Date
                });
            }
        }
        catch
        {
            // Silently fail if unable to load personal records
        }
    }

    private static string FormatSwimTime(double totalSeconds)
    {
        var minutes = (int)(totalSeconds / 60);
        var seconds = totalSeconds % 60;
        
        if (minutes > 0)
        {
            return $"{minutes}:{seconds:00.00}";
        }
        else
        {
            return $"{seconds:0.00}";
        }
    }

    internal static double CalculateMedian(List<double> values)
    {
        if (values == null || values.Count == 0)
            return 0;

        var sorted = values.OrderBy(x => x).ToList();
        int count = sorted.Count;

        if (count % 2 == 0)
        {
            // Even number of elements - average of middle two
            return (sorted[count / 2 - 1] + sorted[count / 2]) / 2.0;
        }
        else
        {
            // Odd number of elements - middle element
            return sorted[count / 2];
        }
    }
}
