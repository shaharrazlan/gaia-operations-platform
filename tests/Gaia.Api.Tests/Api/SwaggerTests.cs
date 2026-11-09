using System.Net;
using System.Text.Json;

namespace Gaia.Api.Tests.Api;

public class SwaggerTests(GaiaApiFactory factory) : IClassFixture<GaiaApiFactory>
{
    [Fact]
    public async Task T23_swagger_json_lists_the_catalog_and_execute_endpoints()
    {
        var response = await factory.CreateClient().GetAsync("/swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var paths = document.RootElement.GetProperty("paths");
        Assert.True(paths.GetProperty("/api/operations").TryGetProperty("get", out _));
        Assert.True(paths.GetProperty("/api/operations/{id}/execute").TryGetProperty("post", out _));
    }

    [Fact]
    public async Task T23_swagger_UI_page_is_served()
    {
        var response = await factory.CreateClient().GetAsync("/swagger/index.html");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
