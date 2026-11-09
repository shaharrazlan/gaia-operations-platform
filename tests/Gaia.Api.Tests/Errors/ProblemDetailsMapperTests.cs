using System.Data.Common;
using Gaia.Api.Errors;
using Microsoft.EntityFrameworkCore;

namespace Gaia.Api.Tests.Errors;

public class ProblemDetailsMapperTests
{
    private sealed class FakeDbException() : DbException("connection lost");

    [Fact]
    public void S6_a_database_failure_while_saving_is_503_database_unavailable()
    {
        var problem = ProblemDetailsMapper.ToProblem(new DbUpdateException("save failed", new FakeDbException()));

        Assert.Equal(503, problem.Status);
        Assert.Equal("database_unavailable", problem.Extensions["code"]);
    }
}
