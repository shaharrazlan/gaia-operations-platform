using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Gaia.Api.Tests.Logging;
using Microsoft.AspNetCore.TestHost;
using Serilog.Core;
using Serilog.Events;

namespace Gaia.Api.Tests.Api;

/// <summary>Add, change and remove need the management key; listing and running operations don't (R13).</summary>
public class ManagementKeyTests(GaiaApiFactory factory) : IClassFixture<GaiaApiFactory>
{
    private static readonly object NewOperation = new
    {
        id = "keyed",
        name = "Keyed",
        kind = "Arithmetic",
        fieldALabel = "A",
        fieldBLabel = "B",
        inputType = "Number",
        formula = "A + B",
    };

    private HttpClient Client(string? key)
    {
        var client = factory.CreateClient();
        if (key is not null)
            client.DefaultRequestHeaders.Add("X-Management-Key", key);
        return client;
    }

    public static TheoryData<string, string?> ManagementCalls => new()
    {
        // method, key sent (null = no header)
        { "POST", null }, { "POST", "wrong-key" },
        { "PUT", null }, { "PUT", "wrong-key" },
        { "DELETE", null }, { "DELETE", "wrong-key" },
        { "LIST-ALL", null }, { "LIST-ALL", "wrong-key" },
        { "LIST-ALL-REPEATED", null }, { "LIST-ALL-UPPER", null },   // no way around the check through the query
    };

    private static Task<HttpResponseMessage> Send(HttpClient client, string call) => call switch
    {
        "POST" => client.PostAsJsonAsync("/api/operations", NewOperation),
        "PUT" => client.PutAsJsonAsync("/api/operations/add", new { name = "Plus", version = "AAAAAAAAB9E=" }),
        "DELETE" => client.DeleteAsync("/api/operations/average"),
        "LIST-ALL" => client.GetAsync("/api/operations?includeDisabled=true"),
        "LIST-ALL-REPEATED" => client.GetAsync("/api/operations?includeDisabled=true&includeDisabled=true"),
        "LIST-ALL-UPPER" => client.GetAsync("/api/operations?includeDisabled=TRUE"),
        _ => throw new ArgumentOutOfRangeException(nameof(call)),
    };

    [Theory]
    [MemberData(nameof(ManagementCalls))]
    public async Task R13_AC1_management_calls_without_the_right_key_are_401_unauthorized_and_change_nothing(string call, string? key)
    {
        var before = await CatalogAsync();

        var response = await Send(Client(key), call);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains("X-Management-Key", response.Headers.WwwAuthenticate.ToString()); // HTTP: every 401 says how to authenticate
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("unauthorized", problem.GetProperty("code").GetString());
        Assert.Equal(before, await CatalogAsync());
    }

    [Fact]
    public async Task R13_AC2_with_the_right_key_management_works()
    {
        var client = Client(GaiaApiFactory.ManagementKey);

        var created = await client.PostAsJsonAsync("/api/operations", NewOperation);
        var all = await client.GetAsync("/api/operations?includeDisabled=true");
        var deleted = await client.DeleteAsync("/api/operations/keyed");

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(HttpStatusCode.OK, all.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
    }

    [Fact]
    public async Task R13_AC3_listing_enabled_operations_and_executing_need_no_key()
    {
        var client = Client(key: null);

        var list = await client.GetAsync("/api/operations");
        var listNotAll = await client.GetAsync("/api/operations?includeDisabled=false");
        var run = await client.PostAsJsonAsync("/api/operations/add/execute", new { fieldA = "1", fieldB = "2" });

        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.Equal(HttpStatusCode.OK, listNotAll.StatusCode);
        Assert.Equal(HttpStatusCode.OK, run.StatusCode);
    }

    private async Task<string> CatalogAsync() =>
        await Client(GaiaApiFactory.ManagementKey).GetStringAsync("/api/operations?includeDisabled=true");
}

/// <summary>A server with no key configured is closed for management, and says so at startup (R13.AC8).</summary>
public class NoManagementKeyTests : IClassFixture<NoManagementKeyTests.NoKeyFactory>
{
    private readonly NoKeyFactory _factory;

    public NoManagementKeyTests(NoKeyFactory factory) => _factory = factory;

    public sealed class NoKeyFactory : GaiaApiFactory
    {
        public ListSink Sink { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseSetting("Management:Key", "");
            builder.ConfigureTestServices(services => services.AddSingleton<ILogEventSink>(Sink));
        }
    }

    [Fact]
    public async Task R13_AC8_with_no_key_configured_management_is_401_even_with_a_header()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Management-Key", "");

        var response = await client.GetAsync("/api/operations?includeDisabled=true");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public void R13_AC8_with_no_key_configured_a_startup_warning_is_logged()
    {
        _factory.CreateClient(); // starts the server

        Assert.Contains(_factory.Sink.Events, e => e.Level == LogEventLevel.Warning && e.RenderMessage().Contains("Management:Key"));
    }
}
