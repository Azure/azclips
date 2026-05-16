using System.Text;

namespace Core.Agent.Tests.Snapshot;

/// <summary>
/// Minimal snapshot harness: read / write / compare a text blob against a
/// baseline file under <c>.snapshots/</c>. On mismatch the actual output is
/// written to <c>.snapshots/&lt;name&gt;.actual.txt</c> and the test fails
/// with a unified-style diff. Set <c>UPDATE_SNAPSHOTS=1</c> in the test
/// runner environment to rebaseline (writes <c>&lt;name&gt;.txt</c> from
/// actual and passes).
///
/// Modeled after the main-repo spike commit 9159959c's SnapshotAssert, but
/// reimplemented because the spike runs against MAF's IChatClient stubs
/// while we feed the SDK's SessionEvent stream into EventRenderer directly.
/// </summary>
internal static class SnapshotAssert
{
    public static void Matches(string name, string actual)
    {
        var baselinePath = LocateBaseline(name);
        var actualPath   = Path.ChangeExtension(baselinePath, ".actual.txt");

        var update = string.Equals(
            Environment.GetEnvironmentVariable("UPDATE_SNAPSHOTS"), "1",
            StringComparison.Ordinal);

        if (!File.Exists(baselinePath) || update)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(baselinePath)!);
            File.WriteAllText(baselinePath, actual);
            if (File.Exists(actualPath)) File.Delete(actualPath);
            return;
        }

        var expected = File.ReadAllText(baselinePath);
        if (Normalize(expected) == Normalize(actual))
        {
            if (File.Exists(actualPath)) File.Delete(actualPath);
            return;
        }

        File.WriteAllText(actualPath, actual);
        throw new Xunit.Sdk.XunitException(
            $"Snapshot mismatch for '{name}'.\n" +
            $"Baseline: {baselinePath}\n" +
            $"Actual:   {actualPath}\n" +
            $"To rebaseline: set UPDATE_SNAPSHOTS=1 and rerun.\n" +
            BuildDiff(expected, actual));
    }

    private static string LocateBaseline(string name)
    {
        // Walk up from the test binary directory until we find the project
        // root (the folder that contains .snapshots/). Falls back to writing
        // next to the test dll if we can't locate one.
        var dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            var candidate = Path.Combine(dir, ".snapshots");
            if (Directory.Exists(candidate))
                return Path.Combine(candidate, name + ".txt");

            var parent = Directory.GetParent(dir);
            if (parent is null) break;

            // Bail at the test project root (csproj sibling).
            if (Directory.GetFiles(parent.FullName, "*.csproj").Length > 0)
                return Path.Combine(parent.FullName, ".snapshots", name + ".txt");

            dir = parent.FullName;
        }

        return Path.Combine(AppContext.BaseDirectory, ".snapshots", name + ".txt");
    }

    // .gitattributes normalizes snapshots to LF on commit but Windows checkout
    // may materialize them as CRLF, so compare line-ending-agnostically.
    private static string Normalize(string s) => s.Replace("\r\n", "\n");

    private static string BuildDiff(string expected, string actual)
    {
        var sb = new StringBuilder();
        sb.AppendLine("--- expected");
        sb.AppendLine(expected);
        sb.AppendLine("+++ actual");
        sb.AppendLine(actual);
        return sb.ToString();
    }
}
