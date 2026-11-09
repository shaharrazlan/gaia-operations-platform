using System.Net;

namespace Gaia.Api.Tests;

public class HealthTests(GaiaApiFactory factory) : IClassFixture<GaiaApiFactory>
{
    [Fact]
    public async Task T4_GET_health_returns_200()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
