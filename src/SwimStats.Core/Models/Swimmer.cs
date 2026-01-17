namespace SwimStats.Core.Models;

public class Swimmer
{
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    
    [Obsolete("Use FirstName and LastName instead")]
    public string? Name { get; set; }
    
    public string DisplayName => $"{FirstName} {LastName}".Trim();
}
