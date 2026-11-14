using Microsoft.EntityFrameworkCore;
using TeamNoteApp.Data;
using TeamNoteApp.Models;
using TeamNoteApp.Security;

namespace TeamNoteApp.Endpoints;

public record SaveNoteRequest(string Title, string Content);

public static class NoteEndpoints
{
    public static IEndpointRouteBuilder MapNoteEndpoints(
        this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/teams/{teamId:int}/notes")
            .WithTags("Notes");

        group.MapGet("", GetNotes);
        group.MapPost("", CreateNote);
        group.MapGet("/{noteId:int}", GetNote);
        group.MapPut("/{noteId:int}", UpdateNote);
        group.MapDelete("/{noteId:int}", DeleteNote);

        return app;
    }

    private static Task<bool> IsMember(
        AppDbContext db, int teamId, int userId)
    {
        return db.TeamMembers.AnyAsync(
            m => m.TeamId == teamId && m.UserId == userId);
    }

    private static bool ValidNote(SaveNoteRequest request)
    {
        return !string.IsNullOrWhiteSpace(request.Title)
            && request.Title.Length <= 200
            && request.Content is not null
            && request.Content.Length <= 10000;
    }

    private static async Task<IResult> GetNotes(
        int teamId, HttpContext context, AppDbContext db)
    {
        var user = await ApiAuth.GetCurrentUserAsync(context, db);
        if (user is null)
            return Results.Unauthorized();

        if (!await IsMember(db, teamId, user.Id))
            return Results.NotFound();

        var notes = await db.Notes
            .AsNoTracking()
            .Where(n => n.TeamId == teamId)
            .OrderByDescending(n => n.UpdatedAtUtc)
            .ToListAsync();

        return Results.Ok(notes);
    }

    private static async Task<IResult> CreateNote(
        int teamId, SaveNoteRequest request,
        HttpContext context, AppDbContext db)
    {
        var user = await ApiAuth.GetCurrentUserAsync(context, db);
        if (user is null)
            return Results.Unauthorized();

        if (!await IsMember(db, teamId, user.Id))
            return Results.NotFound();

        if (!ValidNote(request))
            return Results.BadRequest("Invalid note.");

        var note = new Note
        {
            TeamId = teamId,
            CreatedByUserId = user.Id,
            Title = request.Title.Trim(),
            Content = request.Content,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        db.Notes.Add(note);
        await db.SaveChangesAsync();

        return Results.Created(
            $"/api/teams/{teamId}/notes/{note.Id}", note);
    }

    private static async Task<IResult> GetNote(
        int teamId, int noteId,
        HttpContext context, AppDbContext db)
    {
        var user = await ApiAuth.GetCurrentUserAsync(context, db);
        if (user is null)
            return Results.Unauthorized();

        if (!await IsMember(db, teamId, user.Id))
            return Results.NotFound();

        var note = await db.Notes.AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == noteId && n.TeamId == teamId);

        return note is null ? Results.NotFound() : Results.Ok(note);
    }

    private static async Task<IResult> UpdateNote(
        int teamId, int noteId, SaveNoteRequest request,
        HttpContext context, AppDbContext db)
    {
        var user = await ApiAuth.GetCurrentUserAsync(context, db);
        if (user is null)
            return Results.Unauthorized();

        if (!await IsMember(db, teamId, user.Id))
            return Results.NotFound();

        if (!ValidNote(request))
            return Results.BadRequest("Invalid note.");

        var note = await db.Notes
            .FirstOrDefaultAsync(n => n.Id == noteId && n.TeamId == teamId);

        if (note is null)
            return Results.NotFound();

        note.Title = request.Title.Trim();
        note.Content = request.Content;
        note.UpdatedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync();

        return Results.Ok(note);
    }

    private static async Task<IResult> DeleteNote(
        int teamId, int noteId,
        HttpContext context, AppDbContext db)
    {
        var user = await ApiAuth.GetCurrentUserAsync(context, db);
        if (user is null)
            return Results.Unauthorized();

        if (!await IsMember(db, teamId, user.Id))
            return Results.NotFound();

        var note = await db.Notes
            .FirstOrDefaultAsync(n => n.Id == noteId && n.TeamId == teamId);

        if (note is null)
            return Results.NotFound();

        db.Notes.Remove(note);
        await db.SaveChangesAsync();

        return Results.NoContent();
    }
}