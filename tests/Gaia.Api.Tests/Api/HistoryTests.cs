using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Gaia.Core.History;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Gaia.Api.Tests.Api;

/// <summary>The API with a fixed clock: "now" is 15 Nov 2026 10:00 UTC, so month boundaries are deterministic.</summary>
public sealed class FixedClockFactory : GaiaApiFactory
{
    public static readonly DateTime Now = new(2026, 11, 15, 10, 0, 0, DateTimeKind.Utc);

    /// <summary>1 Nov 2026 00:00 in Israel (UTC+2) = 31 Oct 22:00 UTC.</summary>
    public static readonly DateTime MonthStartUtc = new(2026, 10, 31, 22, 0, 0, DateTimeKind.Utc);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services => services.AddSingleton<TimeProvider>(new FixedTime(Now)));
    }

    private sealed class FixedTime(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow);
    }
}

/// <summary>Every run is stored (R7); history comes back with each execute (R8, R9).</summary>
public class HistoryTests(FixedClockFactory factory) : IClassFixture<FixedClockFactory>
{
    /// <summary>A fresh custom operation per test, so each test's history starts empty.</summary>
    private async Task<string> NewOperationAsync(string formula = "A / B")
    {
        var id = $"op-{Guid.NewGuid():N}"[..20];
        var response = await factory.CreateManagementClient().PostAsJsonAsync("/api/operations", new
        {
            id,
            name = id,
            kind = "Arithmetic",
            fieldALabel = "A",
            fieldBLabel = "B",
            inputType = "Number",
            formula,
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return id;
    }

    private Task<HttpResponseMessage> ExecuteAsync(string id, string a, string b, HttpClient? client = null) =>
        (client ?? factory.CreateClient()).PostAsJsonAsync($"/api/operations/{id}/execute", new { fieldA = a, fieldB = b });

    /// <summary>Runs the operation successfully and returns the response's "history".</summary>
    private async Task<JsonElement> HistoryAfterAsync(string id, string a, string b, HttpClient? client = null)
    {
        var response = await ExecuteAsync(id, a, b, client);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("history");
    }

    private static List<string?> PreviousFieldA(JsonElement history) =>
        history.GetProperty("previousRuns").EnumerateArray().Select(r => r.GetProperty("fieldA").GetString()).ToList();

    private async Task InsertRunsAsync(string id, int count, DateTime createdAtUtc, bool success = true)
    {
        await using var db = factory.CreateDbContext();
        for (var i = 0; i < count; i++)
            db.Runs.Add(new OperationRun { OperationId = id, FieldA = "old", FieldB = "1", Result = success ? "1" : null, IsSuccess = success, CreatedAtUtc = createdAtUtc });
        await db.SaveChangesAsync();
    }

    // --- R7: storing runs ---

    [Fact]
    public async Task R7_AC1_success_and_failure_runs_are_stored_with_all_fields()
    {
        var id = await NewOperationAsync();

        Assert.Equal(HttpStatusCode.OK, (await ExecuteAsync(id, "7", "2")).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await ExecuteAsync(id, "5", "0")).StatusCode);

        await using var db = factory.CreateDbContext();
        var runs = await db.Runs.AsNoTracking().Where(r => r.OperationId == id).OrderBy(r => r.Id).ToListAsync();
        Assert.Equal(2, runs.Count);
        var (ok, failed) = (runs[0], runs[1]);
        Assert.Equal(("7", "2", "3.5", true, (string?)null), (ok.FieldA, ok.FieldB, ok.Result, ok.IsSuccess, ok.ErrorCode));
        Assert.Equal(("5", "0", (string?)null, false, "domain_error"), (failed.FieldA, failed.FieldB, failed.Result, failed.IsSuccess, failed.ErrorCode));
        Assert.Equal("Division by zero.", failed.ErrorMessage);
        Assert.All(runs, r => Assert.Equal(FixedClockFactory.Now, r.CreatedAtUtc));
        Assert.All(runs, r => Assert.Equal(DateTimeKind.Utc, r.CreatedAtUtc.Kind));
    }

    [Fact]
    public async Task R7_AC2_runs_survive_a_restart_a_new_server_on_the_same_database_sees_them()
    {
        var id = await NewOperationAsync();
        await HistoryAfterAsync(id, "8", "2");

        await using var restarted = factory.WithWebHostBuilder(_ => { });
        var history = await HistoryAfterAsync(id, "9", "3", restarted.CreateClient());

        Assert.Equal(["8"], PreviousFieldA(history));
    }

    // --- R8: the 3 previous successful runs ---

    [Fact]
    public async Task R8_AC1_with_4_earlier_runs_the_3_newest_earlier_runs_come_back_newest_first_without_the_current_one()
    {
        var id = await NewOperationAsync();
        foreach (var a in new[] { "1", "2", "3", "4" })
            await HistoryAfterAsync(id, a, "1");

        var history = await HistoryAfterAsync(id, "5", "1");

        Assert.Equal(["4", "3", "2"], PreviousFieldA(history));
    }

    [Fact]
    public async Task R8_AC2_with_no_earlier_runs_the_list_is_empty_and_with_2_both_come_back()
    {
        var id = await NewOperationAsync();

        Assert.Empty(PreviousFieldA(await HistoryAfterAsync(id, "1", "1")));
        await HistoryAfterAsync(id, "2", "1");
        Assert.Equal(["2", "1"], PreviousFieldA(await HistoryAfterAsync(id, "3", "1")));
    }

    [Fact]
    public async Task R8_AC3_runs_of_other_operations_never_appear()
    {
        var id = await NewOperationAsync();
        var other = await NewOperationAsync();
        await HistoryAfterAsync(other, "99", "1");

        Assert.Empty(PreviousFieldA(await HistoryAfterAsync(id, "1", "1")));
    }

    [Fact]
    public async Task R8_AC4_each_previous_run_has_fieldA_fieldB_result_and_its_UTC_time()
    {
        var id = await NewOperationAsync();
        await HistoryAfterAsync(id, "7", "2");

        var run = (await HistoryAfterAsync(id, "9", "3")).GetProperty("previousRuns")[0];

        Assert.Equal("7", run.GetProperty("fieldA").GetString());
        Assert.Equal("2", run.GetProperty("fieldB").GetString());
        Assert.Equal("3.5", run.GetProperty("result").GetString());
        Assert.Equal(FixedClockFactory.Now, run.GetProperty("createdAtUtc").GetDateTime().ToUniversalTime());
        Assert.EndsWith("Z", run.GetProperty("createdAtUtc").GetString());
    }

    [Fact]
    public async Task R8_AC5_failed_runs_never_appear()
    {
        var id = await NewOperationAsync();
        await HistoryAfterAsync(id, "6", "2");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await ExecuteAsync(id, "5", "0")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await ExecuteAsync(id, "abc", "1")).StatusCode);

