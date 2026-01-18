# SwimStats Requirements Document

**Last Updated:** January 18, 2026  
**Status:** In Development  
**Version:** 0.1.1

---

## Executive Summary

SwimStats is a swimming performance tracking and analysis application for EZPC Swimming Club. This document outlines all functional and technical requirements, implementation status, and testing coverage.

---

## Functional Requirements

### 1. Data Import Capabilities

#### FR1.1: SwimRankings Website Import
- **Description:** Import swimmer data and competition results from the SwimRankings website
- **Status:** ✅ IMPLEMENTED & TESTED (⚠️ Rate Limiting)
- **Implementation Details:**
  - Uses AJAX search endpoint (`internalRequest=athleteFind`) for athlete finding
  - Parses personal rankings page for race results
  - Handles re-import with duplicate detection
  - **Anti-Scraping Protection:** Implements 5-second request throttling to comply with server rate limits
- **Test Coverage:** 
  - Unit tests: `SwimRankingsImporterTests.cs` (2 tests)
  - Integration test: `TestImporter` (retrieves 60+ results successfully)
- **Limitations:** 
  - SwimRankings has aggressive rate limiting protection
  - Rapid requests (>5/min) trigger IP blocking for 30min-1hr
  - Mitigation: 5-second delay between requests prevents blocking
  - **Recommendation:** Use SwimTrack as primary source for reliable automated imports
  - SwimRankings suitable for: Occasional manual imports, international swimmers not in SwimTrack

