using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SeatFlow.Api.Domain;

namespace SeatFlow.Api.Data;

public class SeatFlowDbContext(DbContextOptions<SeatFlowDbContext> options) : DbContext(options)
{
    public DbSet<Workshop> Workshops => Set<Workshop>();
    public DbSet<Participant> Participants => Set<Participant>();
    public DbSet<Registration> Registrations => Set<Registration>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Workshop>(b =>
        {
            b.Property(w => w.Title).HasMaxLength(200);
            b.ToTable(t => t.HasCheckConstraint("CK_Workshop_Capacity", "\"Capacity\" > 0"));
        });

        modelBuilder.Entity<Participant>(b =>
        {
            b.Property(p => p.Name).HasMaxLength(200);
            b.Property(p => p.Email).HasMaxLength(320);
            b.HasIndex(p => p.Email).IsUnique();
        });

        modelBuilder.Entity<Registration>(b =>
        {
            b.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);
            b.Property(r => r.IdempotencyKey).HasMaxLength(200);
            b.HasIndex(r => r.IdempotencyKey).IsUnique();

            // Database-level guard for rule 1: at most one non-cancelled registration per participant and workshop.
            b.HasIndex(r => new { r.WorkshopId, r.ParticipantId })
                .IsUnique()
                .HasFilter("\"Status\" <> 'Cancelled'");

            b.HasIndex(r => new { r.WorkshopId, r.Status, r.CreatedAt });
        });
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // All timestamps are UTC. SQLite does not keep DateTimeKind, so restore it on read.
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
        configurationBuilder.Properties<DateTime?>().HaveConversion<UtcDateTimeConverter>();
    }

    private sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
        v => v.Kind == DateTimeKind.Utc ? v : v.ToUniversalTime(),
        v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
}
