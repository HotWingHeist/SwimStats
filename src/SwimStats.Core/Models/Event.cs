namespace SwimStats.Core.Models;

public enum Stroke
{
    Freestyle,
    Backstroke,
    Breaststroke,
    Butterfly,
    IM
}

public enum Course
{
    LongCourse = 50,  // 50m pools
    ShortCourse = 25  // 25m pools
}

public class Event
{
    public int Id { get; set; }
    public Stroke Stroke { get; set; }
    public int DistanceMeters { get; set; }
    public Course Course { get; set; } = Course.ShortCourse;  // Default to 25m
    public string DisplayName => $"{Stroke} {DistanceMeters}m ({(int)Course}m)";
}
