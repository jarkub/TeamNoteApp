using Microsoft.EntityFrameworkCore;
using TeamNoteApp.Data;
using TeamNoteApp.Models;
using TeamNoteApp.Security;

namespace TeamNoteApp.Endpoints;

public record AddMemberRequest(int UserId);

public static class TeamEndpoints
{
    public static IEndpointRouteBuilder MapTeamEndpoints(
        this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/teams").WithTags("Teams");

        group.MapGet("", GetTeams);
        group.MapGet("/{id:int}/members", GetTeamMembers);
        group.MapPost("/{id:int}/members", AddTeamMember);
        group.MapDelete("/{id:int}/members/{userId:int}", RemoveTeamMember);

        return app;
    }

    private static async Task<IResult> GetTeams(
        HttpContext context, AppDbContext db)
    {
        var user = await ApiAuth.GetCurrentUserAsync(context, db);
        if (user is null)
            return Results.Unauthorized();

        var teams = await (
            from team in db.Teams
            join member in db.TeamMembers on team.Id equals member.TeamId
            where member.UserId == user.Id
            select new { team.Id, team.Name, team.OwnerId }
        ).ToListAsync();

        return Results.Ok(teams);
    }

    private static async Task<IResult> GetTeamMembers(
        int id, HttpContext context, AppDbContext db)
    {
        var user = await ApiAuth.GetCurrentUserAsync(context, db);
        if (user is null)
            return Results.Unauthorized();

        if (!await db.TeamMembers.AnyAsync(
            m => m.TeamId == id && m.UserId == user.Id))
            return Results.NotFound();

        var members = await (
            from member in db.TeamMembers
            join u in db.Users on member.UserId equals u.Id
            where member.TeamId == id
            select new
            {
                u.Id,
                u.Name
            }
        ).ToListAsync();

        return Results.Ok(members);
    }

    private static async Task<IResult> AddTeamMember(
        int id, AddMemberRequest request,
        HttpContext context, AppDbContext db)
    {
        var user = await ApiAuth.GetCurrentUserAsync(context, db);
        if (user is null)
            return Results.Unauthorized();

        var team = await db.Teams.FindAsync(id);
        if (team is null)
            return Results.NotFound();

        if (team.OwnerId != user.Id)
            return Results.StatusCode(403);

        if (!await db.Users.AnyAsync(u => u.Id == request.UserId))
            return Results.BadRequest("User does not exist.");

        if (await db.TeamMembers.AnyAsync(
            m => m.TeamId == id && m.UserId == request.UserId))
            return Results.Conflict("Already a member.");

        db.TeamMembers.Add(new TeamMember
        {
            TeamId = id,
            UserId = request.UserId
        });

        await db.SaveChangesAsync();

        return Results.NoContent();
    }

    // DELETE uses a route parameter rather than a JSON body. Minimal APIs
    // do not infer request bodies for DELETE handlers.
    private static async Task<IResult> RemoveTeamMember(
        int id, int userId,
        HttpContext context, AppDbContext db)
    {
        var user = await ApiAuth.GetCurrentUserAsync(context, db);
        if (user is null)
            return Results.Unauthorized();

        var team = await db.Teams.FindAsync(id);
        if (team is null)
            return Results.NotFound();

        if (team.OwnerId != user.Id)
            return Results.StatusCode(403);

        // The owner must remain a member of their own team.
        if (team.OwnerId == userId)
            return Results.Conflict("Cannot remove the team owner.");

        var member = await db.TeamMembers.FindAsync(id, userId);
        if (member is null)
            return Results.NotFound();

        db.TeamMembers.Remove(member);
        await db.SaveChangesAsync();

        return Results.NoContent();
    }
}
