using Live.Tool;

namespace Live.Tool.Tests;

public class GitHubRepoTests
{
    [Theory]
    [InlineData("git@github.com:xuming-ms/cirrus.git", "xuming-ms", "cirrus")]
    [InlineData("git@github.com:xuming-ms/cirrus", "xuming-ms", "cirrus")]
    [InlineData("https://github.com/xuming-ms/cirrus.git", "xuming-ms", "cirrus")]
    [InlineData("https://github.com/xuming-ms/cirrus", "xuming-ms", "cirrus")]
    [InlineData("https://github.com/Azure/azclips.git", "Azure", "azclips")]
    public void ParseOrigin_AcceptsBothRemoteSpellings(string url, string owner, string name)
    {
        var parsed = GitHubRepo.ParseOrigin(url);

        Assert.NotNull(parsed);
        Assert.Equal(owner, parsed!.Value.Owner);
        Assert.Equal(name, parsed.Value.Name);
    }

    [Fact]
    public void ParseOrigin_ToleratesTrailingNewline()
    {
        // `git remote get-url` output arrives with one.
        Assert.NotNull(GitHubRepo.ParseOrigin("https://github.com/xuming-ms/cirrus\n"));
    }

    [Theory]
    [InlineData("git@gitlab.com:xuming-ms/cirrus.git")]
    [InlineData("/some/local/path")]
    [InlineData("")]
    public void ParseOrigin_RejectsNonGitHubRemotes(string url) =>
        Assert.Null(GitHubRepo.ParseOrigin(url));
}
