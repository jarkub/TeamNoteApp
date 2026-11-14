using Microsoft.EntityFrameworkCore;
using TeamNoteApp.Models;

namespace TeamNoteApp.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options)
    : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<TeamMember> TeamMembers => Set<TeamMember>();
    public DbSet<Note> Notes => Set<Note>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Users
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(u => u.Id);

            entity.Property(u => u.Name)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(u => u.ApiTokenHash)
                .IsRequired()
                .HasMaxLength(64);

            entity.HasIndex(u => u.ApiTokenHash)
                .IsUnique();
        });

        // Teams
        modelBuilder.Entity<Team>(entity =>
        {
            entity.HasKey(t => t.Id);

            entity.Property(t => t.Name)
                .IsRequired()
                .HasMaxLength(100);

            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(t => t.OwnerId)
                .OnDelete(DeleteBehavior.Restrict);

            // Each user automatically receives a team when registering. Users can
            // collaborate across multiple teams through membership, but each user
            // owns only one team. This simplifies onboarding, avoids
            // unnecessary team - management endpoints, and keeps the assessment
            // focused on collaborative note-taking.
            entity.HasIndex(t => t.OwnerId).IsUnique();
        });

        // Team membership
        modelBuilder.Entity<TeamMember>(entity =>
        {
            entity.HasKey(m => new { m.TeamId, m.UserId });

            entity.HasOne<Team>()
                .WithMany()
                .HasForeignKey(m => m.TeamId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(m => m.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(m => m.UserId);
        });

        // Notes
        modelBuilder.Entity<Note>(entity =>
        {
            entity.HasKey(n => n.Id);

            entity.Property(n => n.Title)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(n => n.Content)
                .IsRequired();

            entity.HasOne<Team>()
                .WithMany()
                .HasForeignKey(n => n.TeamId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(n => n.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(n => n.TeamId);
        });
    }
}