using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Gaia.Api.Tests.Api;

/// <summary>Add, change and remove operations through the API without restarting the server (R4).</summary>
public class ManagementTests(GaiaApiFactory factory) : IClassFixture<GaiaApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    /// <summary>A unique id per test, so tests sharing this class's database never clash.</summary>
    private static string NewId(string prefix) => $"{prefix}-{Guid.NewGuid():N}"[..30];

    private static object Definition(string id, string formula = "(A + B) / 2", string name = "Mean") => new
    {
        id,
        name,
        kind = "Arithmetic",
        fieldALabel = "First number",
        fieldBLabel = "Second number",
        inputType = "Number",
        formula,
        isEnabled = true,
    };

    private async Task<JsonElement> CreateAsync(string id, string formula = "(A + B) / 2")
    {
        var response = await _client.PostAsJsonAsync("/api/operations", Definition(id, formula));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private async Task<List<string?>> CatalogIdsAsync() =>
        (await _client.GetFromJsonAsync<JsonElement[]>("/api/operations"))!.Select(o => o.GetProperty("id").GetString()).ToList();

    private async Task<string?> ExecuteAsync(string id, string a, string b)
    {
        var response = await _client.PostAsJsonAsync($"/api/operations/{id}/execute", new { fieldA = a, fieldB = b });
        return response.IsSuccessStatusCode
            ? (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("result").GetString()
            : $"HTTP {(int)response.StatusCode}";
    }

    private static async Task<string?> CodeOf(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString();

    // --- POST (T30) ---

    [Fact]
    public async Task R4_AC1_a_created_operation_appears_in_the_catalog_and_executes_without_a_restart()
    {
        var id = NewId("mean");

        var response = await _client.PostAsJsonAsync("/api/operations", Definition(id));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(id, created.GetProperty("id").GetString());
        Assert.False(created.GetProperty("isSystem").GetBoolean());
        Assert.False(string.IsNullOrEmpty(created.GetProperty("version").GetString()));
        Assert.Contains(id, await CatalogIdsAsync());
        Assert.Equal("5", await ExecuteAsync(id, "4", "6"));
    }

    [Fact]
    public async Task R4_AC4_a_bad_formula_is_400_formula_invalid_and_the_catalog_is_unchanged()
    {
        var id = NewId("broken");
        var before = await CatalogIdsAsync();

        var response = await _client.PostAsJsonAsync("/api/operations", Definition(id, formula: "A +"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("formula_invalid", problem.GetProperty("code").GetString());
        Assert.Equal("formula", problem.GetProperty("field").GetString());
        Assert.Equal(before, await CatalogIdsAsync());
    }

    [Fact]
    public async Task R4_AC4_a_duplicate_id_is_409_duplicate_id()
    {
        var response = await _client.PostAsJsonAsync("/api/operations", Definition("divide"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("duplicate_id", await CodeOf(response));
    }

    [Fact]
    public async Task R4_AC4_an_unknown_kind_is_400_naming_the_field()
    {
        var response = await _client.PostAsJsonAsync("/api/operations", new { id = NewId("bad"), kind = "Magic" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("validation_error", problem.GetProperty("code").GetString());
        Assert.Equal("kind", problem.GetProperty("field").GetString());
    }
}
