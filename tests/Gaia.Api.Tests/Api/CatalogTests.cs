using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace Gaia.Api.Tests.Api;

public class CatalogTests(GaiaApiFactory factory) : IClassFixture<GaiaApiFactory>
{
    [Fact]
    public async Task R1_AC1_GET_operations_returns_seeded_operations_with_id_name_kind_and_labels()
    {
        var response = await factory.CreateClient().GetAsync("/api/operations");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var operations = await response.Content.ReadFromJsonAsync<JsonElement[]>();
        var weather = Assert.Single(operations!, o => o.GetProperty("id").GetString() == "weather");
        Assert.Equal("Weather today", weather.GetProperty("name").GetString());
        Assert.Equal("Other", weather.GetProperty("kind").GetString());
        Assert.Equal("Latitude", weather.GetProperty("fieldALabel").GetString());
        Assert.Equal("Longitude", weather.GetProperty("fieldBLabel").GetString());
        Assert.Equal("Coordinate", weather.GetProperty("inputType").GetString());
        Assert.True(weather.GetProperty("isSystem").GetBoolean());
        Assert.Contains(operations!, o => o.GetProperty("id").GetString() == "add");
    }

    [Fact]
    public async Task R1_AC2_disabled_and_deleted_operations_are_absent()
    {
        await using (var db = factory.CreateDbContext())
        {
            await db.Database.ExecuteSqlRawAsync("UPDATE Operations SET IsEnabled = 0 WHERE Id = 'average'");
            await db.Database.ExecuteSqlRawAsync("UPDATE Operations SET IsDeleted = 1 WHERE Id = 'hypotenuse'");
        }

        var operations = await factory.CreateClient().GetFromJsonAsync<JsonElement[]>("/api/operations");

        var ids = operations!.Select(o => o.GetProperty("id").GetString()).ToList();
        Assert.DoesNotContain("average", ids);
        Assert.DoesNotContain("hypotenuse", ids);
        Assert.Contains("add", ids);
    }
}

/// <summary>Own class (and own database) because it disables every operation.</summary>
public class EmptyCatalogTests(GaiaApiFactory factory) : IClassFixture<GaiaApiFactory>
{
    [Fact]
    public async Task R1_AC3_when_every_operation_is_disabled_GET_returns_an_empty_list_with_200()
    {
        await using (var db = factory.CreateDbContext())
            await db.Database.ExecuteSqlRawAsync("UPDATE Operations SET IsEnabled = 0");

        var response = await factory.CreateClient().GetAsync("/api/operations");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty((await response.Content.ReadFromJsonAsync<JsonElement[]>())!);
    }
}