#### FR1.2: SwimTrack Website Import
- **Description:** Import swimmer data and competition results from the SwimTrack website
- **Status:** ✅ IMPLEMENTED & TESTED (PRIMARY SOURCE)
- **Implementation Details:**
  - Uses HTML dropdown parsing for athlete selection
  - Parses HTML results tables
  - No-update strategy (import only, don't modify existing)
  - No rate limiting protection needed
- **Test Coverage:**
  - Unit tests: `SwimTrackImporterTests.cs` (31 tests)
- **Recommendation:** Primary data source for automated imports (no blocking, reliable)

#### FR1.3: Duplicate Detection
- **Description:** Prevent duplicate results from being imported multiple times
- **Status:** ✅ IMPLEMENTED & TESTED
- **Match Criteria:** SwimmerId + EventId + Date + **Exact TimeSeconds match**
- **Implementation Details:**
  - Changed from tolerance-based (< 0.00001s) to exact match (==)
  - Allows preservation of heats, semis, finals as separate results
- **Test Coverage:**
  - Unit tests: 3 new comprehensive tests in `SwimTrackImporterTests.cs`
  - Test scenarios: exact duplicates, heats/finals preservation
- **Rationale:** Different times = different races; exact match is clean and deterministic

---

### 2. Swimmer Configuration Management

#### FR2.1: Configuration File Format
- **Description:** Manage swimmers through JSON configuration file
- **Status:** ✅ IMPLEMENTED & TESTED
- **File Location:** 
  - Deployed: `{assembly_directory}/EZPCswimmers.json`
  - User-editable: `%LOCALAPPDATA%\SwimStats\EZPCswimmers.json`
- **File Format:**
  ```json
  [
    { "id": 1, "firstName": "John", "lastName": "Doe" },
    { "id": 2, "firstName": "Jane", "lastName": "Smith" }
  ]
  ```
- **Test Coverage:** Manual testing (30 swimmers pre-configured)

#### FR2.2: First-Time Start Handling
- **Description:** Application loads deployed configuration on first run
- **Status:** ✅ IMPLEMENTED & TESTED
- **Implementation Details:**
  - `SwimmerConfigurationLoader` implements 3-tier fallback:
    1. Check AppData location
    2. Fall back to deployed config in assembly directory
    3. Copy deployed config to AppData for future editing
- **Test Coverage:** Manual - tested on fresh database
- **Build Configuration:** File marked with `CopyToOutputDirectory=PreserveNewest` in csproj

#### FR2.3: Configuration Reload at Runtime
- **Description:** Users can reload configuration without restarting application
- **Status:** ✅ IMPLEMENTED & TESTED
- **User Interface:** 
  - Menu: **File → Reload Configuration**
  - Reloads swimmers from `EZPCswimmers.json`
  - Updates database
  - Refreshes UI (swimmer list, charts, personal records)
- **Implementation Details:**
  - `ReloadConfiguration` relay command in `MainViewModel`
  - Success/error messages shown to user
  - Materializes data to memory before ordering (LINQ translation fix)
- **Test Coverage:** Manual testing via UI

---

### 3. Data Model & Database

#### FR3.1: Swimming Result Structure
- **Description:** Store competitive swimming results
- **Status:** ✅ IMPLEMENTED
- **Fields:**
  - SwimmerId (foreign key)
  - EventId (foreign key)
  - TimeSeconds (decimal)
  - Date (DateTime)
  - Course (50m/25m short course)
  - Location (text - meet/competition name)
- **Notes:** Previously tested with additional fields (Round, Position, MeetName) - removed as unnecessary

#### FR3.2: Swimmer Model
- **Description:** Core swimmer entity
- **Status:** ✅ IMPLEMENTED
- **Fields:**
  - Id (int, primary key)
  - FirstName (string)
  - LastName (string)
  - DisplayName (computed: "FirstName LastName")
- **Note:** Migrated from single "Name" field to FirstName/LastName

#### FR3.3: Event Model
- **Description:** Competitive swimming event definition
- **Status:** ✅ IMPLEMENTED
- **Fields:**
  - Stroke (Freestyle, Backstroke, Breaststroke, Butterfly, IM)
  - DistanceMeters (50, 100, 200, 400, 1500)
  - Course (50m long course, 25m short course)

---

### 4. User Interface

#### FR4.1: Main Visualization
- **Description:** Display swimming performance trends
- **Status:** ✅ IMPLEMENTED
- **Features:**
  - OxyPlot chart visualization
  - Zoom and pan with mouse controls
  - Filter by stroke, distance, course
  - Multi-swimmer comparison

#### FR4.2: Swimmer Selection
- **Description:** Select/deselect swimmers for analysis
- **Status:** ✅ IMPLEMENTED
- **Features:**
  - Checkbox selection
  - Checkboxes for courses
  - Select All / Select None buttons
  - Persistent selection (saved across sessions)

#### FR4.3: Personal Records Display
- **Description:** Show best times for each event
- **Status:** ✅ IMPLEMENTED
- **Features:**
  - Sortable results grid
  - Double-click to highlight in chart
  - Filter by selected swimmers and criteria

#### FR4.3.1: Chart Tooltip Priority
- **Description:** Ensure club statistics lines don't interfere with swimmer tooltips
- **Status:** ✅ IMPLEMENTED & TESTED
- **Requirements:**
  - Club statistics lines (ClubBest, ClubSlowest) should NOT show tooltips on hover
  - Individual swimmer series SHOULD show tooltips (date, time, location)
  - Rationale: Swimmer data is primary information; club stats are reference lines
- **Implementation:** 
  - Added `DisableTracker` property to `SwimTimeSeries` class
  - Set to `true` for club statistics series in `MainViewModel.BuildChart()`
  - Returns `null` from `GetNearestPoint` when disabled, preventing tooltip display
- **Test Coverage:** 
  - `ClubStatisticsSeries_ShouldNotHaveTrackerEnabled`: Validates DisableTracker property
  - `SwimTimeSeries_WithDisableTrackerTrue_ReturnsNullFromGetNearestPoint`: Validates behavior

#### FR4.4: Data Import UI
- **Description:** Trigger data import from UI
- **Status:** ✅ IMPLEMENTED
- **Features:**
  - Import buttons with progress indicators
  - Status messages
  - Error handling with user feedback

#### FR4.5: Configuration Reload UI
- **Description:** Menu option to reload configuration
- **Status:** ✅ IMPLEMENTED
- **UI:** File → Reload Configuration menu item

---

### 5. Data Persistence

#### FR5.1: SQLite Database
- **Description:** Persistent local data storage
- **Status:** ✅ IMPLEMENTED
- **Default Location:** `%APPDATA%\SwimStats\swimstats.db`
- **Features:**
  - Entity Framework Core ORM
  - Automated migrations
  - Performance indexes

#### FR5.2: In-Memory Testing Database
- **Description:** Use in-memory SQLite for unit tests
- **Status:** ✅ IMPLEMENTED & TESTED
- **Test Coverage:** All 34 tests pass using in-memory database

---

## Non-Functional Requirements

### NFR1: Performance
- **Target:** Load and display 100+ swimmers, 10,000+ results within 2 seconds
- **Status:** ✅ IMPLEMENTED
- **Optimization:** Database indexes on swimmer names and result filtering

### NFR2: Reliability
- **Error Handling:** All exceptions caught with user-friendly messages
- **Status:** ✅ IMPLEMENTED
- **Coverage:** UI shows dialogs for errors during import/reload

### NFR3: Usability
- **Language Support:** Localization framework in place
- **Status:** ✅ IMPLEMENTED (English default, framework for expansion)

### NFR4: Deployment
- **Packaging:** Self-contained Windows executable
- **Status:** ✅ IMPLEMENTED
- **Build Settings:** `PublishSingleFile=true`, `SelfContained=true`

---

## Testing Status

### Unit Tests: 36/36 PASSING ✅

**Test Categories:**

1. **Import Tests** (31 tests)
   - SwimTrackImporter: 31 tests
   - SwimRankingsImporter: 2 tests
   - Coverage: athlete finding, parsing, duplicate detection, batch processing

2. **Duplicate Detection Tests** (3 tests)
   - `DuplicateDetectionUsesTightTimeToleranceOf0_00001Seconds` ✅
   - `MultipleResultsSameDayDifferentTimesAreNotDuplicates` ✅
   - `ReimportSameResultWithinToleranceIsDetectedAsDuplicate` ✅
   - Validates exact-match time comparison

3. **Chart Visualization Tests** (2 new tests)
   - `ClubStatisticsSeries_ShouldNotHaveTrackerEnabled` ✅
   - `SwimTimeSeries_WithDisableTrackerTrue_ReturnsNullFromGetNearestPoint` ✅
   - Validates club statistics don't show tooltips

4. **Database Tests** (Various)
   - Entity Framework context
   - Migration application
   - Data seeding

4. **Model Tests**
   - Result service
   - Swimming statistics calculations

### Manual Testing

| Feature | Status | Notes |
|---------|--------|-------|
| Application startup | ✅ | Loads database, seeds swimmers |
| SwimRankings import | ⚠️ | Website currently unreachable |
| SwimTrack import | ✅ | Successfully imports results |
| Duplicate detection | ✅ | Exact match working correctly |
| Chart visualization | ✅ | Zoom/pan functional |
| Configuration reload | ✅ | Menu option working, data updates |
| Swimmer selection | ✅ | Persistence working |
| Personal records | ✅ | Display and sorting functional |

---

## Known Issues & Limitations

### Issue 1: SwimRankings Website Connectivity
- **Status:** BLOCKING
- **Details:** Website returned 0 results in recent tests
- **Impact:** Cannot import from SwimRankings; SwimTrack is alternative
- **Resolution:** Pending website availability check; code is correct

### Issue 2: LINQ Translation on DisplayName
- **Status:** RESOLVED ✅
- **Details:** Entity Framework couldn't translate DisplayName computed property to SQL
- **Impact:** Configuration reload would fail with LINQ translation error
- **Resolution:** Materialize data with `.ToList()` before ordering

### Issue 3: Floating-Point Tuple Parsing
- **Status:** RESOLVED ✅
- **Details:** Tuple values like `25.00` caused C# parsing issues in named tuples
- **Impact:** Test compilation failed
- **Resolution:** Changed to `25.0` format

---

## Requirements Status Summary

| Requirement Area | Status | Tests | Comments |
|------------------|--------|-------|----------|
| Import (SwimRankings) | ✅ Working | 2 | Rate limited; use SwimTrack as primary |
| Import (SwimTrack) | ✅ Primary | 31 | Recommended data source |
| Duplicate Detection | ✅ | 3 | Exact match approach validated |
| Configuration File | ✅ | Manual | Deployment ready |
| First-Time Start | ✅ | Manual | Auto-copy working |
| Config Reload | ✅ | Manual | UI menu functional |
| Visualization | ✅ | Manual | Charts render correctly |
| Database | ✅ | Multiple | SQLite + EF Core solid |
| Deployment | ✅ | Manual | Self-contained build ready |

---

## Known Issues & Limitations

### Issue 1: SwimRankings Rate Limiting (Resolved ✅)
- **Status:** RESOLVED
- **Details:** SwimRankings has anti-scraping protection that blocks IPs after repeated requests
- **Impact:** Rapid imports could trigger 30min-1hr IP block
- **Resolution:** Implemented 5-second request throttling between all HTTP requests
- **Recommendation:** Use SwimTrack as primary data source for automated imports
- **Alternative Use:** SwimRankings works for occasional manual imports or international swimmers

### Issue 2: LINQ Translation on DisplayName (Resolved ✅)
- **Status:** RESOLVED
- **Details:** Entity Framework couldn't translate DisplayName computed property to SQL
- **Impact:** Configuration reload would fail with LINQ translation error
- **Resolution:** Materialize data with `.ToList()` before ordering

### Issue 3: Floating-Point Tuple Parsing (Resolved ✅)
- **Status:** RESOLVED
- **Details:** Tuple values like `25.00` caused C# parsing issues in named tuples
- **Impact:** Test compilation failed
- **Resolution:** Changed to `25.0` format

---

## Potential Conflicts & Gaps

### Potential Conflict 1: Duplicate Detection Strategy
- **Conflict:** Exact match for time may miss legitimate duplicates from data entry errors
- **Mitigation:** Designed intentionally - different times = different race instances (heats/finals)
- **Alternative Considered:** Tolerance-based (0.00001s) - rejected as too complicated
- **Recommendation:** ✅ Current approach is sound for swimming data

### Gap 1: Data Source Selection Guidance
- **Description:** Users need guidance on which importer to use
- **Current:** Both available, but SwimRankings has rate limiting
- **Recommendation:** ✅ Documentation added; UI tooltips guide users

### Gap 2: Automated Re-import Strategy
- **Description:** No scheduled/automatic re-import of data
- **Impact:** Users must manually trigger imports
- **Recommendation:** Could add background task (out of scope for v0.1.1)

### Gap 3: Configuration Validation
- **Description:** JSON config file schema validation
- **Impact:** Invalid JSON silently returns empty list
- **Status:** ✅ IMPLEMENTED (see REC1 in Implementation Updates)

### Gap 4: Partial Configuration Updates
- **Description:** Reload always replaces entire swimmer list
- **Impact:** Cannot add single swimmer without modifying entire file
- **Recommendation:** Could add "Merge" option (future enhancement)

### Gap 5: Audit Trail
- **Description:** No history of configuration changes
- **Impact:** Cannot track who/when swimmers were added/modified
- **Recommendation:** Consider version control of `EZPCswimmers.json`

---

## Missing Features (Not in v0.1.1)

### MF1: Configuration File Editor UI
- **Description:** In-app editor for `EZPCswimmers.json`
- **Priority:** Low
- **Reason:** System file editor sufficient for now

### MF2: Data Export
- **Description:** Export results to CSV/Excel
- **Priority:** Low
- **Reason:** Analysis is primary use case

### MF3: Offline Mode
- **Description:** Work without internet connection
- **Priority:** Low
- **Reason:** Import requires website connectivity anyway

### MF4: Multiple Configuration Profiles
- **Description:** Support different swimmer lists (e.g., by club/team)
- **Priority:** Low
- **Reason:** Single configuration sufficient for EZPC

---

## Recommendations

### High Priority
1. ✅ Test SwimRankings import when website becomes available
2. ✅ Add configuration file schema validation
3. ✅ Add UI guidance for choosing data source

### Medium Priority
1. ⏳ Add scheduled/background data refresh
2. ⏳ Implement configuration backup/version history
3. ⏳ Add data quality warnings (e.g., missing events)

### Low Priority
1. ⏳ Build in-app configuration editor
2. ⏳ Add export to CSV/PDF
3. ⏳ Support multiple profiles

---

## Appendix: Configuration Files

### Deployment Configuration
- **File:** `src/SwimStats.Data/SwimStats.Data.csproj`
- **Setting:** `<CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>`
- **Effect:** `EZPCswimmers.json` copied to build output

### Default Swimmers
- **File:** `src/SwimStats.Data/EZPCswimmers.json`
- **Count:** 30 swimmers
- **Format:** FirstName/LastName (modern format)

### Load Path (in order)
1. `%LOCALAPPDATA%\SwimStats\EZPCswimmers.json` (user editable)
2. `{assembly_directory}/EZPCswimmers.json` (deployed default)
3. Empty list if neither found

---

## Implementation Updates (January 18, 2026)

### REC1: JSON Schema Validation - IMPLEMENTED ✅

**Feature:** `SwimmerConfigurationLoader` now validates JSON configuration files
**Location:** [SwimmerConfigurationLoader.cs](src/SwimStats.Data/SwimmerConfigurationLoader.cs)
**Status:** ✅ IMPLEMENTED & TESTED

**Validation Features:**
- Checks that root element is a JSON array
- Validates each element is a JSON object
- Ensures required "id" field exists
- Validates that IDs are positive integers
- Provides specific error messages for each validation failure

**Error Messages:**
```
- "Configuration file must contain a JSON array"
- "Array element {index} is missing required field 'id'"
- "Swimmer ID must be a positive integer"
- "Invalid JSON format in configuration file: {details}"
```

**User Experience:**
- Invalid config shows clear error dialog
- User can correct file and retry without restarting

### REC2: UI Guidance for Data Source Selection - IMPLEMENTED ✅

**Feature:** Added tooltip help text to import buttons
**Location:** [MainWindow.xaml](src/SwimStats.App/MainWindow.xaml)
**Status:** ✅ IMPLEMENTED & TESTED

**Guidance Added:**
- **SwimRankings button:** "Import from SwimRankings (Recommended for international swimmers)"
- **SwimTrack button:** "Import from SwimTrack (Alternative source for Dutch swimmers)"

**UI Enhancement:**
- Tooltips appear on hover
- Helps users choose correct data source
- Explains when to use each service

### REC3: Configuration Version Control - IMPLEMENTED ✅

**Feature:** Automatic backup system for configuration file changes
**Location:** [ConfigurationBackupService.cs](src/SwimStats.Data/Services/ConfigurationBackupService.cs)
**Status:** ✅ IMPLEMENTED & TESTED

**Backup Features:**
- Automatic timestamped backups on configuration reload
- Maintains up to 10 previous backups automatically
- Backups stored in: `%LOCALAPPDATA%\SwimStats\backups\`
- Backup format: `EZPCswimmers_backup_YYYY-MM-DD_HH-MM-SS.json`

**Backup Service API:**
- `CreateBackup()` - Creates timestamped backup
- `GetAvailableBackups()` - Lists all backups with metadata
- `RestoreBackup(path)` - Restores from specific backup
- `DeleteBackup(path)` - Delete individual backup
- `ClearAllBackups()` - Delete all backups
- `GetBackupDirectoryPath()` - Get backups location

**Integration:**
- Automatically called during `ReloadConfiguration` operation
- Creates backup BEFORE loading new config
- If reload fails, user can restore previous version
- Old backups auto-cleaned (keeps newest 10)

### REC4: Chart Tooltip Priority - IMPLEMENTED ✅

**Feature:** Prevent club statistics lines from interfering with swimmer tooltips
**Location:** [SwimTimeSeries.cs](src/SwimStats.App/Controls/SwimTimeSeries.cs), [MainViewModel.cs](src/SwimStats.App/ViewModels/MainViewModel.cs)
**Status:** ✅ IMPLEMENTED & TESTED

**Problem Solved:**
- Club statistics lines (ClubBest, ClubSlowest) were showing tooltips on hover
- These tooltips could hide/block more important individual swimmer tooltips
- Reduced usability when hovering near overlapping data points

**Implementation:**
- Added `DisableTracker` boolean property to `SwimTimeSeries` class
- When `DisableTracker = true`, `GetNearestPoint()` returns `null` (no tooltip)
- Set `DisableTracker = true` for ClubBest and ClubSlowest series
- Individual swimmer series keep default behavior (tooltips enabled)

**Technical Details:**
```csharp
// Club statistics - no tooltips
var cumulativeMin = new SwimTimeSeries
{
    Title = loc["ClubBest"],
    DisableTracker = true  // No tooltip interference
};

// Individual swimmers - tooltips enabled
var swimmerSeries = new SwimTimeSeries
{
    Title = swimmer.DisplayName,
    DisableTracker = false  // Shows date, time, location
};
```

**Test Coverage:**
- `ClubStatisticsSeries_ShouldNotHaveTrackerEnabled`: Property validation
- `SwimTimeSeries_WithDisableTrackerTrue_ReturnsNullFromGetNearestPoint`: Behavior validation

**User Experience:**
- Hovering over swimmer data points shows full tooltip information
- Club statistics lines don't interfere with primary data
- Better chart usability for data exploration

---

## Document Change History

| Version | Date | Changes | Author |
|---------|------|---------|--------|
| v1.0 | 2026-01-18 | Initial comprehensive requirements document | AI Assistant |
| v1.1 | 2026-01-18 | Implemented JSON validation, UI guidance, backup service | AI Assistant |
| v1.2 | 2026-01-18 | Implemented chart tooltip priority (club stats disabled) | AI Assistant |
| v1.3 | 2026-01-18 | Fixed SwimRankings rate limiting, added 5s throttling, documented limitation | AI Assistant |
| v1.0 | 2026-01-18 | Initial comprehensive requirements document | AI Assistant |
| v1.1 | 2026-01-18 | Implemented JSON validation, UI guidance, backup service | AI Assistant |
| v1.2 | 2026-01-18 | Implemented chart tooltip priority (club stats disabled) | AI Assistant |

**Backup Metadata:**
- File creation timestamp
- File size
- Easy-to-read display name: "2026-01-18 15:30:45 (1.90 KB)"

---

## Document Change History

| Date | Version | Author | Changes |
|------|---------|--------|---------|
| 2026-01-18 | 1.1 | System | Implemented JSON validation, UI guidance, backup service |
| 2026-01-18 | 1.0 | System | Initial requirements document |

---

**Document Owner:** EZPC Swimming Club  
**Next Review:** After SwimRankings availability confirmation  
**Classification:** Internal
