using System.Runtime.InteropServices;

namespace Amp.Sdk.Tests;

public class AmpCliLocatorTests
{
    private static readonly string Exe = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "amp.exe" : "amp";

    [Fact]
    public void ExplicitPathComesFirstThenSdkBinThenHomeBinThenPath()
    {
        var env = new Dictionary<string, string?>
        {
            ["AMP_CLI_PATH"] = "/explicit/amp",
            ["AMP_HOME"] = "/amphome",
            ["PATH"] = string.Join(Path.PathSeparator, "/usr/bin", "/opt/bin"),
        };
        var candidates = AmpCliLocator.CandidatePaths(k => env.GetValueOrDefault(k)).ToList();

        Assert.Equal("/explicit/amp", candidates[0]);
        Assert.Equal(Path.Combine("/amphome", "sdk", "bin", Exe), candidates[1]);
        var homeBin = Path.Combine("/amphome", "bin", Exe);
        var pathHit = Path.Combine("/usr/bin", Exe);
        Assert.True(candidates.IndexOf(homeBin) < candidates.IndexOf(pathHit));
        Assert.Contains(Path.Combine("/opt/bin", Exe), candidates);
    }

    [Fact]
    public void FindReturnsFirstExistingCandidate()
    {
        var home = Directory.CreateTempSubdirectory("amp-locator").FullName;
        try
        {
            var binDir = Directory.CreateDirectory(Path.Combine(home, "bin")).FullName;
            var ampPath = Path.Combine(binDir, Exe);
            File.WriteAllText(ampPath, "");

            var env = new Dictionary<string, string?> { ["AMP_HOME"] = home, ["PATH"] = "" };
            var found = AmpCliLocator.Find(k => env.GetValueOrDefault(k));

            Assert.NotNull(found);
            Assert.Equal(ampPath, found.Command);
            Assert.Empty(found.Args);
        }
        finally
        {
            Directory.Delete(home, recursive: true);
        }
    }

    [Fact]
    public void FindReturnsNullWhenNothingExists()
    {
        var env = new Dictionary<string, string?> { ["AMP_HOME"] = Path.Combine(Path.GetTempPath(), "does-not-exist-" + Guid.NewGuid()), ["PATH"] = "" };
        Assert.Null(AmpCliLocator.Find(k => env.GetValueOrDefault(k)));
    }
}
