using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SeatFlow.Api.Domain;

namespace SeatFlow.Api.Data;

public sealed class SeatFlowDbContext(DbContextOptions<SeatFlowDbContext> options) : DbContext(options)
{
    public DbSet<Workshop> Workshops => Set<Workshop>();
    public DbSet<Participant> Participants => Set<Participant>();
    public DbSet<Registration> Registrations => Set<Registration>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var utcTicksConverter = new ValueConverter<DateTimeOffset, long>(value => value.UtcTicks, value => new DateTimeOffset(value, TimeSpan.Zero));
        modelBuilder.Entity<Workshop>().Property(x => x.Title).HasMaxLength(160).IsRequired();
        modelBuilder.Entity<Workshop>().Property(x => x.StartTime).HasConversion(utcTicksConverter);
        modelBuilder.Entity<Workshop>().Property(x => x.EndTime).HasConversion(utcTicksConverter);
        modelBuilder.Entity<Participant>().Property(x => x.Name).HasMaxLength(120).IsRequired();
        modelBuilder.Entity<Participant>().Property(x => x.Email).HasMaxLength(254).IsRequired();
        modelBuilder.Entity<Participant>().HasIndex(x => x.Email).IsUnique();
        modelBuilder.Entity<Registration>().Property(x => x.Status).HasConversion<string>();
        modelBuilder.Entity<Registration>().Property(x => x.CreatedAt).HasConversion(utcTicksConverter);
        modelBuilder.Entity<Registration>().HasIndex(x => new { x.WorkshopId, x.ParticipantId });
        modelBuilder.Entity<Registration>().HasIndex(x => new { x.WorkshopId, x.Status, x.CreatedAt, x.Id });
        modelBuilder.Entity<IdempotencyRecord>().HasKey(x => x.Key);
        modelBuilder.Entity<IdempotencyRecord>().Property(x => x.Key).HasMaxLength(200);
    }
}
