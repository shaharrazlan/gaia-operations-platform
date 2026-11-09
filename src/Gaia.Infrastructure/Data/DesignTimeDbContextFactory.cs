using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Gaia.Infrastructure.Data;

/// <summary>
/// Used only by the "dotnet ef" tool (creating migrations), never at run time.
/// The running API gets its connection string from configuration.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<GaiaDbContext>
{
    public GaiaDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<GaiaDbContext>()
            .UseSqlServer(@"Server=(localdb)\MSSQLLocalDB;Database=GaiaDev;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;
        return new GaiaDbContext(options);
    }
}
