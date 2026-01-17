using System.Net.Http;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Microsoft.EntityFrameworkCore;
using SwimStats.Core.Interfaces;
using SwimStats.Core.Models;

namespace SwimStats.Data.Services;

public class SwimTrackImporter : ISwimTrackImporter
{
    private readonly SwimStatsDbContext _db;
    private readonly HttpClient _httpClient;
    private readonly Action<int, int, string>? _progressCallback;

    public SwimTrackImporter(SwimStatsDbContext db, Action<int, int, string>? progressCallback = null)
    {
        _db = db;
        
        // Create HttpClient with automatic decompression and browser headers
        var handler = new HttpClientHandler();
        handler.AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate;
        
        _httpClient = new HttpClient(handler);
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        _httpClient.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");
        _httpClient.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.5");
        _httpClient.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate");
        _httpClient.DefaultRequestHeaders.Add("DNT", "1");
        _httpClient.DefaultRequestHeaders.Add("Connection", "keep-alive");
        _httpClient.DefaultRequestHeaders.Add("Upgrade-Insecure-Requests", "1");
        _httpClient.Timeout = TimeSpan.FromSeconds(30); // Reduced from 60 to 30 seconds
        _progressCallback = progressCallback;
    }

    /// <summary>
    /// Checks if the SwimTrack website is reachable with robust probing:
    /// 1) DNS resolution, 2) quick HEAD, 3) lightweight GET fallback.
    /// Uses short timeouts and a single retry with backoff.
    /// </summary>
    public async Task<bool> IsWebsiteReachableAsync()
    {
        var host = "www.swimtrack.nl";
        var baseUrl = "https://www.swimtrack.nl";

        // 1) DNS resolution check (quick)
        try
        {
            var dnsTask = System.Net.Dns.GetHostAddressesAsync(host);
            var completed = await Task.WhenAny(dnsTask, Task.Delay(3000));
            if (completed != dnsTask)
            {
                System.Diagnostics.Debug.WriteLine("[Reachability] DNS resolution timed out for SwimTrack");
                return false;
            }
            var addresses = await dnsTask;
            if (addresses == null || addresses.Length == 0)
            {
                System.Diagnostics.Debug.WriteLine("[Reachability] DNS resolution returned no addresses for SwimTrack");
                return false;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Reachability] DNS resolution failed: {ex.Message}");
            return false;
        }

        // 2) Quick HEAD request; some servers may not support HEAD so ignore 405 and fallback
        async Task<bool> HeadProbeAsync()
        {
            try
            {
                using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(5));
                var req = new HttpRequestMessage(HttpMethod.Head, baseUrl);
                var resp = await _httpClient.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cts.Token);
                if ((int)resp.StatusCode == 405)
                {
                    // Method Not Allowed: server may not support HEAD; fallback to GET
                    return false;
                }
                return (int)resp.StatusCode < 500; // treat 2xx/3xx/4xx (except 5xx) as reachable
            }
            catch (TaskCanceledException)
            {
                System.Diagnostics.Debug.WriteLine("[Reachability] HEAD probe timed out");
                return false;
            }
            catch (HttpRequestException ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Reachability] HEAD probe request error: {ex.Message}");
                return false;
            }
        }

        // 3) Lightweight GET for robots.txt or base page headers
        async Task<bool> GetProbeAsync()
        {
            try
            {
                using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(7));
                var resp = await _httpClient.GetAsync(baseUrl + "/robots.txt", HttpCompletionOption.ResponseHeadersRead, cts.Token);
                if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    // robots.txt not present; try base page headers only
                    resp = await _httpClient.GetAsync(baseUrl, HttpCompletionOption.ResponseHeadersRead, cts.Token);
                }
                return (int)resp.StatusCode < 500;
            }
            catch (TaskCanceledException)
            {
                System.Diagnostics.Debug.WriteLine("[Reachability] GET probe timed out");
                return false;
            }
            catch (HttpRequestException ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Reachability] GET probe request error: {ex.Message}");
                return false;
            }
        }

        // Try HEAD, then GET with a single retry and simple backoff
        if (await HeadProbeAsync()) return true;
        if (await GetProbeAsync()) return true;
        await Task.Delay(500);
        return await GetProbeAsync();
    }

    public async Task<int> ImportSwimmersAsync(string baseUrl)
    {
        try
        {
            var html = await _httpClient.GetStringAsync(baseUrl);
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            int count = 0;

            // Look for select dropdown with swimmer names/IDs
            var selectNode = doc.DocumentNode.SelectSingleNode("//select[@name='zwemmer' or @id='zwemmer' or @name='swimmer' or @id='swimmer']");
            
            if (selectNode == null)
            {
                // Try to find any select element
                selectNode = doc.DocumentNode.SelectSingleNode("//select");
            }

            if (selectNode == null)
            {
                // Save HTML to file for debugging
                var debugPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SwimStats", "debug.html");
                await System.IO.File.WriteAllTextAsync(debugPath, html);
                throw new Exception($"Could not find swimmer dropdown on page. HTML saved to: {debugPath}");
            }

            if (selectNode != null)
            {
                var options = selectNode.SelectNodes(".//option");
                if (options != null)
                {
                    foreach (var option in options)
                    {
                        var name = System.Net.WebUtility.HtmlDecode(option.InnerText.Trim());
                        var value = option.GetAttributeValue("value", "");
                        
                        // Skip the default/placeholder option
                        if (string.IsNullOrEmpty(name) || 
                            name.ToLower().Contains("kies") || 
                            name.ToLower().Contains("select") ||
                            string.IsNullOrEmpty(value))
                        {
                            continue;
                        }

                        var existingSwimmer = await _db.Swimmers.FirstOrDefaultAsync(s => s.Name == name);
                        if (existingSwimmer == null)
                        {
                            var names = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
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
            }

            return count;
        }
        catch
        {
            return 0;
        }
    }

    public async Task<int> ImportResultsAsync(string baseUrl)
    {
        try
        {
            // First, get the page to find all swimmers in the dropdown
            var html = await _httpClient.GetStringAsync(baseUrl);
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            int totalCount = 0;

            // Find the select element with swimmers
            var selectNode = doc.DocumentNode.SelectSingleNode("//select[@name='zwemmer' or @id='zwemmer' or @name='swimmer' or @id='swimmer']");
            
            if (selectNode == null)
            {
                selectNode = doc.DocumentNode.SelectSingleNode("//select");
            }

            if (selectNode != null)
            {
                var options = selectNode.SelectNodes(".//option");
                if (options != null)
                {
                    var validOptions = options.Where(o => 
                    {
                        var name = System.Net.WebUtility.HtmlDecode(o.InnerText.Trim());
                        var value = o.GetAttributeValue("value", "");
                        return !string.IsNullOrEmpty(name) && 
                               !name.ToLower().Contains("kies") && 
                               !name.ToLower().Contains("select") &&
                               !string.IsNullOrEmpty(value);
                    }).ToList();

                    int current = 0;
                    int total = validOptions.Count;

                    foreach (var option in validOptions)
                    {
                        current++;
                        var swimmerName = System.Net.WebUtility.HtmlDecode(option.InnerText.Trim());
                        var swimmerValue = option.GetAttributeValue("value", "");
                        
                        _progressCallback?.Invoke(current, total, $"Processing {swimmerName} ({current}/{total})...");

                        // The value already contains the relative URL like "perstijden.php?startnr=200904472"
                        // Construct the full URL
                        var baseUri = new Uri(baseUrl);
                        var swimmerUrl = new Uri(baseUri, swimmerValue).ToString();
                        
                        // Fetch this swimmer's page
                        var swimmerHtml = await _httpClient.GetStringAsync(swimmerUrl);
                        var swimmerDoc = new HtmlDocument();
                        swimmerDoc.LoadHtml(swimmerHtml);

                        // Get or create swimmer in database
                        var swimmer = await _db.Swimmers.FirstOrDefaultAsync(s => s.Name == swimmerName);
                        if (swimmer == null)
                        {
                            var names = swimmerName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                            swimmer = new Swimmer 
                            { 
                                FirstName = names.Length > 0 ? names[0] : "",
                                LastName = names.Length > 1 ? string.Join(" ", names.Skip(1)) : ""
                            };
                            _db.Swimmers.Add(swimmer);
                            await _db.SaveChangesAsync();
                        }

                        // Parse results from this swimmer's page
                        var (retrieved, added, existing) = await ParseSwimmerResults(swimmerDoc, swimmer.Id);
                        totalCount += added;

                        // Small delay to be polite to the server (reduced from 500ms to 200ms)
                        await Task.Delay(200);
                    }
                }
            }

            return totalCount;
        }
        catch (Exception ex)
        {
            throw new Exception($"Import failed: {ex.Message}", ex);
        }
    }

    private async Task<(int retrieved, int newCount, int existing)> ParseSwimmerResults(HtmlDocument doc, int swimmerId)
    {
        int newCount = 0;
        int existingCount = 0;

        // Look for all links with href containing "slag=" (stroke) and title="Gezwommen op" (swum on)
        // Exclude links that contain "tuss" (tussentijden = intermediate times)
        var timeLinks = doc.DocumentNode.SelectNodes("//a[contains(@href, 'slag=') and contains(@title, 'Gezwommen op') and not(contains(@href, 'tuss'))]");

        if (timeLinks != null)
        {
            // Batch results to add
            var resultsToAdd = new List<Result>();
            
            foreach (var link in timeLinks)
            {
                try
                {
                    // Get time from link text (e.g., "54.80" or "1:58.88")
                    var timeText = link.InnerText.Trim();
                    
                    // Get date from title attribute (e.g., "Gezwommen op 29-01-2023")
                    var title = link.GetAttributeValue("title", "");
                    var dateMatch = Regex.Match(title, @"(\d{2})-(\d{2})-(\d{4})");
                    if (!dateMatch.Success) continue;
                    
                    var day = int.Parse(dateMatch.Groups[1].Value);
                    var month = int.Parse(dateMatch.Groups[2].Value);
                    var year = int.Parse(dateMatch.Groups[3].Value);
                    var date = new DateTime(year, month, day);
                    
                    // Parse href to get stroke and distance (e.g., "slag=vl50" = butterfly 50m)
                    var href = link.GetAttributeValue("href", "");
                    var slagMatch = Regex.Match(href, @"slag=([a-z]+)(\d+)");
                    if (!slagMatch.Success) continue;
                    
                    var strokeCode = slagMatch.Groups[1].Value;
                    var distance = int.Parse(slagMatch.Groups[2].Value);
                    
                    // Map stroke codes: vl=butterfly, ru=backstroke, ss=breaststroke, vr=freestyle, wi=IM
                    Stroke? stroke = strokeCode switch
                    {
                        "vl" => Stroke.Butterfly,
                        "ru" => Stroke.Backstroke,
                        "ss" => Stroke.Breaststroke,
                        "vr" => Stroke.Freestyle,
                        "wi" => Stroke.IM,
                        _ => null
                    };
                    
                    if (stroke == null) continue;
                    
                    // Parse time to seconds
                    var seconds = ParseTime(timeText);
                    if (seconds == null) continue;
                    
                    // Find or create the event (cache this in memory to avoid DB calls)
                    // SwimTrack data is short course (25m) by default if not specified
                    var evt = await _db.Events.FirstOrDefaultAsync(e => 
                        e.Stroke == stroke.Value && e.DistanceMeters == distance && e.Course == Course.ShortCourse);
                    
                    if (evt == null)
                    {
                        evt = new Event { Stroke = stroke.Value, DistanceMeters = distance, Course = Course.ShortCourse };
                        _db.Events.Add(evt);
                        await _db.SaveChangesAsync(); // Save to get the ID
                    }
                    
                    resultsToAdd.Add(new Result
                    {
                        SwimmerId = swimmerId,
                        EventId = evt.Id,
                        TimeSeconds = seconds.Value,
                        Date = date
                    });
                }
                catch
                {
                    // Skip this result if parsing fails
                    continue;
                }
            }
            
            // Batch check for existing results
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
                        newCount++;
                    }
                    else
                    {
                        existingCount++;
                    }
                }
                
                // Single save for all results
                if (newCount > 0)
                {
                    await _db.SaveChangesAsync();
                }
                
                return (resultsToAdd.Count, newCount, existingCount);
            }
        }

        return (0, 0, 0);
    }

    private Stroke? ParseStroke(string strokeName)
    {
        var normalized = strokeName.ToLowerInvariant().Trim();
        
        if (normalized.Contains("vrij") || normalized.Contains("free") || normalized.Contains("crawl")) return Stroke.Freestyle;
        if (normalized.Contains("rug") || normalized.Contains("back")) return Stroke.Backstroke;
        if (normalized.Contains("school") || normalized.Contains("breast")) return Stroke.Breaststroke;
        if (normalized.Contains("vlinder") || normalized.Contains("fly") || normalized.Contains("butterfly")) return Stroke.Butterfly;
        if (normalized.Contains("wissel") || normalized.Contains("medley") || normalized == "im") return Stroke.IM;
        
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

    public async Task<(int retrieved, int newCount, int existing)> ImportSwimmerByNameAsync(string firstName, string lastName)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"[SwimTrack] Importing swimmer: {firstName} {lastName}");
            
            var fullName = $"{firstName} {lastName}".Trim();
            var baseUrl = "https://www.swimtrack.nl/ez-pc/perstijden.php";
            
            // Get the main page to find the swimmer dropdown
            var html = await _httpClient.GetStringAsync(baseUrl);
            
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // Find the swimmer in the dropdown
            var selectNode = doc.DocumentNode.SelectSingleNode("//select[@name='zwemmer' or @id='zwemmer' or @name='swimmer' or @id='swimmer']");
            
            if (selectNode == null)
            {
                selectNode = doc.DocumentNode.SelectSingleNode("//select");
            }

            if (selectNode == null)
            {
                System.Diagnostics.Debug.WriteLine($"[SwimTrack] ERROR: No select element found");
                throw new Exception("Could not find swimmer dropdown on SwimTrack");
            }

            var options = selectNode.SelectNodes(".//option");
            
            if (options == null || options.Count == 0)
            {
                throw new Exception("No swimmers found in dropdown");
            }

            // Find the matching swimmer
            HtmlNode matchingOption = null;
            foreach (var option in options)
            {
                var name = System.Net.WebUtility.HtmlDecode(option.InnerText.Trim());
                if (name.Equals(fullName, StringComparison.OrdinalIgnoreCase))
                {
                    matchingOption = option;
                    break;
                }
            }

            if (matchingOption == null)
            {
                // Swimmer not found - try partial match
                foreach (var option in options)
                {
                    var name = System.Net.WebUtility.HtmlDecode(option.InnerText.Trim());
                    if (name.Contains(firstName, StringComparison.OrdinalIgnoreCase) && 
                        name.Contains(lastName, StringComparison.OrdinalIgnoreCase))
                    {
                        matchingOption = option;
                        break;
                    }
                }
            }

            if (matchingOption == null)
            {
                System.Diagnostics.Debug.WriteLine($"[SwimTrack] Swimmer not found: {fullName}");
                return (0, 0, 0); // Swimmer not found, but don't fail
            }

            var swimmerValue = matchingOption.GetAttributeValue("value", "");
            if (string.IsNullOrEmpty(swimmerValue))
            {
                System.Diagnostics.Debug.WriteLine($"[SwimTrack] ERROR: Swimmer option has no value attribute");
                return (0, 0, 0);
            }

            // Get or create swimmer in database
            var swimmer = await _db.Swimmers.FirstOrDefaultAsync(s => 
                s.FirstName == firstName && s.LastName == lastName);
            if (swimmer == null)
            {
                swimmer = new Swimmer 
                { 
                    FirstName = firstName,
                    LastName = lastName,
                    Name = fullName  // Keep for backward compatibility
                };
                _db.Swimmers.Add(swimmer);
                await _db.SaveChangesAsync();
            }

            // Construct the full URL for this swimmer's page
            var baseUri = new Uri(baseUrl);
            var swimmerUrl = new Uri(baseUri, swimmerValue).ToString();
            
            // Fetch this swimmer's page
            var swimmerHtml = await _httpClient.GetStringAsync(swimmerUrl);
            
            var swimmerDoc = new HtmlDocument();
            swimmerDoc.LoadHtml(swimmerHtml);

            // Parse results from this swimmer's page
            var (retrieved, newCount, existingCount) = await ParseSwimmerResults(swimmerDoc, swimmer.Id);
            System.Diagnostics.Debug.WriteLine($"[SwimTrack] Parsed {retrieved} results for {fullName}");
            
            return (retrieved, newCount, existingCount);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SwimTrack] Error importing swimmer {firstName} {lastName}: {ex.Message}");
            return (0, 0, 0);
        }
    }

    public async Task<(int retrieved, int newCount, int existing)> ImportSingleSwimmerAsync(string swimmerName)
    {
        try
        {
            var names = swimmerName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var firstName = names.Length > 0 ? names[0] : "";
            var lastName = names.Length > 1 ? string.Join(" ", names.Skip(1)) : "";
            
            return await ImportSwimmerByNameAsync(firstName, lastName);
        }
        catch
        {
            return (0, 0, 0);
        }
    }
}

