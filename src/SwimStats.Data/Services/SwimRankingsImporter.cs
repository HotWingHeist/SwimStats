using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Microsoft.EntityFrameworkCore;
using SwimStats.Core.Interfaces;
using SwimStats.Core.Models;

namespace SwimStats.Data.Services;

public class SwimRankingsImporter : ISwimTrackImporter
{
    private readonly SwimStatsDbContext _db;
    private readonly HttpClient _httpClient;
    private readonly Action<int, int, string>? _progressCallback;

    public SwimRankingsImporter(SwimStatsDbContext db, Action<int, int, string>? progressCallback = null)
    {
        _db = db;
        
        // Create HttpClient with automatic decompression
        var handler = new HttpClientHandler();
        handler.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
        
        _httpClient = new HttpClient(handler);
        // Add realistic browser headers to avoid 503 errors
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        _httpClient.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");
        _httpClient.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.5");
        _httpClient.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate");
        _httpClient.DefaultRequestHeaders.Add("DNT", "1");
        _httpClient.DefaultRequestHeaders.Add("Connection", "keep-alive");
        _httpClient.DefaultRequestHeaders.Add("Upgrade-Insecure-Requests", "1");
        _httpClient.Timeout = TimeSpan.FromSeconds(60);
        _progressCallback = progressCallback;
    }

