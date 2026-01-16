using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.IO;
using SwimStats.Core.Interfaces;
using SwimStats.Data;
using SwimStats.Data.Services;

namespace SwimStats.App;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
	public static IServiceProvider? Services { get; private set; }

	protected override void OnStartup(StartupEventArgs e)
	{
		base.OnStartup(e);

		// Initialize application data directory
		var appDataPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SwimStats");
		try
		{
			if (!Directory.Exists(appDataPath))
			{
				Directory.CreateDirectory(appDataPath);
			}
		}
		catch (Exception dirEx)
		{
			MessageBox.Show($"Failed to create application data directory at {appDataPath}:\n{dirEx.Message}", 
				"Initialization Error", MessageBoxButton.OK, MessageBoxImage.Error);
			this.Shutdown(1);
			return;
		}

		var logPath = System.IO.Path.Combine(appDataPath, "startup.log");
		void Log(string msg)
		{
			try
			{
				File.AppendAllText(logPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - {msg}\n");
			}
			catch
			{
				// Silently fail if logging is not possible, don't crash the app
			}
		}

		try
		{
			Log("OnStartup called");

			var services = new ServiceCollection();

			var dbPath = System.IO.Path.Combine(appDataPath, "swimstats.db");
			Log($"DB path: {dbPath}");

			services.AddDbContext<SwimStatsDbContext>(opts => opts.UseSqlite($"Data Source={dbPath}"));
			services.AddScoped<IResultService, ResultService>();
			services.AddScoped<ISwimTrackImporter, SwimTrackImporter>();

			Services = services.BuildServiceProvider();
			Log("Services configured");

			// Ensure database is created and seeded
			using (var scope = Services.CreateScope())
			{
				var db = scope.ServiceProvider.GetRequiredService<SwimStatsDbContext>();
				db.Database.EnsureCreated();
				Log("Database ensured");
				
				// Add performance indexes if they don't exist
				try
				{
					db.Database.ExecuteSqlRaw(@"CREATE INDEX IF NOT EXISTS ""IX_Swimmers_Name"" ON ""Swimmers"" (""Name"");");
					db.Database.ExecuteSqlRaw(@"CREATE INDEX IF NOT EXISTS ""IX_Events_Stroke_DistanceMeters"" ON ""Events"" (""Stroke"", ""DistanceMeters"");");
					db.Database.ExecuteSqlRaw(@"CREATE INDEX IF NOT EXISTS ""IX_Results_SwimmerId_EventId_Date"" ON ""Results"" (""SwimmerId"", ""EventId"", ""Date"");");
					db.Database.ExecuteSqlRaw(@"CREATE INDEX IF NOT EXISTS ""IX_Results_Date"" ON ""Results"" (""Date"");");
					Log("Performance indexes added");
				}
				catch (Exception ex)
				{
					Log($"Index creation warning: {ex.Message}");
				}
			}

			// Force the main window to show using ShowDialog (blocking modal) to ensure visibility
			Log("About to create MainWindow");
			var mainWindow = new MainWindow();
			Log($"MainWindow created, about to call ShowDialog");
			mainWindow.ShowDialog();
			Log("ShowDialog returned");
		}
		catch (Exception ex)
		{
			Log($"ERROR: {ex}");
			MessageBox.Show($"Startup error: {ex.Message}\n\nSee log at: {logPath}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
		}
	}

	protected override void OnExit(ExitEventArgs e)
	{
		base.OnExit(e);
	}
}

