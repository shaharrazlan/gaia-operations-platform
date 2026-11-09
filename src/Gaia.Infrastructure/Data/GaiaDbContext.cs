using Gaia.Core.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Gaia.Infrastructure.Data;

/// <summary>EF Core's view of the Gaia database. Table shapes live in the *EntityConfig classes.</summary>
public class GaiaDbContext(DbContextOptions<GaiaDbContext> options) : DbContext(options)
{
    public DbSet<OperationDefinition> Operations => Set<OperationDefinition>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(GaiaDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // All timestamps are stored in UTC. SQL Server's datetime2 doesn't remember that, so mark values
        // read back as UTC; otherwise they'd serialize without a "Z" and clients would treat them as local time.
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
    }

    private sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
        toDatabase => toDatabase.Kind == DateTimeKind.Local ? toDatabase.ToUniversalTime() : toDatabase,
        fromDatabase => DateTime.SpecifyKind(fromDatabase, DateTimeKind.Utc));
}
