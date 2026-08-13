using Live.Tool;

namespace Live.Tool.Tests;

/// <summary>
/// The alias is the per-developer namespace: it names the state account, the state
/// container and every resource in the module. Terraform will accept a bad one and fail
/// much later with an opaque Azure naming error, so it is validated up front.
/// </summary>
public class AliasTests
{
    [Theory]
    [InlineData("mxaa")]
    [InlineData("ming")]
    [InlineData("some-dev")]
    [InlineData("abcdefghijklmn")] // 14 chars, the maximum
    public void Validate_AcceptsWellFormedAliases(string alias)
    {
        Tf.ValidateAlias(alias);
    }

    [Theory]
    [InlineData("", "empty")]
    [InlineData("ab", "too short for the 4-char minimum")]
    [InlineData("abcdefghijklmno", "15 chars would overflow the 24-char account name")]
    [InlineData("Ming", "uppercase is illegal in a storage account name")]
    [InlineData("1ming", "must start with a letter")]
    [InlineData("ming-", "must not end with a hyphen")]
    [InlineData("ming_dev", "underscore is illegal")]
    public void Validate_RejectsMalformedAliases(string alias, string why)
    {
        var ex = Assert.Throws<LiveToolException>(() => Tf.ValidateAlias(alias));
        Assert.Contains("alias", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(string.IsNullOrWhiteSpace(why));
    }

    [Theory]
    [InlineData("alias = \"ming\"", "ming")]
    [InlineData("alias=\"ming\"", "ming")]
    [InlineData("  alias   =  \"some-dev\"  # trailing comment", "some-dev")]
    [InlineData("# alias = \"commented\"\nalias = \"real\"", "real")]
    public void ReadAliasFromTfvars_ExtractsTheAssignment(string content, string expected)
    {
        Assert.Equal(expected, Tf.ParseAlias(content));
    }

    [Theory]
    [InlineData("")]
    [InlineData("location = \"westus2\"")]
    [InlineData("# alias = \"only-a-comment\"")]
    public void ReadAliasFromTfvars_ReturnsNullWhenAbsent(string content)
    {
        Assert.Null(Tf.ParseAlias(content));
    }

    /// <summary>
    /// The drift guard fires on a stale marker, so a destroy has to clear it — otherwise
    /// the next alias change is blocked by resources that no longer exist.
    /// </summary>
    [Fact]
    public void ClearAlias_RemovesTheMarkerAndTheDriftGuardStopsFiring()
    {
        var dir = Directory.CreateTempSubdirectory("livetool-alias");
        try
        {
            Tf.RecordAlias(dir.FullName, "ming");
            Assert.Throws<LiveToolException>(() => Tf.CheckAliasDrift(dir.FullName, "identity-ming"));

            Tf.ClearAlias(dir.FullName);

            Assert.False(File.Exists(Path.Combine(dir.FullName, ".alias")));
            Tf.CheckAliasDrift(dir.FullName, "identity-ming");
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Fact]
    public void ClearAlias_IsANoOpWhenThereIsNoMarker()
    {
        var dir = Directory.CreateTempSubdirectory("livetool-alias");
        try
        {
            Tf.ClearAlias(dir.FullName);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }
}
