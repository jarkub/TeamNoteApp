using Microsoft.EntityFrameworkCore;
using TeamNoteApp.Data;
using TeamNoteApp.Endpoints;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration
    .GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Missing database connection string.");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(connectionString));

builder.Services.AddOpenApi();

var app = builder.Build();

// Convenience for a self-contained assessment: first launch creates the DB.
// Production systems should apply migrations as a separate deployment step.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

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

app.MapUserEndpoints();
app.MapTeamEndpoints();
app.MapNoteEndpoints();

app.Run();
