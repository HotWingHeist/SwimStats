# SwimStats

Simple WPF (.NET 8) application to track swimmers' performance by stroke and distance.

Projects:
- src/SwimStats.App - WPF application
- src/SwimStats.Core - Domain models and interfaces
- src/SwimStats.Data - EF Core DbContext and services (SQLite)
- tests/SwimStats.Tests - xUnit tests

Build & run (PowerShell on Windows):

```powershell
# From repository root
dotnet build
# Run the WPF app
dotnet run --project src/SwimStats.App
```

The app uses a SQLite database located at `%LOCALAPPDATA%\SwimStats\swimstats.db`. On first run the DB is created and seeded with sample data.

Notes:
- Uses OxyPlot for charting.
- Minimal starter app created; extend viewmodels, services, and UI as needed.
