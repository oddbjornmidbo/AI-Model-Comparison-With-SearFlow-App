using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SeatFlow.Api.Domain;

namespace SeatFlow.Api.Data;

public class SeatFlowDbContext(DbContextOptions<SeatFlowDbContext> options) : DbContext(options)
{
    public DbSet<Workshop> Workshops => Set<Workshop>();
    public DbSet<Participant> Participants => Set<Participant>();
    public DbSet<Registration> Registrations => Set<Registration>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // All timestamps are stored and returned as UTC.
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Workshop>(e =>
        {
            e.Property(w => w.Title).HasMaxLength(200);
            e.ToTable(t => t.HasCheckConstraint("CK_Workshop_Capacity", "Capacity > 0"));
        });

        modelBuilder.Entity<Participant>(e =>
        {
            e.Property(p => p.Name).HasMaxLength(200);
            e.Property(p => p.Email).HasMaxLength(320);
            e.HasIndex(p => p.Email).IsUnique();
        });

        modelBuilder.Entity<Registration>(e =>
        {
            e.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);
            // Backstop for business rule 1: at most one active registration per participant and workshop.
            e.HasIndex(r => new { r.WorkshopId, r.ParticipantId })
                .IsUnique()
                .HasFilter("Status <> 'Cancelled'");
            e.HasIndex(r => new { r.WorkshopId, r.Status, r.CreatedAt });
        });

        modelBuilder.Entity<IdempotencyRecord>(e =>
        {
            e.HasKey(r => r.Key);
            e.Property(r => r.Key).HasMaxLength(200);
        });
    }

    private sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
        v => v.Kind == DateTimeKind.Local ? v.ToUniversalTime() : DateTime.SpecifyKind(v, DateTimeKind.Utc),
        v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
}
