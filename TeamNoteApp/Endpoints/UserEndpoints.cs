using System.Security.Cryptography;
using System.Text;
using TeamNoteApp.Contracts;
using TeamNoteApp.Data;
using TeamNoteApp.Models;
using TeamNoteApp.Security;

namespace TeamNoteApp.Endpoints;

public static class UserEndpoints
{
    public static IEndpointRouteBuilder MapUserEndpoints(
        this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/users")
            .WithTags("Users");

        group.MapPost("", RegisterUser);
        group.MapGet("/me", GetCurrentUser);

        return app;
    }

    // Register a new user
    private static async Task<IResult> RegisterUser(
        CreateUserRequest request,
        AppDbContext db)
    {
        var name = request.Name?.Trim();

        if (string.IsNullOrWhiteSpace(name) || name.Length > 100)
            return Results.BadRequest(new
            {
                error = "Name must be between 1 and 100 characters."
            });

        var token = Convert.ToHexString(
            RandomNumberGenerator.GetBytes(32));

        var tokenHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(token)));

        var user = new User
        {
            Name = name,
            ApiTokenHash = tokenHash
        };

        // All three operations succeed or fail together.
        await using var transaction = await db.Database.BeginTransactionAsync();

        db.Users.Add(user);
        await db.SaveChangesAsync();

        var team = new Team
        {
            Name = name,
            OwnerId = user.Id
        };

        db.Teams.Add(team);
        await db.SaveChangesAsync();

        db.TeamMembers.Add(new TeamMember
        {
            TeamId = team.Id,
            UserId = user.Id
        });

        await db.SaveChangesAsync();
        await transaction.CommitAsync();

        return Results.Created("/api/users/me", new
        {
            user.Id,
            user.Name,
            TeamId = team.Id,
            ApiToken = token
        });
    }

    private static async Task<IResult> GetCurrentUser(
        HttpContext context,
        AppDbContext db)
    {
        var user = await ApiAuth.GetCurrentUserAsync(context, db);

        if (user is null)
            return Results.Unauthorized();

        return Results.Ok(new
        {
            user.Id,
            user.Name
        });
    }
}