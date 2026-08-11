using System.Text.RegularExpressions;

namespace Live.Tool;

/// <summary>
/// Resolves which GitHub repository to configure, and refuses to configure the wrong one.
/// Port of <c>require_gh_repo</c>.
/// </summary>
internal static partial class GitHubRepo
{
    private static readonly string[] ExpectedNames = ["cirrus", "azclips"];

    /// <summary>
    /// The shared upstream. The identity created here is per developer and grants
    /// Contributor over a subscription, so it must never be published there.
    /// </summary>
    private static readonly string[] UpstreamOwners = ["Azure"];

    [GeneratedRegex(@"^(?:git@github\.com:|https://github\.com/)([^/]+)/(.+?)(?:\.git)?$")]
    private static partial Regex OriginRegex();

    /// <summary>Returns <c>owner/repo</c> for the <c>origin</c> remote.</summary>
    public static string Require()
    {
        if (!Proc.Exists("gh"))
        {
            throw new LiveToolException("The GitHub CLI (gh) is not installed, or is not on PATH.");
        }

        if (!Proc.Run("gh", ["auth", "status"]).Ok)
        {
            throw new LiveToolException("Not signed in to GitHub. Run: gh auth login");
        }

        var url = Proc.Run("git", ["remote", "get-url", "origin"]).Out;
        if (string.IsNullOrEmpty(url))
        {
            throw new LiveToolException("No 'origin' git remote found; cannot tell which repository to configure.");
        }

        var (owner, name) = ParseOrigin(url)
            ?? throw new LiveToolException($"Could not parse a GitHub owner/repo out of origin: {url}");

        var repo = $"{owner}/{name}";

        if (!ExpectedNames.Contains(name, StringComparer.OrdinalIgnoreCase))
        {
            throw new LiveToolException(
                "'origin' does not look like your fork of this project.\n"
                + $"   origin: {repo}\n"
                + $"   Expected a repository named one of: {string.Join(", ", ExpectedNames)}");
        }

        if (UpstreamOwners.Contains(owner, StringComparer.OrdinalIgnoreCase))
        {
            throw new LiveToolException(
                $"Refusing to configure the shared upstream repository '{repo}'.\n"
                + "   This creates a per-developer identity and must point at your own fork.\n"
                + "   Set 'origin' to your fork (upstream stays as a separate remote).");
        }

        // Catches the case where origin is somebody else's fork: the variables would be
        // written to a repository whose workflows you do not control.
        var login = Proc.Run("gh", ["api", "user", "--jq", ".login"]).Out;
        if (!string.IsNullOrEmpty(login) && !string.Equals(login, owner, StringComparison.OrdinalIgnoreCase))
        {
            throw new LiveToolException(
                $"Signed in to GitHub as '{login}', but origin belongs to '{owner}'.\n"
                + $"   Sign in as '{owner}' (gh auth login), or point origin at your own fork.");
        }

        return repo;
    }

    /// <summary>
    /// Accepts both remote spellings: <c>git@github.com:owner/repo.git</c> and
    /// <c>https://github.com/owner/repo</c>, with or without the <c>.git</c> suffix.
    /// </summary>
    internal static (string Owner, string Name)? ParseOrigin(string url)
    {
        var match = OriginRegex().Match(url.Trim());
        return match.Success ? (match.Groups[1].Value, match.Groups[2].Value) : null;
    }
}
