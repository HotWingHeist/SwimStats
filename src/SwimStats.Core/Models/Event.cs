namespace SwimStats.Core.Models;

public enum Stroke
{
    Freestyle,
    Backstroke,
    Breaststroke,
    Butterfly,
    IM
}

public class Event
{
    public int Id { get; set; }
    public Stroke Stroke { get; set; }
    public int DistanceMeters { get; set; }
    public string DisplayName => $"{Stroke} {DistanceMeters}m";
}
