namespace TeamNotesApp.Models;

public class User
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ApiTokenHash { get; set; } = string.Empty;
}