    /// <summary>
    /// Checks if the SwimRankings website is reachable
    /// </summary>
    public async Task<bool> IsWebsiteReachableAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("https://www.swimrankings.net", HttpCompletionOption.ResponseHeadersRead);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            return false;
        }
        catch (TaskCanceledException)
        {
            return false;
        }
        catch
        {
            return false;
        }
    }

    public async Task<int> ImportSwimmersAsync(string baseUrl)
    {
        try
        {
            var html = await FetchWithRetry(baseUrl);
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            int count = 0;

            // Look for athlete links in search results on SwimRankings
            // These typically link to athlete detail/ranking pages
            var athleteLinks = doc.DocumentNode.SelectNodes("//a[contains(@href, 'athleteID')]");

            if (athleteLinks == null || athleteLinks.Count == 0)
            {
                // Try alternative XPath for athlete links
                athleteLinks = doc.DocumentNode.SelectNodes("//table//tr//td//a[contains(@href, 'page=')]");
            }

            if (athleteLinks != null && athleteLinks.Count > 0)
            {
                var uniqueSwimmers = new Dictionary<string, string>(); // name -> URL

                foreach (var link in athleteLinks)
                {
                    var name = link.InnerText.Trim();
                    var href = link.GetAttributeValue("href", "");

                    if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(href))
                        continue;

                    // Skip if name is too short or looks like navigation
                    if (name.Length < 2 || name.ToLower().Contains("select") || name.ToLower().Contains("home"))
                        continue;

                    if (!uniqueSwimmers.ContainsKey(name))
                    {
                        uniqueSwimmers[name] = href;
                    }
                }

                foreach (var swimmerEntry in uniqueSwimmers)
                {
                    var existingSwimmer = await _db.Swimmers.FirstOrDefaultAsync(s => s.DisplayName == swimmerEntry.Key);
                    if (existingSwimmer == null)
                    {
                        var names = swimmerEntry.Key.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        _db.Swimmers.Add(new Swimmer 
                        { 
                            FirstName = names.Length > 0 ? names[0] : "",
                            LastName = names.Length > 1 ? string.Join(" ", names.Skip(1)) : ""
                        });
                        count++;
                    }
                }

                if (count > 0)
                {
                    await _db.SaveChangesAsync();
                }
            }

            return count;
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to import swimmers from SwimRankings: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Imports data for a single swimmer using separate first and last names.
    /// </summary>
    public async Task<int> ImportSwimmerByNameAsync(string firstName, string lastName)
    {
        return await ImportSingleSwimmerAsync($"{firstName} {lastName}");
    }

    /// <summary>
    /// Imports data for a single swimmer by name search.
    /// </summary>
    public async Task<int> ImportSingleSwimmerAsync(string swimmerName)
    {
        try
        {
            var names = swimmerName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (names.Length < 2)
            {
                throw new Exception("Please enter a swimmer name as 'First Last'");
            }

            var firstName = names[0];
            var lastName = string.Join(" ", names.Skip(1));

            _progressCallback?.Invoke(0, 100, $"Searching for {swimmerName}...");

            // Try the internal AJAX endpoint that SwimRankings uses for search
            var internalSearchUrl = $"https://www.swimrankings.net/index.php?internalRequest=athleteFind&athlete_firstname={Uri.EscapeDataString(firstName)}&athlete_lastname={Uri.EscapeDataString(lastName)}&athlete_clubId=-1&athlete_gender=-1";

            string searchHtml = await FetchWithRetry(internalSearchUrl);
            if (string.IsNullOrWhiteSpace(searchHtml))
            {
                throw new Exception($"No results found for {swimmerName}");
            }

            var searchDoc = new HtmlDocument();
            searchDoc.LoadHtml(searchHtml);

            // Find athlete links in the format: ?page=athleteDetail&athleteId=XXXXX
            var athleteDetailLinks = searchDoc.DocumentNode.SelectNodes("//a[contains(@href, 'athleteDetail')]");

            if (athleteDetailLinks == null || athleteDetailLinks.Count == 0)
            {
                throw new Exception($"No results found for {swimmerName}");
            }

            // Use the first result
            var athleteDetailLink = athleteDetailLinks[0];
            var detailUrl = athleteDetailLink.GetAttributeValue("href", "");
            detailUrl = System.Net.WebUtility.HtmlDecode(detailUrl);

            if (string.IsNullOrEmpty(detailUrl))
            {
                throw new Exception("Could not parse athlete link");
            }

            if (!detailUrl.StartsWith("http"))
            {
                detailUrl = "https://www.swimrankings.net/index.php" + (detailUrl.StartsWith("?") ? detailUrl : "?" + detailUrl);
            }

            _progressCallback?.Invoke(25, 100, $"Found {swimmerName}, fetching details...");

            // Fetch the athlete detail page
            var detailHtml = await FetchWithRetry(detailUrl);
            var detailDoc = new HtmlDocument();
            detailDoc.LoadHtml(detailHtml);

            // Extract athlete ID and add/get swimmer
            var athleteIdMatch = Regex.Match(detailUrl, @"athleteId=(\d+)");
            if (!athleteIdMatch.Success || !int.TryParse(athleteIdMatch.Groups[1].Value, out var athleteId))
            {
                throw new Exception("Could not extract athlete ID");
            }

            // Add swimmer to database if not exists
            var existingSwimmer = await _db.Swimmers.FirstOrDefaultAsync(s => s.DisplayName == swimmerName);
            if (existingSwimmer == null)
            {
                existingSwimmer = new Swimmer 
                { 
                    FirstName = firstName,
                    LastName = lastName
                };
                _db.Swimmers.Add(existingSwimmer);
                await _db.SaveChangesAsync();
            }

            _progressCallback?.Invoke(50, 100, $"Parsing results for {swimmerName}...");

            // Parse personal bests for all ranking styles
            var count = await ParseAllPersonalRankings(detailDoc, athleteId, existingSwimmer.Id);

            _progressCallback?.Invoke(100, 100, "Import complete!");

            return count;
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to import swimmer: {ex.Message}", ex);
        }
    }

    public async Task<int> ImportResultsAsync(string baseUrl)
    {
        try
        {
            var html = await FetchWithRetry(baseUrl);
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            int totalCount = 0;

            // Get all swimmers from database
            var swimmers = await _db.Swimmers.ToListAsync();
            int processedCount = 0;

            foreach (var swimmer in swimmers)
            {
                processedCount++;
                _progressCallback?.Invoke(processedCount, swimmers.Count, $"Processing {swimmer.DisplayName} ({processedCount}/{swimmers.Count})...");

                try
                {
                    // Search for this swimmer on SwimRankings
                    var firstName = swimmer.FirstName;
                    var lastName = swimmer.LastName;

                    // Try the internal AJAX endpoint that SwimRankings uses for search
                    // This returns athlete suggestion links
                    var internalSearchUrl = $"https://www.swimrankings.net/index.php?internalRequest=athleteFind&athlete_firstname={Uri.EscapeDataString(firstName)}&athlete_lastname={Uri.EscapeDataString(lastName)}&athlete_clubId=-1&athlete_gender=-1";

                    string searchHtml = "";
                    try
                    {
                        searchHtml = await FetchWithRetry(internalSearchUrl);
                    }
                    catch
                    {
                        // If internal search fails, skip this swimmer
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(searchHtml))
                    {
                        continue;
                    }

                    var searchDoc = new HtmlDocument();
                    searchDoc.LoadHtml(searchHtml);

                    // The AJAX response returns a table with athlete links
                    // Find athlete links in the format: ?page=athleteDetail&athleteId=XXXXX
                    var athleteDetailLinks = searchDoc.DocumentNode.SelectNodes("//a[contains(@href, 'athleteDetail')]");
                    
                    // If no links found, try broader search
                    if (athleteDetailLinks == null || athleteDetailLinks.Count == 0)
                    {
                        athleteDetailLinks = searchDoc.DocumentNode.SelectNodes("//a[@href]");
                    }
                    
                    if (athleteDetailLinks != null && athleteDetailLinks.Count > 0)
                    {
                        // Find the first link that contains athleteDetail
                        HtmlNode athleteDetailLink = null;
                        foreach (var link in athleteDetailLinks)
                        {
                            var href = link.GetAttributeValue("href", "");
                            if (href.Contains("athleteDetail"))
                            {
                                athleteDetailLink = link;
                                break;
                            }
                        }
                        
                        if (athleteDetailLink != null)
                        {
                            var detailUrl = athleteDetailLink.GetAttributeValue("href", "");
                            // Decode HTML entities (e.g., &amp; -> &)
                            detailUrl = System.Net.WebUtility.HtmlDecode(detailUrl);
                            
                            if (!string.IsNullOrEmpty(detailUrl))
                            {
                                // Make detailUrl absolute if needed
                                if (!detailUrl.StartsWith("http"))
                                {
                                    detailUrl = "https://www.swimrankings.net/index.php" + (detailUrl.StartsWith("?") ? detailUrl : "?" + detailUrl);
                                }

                                // Fetch the athlete detail page
                                var detailHtml = await FetchWithRetry(detailUrl);
                                var detailDoc = new HtmlDocument();
                                detailDoc.LoadHtml(detailHtml);

                                // Extract athlete ID from URL if possible
                                var athleteIdMatch = Regex.Match(detailUrl, @"athleteId=(\d+)");
                                if (athleteIdMatch.Success && int.TryParse(athleteIdMatch.Groups[1].Value, out var athleteId))
                                {
                                    // Parse personal bests for all ranking styles
                                    var count = await ParseAllPersonalRankings(detailDoc, athleteId, swimmer.Id);
                                    totalCount += count;
                                }
                                else
                                {
                                }
                            }
                        }
                    }

                    // Polite delay between requests
                    await Task.Delay(500);
                }
                catch
                {
                    // Continue with next swimmer if this one fails
                    continue;
                }
            }

            return totalCount;
        }
        catch (Exception ex)
        {
            throw new Exception($"Import results failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Extracts all personal ranking style options from the dropdown and parses each one.
    /// Each styleId represents a different event ranking (e.g., 50m Freestyle, 100m Backstroke, etc.)
    /// </summary>
    private async Task<int> ParseAllPersonalRankings(HtmlDocument doc, int athleteId, int swimmerId)
    {
        int totalCount = 0;

        try
        {
            // Extract all styleId options from the ranking dropdown (with event names)
            var styleIdOptions = ExtractRankingStyleIds(doc);

            if (styleIdOptions.Count == 0)
            {
                // If no styleIds found in dropdown, try parsing the current page
                return await ParsePersonalRankingsDropdown(doc, swimmerId);
            }

            // For each ranking style, fetch and parse the data
            foreach (var (styleId, eventName) in styleIdOptions)
            {
                try
                {
                    // Build URL with styleId parameter
                    var rankingUrl = $"https://www.swimrankings.net/index.php?page=athleteDetail&athleteId={athleteId}&styleId={styleId}";
                    
                    var rankingHtml = await FetchWithRetry(rankingUrl);
                    var rankingDoc = new HtmlDocument();
                    rankingDoc.LoadHtml(rankingHtml);

                    // Parse the ranking data from this specific event page
                    var count = await ParsePersonalRankingsDropdown(rankingDoc, swimmerId, eventName);
                    totalCount += count;

                    // Polite delay between requests
                    await Task.Delay(300);
                }
                catch
                {
                    // Continue with next style if this one fails
                    continue;
                }
            }
        }
        catch
        {
            // Return whatever was successfully parsed
        }

        return totalCount;
    }

    /// <summary>
    /// Extracts all styleId values from the "Personal rankings" dropdown.
    /// </summary>
    private List<(int styleId, string eventName)> ExtractRankingStyleIds(HtmlDocument doc)
    {
        var styleIds = new List<(int, string)>();

        try
        {
            // Try multiple XPath approaches to find the ranking dropdown
            HtmlNode selectNode = null;
            
            // Approach 1: Look for select by name
            selectNode = doc.DocumentNode.SelectSingleNode("//select[@name='rankingStyleId']");
            
            // Approach 2: If not found, search all selects and look for the one containing ranking options
            if (selectNode == null)
            {
                var allSelects = doc.DocumentNode.SelectNodes("//select");
                if (allSelects != null)
                {
                    foreach (var select in allSelects)
                    {
                        if (select.InnerHtml.Contains("50m Freestyle") || select.InnerHtml.Contains("Backstroke"))
                        {
                            selectNode = select;
                            break;
                        }
                    }
                }
            }
            
            if (selectNode != null)
            {
                // Extract all option values and labels
                var options = selectNode.SelectNodes(".//option");
                
                if (options != null)
                {
                    foreach (var option in options)
                    {
                        var value = option.GetAttributeValue("value", "");
                        var label = option.InnerText.Trim();
                        if (!string.IsNullOrEmpty(value) && value != "0" && int.TryParse(value, out var styleId))
                        {
                            styleIds.Add((styleId, label));
                        }
                    }
                }
            }
        }
        catch
        {
            // Return empty list if extraction fails
        }

        return styleIds;
    }

    /// <summary>
    /// Parses personal best records from the athlete detail page.
    /// SwimRankings displays personal bests in a table with class "athleteBest" (when no styleId),
    /// or personal rankings (history) in a table with class "athleteRanking" (when styleId is set).
    /// </summary>
    private async Task<int> ParsePersonalRankingsDropdown(HtmlDocument doc, int swimmerId, string overrideEventName = "")
    {
        int count = 0;

        try
        {
            // First try to find athleteBest table (personal bests, all events)
            var pbTables = doc.DocumentNode.SelectNodes("//table[@class='athleteBest']");
            bool isRankingTable = false;

            // If not found, try athleteRanking tables (personal ranking history for specific event)
            if (pbTables == null || pbTables.Count == 0)
            {
                pbTables = doc.DocumentNode.SelectNodes("//table[@class='athleteRanking']");
                isRankingTable = true;
            }

            if (pbTables != null && pbTables.Count > 0)
            {
                // Store rows with their associated course information
                var allDataRowsWithCourse = new List<(HtmlNode row, Course course)>();

                // Combine rows from all tables, extracting course info
                foreach (var pbTable in pbTables)
                {
                    // Extract course from table header
                    var headerRow = pbTable.SelectSingleNode(".//tr[@class='athleteBestHead' or @class='athleteRankingHead']");
                    var headerText = headerRow?.InnerText ?? "";
                    
                    Course tableCurrentCourse = Course.LongCourse;  // Default to 50m
                    if (headerText.Contains("25m") || headerText.Contains("Short Course"))
                    {
                        tableCurrentCourse = Course.ShortCourse;
                    }
                    else if (headerText.Contains("50m") || headerText.Contains("Long Course"))
                    {
                        tableCurrentCourse = Course.LongCourse;
                    }

                    var allRows = pbTable.SelectNodes(".//tr");
                    
                    // Filter to data rows (excluding headers which contain "Head" in class)
                    var rows = allRows?.Where(r => 
                    {
                        var cls = r.GetAttributeValue("class", "");
                        return (cls.Contains("athleteBest") || cls.Contains("athleteRanking")) 
                            && !cls.Contains("Head");
                    })
                    .ToList();

                    if (rows != null && rows.Count > 0)
                    {
                        foreach (var row in rows)
                        {
                            allDataRowsWithCourse.Add((row, tableCurrentCourse));
                        }
                    }
                }

                if (allDataRowsWithCourse.Count > 0)
                {
                    var resultsToAdd = new List<Result>();

                    // For ranking table, use the override event name
                    string eventText = overrideEventName;

                    // Iterate through ALL rows with course info
                    foreach (var (row, rowCourse) in allDataRowsWithCourse)
                    {
                        try
                        {
                            var cells = row.SelectNodes(".//td");
                            if (cells == null || cells.Count < 3)
                            {
                                continue;
                            }

                            string currentEventText = eventText;

                            // If this is athleteBest table, extract event from first column
                            if (!isRankingTable && cells.Count >= 5)
                            {
                                var eventCell = cells[0];
                                var eventLink = eventCell.SelectSingleNode(".//a");
                                if (eventLink != null)
                                {
                                    currentEventText = System.Net.WebUtility.HtmlDecode(eventLink.InnerText.Trim());
                                }
                            }

                            // Extract time - typically in first column
                            string timeText = "";
                            HtmlNode timeCell = cells[0];

                            if (timeCell != null)
                            {
                                var timeLink = timeCell.SelectSingleNode(".//a");
                                if (timeLink != null)
                                {
                                    timeText = System.Net.WebUtility.HtmlDecode(timeLink.InnerText.Trim());
                                }
                                else
                                {
                                    timeText = System.Net.WebUtility.HtmlDecode(timeCell.InnerText.Trim());
                                }
                            }

                            if (string.IsNullOrWhiteSpace(currentEventText) || string.IsNullOrWhiteSpace(timeText))
                            {
                                continue;
                            }

                            // Extract stroke
                            Stroke? stroke = ExtractStroke(currentEventText);
                            if (stroke == null)
                            {
                                continue;
                            }

                            // Extract distance
                            var distanceMatch = Regex.Match(currentEventText, @"(\d+)\s*m(?:eter)?");
                            if (!distanceMatch.Success)
                            {
                                continue;
                            }

                            var distance = int.Parse(distanceMatch.Groups[1].Value);

                            // Parse time
                            var timeSeconds = ParseTime(timeText);
                            if (timeSeconds == null || timeSeconds <= 0)
                            {
                                continue;
                            }

                            // Extract date - index varies depending on table type
                            DateTime? date = null;
                            HtmlNode dateCell = null;
                            string? location = null;

                            if (isRankingTable)
                            {
                                // In athleteRanking: time, code, date, city = cells 0,1,2,3
                                if (cells.Count > 2)
                                    dateCell = cells[2];
                                if (cells.Count > 3)
                                    location = System.Net.WebUtility.HtmlDecode(cells[3].InnerText.Trim());
                            }
                            else
                            {
                                // In athleteBest: event, course, time, code, date
                                if (cells.Count > 4)
                                    dateCell = cells[4];
                                // No location in athleteBest table
                            }

                            if (dateCell != null)
                            {
                                var dateText = System.Net.WebUtility.HtmlDecode(dateCell.InnerText.Trim());
                                dateText = dateText.Replace("&nbsp;", " ");
                                var dateMatch = Regex.Match(dateText, @"(\d{1,2})\s+(\w{3})\s+(\d{4})");

                                if (dateMatch.Success)
                                {
                                    var day = int.Parse(dateMatch.Groups[1].Value);
                                    var monthStr = dateMatch.Groups[2].Value;
                                    var year = int.Parse(dateMatch.Groups[3].Value);

                                    var monthMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                                    {
                                        { "Jan", 1 }, { "Feb", 2 }, { "Mar", 3 }, { "Apr", 4 },
                                        { "May", 5 }, { "Jun", 6 }, { "Jul", 7 }, { "Aug", 8 },
                                        { "Sep", 9 }, { "Oct", 10 }, { "Nov", 11 }, { "Dec", 12 }
                                    };

                                    if (monthMap.TryGetValue(monthStr, out var month))
                                    {
                                        try
                                        {
                                            date = new DateTime(year, month, day);
                                        }
                                        catch
                                        {
                                            // Invalid date, skip
                                            continue;
                                        }
                                    }
                                }
                            }

                            if (date == null)
                            {
                                date = DateTime.Now;
                            }

                            // Find or create the event (with course information)
                            var evt = await _db.Events.FirstOrDefaultAsync(e =>
                                e.Stroke == stroke.Value && e.DistanceMeters == distance && e.Course == rowCourse);

                            if (evt == null)
                            {
                                evt = new Event { Stroke = stroke.Value, DistanceMeters = distance, Course = rowCourse };
                                _db.Events.Add(evt);
                                await _db.SaveChangesAsync();
                            }

                            resultsToAdd.Add(new Result
                            {
                                SwimmerId = swimmerId,
                                EventId = evt.Id,
                                TimeSeconds = timeSeconds.Value,
                                Date = date.Value,
                                Course = rowCourse,
                                Location = location
                            });
                        }
                        catch
                        {
                            // Skip this row if parsing fails
                            continue;
                        }
                    }

                    // Batch save results
                    if (resultsToAdd.Any())
                    {
                        var existingResults = await _db.Results
                            .Where(r => r.SwimmerId == swimmerId)
                            .ToListAsync();

                        foreach (var newResult in resultsToAdd)
                        {
                            var exists = existingResults.Any(r =>
                                r.EventId == newResult.EventId &&
                                r.Date == newResult.Date &&
                                Math.Abs(r.TimeSeconds - newResult.TimeSeconds) < 0.01);

                            if (!exists)
                            {
                                _db.Results.Add(newResult);
                                count++;
                            }
                        }

                        if (count > 0)
                        {
                            await _db.SaveChangesAsync();
                        }
                    }
                }
            }
        }
        catch
        {
            // Return whatever was successfully parsed
        }

        return count;
    }

    /// <summary>
    /// Parses a personal ranking option text to extract stroke, distance, time, and date.
    /// Expected formats:
    /// - "50m Freestyle - 24.50 (2023-01-15)"
    /// - "100m Breaststroke - 1:05.40 (2022-12-20)"
    /// - "200m IM - 2:10.35 (2023-01-10)"
    /// </summary>
    private (Stroke stroke, int distance, double timeSeconds, DateTime date)? ParseRankingOptionText(string text)
    {
        try
        {
            // Extract distance (e.g., "50m", "100m", "1500m")
            var distanceMatch = Regex.Match(text, @"(\d+)\s*m(?:eter)?");
            if (!distanceMatch.Success)
            {
                return null;
            }

            var distance = int.Parse(distanceMatch.Groups[1].Value);

            // Extract stroke name (e.g., "Freestyle", "Backstroke", "Breaststroke", "Butterfly", "IM", "Individual Medley")
            Stroke? stroke = ExtractStroke(text);
            if (stroke == null)
            {
                return null;
            }

            // Extract time (format: mm:ss.ms or ss.ms or m:ss.ms)
            var timeMatch = Regex.Match(text, @"(\d{1,2}):(\d{2})\.(\d{2})|(\d{1,2})\.(\d{2})(?!\d)");
            double? timeSeconds = null;

            if (timeMatch.Groups[1].Success)
            {
                // mm:ss.ms format
                var minutes = int.Parse(timeMatch.Groups[1].Value);
                var secs = int.Parse(timeMatch.Groups[2].Value);
                var centisecs = int.Parse(timeMatch.Groups[3].Value);
                timeSeconds = minutes * 60 + secs + centisecs / 100.0;
            }
            else if (timeMatch.Groups[4].Success)
            {
                // ss.ms format
                var secs = int.Parse(timeMatch.Groups[4].Value);
                var centisecs = int.Parse(timeMatch.Groups[5].Value);
                timeSeconds = secs + centisecs / 100.0;
            }

            if (timeSeconds == null || timeSeconds <= 0)
            {
                return null;
            }

            // Extract date (format: YYYY-MM-DD or DD-MM-YYYY or DD/MM/YYYY)
            var dateMatch = Regex.Match(text, @"(\d{4})-(\d{1,2})-(\d{1,2})|(\d{1,2})[/-](\d{1,2})[/-](\d{4})|(\d{1,2})[/-](\d{1,2})[/-](\d{2})");
            DateTime? date = null;

            if (dateMatch.Groups[1].Success)
            {
                // YYYY-MM-DD format
                var year = int.Parse(dateMatch.Groups[1].Value);
                var month = int.Parse(dateMatch.Groups[2].Value);
                var day = int.Parse(dateMatch.Groups[3].Value);
                date = new DateTime(year, month, day);
            }
            else if (dateMatch.Groups[4].Success)
            {
                // DD-MM-YYYY or DD/MM/YYYY format
                var day = int.Parse(dateMatch.Groups[4].Value);
                var month = int.Parse(dateMatch.Groups[5].Value);
                var year = int.Parse(dateMatch.Groups[6].Value);
                date = new DateTime(year, month, day);
            }
            else if (dateMatch.Groups[7].Success)
            {
                // DD-MM-YY or DD/MM/YY format
                var day = int.Parse(dateMatch.Groups[7].Value);
                var month = int.Parse(dateMatch.Groups[8].Value);
                var year = int.Parse(dateMatch.Groups[9].Value);
                if (year < 100)
                    year += year < 50 ? 2000 : 1900;
                date = new DateTime(year, month, day);
            }

            if (date == null)
            {
                // If no date found, use today
                date = DateTime.Now;
            }

            return (stroke.Value, distance, timeSeconds.Value, date.Value);
        }
        catch
        {
            return null;
        }
    }

    private Stroke? ExtractStroke(string text)
    {
        var normalized = text.ToLowerInvariant();

        if (normalized.Contains("freestyle") || normalized.Contains("free") || normalized.Contains("crawl"))
            return Stroke.Freestyle;
        if (normalized.Contains("backstroke") || normalized.Contains("back") || normalized.Contains("dorsal"))
            return Stroke.Backstroke;
        if (normalized.Contains("breaststroke") || normalized.Contains("breast"))
            return Stroke.Breaststroke;
        if (normalized.Contains("butterfly") || normalized.Contains("fly") || normalized.Contains("papillon"))
            return Stroke.Butterfly;
        if (normalized.Contains("medley") || normalized.Contains("im") || normalized.Contains("individual"))
            return Stroke.IM;

        return null;
    }

    private double? ParseTime(string timeText)
    {
        try
        {
            timeText = timeText.Trim().Replace(',', '.');

            // Format: mm:ss.ms or m:ss.ms
            if (timeText.Contains(':'))
            {
                var parts = timeText.Split(':');
                var minutes = int.Parse(parts[0]);
                var seconds = double.Parse(parts[1]);
                return minutes * 60 + seconds;
            }
            // Format: ss.ms
            else
            {
                return double.Parse(timeText);
            }
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Fetches a URL with retry logic to handle temporary server issues
    /// </summary>
    private async Task<string> FetchWithRetry(string url, int maxRetries = 3)
    {
        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync();
            }
            catch (HttpRequestException ex) when (attempt < maxRetries - 1)
            {
                // Wait longer on each retry
                await Task.Delay(1000 * (attempt + 1));
                continue;
            }
        }

        // If we get here, all retries failed
        throw new Exception($"Failed to fetch {url} after {maxRetries} attempts");
    }
}

