using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Gaia.Api.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Gaia.Api.Tests.Api;

/// <summary>Edge cases found in the S4 review.</summary>
public class ApiEdgeTests(GaiaApiFactory factory) : IClassFixture<GaiaApiFactory>
{
    [Fact]
    public async Task R2_AC1_fields_may_be_sent_as_JSON_numbers()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/operations/divide/execute", new { fieldA = 7, fieldB = 2 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("3.5", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("result").GetString());
    }

    [Fact]
    public async Task R2_AC6_a_field_of_the_wrong_JSON_type_names_the_field()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/operations/add/execute", new { fieldA = new[] { 1 }, fieldB = "2" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("validation_error", problem.GetProperty("code").GetString());
        Assert.Equal("fieldA", problem.GetProperty("field").GetString());
    }

    [Theory]
    [InlineData("GET", "/api/nothing-here", 404, "route_not_found")]
    [InlineData("GET", "/api/operations/add/execute", 405, "method_not_allowed")]
    public async Task R2_AC6_framework_errors_also_carry_a_code(string method, string path, int status, string code)
    {
        var response = await factory.CreateClient().SendAsync(new HttpRequestMessage(new HttpMethod(method), path));

        Assert.Equal(status, (int)response.StatusCode);
        Assert.Equal(code, (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
    }

    [Fact]
    public async Task T20_a_request_cancelled_by_the_client_is_499_not_a_server_error()
    {
        using var aborted = new CancellationTokenSource();
        await aborted.CancelAsync();
        var context = new DefaultHttpContext { RequestAborted = aborted.Token };
        var mapper = ActivatorUtilities.CreateInstance<ProblemDetailsMapper>(factory.Services);

        var handled = await mapper.TryHandleAsync(context, new OperationCanceledException(), CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(499, context.Response.StatusCode);
    }

    [Theory]
    [InlineData("Server=(localdb)\\MSSQLLocalDB;Database=GaiaE2E;Trusted_Connection=True", true)]
    [InlineData("Server=(localdb)\\MSSQLLocalDB;Database=GaiaE2E_2;Trusted_Connection=True", true)]
    [InlineData("Server=(localdb)\\MSSQLLocalDB;Database=GaiaDev;Trusted_Connection=True", false)]
    public void T19_E2E_only_ever_deletes_a_database_named_GaiaE2E(string connectionString, bool allowed)
    {
        Assert.Equal(allowed, DatabaseStartup.IsE2EDatabase(connectionString));
    }
}
