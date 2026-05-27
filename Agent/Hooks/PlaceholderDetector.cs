using System.Text.RegularExpressions;

namespace Core.Agent.Hooks;

/// <summary>Detects unfilled placeholders in a candidate command line.</summary>
public static class PlaceholderDetector
{
    private static readonly Regex[] Patterns =
    {
        new(@"<[a-zA-Z][a-zA-Z0-9._-]{1,40}>", RegexOptions.Compiled),
        new(@"\{\{[^}]+\}\}", RegexOptions.Compiled),
        new(@"\b(YOUR|MY)_[A-Z][A-Z0-9_]*\b", RegexOptions.Compiled),
        new(@"\b[A-Za-z0-9_-]+-PLACEHOLDER\b", RegexOptions.Compiled),
    };

    public static bool TryDetect(string commandLine, out string match)
    {
        foreach (var re in Patterns)
        {
            var m = re.Match(commandLine);
            if (m.Success) { match = m.Value; return true; }
        }
        match = string.Empty;
        return false;
    }
}
