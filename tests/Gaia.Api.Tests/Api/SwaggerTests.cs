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
    public async Task T38_swagger_json_declares_the_management_key_for_management_calls_only()
    {
        using var document = JsonDocument.Parse(await factory.CreateClient().GetStringAsync("/swagger/v1/swagger.json"));
        var root = document.RootElement;

        var scheme = root.GetProperty("components").GetProperty("securitySchemes").GetProperty("ManagementKey");
        Assert.Equal("apiKey", scheme.GetProperty("type").GetString());
        Assert.Equal("header", scheme.GetProperty("in").GetString());
        Assert.Equal("X-Management-Key", scheme.GetProperty("name").GetString());

        var paths = root.GetProperty("paths");
        Assert.True(paths.GetProperty("/api/operations").GetProperty("post").TryGetProperty("security", out _));
        Assert.True(paths.GetProperty("/api/operations/{id}").GetProperty("put").TryGetProperty("security", out _));
        Assert.True(paths.GetProperty("/api/operations/{id}").GetProperty("delete").TryGetProperty("security", out _));
        Assert.True(paths.GetProperty("/api/operations/{id}").GetProperty("delete").GetProperty("responses").TryGetProperty("401", out _));
        Assert.False(paths.GetProperty("/api/operations/{id}/execute").GetProperty("post").TryGetProperty("security", out _));
    }

    [Fact]
    public async Task T23_swagger_UI_page_is_served()
    {
        var response = await factory.CreateClient().GetAsync("/swagger/index.html");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
