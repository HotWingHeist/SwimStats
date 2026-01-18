# SwimStats - Swimming Performance Analysis Platform

**For:** EZPC Swimming Club  
**Status:** ✅ Stable (Ready for Use)  
**Latest Version:** v1.3 (January 18, 2026)

---

## 📖 Quick Navigation

- **👤 For Users:** [Getting Started](#-getting-started) | [How to Use](#how-to-use-guide)
- **👨‍💻 For Developers:** [Architecture](#-architecture) | [Technical Details](#technical-details)
- **📋 Status:** [Feature List](#-complete-feature-list) | [Test Results](#-test-status) | [Changes](#-whats-new)

---

## 📌 What is SwimStats?

SwimStats is a free, easy-to-use application that helps swimming clubs track and analyze swimmer performance.

**Think of it as:** A personal training log for swimmers that shows trends, highlights improvements, and compares performance across the team.

**Main uses:**
- 📊 Track individual swimmer progress over time
- 📈 Compare multiple swimmers side-by-side
- 🎯 Identify improvements and plateaus
- 📋 Manage team swimmer data

---

## 🚀 Getting Started

### Installation
1. Download `SwimStats.exe`
2. Run it (no installation needed)
3. App automatically loads with 30 EZPC swimmers
4. Start importing data

### First Import
1. Select stroke, distance, and course from dropdowns
2. Check 2-3 swimmers in the list
3. Click **"📥 SwimTrack"** button
4. Wait for import to finish
5. View results in chart and table below

---

## 💡 How to Use Guide

### Importing Data

**Option 1: SwimTrack (Recommended) ✅**
- Fastest & most reliable
- No delays or blocking
- Best for most swimmers
- Click: **"📥 SwimTrack"** button

**Option 2: SwimRankings (International)**
- Slower (5-10 minute imports)
- Good for international swimmers
- Click: **"📥 SwimRankings"** button
- ⚠️ Don't use for bulk imports (website rate limits)

---

### Managing Your Swimmer List

**Where is the file?**
```
%LOCALAPPDATA%\SwimStats\EZPCswimmers.json
```

**How to add/remove swimmers:**
1. Open file in Notepad
2. Edit the JSON:
   ```json
   { "id": 1, "firstName": "John", "lastName": "Doe" }
   ```
3. Save file
4. In app: Click **File → Reload Configuration**

**Reload Configuration:** 
- Restarts app with new swimmer list
- Creates automatic backup first
- Shows success/error message

---

### Understanding the Chart

**Lines you'll see:**

| Line Type | Meaning |
|-----------|---------|
| **Solid colored** | Individual swimmer's times |
| **Dashed blue** | Club's best time (reference line) |
| **Dashed orange** | Club's slowest time (reference line) |

**How to use it:**
- **Hover** over solid lines = See date, time, location
- **Scroll** to zoom in/out
- **Drag** to move around
- **Right-click** to reset view

---

### Personal Records Table

**Shows:** Best times for each swimmer

**Features:**
- Click column headers to sort
- Double-click a row to highlight that swimmer in the chart
- Shows best time, seasonal best, previous seasonal best

---

### Filtering Options

**Available filters:**
- **Stroke:** Freestyle, Backstroke, Breaststroke, Butterfly, IM
- **Distance:** 50m, 100m, 200m, 400m, 1500m
- **Course:** 50m (long course) or 25m (short course)
- **Swimmers:** Check boxes to select

**Quick buttons:**
- ✓ **Select All** - Check all swimmers
- ✗ **Clear All** - Uncheck all swimmers

---

## ⚠️ Important Notes

### SwimRankings Rate Limiting
**The Problem:** SwimRankings website blocks scrapers that make too many requests

**What happens:** If you import too fast, the website will block your IP for 30min-1hr

**The Solution:** Use SwimTrack instead (no blocking, instant results)

**If SwimRankings is slow or blocked:**
- Try SwimTrack button instead
- Wait 1 hour and try again
- Use SwimTrack for bulk imports

### Configuration Backups
**Automatic backups created when:**
- You click "File → Reload Configuration"

**Location:** `%LOCALAPPDATA%\SwimStats\backups\`

**Keep:** Last 10 backups automatically

---

## 📊 Test Status

| Test Type | Count | Result |
|-----------|-------|--------|
| Import Tests | 31 | ✅ Passing |
| Duplicate Detection | 3 | ✅ Passing |
| Chart Tests | 2 | ✅ Passing |
| Configuration | 2 | ✅ Passing |
| **Total** | **36** | **✅ All Passing** |

---

## 📋 Complete Feature List

| Feature | Status | Notes |
|---------|--------|-------|
| SwimTrack Import | ✅ | **Primary source** |
| SwimRankings Import | ✅ | Rate limited (use SwimTrack) |
| Duplicate Detection | ✅ | Automatic |
| Configuration Management | ✅ | JSON file |
| Reload Without Restart | ✅ | File menu option |
| Interactive Chart | ✅ | Zoom & pan enabled |
| Tooltip Hover Info | ✅ | Shows date/time/location |
| Personal Records Table | ✅ | Sortable, filterable |
| Multi-Swimmer Compare | ✅ | Side-by-side comparison |
| JSON Validation | ✅ | Error messages shown |
| Auto Backups | ✅ | Keeps 10 recent |
| Export to CSV | ❌ | Future version |
| Scheduled Imports | ❌ | Future version |
| Web Version | ❌ | Planned |

---

## 🔧 Troubleshooting

| Problem | Solution |
|---------|----------|
| **No swimmers showing** | Check `EZPCswimmers.json` exists in AppData |
| **Import fails** | Try SwimTrack instead of SwimRankings |
| **Chart is empty** | Select swimmers from dropdown first |
| **JSON error on reload** | Validate JSON syntax (check quotes, brackets) |
| **App won't start** | Delete `swimstats.db` file and restart |
| **Configuration lost** | Restore from `%LOCALAPPDATA%\SwimStats\backups\` |

---

## 📚 Technical Details (For Developers)

### Architecture Overview

```
┌─────────────────────┐
│   WPF UI Layer      │ <- MainWindow.xaml, MainViewModel
├─────────────────────┤
│   Business Logic    │ <- MainViewModel, Services
├─────────────────────┤
│   Data Layer        │ <- Importers, ConfigLoader
├─────────────────────┤
│   Database          │ <- SQLite + EF Core
└─────────────────────┘
```

### Technology Stack
- **UI Framework:** WPF (.NET 8.0)
- **Database:** SQLite with Entity Framework Core
- **Charts:** OxyPlot with custom SwimTimeSeries
- **Testing:** xUnit (36 tests)
- **Data Format:** JSON configuration

### Project Structure
```
src/
  SwimStats.App/              # User interface (WPF)
    MainWindow.xaml
    ViewModels/
    Controls/
    Converters/
    
  SwimStats.Core/             # Data models
    Models/
    Interfaces/
    
  SwimStats.Data/             # Data access layer
    Services/
      SwimTrackImporter.cs    # HTML parser
      SwimRankingsImporter.cs # HTML parser with throttling
      ConfigurationBackupService.cs
      SwimmerConfigurationLoader.cs
      
  SwimStats.TestImporter/     # Testing utility

tests/
  SwimStats.Tests/            # Unit tests
    SwimTrackImporterTests.cs (31 tests)
    SwimRankingsImporterTests.cs (2 tests)
    MainViewModelTests.cs (3 tests)
```

### Key Components

**SwimTrackImporter**
- Parses HTML from SwimTrack website
- Extracts athlete dropdown data
- Extracts competition results table
- 31 comprehensive tests
- No rate limiting

**SwimRankingsImporter**
- Parses HTML from SwimRankings website
- Uses AJAX search endpoint for athlete finding
- Implements 5-second request throttling
- Handles rate limiting (429, 503 responses)
- 2 unit tests

**SwimmerConfigurationLoader**
- Loads JSON swimmer configuration
- Validates JSON schema
- Provides helpful error messages
- 3-tier fallback strategy

**ConfigurationBackupService**
- Creates timestamped backups
- Maintains backup history (10 max)
- Auto-cleanup of old backups
- Backup/restore functionality

**SwimTimeSeries** (Custom Chart Control)
- Extends OxyPlot LineSeries
- Custom tooltip formatting
- DisableTracker property to hide club statistics tooltips
- Formats swimming times (MM:SS.00 format)

---

## 🔄 Data Flow

### Import Process
```
User selects swimmers → Chooses criteria → Clicks import button
  ↓
Importer fetches website data → Parses HTML
  ↓
Duplicate detection checks existing data → Stores new results only
  ↓
Database updated → UI refreshed with new data
```

### Configuration Reload
```
User edits EZPCswimmers.json → Clicks "Reload Configuration"
  ↓
Backup created (before loading new config)
  ↓
JSON validated → Loaded into memory
  ↓
Database updated → UI refreshed with new swimmer list
```

---

## 🎯 Recent Improvements (v1.3)

### Fixed: SwimRankings Rate Limiting
- **Problem:** Website blocks IP after rapid requests
- **Solution:** Added 5-second delay between all requests
- **Impact:** Prevents IP blocking during imports
- **Trade-off:** Slower imports (but reliable)

### Added: Request Throttling
- **SemaphoreSlim:** Serializes HTTP requests (no parallel requests)
- **Exponential Backoff:** Handles 429/503 responses
- **Status:** ✅ Tested with 60+ swimmer imports

---

## 📝 Change History

| Version | Date | What Changed |
|---------|------|--------------|
| **v1.3** | 2026-01-18 | Fixed SwimRankings rate limiting (5s throttling) |
| **v1.2** | 2026-01-18 | Chart tooltips (swimmer data only, not club stats) |
| **v1.1** | 2026-01-18 | JSON validation, UI guidance, auto backups |
| **v1.0** | 2026-01-18 | Initial release |

---

## 🚀 Future Roadmap

**Planned Features:**
- 📅 Scheduled automatic imports
- 📊 Data export (CSV, PDF)
- 🌐 Web-based version
- 📱 Mobile app
- 👥 Multi-club support
- 📈 Advanced statistics & analytics

---

## 📞 Support

**Issues or Questions?**
- Check [Troubleshooting](#-troubleshooting) section above
- Review this entire document (comprehensive coverage)
- Check GitHub issues: https://github.com/HotWingHeist/SwimStats

**Report a Bug:**
- Open issue on GitHub with:
  - What happened
  - Steps to reproduce
  - Error message (if any)
  - SwimStats version

---

## 📄 License & Credits

**Project:** SwimStats  
**For:** EZPC Swimming Club  
**License:** Open Source  
**Repository:** https://github.com/HotWingHeist/SwimStats

**Built with:**
- .NET 8.0
- OxyPlot (charting)
- Entity Framework Core (database)
- HtmlAgilityPack (web scraping)

---

**Last Updated:** January 18, 2026  
**Document Version:** 1.0 (Rewritten for Readability)  
**Status:** ✅ Ready to Use
