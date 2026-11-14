using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using TeamNotesApp.Contracts;
using TeamNotesApp.Data;
using TeamNotesApp.Models;
using TeamNotesApp.Security;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration
    .GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Missing database connection string.");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(connectionString));

builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapGet("/api/health", async (AppDbContext db) =>
{
    var canConnect = await db.Database.CanConnectAsync();

    return canConnect
        ? Results.Ok(new { status = "healthy" })
        : Results.Problem("Database unavailable");
});

// Register a new user
app.MapPost("/api/users", async (
    CreateUserRequest request,
    AppDbContext db) =>
{
    var name = request.Name?.Trim();

    if (string.IsNullOrWhiteSpace(name) || name.Length > 100)
        return Results.BadRequest(new
        {
            error = "Name must be between 1 and 100 characters."
        });

    // Generate a cryptographically secure, 256-bit token.
    var token = Convert.ToHexString(
        RandomNumberGenerator.GetBytes(32));

    var tokenHash = Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    var user = new User
    {
        Name = name,
        ApiTokenHash = tokenHash
    };

    db.Users.Add(user);
    await db.SaveChangesAsync();

    return Results.Created("/api/users/me", new
    {
        user.Id,
        user.Name,
        ApiToken = token
    });
});

app.MapGet("/api/users/me", async (
    HttpContext context,
    AppDbContext db) =>
{
    var user = await ApiAuth.GetCurrentUserAsync(context, db);

    if (user is null)
        return Results.Unauthorized();

    return Results.Ok(new
    {
        user.Id,
        user.Name
    });
});

app.Run();