        Assert.Equal(["6"], PreviousFieldA(await HistoryAfterAsync(id, "8", "2")));
    }

    // --- R9: the monthly count ---

    [Fact]
    public async Task R9_AC1_counts_runs_this_Israel_month_including_the_current_one_and_not_earlier_months()
    {
        var id = await NewOperationAsync();
        await InsertRunsAsync(id, 3, FixedClockFactory.MonthStartUtc.AddMinutes(-1));   // 31 Oct 23:59 Israel time: last month
        await InsertRunsAsync(id, 1, FixedClockFactory.MonthStartUtc);                  // 1 Nov 00:00 Israel time: this month
        await HistoryAfterAsync(id, "1", "1");

        var history = await HistoryAfterAsync(id, "2", "1");

        Assert.Equal(3, history.GetProperty("monthCount").GetInt32());   // the 00:00 one + 2 runs now (incl. the current)
    }

    [Fact]
    public async Task R9_AC2_other_operations_and_failed_runs_are_not_counted()
    {
        var id = await NewOperationAsync();
        var other = await NewOperationAsync();
        await HistoryAfterAsync(other, "1", "1");
        await InsertRunsAsync(id, 2, FixedClockFactory.Now, success: false);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await ExecuteAsync(id, "5", "0")).StatusCode);

        var history = await HistoryAfterAsync(id, "4", "2");

        Assert.Equal(1, history.GetProperty("monthCount").GetInt32());
    }

    [Fact]
    public async Task R9_AC3_the_monthly_count_is_part_of_the_API_response()
    {
        var id = await NewOperationAsync();

        var history = await HistoryAfterAsync(id, "4", "2");

        Assert.Equal(JsonValueKind.Number, history.GetProperty("monthCount").ValueKind);
        Assert.Equal(1, history.GetProperty("monthCount").GetInt32());
    }
}
