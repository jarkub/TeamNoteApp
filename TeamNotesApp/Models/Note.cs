namespace TeamNotesApp.Models;

public class Note
{
    public int Id { get; set; }
    public int TeamId { get; set; }
    public int CreatedByUserId { get; set; }

    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}