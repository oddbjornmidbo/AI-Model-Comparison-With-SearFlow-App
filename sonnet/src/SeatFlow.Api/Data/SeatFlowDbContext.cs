using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SeatFlow.Domain;

namespace SeatFlow.Data;

public class SeatFlowDbContext(DbContextOptions<SeatFlowDbContext> options) : DbContext(options)
{
    public DbSet<Workshop> Workshops => Set<Workshop>();
    public DbSet<Participant> Participants => Set<Participant>();
    public DbSet<Registration> Registrations => Set<Registration>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();

    // SQLite stores DateTime as text without a kind; read everything back as UTC so JSON carries a 'Z'.
    protected override void ConfigureConventions(ModelConfigurationBuilder cfg) =>
        cfg.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();

    private class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
        v => v.ToUniversalTime(), v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Participant>().HasIndex(p => p.Email).IsUnique();

        b.Entity<Registration>(e =>
        {
            e.Property(r => r.Status).HasConversion<string>();
            // Database-level backstop for rule 1: at most one live registration per participant/workshop.
            e.HasIndex(r => new { r.WorkshopId, r.ParticipantId })
                .IsUnique()
                .HasFilter("\"Status\" <> 'Cancelled'");
        });

        b.Entity<IdempotencyRecord>().HasIndex(r => r.Key).IsUnique();
    }
}
