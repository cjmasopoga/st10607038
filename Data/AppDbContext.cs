using Microsoft.EntityFrameworkCore;
using SmartMonitoringSolutions.Models;

namespace SmartMonitoringSolutions.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<DeviceRegistry> DeviceRegistry => Set<DeviceRegistry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(u => u.Username).IsUnique();
            entity.Property(u => u.Role).HasConversion<string>().HasMaxLength(16);
        });

        modelBuilder.Entity<DeviceRegistry>(entity =>
        {
            entity.HasIndex(d => d.DeviceId).IsUnique();
        });

        base.OnModelCreating(modelBuilder);
    }
}
