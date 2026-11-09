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

    // --- PUT (T31) ---

    private static object Change(JsonElement loaded, string? formula = null, string? name = null) => new
    {
        id = loaded.GetProperty("id").GetString(),
        name = name ?? loaded.GetProperty("name").GetString(),
        kind = loaded.GetProperty("kind").GetString(),
        fieldALabel = loaded.GetProperty("fieldALabel").GetString(),
        fieldBLabel = loaded.GetProperty("fieldBLabel").GetString(),
        inputType = loaded.GetProperty("inputType").GetString(),
        formula = formula ?? loaded.GetProperty("formula").GetString(),
        isEnabled = loaded.GetProperty("isEnabled").GetBoolean(),
        version = loaded.GetProperty("version").GetString(),
    };

    private async Task<JsonElement> LoadAsync(string id) =>
        (await _client.GetFromJsonAsync<JsonElement[]>("/api/operations?includeDisabled=true"))!
        .Single(o => o.GetProperty("id").GetString() == id);

    [Fact]
    public async Task R4_AC2_a_changed_formula_is_used_by_the_next_execute()
    {
        var id = NewId("mean");
        var loaded = await CreateAsync(id);

        var response = await _client.PutAsJsonAsync($"/api/operations/{id}", Change(loaded, formula: "A * B"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("24", await ExecuteAsync(id, "4", "6"));
    }

    [Theory]
    [InlineData("divide", "A * B")]
    [InlineData("weather", "A + B")]
    public async Task R4_AC6_changing_a_system_formula_is_400_system_logic_readonly(string id, string formula)
    {
        var loaded = await LoadAsync(id);

        var response = await _client.PutAsJsonAsync($"/api/operations/{id}", Change(loaded, formula: formula));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("system_logic_readonly", await CodeOf(response));
    }

    [Fact]
    public async Task R4_AC6_a_system_operation_can_be_renamed()
    {
        var loaded = await LoadAsync("multiply");

        var response = await _client.PutAsJsonAsync("/api/operations/multiply", Change(loaded, name: "Times"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Times", (await LoadAsync("multiply")).GetProperty("name").GetString());
        Assert.Equal("12", await ExecuteAsync("multiply", "3", "4"));
    }

    [Fact]
    public async Task R4_AC7_two_saves_from_the_same_version_200_then_409_and_the_first_values_are_kept()
    {
        var id = NewId("race");
        var loaded = await CreateAsync(id);

        var first = await _client.PutAsJsonAsync($"/api/operations/{id}", Change(loaded, name: "First"));
        var second = await _client.PutAsJsonAsync($"/api/operations/{id}", Change(loaded, name: "Second"));

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal("edit_conflict", await CodeOf(second));
        Assert.Equal("First", (await LoadAsync(id)).GetProperty("name").GetString());
    }

    [Fact]
    public async Task R4_AC7_a_save_based_on_the_fresh_version_succeeds()
    {
        var id = NewId("fresh");
        var loaded = await CreateAsync(id);
        var first = await _client.PutAsJsonAsync($"/api/operations/{id}", Change(loaded, name: "First"));
        var fresh = await first.Content.ReadFromJsonAsync<JsonElement>();

        var second = await _client.PutAsJsonAsync($"/api/operations/{id}", Change(fresh, name: "Second"));

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal("Second", (await LoadAsync(id)).GetProperty("name").GetString());
    }

    [Fact]
    public async Task T31_PUT_on_an_unknown_id_is_404()
    {
        var response = await _client.PutAsJsonAsync("/api/operations/no-such-op", Definition("no-such-op"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("operation_not_found", await CodeOf(response));
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
