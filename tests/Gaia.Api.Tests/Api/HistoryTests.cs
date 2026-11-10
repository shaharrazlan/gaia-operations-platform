using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace Gaia.Api.Tests.Api;

/// <summary>Every run is stored (R7); history comes back with each execute (R8, R9).</summary>
public class HistoryTests(GaiaApiFactory factory) : IClassFixture<GaiaApiFactory>
{
    /// <summary>A fresh custom "divide" per test, so each test's history starts empty.</summary>
    private async Task<string> NewDivideAsync()
    {
        var id = $"div-{Guid.NewGuid():N}"[..20];
        var response = await factory.CreateManagementClient().PostAsJsonAsync("/api/operations", new
        {
            id,
            name = id,
            kind = "Arithmetic",
            fieldALabel = "A",
            fieldBLabel = "B",
            inputType = "Number",
            formula = "A / B",
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return id;
    }

    private Task<HttpResponseMessage> ExecuteAsync(string id, string a, string b) =>
        factory.CreateClient().PostAsJsonAsync($"/api/operations/{id}/execute", new { fieldA = a, fieldB = b });

    [Fact]
    public async Task R7_AC1_success_and_failure_runs_are_stored_with_all_fields()
    {
        var id = await NewDivideAsync();
        var before = DateTime.UtcNow.AddSeconds(-1);

        Assert.Equal(HttpStatusCode.OK, (await ExecuteAsync(id, "7", "2")).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await ExecuteAsync(id, "5", "0")).StatusCode);

        await using var db = factory.CreateDbContext();
        var runs = await db.Runs.AsNoTracking().Where(r => r.OperationId == id).OrderBy(r => r.Id).ToListAsync();
        Assert.Equal(2, runs.Count);
        var (ok, failed) = (runs[0], runs[1]);
        Assert.Equal(("7", "2", "3.5", true, (string?)null), (ok.FieldA, ok.FieldB, ok.Result, ok.IsSuccess, ok.ErrorCode));
        Assert.Equal(("5", "0", (string?)null, false, "domain_error"), (failed.FieldA, failed.FieldB, failed.Result, failed.IsSuccess, failed.ErrorCode));
        Assert.Equal("Division by zero.", failed.ErrorMessage);
        Assert.All(runs, r => Assert.InRange(r.CreatedAtUtc, before, DateTime.UtcNow.AddSeconds(1)));
        Assert.All(runs, r => Assert.Equal(DateTimeKind.Utc, r.CreatedAtUtc.Kind));
    }
}
