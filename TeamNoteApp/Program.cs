using Microsoft.EntityFrameworkCore;
using TeamNoteApp.Data;

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

app.Run();
