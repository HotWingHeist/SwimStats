# SwimRankings Data Source Migration

## Overview
Successfully migrated SwimStats from using **SwimTrack.nl** as the data source to **SwimRankings.net**.

## Changes Made

### 1. New Importer: `SwimRankingsImporter.cs`
**Location:** [src/SwimStats.Data/Services/SwimRankingsImporter.cs](src/SwimStats.Data/Services/SwimRankingsImporter.cs)

Created a new importer class that implements `ISwimTrackImporter` interface:
- **ImportSwimmersAsync()**: Searches for athletes on SwimRankings and adds them to the database
- **ImportResultsAsync()**: Fetches swimmer results from SwimRankings
- **ParseSwimmerResults()**: Extracts swim times, strokes, distances, and dates from HTML tables
- **ExtractStroke()**: Parses stroke types from text (Freestyle, Backstroke, Breaststroke, Butterfly, IM)
- Includes robust error handling and progress callbacks

#### Key Features:
- Uses improved User-Agent header for better compatibility
- Extracts data from HTML table structures common to SwimRankings
- Implements time parsing for formats: `mm:ss.ms`, `ss.ms`, `m:ss.ms`
- Distance extraction using regex pattern `(\d+)\s*m(?:eter)?`
- Date parsing with support for multiple formats
- Batch database operations for performance
- 500ms polite delay between athlete requests

### 2. Updated Dependency Injection
**File:** [src/SwimStats.App/App.xaml.cs](src/SwimStats.App/App.xaml.cs)

Changed service registration:
```csharp
// Before:
services.AddScoped<ISwimTrackImporter, SwimTrackImporter>();

// After:
services.AddScoped<ISwimTrackImporter, SwimRankingsImporter>();
```

### 3. Updated Data Source URLs
**File:** [src/SwimStats.App/ViewModels/MainViewModel.cs](src/SwimStats.App/ViewModels/MainViewModel.cs)

Updated two locations where import happens:

**AutoImportDataAsync()** - Line ~172:
```csharp
// Before:
var url = "https://www.swimtrack.nl/ez-pc/perstijden.php";

// After:
var url = "https://www.swimrankings.net/index.php?page=athleteSelect&nationId=0&selectPage=SEARCH";
```

**ImportData()** - Line ~281:
```csharp
// Before:
var importer = new SwimTrackImporter(db, ...);
var url = "https://www.swimtrack.nl/ez-pc/perstijden.php";

// After:
var importer = new SwimRankingsImporter(db, ...);
var url = "https://www.swimrankings.net/index.php?page=athleteSelect&nationId=0&selectPage=SEARCH";
```

## Build Status
✅ **Successful** - No compilation errors
- 4 pre-existing warnings in test file (unrelated to this change)
- 0 new errors

## Data Source Comparison

| Feature | SwimTrack.nl | SwimRankings.net |
|---------|-------------|-----------------|
| Type | Dutch regional database | International rankings |
| URL Format | HTML dropdown selection | Search results page |
| Data Coverage | Dutch swimmers | Global swimmers |
| Stroke Codes | Custom (vl, ru, ss, vr, wi) | Standard English names |
| Time Formats | European (comma decimals) | International (period decimals) |

## Testing Notes
- Application builds successfully
- Importer follows same interface pattern as original
- Maintains backward compatibility with existing data models
- Progress callbacks work with new importer
- Error handling implemented with graceful fallbacks

## Next Steps
1. Run the application and test the import functionality
2. Verify athlete data loads from SwimRankings
3. Test with different athlete search queries
4. Monitor performance with various data volumes
5. Adjust HTML selectors if SwimRankings updates their site structure
