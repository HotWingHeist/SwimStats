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
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
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
    public ObservableCollection<SelectableCourse> SelectableCourses { get; } = new();
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
    
    private CancellationTokenSource? _importCancellationTokenSource;
    
    [ObservableProperty]
    private PersonalRecordViewModel? selectedPersonalRecord;
    
    [ObservableProperty]
    private bool hasSwimmersSelected;
    
    [ObservableProperty]
    private bool hasNoData;
    
    private bool _isInitializing = true;

    public MainViewModel()
    {
        // Add stroke options
        Strokes.Add(new LocalizedStroke(Stroke.Freestyle));
        Strokes.Add(new LocalizedStroke(Stroke.Breaststroke));
        Strokes.Add(new LocalizedStroke(Stroke.Backstroke));
        Strokes.Add(new LocalizedStroke(Stroke.Butterfly));
        Strokes.Add(new LocalizedStroke(Stroke.IM));

        // Add course options - both selected by default
        var longCourse = new SelectableCourse(Course.LongCourse, "50m pool") { IsSelected = true };
        var shortCourse = new SelectableCourse(Course.ShortCourse, "25m pool") { IsSelected = true };
        
        // Hook into course selection changes
        longCourse.PropertyChanged += (sender, e) =>
        {
            if (e.PropertyName == nameof(SelectableCourse.IsSelected))
            {
                if (!_isInitializing)
                {
                    SaveSelections();
                    BuildChart();
                    LoadPersonalRecords();
                }
            }
        };
        
        shortCourse.PropertyChanged += (sender, e) =>
        {
            if (e.PropertyName == nameof(SelectableCourse.IsSelected))
            {
                if (!_isInitializing)
                {
                    SaveSelections();
                    BuildChart();
                    LoadPersonalRecords();
                }
            }
        };
        
        SelectableCourses.Add(longCourse);
        SelectableCourses.Add(shortCourse);

        // Load saved settings
        var settings = AppSettings.Instance;
        
        // Set selected stroke (will be available immediately)
        if (settings.SelectedStroke != null && Enum.TryParse<Stroke>(settings.SelectedStroke, out var savedStroke))
        {
            SelectedStroke = Strokes.FirstOrDefault(s => s.Stroke == savedStroke) ?? Strokes.FirstOrDefault();
        }
        else
        {
            SelectedStroke = Strokes.FirstOrDefault();
        }

        // Load swimmers and distances from database on startup
        InitializeAllDataSync();
    }

    private void InitializeAllDataSync()
    {
        try
        {
            if (App.Services == null) return;

            using var scope = App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<SwimStatsDbContext>();
            
            // Debug: Check database state at startup
            var dbConnection = db.Database.GetDbConnection();
            var dbFilePath = dbConnection.DataSource;
            var resultCount = db.Results.Count();
            var swimmerCount = db.Swimmers.Count();
            System.Diagnostics.Debug.WriteLine($"[Startup] Database: {dbFilePath}");
            System.Diagnostics.Debug.WriteLine($"[Startup] Database has {resultCount} results, {swimmerCount} swimmers");
            
            // Load distances from database
            var uniqueDistances = db.Events
                .Select(e => e.DistanceMeters)
                .Distinct()
                .OrderBy(d => d)
                .ToList();
            
            Distances.Clear();
            foreach (var distance in uniqueDistances)
            {
                Distances.Add(distance);
            }
            
            // If no distances found, add defaults
            if (Distances.Count == 0)
            {
                Distances.Add(50);
                Distances.Add(100);
                Distances.Add(200);
                Distances.Add(400);
                Distances.Add(800);
                Distances.Add(1500);
            }

            // Set selected distance
            var settings = AppSettings.Instance;
            SelectedDistance = settings.SelectedDistance ?? Distances.FirstOrDefault();

            // Load swimmers from database
            var swimmers = db.Swimmers.ToList();
            foreach (var s in swimmers)
            {
                var selectableSwimmer = new SelectableSwimmer(s);
                
                // Auto-select all swimmers on first run
                selectableSwimmer.IsSelected = settings.SelectedSwimmerIds.Contains(s.Id) || settings.SelectedSwimmerIds.Count == 0;
                
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
            
            // All data is loaded, now build UI
            _isInitializing = false;
            BuildChart();
            LoadPersonalRecords();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[InitializeAllDataSync] Error: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[InitializeAllDataSync] StackTrace: {ex.StackTrace}");
        }
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
            var importer = new SwimRankingsImporter(db, (current, total, status) =>
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    ImportProgress = total > 0 ? (int)((double)current / total * 100) : 0;
                    ImportStatus = status;
                });
            });

            // Check if website is reachable before attempting import
            ImportStatus = loc["CheckingConnection"];
            if (!await importer.IsWebsiteReachableAsync())
            {
                // Website is down
                IsImporting = false;
                ImportProgress = 0;
                ImportStatus = "";
                
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show(
                        loc["WebsiteUnavailableMessage"],
                        loc["WebsiteUnavailableTitle"],
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                });
                return;
            }

            // Use SwimRankings athlete search page
            var url = "https://www.swimrankings.net/index.php?page=athleteSelect&nationId=0&selectPage=SEARCH";

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
        catch (Exception ex)
        {
            // Show error message for import failures
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                var loc = LocalizationManager.Instance;
                IsImporting = false;
                ImportProgress = 0;
                ImportStatus = "";
                
                MessageBox.Show(
                    $"{loc["ImportFailedMessage"]}\n\n{ex.Message}",
                    loc["ImportFailedTitle"],
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
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
    private async Task ImportFromSwimRankings()
    {
        if (App.Services == null) return;
        
        _importCancellationTokenSource = new CancellationTokenSource();
        
        using var scope = App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SwimStatsDbContext>();
        
        // Run import on background thread to prevent UI blocking
        await Task.Run(() => ImportDataAsync(new SwimRankingsImporter(db, GetProgressCallback()), "SwimRankings", _importCancellationTokenSource.Token));
    }

    [RelayCommand]
    private async Task ImportFromSwimTrack()
    {
        if (App.Services == null) return;
        
        _importCancellationTokenSource = new CancellationTokenSource();
        
        using var scope = App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SwimStatsDbContext>();
        
        // Run import on background thread to prevent UI blocking
        await Task.Run(() => ImportDataAsync(new SwimTrackImporter(db, GetProgressCallback()), "SwimTrack", _importCancellationTokenSource.Token));
    }

    private Action<int, int, string> GetProgressCallback()
    {
        return (current, total, status) =>
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                ImportProgress = total > 0 ? (int)((double)current / total * 100) : 0;
                ImportStatus = status;
            });
        };
    }

    [RelayCommand]
    private void CancelImport()
    {
        if (_importCancellationTokenSource != null && !_importCancellationTokenSource.Token.IsCancellationRequested)
        {
            _importCancellationTokenSource.Cancel();
            IsImporting = false;
            ImportProgress = 0;
            ImportStatus = "Import cancelled";
            
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                MessageBox.Show("Data import has been cancelled.", "Import Cancelled", MessageBoxButton.OK, MessageBoxImage.Information);
            });
        }
    }

    private async Task ImportDataAsync(ISwimTrackImporter importer, string sourceLabel, CancellationToken cancellationToken = default)
    {
        try
        {
            var loc = LocalizationManager.Instance;
            
            if (App.Services == null)
            {
                MessageBox.Show("Services not initialized", loc["ImportErrorTitle"], MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            IsImporting = true;
            ImportProgress = 0;
            ImportStatus = loc["StartingImport"];

            using var scope = App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<SwimStatsDbContext>();
            
            // Check if website is reachable before attempting import
            ImportStatus = $"Checking {sourceLabel} connection...";
            if (!await importer.IsWebsiteReachableAsync())
            {
                // Website is down
                IsImporting = false;
                ImportProgress = 0;
                ImportStatus = "";
                
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show(
                        $"The {sourceLabel} website is currently unavailable or not reachable. Please check your internet connection and try again later.",
                        loc["WebsiteUnavailableTitle"],
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                });
                return;
            }

            // Get all swimmers from the database
            var allSwimmers = await db.Swimmers.ToListAsync();
            
            if (!allSwimmers.Any())
            {
                MessageBox.Show("No swimmers found in the database.", loc["ImportErrorTitle"], MessageBoxButton.OK, MessageBoxImage.Warning);
                IsImporting = false;
                ImportProgress = 0;
                ImportStatus = "";
                return;
            }

            int totalResultsImported = 0;
            int totalRetrieved = 0;
            int totalExisting = 0;
            int successCount = 0;
            int failureCount = 0;
            
            // Import data for each swimmer
            for (int i = 0; i < allSwimmers.Count; i++)
            {
                // Check if cancellation was requested
                if (cancellationToken.IsCancellationRequested)
                {
                    IsImporting = false;
                    ImportProgress = 0;
                    ImportStatus = "Import cancelled by user";
                    return;
                }
                
                var swimmer = allSwimmers[i];
                ImportStatus = $"Importing from {sourceLabel} for {swimmer.DisplayName} ({i + 1}/{allSwimmers.Count})...";
                ImportProgress = (int)((i / (double)allSwimmers.Count) * 100);
                
                try
                {
                    var (retrieved, newCount, existing) = await importer.ImportSwimmerByNameAsync(swimmer.FirstName, swimmer.LastName);
                    totalRetrieved += retrieved;
                    totalResultsImported += newCount;
                    totalExisting += existing;
                    successCount++;
                }
                catch (HttpRequestException hre)
                {
                    // Network error - likely website is unreachable
                    failureCount++;
                    System.Diagnostics.Debug.WriteLine($"Network error importing {swimmer.DisplayName}: {hre.Message}");
                    
                    // If we're getting network errors for multiple swimmers, ask user to retry
                    if (failureCount >= 3)
                    {
                        IsImporting = false;
                        ImportProgress = 0;
                        ImportStatus = "";
                        
                        var result = MessageBox.Show(
                            $"{sourceLabel} website appears to be unreachable. This might be a temporary issue.\n\nWould you like to:\n- Retry: Try again\n- Cancel: Stop import",
                            "Network Error",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Warning);
                        
                        if (result == MessageBoxResult.Yes)
                        {
                            // Restart the import
                            IsImporting = true;
                            ImportProgress = 0;
                            ImportStatus = loc["StartingImport"];
                            i = -1; // Reset to start from beginning
                            totalResultsImported = 0;
                            successCount = 0;
                            failureCount = 0;
                            continue;
                        }
                        else
                        {
                            return;
                        }
                    }
                    continue;
                }
                catch (Exception ex)
                {
                    // Skip if import fails for this swimmer and continue with next
                    failureCount++;
                    System.Diagnostics.Debug.WriteLine($"Error importing {swimmer.DisplayName}: {ex.Message}");
                    System.Diagnostics.Debug.WriteLine($"StackTrace: {ex.StackTrace}");
                    continue;
                }
            }

            IsImporting = false;
            ImportProgress = 100;
            ImportStatus = "";
            
            // Debug: Check what was actually saved
            var dbConnection = db.Database.GetDbConnection();
            var dbFilePath = dbConnection.DataSource;
            var finalResultCount = await db.Results.CountAsync();
            System.Diagnostics.Debug.WriteLine($"[ImportDataAsync] Saved to: {dbFilePath}");
            System.Diagnostics.Debug.WriteLine($"[ImportDataAsync] After import, database has {finalResultCount} results");

            string message;
            if (failureCount == 0)
            {
                message = $"Import from {sourceLabel} complete!\n\n" +
                          $"Retrieved: {totalRetrieved} data points\n" +
                          $"New records added: {totalResultsImported}\n" +
                          $"Already existed: {totalExisting}\n" +
                          $"Successfully processed: {successCount} swimmers";
            }
            else
            {
                message = $"Import from {sourceLabel} complete with errors!\n\n" +
                          $"Retrieved: {totalRetrieved} data points\n" +
                          $"New records added: {totalResultsImported}\n" +
                          $"Already existed: {totalExisting}\n\n" +
                          $"Successful: {successCount} swimmers\n" +
                          $"Failed: {failureCount} swimmers";
            }

            MessageBox.Show(
                message,
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
                $"Import failed: {ex.Message}\n\n{ex.InnerException?.Message ?? ""}", 
                loc["ImportFailedTitle"], 
                MessageBoxButton.OK, 
                MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task ImportData()
    {
        // Keep this for backward compatibility, but delegate to SwimRankings
        await ImportFromSwimRankings();
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

    [RelayCommand]
    private void SelectAllSwimmers()
    {
        foreach (var s in SelectableSwimmers)
        {
            s.IsSelected = true;
        }

        SaveSelections();
        BuildChart();
        LoadPersonalRecords();
    }

    [RelayCommand]
    private void SelectNoneSwimmers()
    {
        foreach (var s in SelectableSwimmers)
        {
            s.IsSelected = false;
        }

        SaveSelections();
        BuildChart();
        LoadPersonalRecords();
    }

    [RelayCommand]
    private void ReloadConfiguration()
    {
        try
        {
            if (App.Services == null) return;

            using var scope = App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<SwimStatsDbContext>();

            // Create backup before reloading
            var backupService = new SwimStats.Data.Services.ConfigurationBackupService();
            var backupPath = backupService.CreateBackup();
            if (backupPath != null)
            {
                System.Diagnostics.Debug.WriteLine($"[MainViewModel] Configuration backup created: {backupPath}");
            }

            // Reload swimmers from configuration file
            System.Diagnostics.Debug.WriteLine("[MainViewModel] Reloading configuration from file");
            
            // Load fresh swimmers from configuration
            var freshSwimmers = SwimStats.Data.SwimmerConfigurationLoader.LoadSwimmers();
            
            if (freshSwimmers.Count == 0)
            {
                System.Windows.MessageBox.Show("No swimmers found in configuration file.", "Warning", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }
            
            // Update database with configuration
            foreach (var configSwimmer in freshSwimmers)
            {
                var dbSwimmer = db.Swimmers.FirstOrDefault(s => s.Id == configSwimmer.Id);
                if (dbSwimmer == null)
                {
                    // New swimmer from config
                    dbSwimmer = new Swimmer
                    {
                        Id = configSwimmer.Id,
                        FirstName = configSwimmer.FirstName ?? "",
                        LastName = configSwimmer.LastName ?? ""
                    };
                    db.Swimmers.Add(dbSwimmer);
                }
                else
                {
                    // Update existing swimmer
                    dbSwimmer.FirstName = configSwimmer.FirstName ?? "";
                    dbSwimmer.LastName = configSwimmer.LastName ?? "";
                }
            }
            db.SaveChanges();

            // Refresh UI
            SelectableSwimmers.Clear();
            var swimmers = db.Swimmers.ToList().OrderBy(s => s.DisplayName).ToList();
            var settings = AppSettings.Instance;
            
            foreach (var swimmer in swimmers)
            {
                var selectableSwimmer = new SelectableSwimmer(swimmer);
                selectableSwimmer.IsSelected = settings.SelectedSwimmerIds.Contains(swimmer.Id);
                SelectableSwimmers.Add(selectableSwimmer);
            }

            System.Diagnostics.Debug.WriteLine($"[MainViewModel] Configuration reloaded with {swimmers.Count} swimmers");
            System.Windows.MessageBox.Show($"Configuration reloaded successfully!\nLoaded {swimmers.Count} swimmers.", "Success", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            
            // Refresh the display
            BuildChart();
            LoadPersonalRecords();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainViewModel] ERROR reloading configuration: {ex.Message}\n{ex.StackTrace}");
            System.Windows.MessageBox.Show($"Error reloading configuration: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
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

    private void OnSelectableCourseChanged()
    {
        if (_isInitializing) return;
        SaveSelections();
        BuildChart();
        LoadPersonalRecords();
    }

    private List<Course> GetSelectedCourses() => SelectableCourses.Where(c => c.IsSelected).Select(c => c.CourseValue).ToList();

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
        var selectedCourses = GetSelectedCourses();

        if (SelectedStroke != null && SelectedDistance != null && selectedCourses.Any() && App.Services != null)
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
                                   r.Event.DistanceMeters == SelectedDistance.Value &&
                                   selectedCourses.Contains(r.Course))
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
                                   r.Date <= maxDate.Value &&
                                   selectedCourses.Contains(r.Course))
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
                        var cumulativeMin = new SwimTimeSeries
                        {
                            Title = loc["ClubBest"],
                            Color = OxyColor.FromRgb(0, 158, 115),  // Bluish Green (color-blind safe)
                            StrokeThickness = 2.5,
                            LineStyle = LineStyle.Dash,
                            MarkerType = MarkerType.None,
                            DisableTracker = true  // Disable tooltip for club statistics
                        };

                        var cumulativeMax = new SwimTimeSeries
                        {
                            Title = loc["ClubSlowest"],
                            Color = OxyColor.FromRgb(213, 94, 0),   // Vermillion (color-blind safe)
                            StrokeThickness = 2.5,
                            LineStyle = LineStyle.DashDot,
                            MarkerType = MarkerType.None,
                            DisableTracker = true  // Disable tooltip for club statistics
                        };

                        double runningMin = double.MaxValue;
                        double runningMax = double.MinValue;

                        foreach (var stat in statsByDate)
                        {
                            runningMin = Math.Min(runningMin, stat.Min);
                            runningMax = Math.Max(runningMax, stat.Max);

                            var dateValue = DateTimeAxis.ToDouble(stat.Date);
                            cumulativeMin.Points.Add(new DataPoint(dateValue, runningMin));
                            cumulativeMax.Points.Add(new DataPoint(dateValue, runningMax));
                        }

                        pm.Series.Add(cumulativeMin);
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
                        // Get results for each selected course and combine them
                        var combinedResults = new List<Result>();
                        foreach (var course in selectedCourses)
                        {
                            var courseResults = await resultService.GetResultsAsync(swimmer.Id, SelectedStroke.Stroke, SelectedDistance, course);
                            combinedResults.AddRange(courseResults);
                        }
                        
                        if (combinedResults.Any())
                        {
                            // Check if this swimmer is selected in the personal records table
                            bool isHighlighted = SelectedPersonalRecord != null && 
                                                SelectedPersonalRecord.SwimmerName == swimmer.DisplayName;
                            
                            var series = new SwimTimeSeries 
                            { 
                                Title = swimmer.DisplayName,
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
                                
                                foreach (var r in combinedResults)
                                {
                                    shadowSeries.Points.Add(new DataPoint(DateTimeAxis.ToDouble(r.Date), r.TimeSeconds));
                                }
                                
                                pm.Series.Add(shadowSeries);
                            }
                            
                            foreach (var r in combinedResults)
                            {
                                series.AddDataPoint(DateTimeAxis.ToDouble(r.Date), r.TimeSeconds, r.Location);
                            }
                            
                            pm.Series.Add(series);
                            colorIndex++;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Log the error for debugging
                System.Diagnostics.Debug.WriteLine($"[BuildChart] Error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[BuildChart] StackTrace: {ex.StackTrace}");
            }
        }

        PlotModel = pm;
        
        // Update empty state flags
        HasNoData = !SelectableSwimmers.Any();
        HasSwimmersSelected = selectedSwimmers.Any();
    }

    private void LoadPersonalRecords()
    {
        PersonalRecords.Clear();
        
        if (App.Services == null) return;
        var selectedCourses = GetSelectedCourses();
        if (SelectedStroke == null || SelectedDistance == null || !selectedCourses.Any()) return;
        
        var selectedSwimmers = SelectableSwimmers.Where(s => s.IsSelected).ToList();
        if (!selectedSwimmers.Any()) return;

        try
        {
            using var scope = App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<SwimStatsDbContext>();
            var loc = LocalizationManager.Instance;

            var currentStroke = SelectedStroke.Stroke;
            var currentDistance = SelectedDistance.Value;

            // Define calendar years (Jan 1 to Dec 31)
            var today = DateTime.Today;
            var currentYearStart = new DateTime(today.Year, 1, 1);
            var currentYearEnd = new DateTime(today.Year, 12, 31);
            
            var previousYearStart = currentYearStart.AddYears(-1);
            var previousYearEnd = currentYearStart.AddDays(-1);

            // Get swimmer IDs
            var swimmerIds = selectedSwimmers.Select(s => s.Swimmer.Id).ToList();

            // Single query to get all results for selected swimmers/stroke/distance/courses
            var allResults = db.Results
                .Include(r => r.Event)
                .Where(r => swimmerIds.Contains(r.SwimmerId) && 
                           r.Event!.Stroke == currentStroke && 
                           r.Event.DistanceMeters == currentDistance &&
                           selectedCourses.Contains(r.Course))
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

                // Current year best (Jan 1 - Dec 31)
                var yearlyBest = swimmerResults
                    .Where(r => r.Date >= currentYearStart && r.Date <= currentYearEnd)
                    .OrderBy(r => r.TimeSeconds)
                    .FirstOrDefault();

                // Previous year best
                var previousYearlyBest = swimmerResults
                    .Where(r => r.Date >= previousYearStart && r.Date <= previousYearEnd)
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
                    SeasonalBest = yearlyBest?.TimeSeconds,
                    SeasonalBestFormatted = yearlyBest != null ? FormatSwimTime(yearlyBest.TimeSeconds) : "-",
                    SeasonalBestDate = yearlyBest?.Date,
                    PreviousSeasonalBest = previousYearlyBest?.TimeSeconds,
                    PreviousSeasonalBestFormatted = previousYearlyBest != null ? FormatSwimTime(previousYearlyBest.TimeSeconds) : "-",
                    PreviousSeasonalBestDate = previousYearlyBest?.Date
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LoadPersonalRecords] Error: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[LoadPersonalRecords] StackTrace: {ex.StackTrace}");
        }
    }

    private static string FormatSwimTime(double totalSeconds)
    {
        var minutes = (int)(totalSeconds / 60);
        var remainingSeconds = (int)(totalSeconds % 60);
        var centiseconds = (int)((totalSeconds % 1) * 100);
        
        if (minutes > 0)
        {
            return $"{minutes:00}:{remainingSeconds:00}.{centiseconds:00}";
        }
        else
        {
            return $"{remainingSeconds:00}.{centiseconds:00}";
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
