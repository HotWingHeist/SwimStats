namespace SwimStats.App.Models;

public class PersonalRecordViewModel
{
    public string SwimmerName { get; set; } = string.Empty;
    public string StrokeName { get; set; } = string.Empty;
    public int Distance { get; set; }
    public double BestTime { get; set; }
    public string BestTimeFormatted { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    
    public double? SeasonalBest { get; set; }
    public string SeasonalBestFormatted { get; set; } = string.Empty;
    public DateTime? SeasonalBestDate { get; set; }
    
    public double? PreviousSeasonalBest { get; set; }
    public string PreviousSeasonalBestFormatted { get; set; } = string.Empty;
    public DateTime? PreviousSeasonalBestDate { get; set; }
}
