namespace TeamNoteApp.Models;

public class Team
{
    public int Id { get; set; }
    public int OwnerId { get; set; }
    public string Name { get; set; } = string.Empty;
}
