using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MVCS.Server.Models;

namespace MVCS.Server.Data;

public class ApplicationDbContext : IdentityDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<CompassLog> CompassLogs => Set<CompassLog>();
    public DbSet<WaterLevelLog> WaterLevelLogs => Set<WaterLevelLog>();
    public DbSet<PumpLog> PumpLogs => Set<PumpLog>();
    public DbSet<LedLog> LedLogs => Set<LedLog>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<CompassLog>(e =>
        {
            e.HasIndex(x => x.Timestamp);
        });

        builder.Entity<WaterLevelLog>(e =>
        {
            e.HasIndex(x => x.Timestamp);
        });

        builder.Entity<PumpLog>(e =>
        {
            e.HasIndex(x => x.Timestamp);
        });

        builder.Entity<LedLog>(e =>
        {
            e.HasIndex(x => x.Timestamp);
        });
    }
}
