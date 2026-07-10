using AiMeetingAssistant.Core.Entities;
using AiMeetingAssistant.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AiMeetingAssistant.Infrastructure.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<AppUser>(options)
{
    public DbSet<Meeting> Meetings => Set<Meeting>();
    public DbSet<KeyDecision> KeyDecisions => Set<KeyDecision>();
    public DbSet<ActionItem> ActionItems => Set<ActionItem>();
    public DbSet<JiraTicket> JiraTickets => Set<JiraTicket>();
    public DbSet<AppSettings> AppSettings => Set<AppSettings>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Meeting>(entity =>
        {
            entity.Property(m => m.Status).HasConversion<string>();
            entity.HasMany(m => m.KeyDecisions)
                .WithOne()
                .HasForeignKey(kd => kd.MeetingId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(m => m.ActionItems)
                .WithOne()
                .HasForeignKey(ai => ai.MeetingId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ActionItem>(entity =>
        {
            entity.Property(ai => ai.Priority).HasConversion<string>();
            entity.HasMany(ai => ai.JiraTickets)
                .WithOne()
                .HasForeignKey(t => t.ActionItemId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<JiraTicket>(entity =>
        {
            entity.Property(t => t.Status).HasConversion<string>();
        });

        builder.Entity<AppSettings>(entity =>
        {
            entity.HasIndex(s => s.UserId).IsUnique();
        });
    }
}
