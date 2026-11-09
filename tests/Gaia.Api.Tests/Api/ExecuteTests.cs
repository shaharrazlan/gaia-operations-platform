using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Gaia.Core.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Gaia.Api.Tests.Api;

public class ExecuteTests(GaiaApiFactory factory) : IClassFixture<GaiaApiFactory>
{
    private Task<HttpResponseMessage> Execute(string id, object? body) =>
        factory.CreateClient().PostAsJsonAsync($"/api/operations/{id}/execute", body);

    private static async Task<JsonElement> Json(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>())!;

    [Fact]
    public async Task R2_AC1_POST_execute_divide_7_2_returns_3_5()
    {
        var response = await Execute("divide", new { fieldA = "7", fieldB = "2" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await Json(response);
        Assert.Equal("divide", body.GetProperty("operationId").GetString());
        Assert.Equal("3.5", body.GetProperty("result").GetString());
    }

    [Fact]
    public async Task R2_AC2_unknown_id_returns_404_operation_not_found_naming_the_id()
    {
        var response = await Execute("nope", new { fieldA = "1", fieldB = "2" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await Json(response);
        Assert.Equal("operation_not_found", problem.GetProperty("code").GetString());
        Assert.Contains("'nope'", problem.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task R2_AC3_missing_fieldA_returns_400_naming_the_field()
    {
        var response = await Execute("add", new { fieldB = "2" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await Json(response);
        Assert.Equal("validation_error", problem.GetProperty("code").GetString());
        Assert.Equal("fieldA", problem.GetProperty("field").GetString());
    }

    [Fact]
    public async Task R2_AC4_non_number_for_a_number_operation_returns_400_validation_error()
    {
        var response = await Execute("add", new { fieldA = "abc", fieldB = "2" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await Json(response);
        Assert.Equal("validation_error", problem.GetProperty("code").GetString());
        Assert.Equal("fieldA", problem.GetProperty("field").GetString());
    }

    [Fact]
    public async Task R2_AC5_divide_by_zero_returns_422_domain_error()
    {
        var response = await Execute("divide", new { fieldA = "5", fieldB = "0" });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var problem = await Json(response);
        Assert.Equal("domain_error", problem.GetProperty("code").GetString());
        Assert.Contains("Division by zero", problem.GetProperty("detail").GetString());
    }

    [Theory]
    [InlineData("nope", "{\"fieldA\":\"1\",\"fieldB\":\"2\"}", 404, "operation_not_found")]
    [InlineData("add", "{\"fieldA\":\"x\",\"fieldB\":\"2\"}", 400, "validation_error")]
    [InlineData("divide", "{\"fieldA\":\"1\",\"fieldB\":\"0\"}", 422, "domain_error")]
    [InlineData("add", "not json", 400, "validation_error")]
    [InlineData("add", "", 400, "validation_error")]
    public async Task R2_AC6_every_error_is_a_problem_with_a_machine_readable_code(string id, string body, int status, string code)
    {
        var content = new StringContent(body, Encoding.UTF8, "application/json");

        var response = await factory.CreateClient().PostAsync($"/api/operations/{id}/execute", content);

        Assert.Equal(status, (int)response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(code, (await Json(response)).GetProperty("code").GetString());
    }

    [Theory]
    [InlineData("hypotenuse", "3", "4")]
    [InlineData("concat", "foo", "bar")]
    public async Task R6_AC2_HTTP_result_equals_the_service_result_for_the_same_inputs(string id, string a, string b)
    {
        using var scope = factory.Services.CreateScope();
        var direct = await scope.ServiceProvider.GetRequiredService<OperationService>().ExecuteAsync(id, a, b);

        var overHttp = await Json(await Execute(id, new { fieldA = a, fieldB = b }));

        Assert.Equal(direct.Result, overHttp.GetProperty("result").GetString());
    }
}

public class DatabaseDownTests
{
    [Fact]
    public async Task T20_database_down_returns_503_database_unavailable_without_internals()
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("ConnectionStrings:Gaia",
                "Server=(localdb)\\NoSuchGaiaInstance;Database=Nothing;Trusted_Connection=True;Connect Timeout=3");
        });

        var response = await factory.CreateClient().GetAsync("/api/operations");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync();
        Assert.Contains("database_unavailable", text);
        Assert.DoesNotContain("NoSuchGaiaInstance", text);
        Assert.DoesNotContain("   at ", text); // no stack trace
    }
}
