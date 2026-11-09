using Gaia.Core.Operations;
using Microsoft.EntityFrameworkCore;

namespace Gaia.Infrastructure.Data;

/// <summary>EF Core's view of the Gaia database. Table shapes live in the *EntityConfig classes.</summary>
public class GaiaDbContext(DbContextOptions<GaiaDbContext> options) : DbContext(options)
{
    public DbSet<OperationDefinition> Operations => Set<OperationDefinition>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(GaiaDbContext).Assembly);
    }
}
