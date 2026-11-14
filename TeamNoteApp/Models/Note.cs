namespace TeamNoteApp.Models;

public class Note
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int TeamId { get; set; }
    public string Text { get; set; } = string.Empty;
}
