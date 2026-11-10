using System.Text.Json;
using Microsoft.Data.SqlClient;

namespace Gaia.Api.Tests.Data;

public class ConnectionStringTests
{
    // LocalDB starts its instance on the first connection, which can take longer than the 15 s default;
    // the API would then crash at startup while preparing the database.
    [Theory]
    [InlineData("appsettings.json")]
    [InlineData("appsettings.E2E.json")]
    public void T54_connection_strings_wait_long_enough_for_LocalDB_to_start(string file)
    {
        var path = Path.Combine(AppContext.BaseDirectory, file);
        using var settings = JsonDocument.Parse(File.ReadAllText(path));
        var connectionString = settings.RootElement.GetProperty("ConnectionStrings").GetProperty("Gaia").GetString();

        Assert.True(new SqlConnectionStringBuilder(connectionString).ConnectTimeout >= 60);
    }
}
