using Microsoft.EntityFrameworkCore;
namespace SeatFlow.Api;
public sealed class SeatFlowDb(DbContextOptions<SeatFlowDb> options) : DbContext(options)
{
    public DbSet<Workshop> Workshops => Set<Workshop>(); public DbSet<Participant> Participants => Set<Participant>(); public DbSet<Registration> Registrations => Set<Registration>();
    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<Registration>().HasIndex(x => new { x.WorkshopId, x.ParticipantId });
        model.Entity<Registration>().HasIndex(x => new { x.WorkshopId, x.IdempotencyKey }).IsUnique();
        model.Entity<Registration>().Property(x => x.CreatedAt).HasConversion(x => x.ToUniversalTime().Ticks, x => new DateTime(x, DateTimeKind.Utc));
        model.Entity<Workshop>().Property(x => x.StartTime).HasConversion(x => x.ToUniversalTime().Ticks, x => new DateTime(x, DateTimeKind.Utc));
        model.Entity<Workshop>().Property(x => x.EndTime).HasConversion(x => x.ToUniversalTime().Ticks, x => new DateTime(x, DateTimeKind.Utc));
        model.Entity<Workshop>().Property(x => x.Title).IsRequired();
        model.Entity<Participant>().HasIndex(x => x.Email).IsUnique();
    }
}
