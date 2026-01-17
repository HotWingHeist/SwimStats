namespace SwimStats.Core.Models;

public class Result
{
    public int Id { get; set; }
    public int SwimmerId { get; set; }
    public Swimmer? Swimmer { get; set; }
    public int EventId { get; set; }
    public Event? Event { get; set; }
    // Time in seconds
    public double TimeSeconds { get; set; }
    public DateTime Date { get; set; }
    public Course Course { get; set; } = Course.ShortCourse;  // Track which course the result is from
    public string? Location { get; set; } // Meet location or city
}
