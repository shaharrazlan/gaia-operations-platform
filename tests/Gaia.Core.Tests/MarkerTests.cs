namespace Gaia.Core.Tests;

/// <summary>The assignment requires the comment "A34D" in the code (spec C4, NFR5). This keeps it from disappearing.</summary>
public class MarkerTests
{
    [Fact]
    public void NFR5_an_A34D_comment_exists_in_the_server_code()
    {
        var apiFolder = Path.Combine(RepositoryRoot(), "src", "Gaia.Api");

        var hasMarker = Directory.EnumerateFiles(apiFolder, "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .SelectMany(File.ReadLines)
            .Any(line => line.TrimStart().StartsWith("// A34D", StringComparison.Ordinal));

        Assert.True(hasMarker, "Expected a line starting with '// A34D' in a .cs file under src/Gaia.Api.");
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Gaia.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Gaia.sln not found above the test folder.");
    }
}
