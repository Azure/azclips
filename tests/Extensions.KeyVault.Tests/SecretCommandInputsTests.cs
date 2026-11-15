using Microsoft.Azure.Azclips.Extensions.KeyVault.Customization;

namespace Extensions.KeyVault.Tests;

public class SecretCommandInputsTests
{
    private const string NameMessage = "Secret names must contain 1-127 letters, digits or hyphens.";
    private const string VersionMessage = "Secret version must be an alphanumeric version identifier.";
    private const string PageSizeMessage = "Page size must be between 1 and 25.";
    private const string EnabledMessage = "Enabled must be true or false.";
    private const string DateMessage = "Dates must be UTC timestamps between 1970 and January 2038 supported by the selected API.";
    private const string TagsMessage = "Tags must use NAME=VALUE syntax.";

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("bad_name")]
    [InlineData("caf\u00e9")]
    public void ValidateName_InvalidName_Throws(string name)
    {
        var ex = Assert.Throws<ArgumentException>(() => SecretCommandInputs.ValidateName(name));

        Assert.Equal(NameMessage, ex.Message);
    }

    [Fact]
    public void ValidateName_NameOf128Characters_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => SecretCommandInputs.ValidateName(new string('a', 128)));

        Assert.Equal(NameMessage, ex.Message);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(127)]
    public void ValidateName_NameAtLengthBoundary_DoesNotThrow(int length) =>
        Assert.Null(Record.Exception(() => SecretCommandInputs.ValidateName(new string('a', length))));

    // Current behaviour: no null guard, so the framework ArgumentNullException surfaces.
    [Fact]
    public void ValidateName_Null_ThrowsArgumentNullException()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => SecretCommandInputs.ValidateName(null!));

        Assert.Equal("Value cannot be null. (Parameter 'input')", ex.Message);
    }

    // Current behaviour: the pattern's '$' anchor also matches before a single trailing newline.
    [Fact]
    public void ValidateName_SingleTrailingNewline_DoesNotThrow() =>
        Assert.Null(Record.Exception(() => SecretCommandInputs.ValidateName("test\n")));

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("bad-version")]
    [InlineData("v\u00e9")]
    public void ValidateVersion_InvalidVersion_Throws(string version)
    {
        var ex = Assert.Throws<ArgumentException>(() => SecretCommandInputs.ValidateVersion(version));

        Assert.Equal(VersionMessage, ex.Message);
    }

    [Fact]
    public void ValidateVersion_VersionOf65Characters_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => SecretCommandInputs.ValidateVersion(new string('a', 65)));

        Assert.Equal(VersionMessage, ex.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("a")]
    public void ValidateVersion_NullOrValid_DoesNotThrow(string? version) =>
        Assert.Null(Record.Exception(() => SecretCommandInputs.ValidateVersion(version)));

    [Fact]
    public void ValidateVersion_VersionOf64Characters_DoesNotThrow() =>
        Assert.Null(Record.Exception(() => SecretCommandInputs.ValidateVersion(new string('a', 64))));

    [Theory]
    [InlineData(0)]
    [InlineData(26)]
    public void ValidatePageSize_OutOfRange_Throws(int pageSize)
    {
        var ex = Assert.Throws<ArgumentException>(() => SecretCommandInputs.ValidatePageSize(pageSize));

        Assert.Equal(PageSizeMessage, ex.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(1)]
    [InlineData(25)]
    public void ValidatePageSize_InRangeOrNull_DoesNotThrow(int? pageSize) =>
        Assert.Null(Record.Exception(() => SecretCommandInputs.ValidatePageSize(pageSize)));

    [Theory]
    [InlineData("maybe")]
    [InlineData("")]
    [InlineData("1")]
    public void ParseEnabled_InvalidValue_Throws(string enabled)
    {
        var ex = Assert.Throws<ArgumentException>(() => SecretCommandInputs.ParseEnabled(enabled));

        Assert.Equal(EnabledMessage, ex.Message);
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    [InlineData(null, null)]
    public void ParseEnabled_ValidOrNull_ReturnsValue(string? enabled, bool? expected) =>
        Assert.Equal(expected, SecretCommandInputs.ParseEnabled(enabled));

    [Theory]
    [InlineData("not-a-date")]
    [InlineData("")]
    [InlineData("1969-12-31T23:59:59Z")]
    [InlineData("2038-01-19T03:14:08Z")]
    public void ParseTime_InvalidOrOutOfRangeDate_Throws(string text)
    {
        var ex = Assert.Throws<ArgumentException>(() => SecretCommandInputs.ParseTime(text));

        Assert.Equal(DateMessage, ex.Message);
    }

    [Theory]
    [InlineData("1970-01-01T00:00:00Z", 0)]
    [InlineData("2038-01-19T03:14:07Z", int.MaxValue)]
    [InlineData(null, null)]
    public void ParseTime_BoundaryOrNull_ReturnsUnixSeconds(string? text, int? expected) =>
        Assert.Equal(expected, SecretCommandInputs.ParseTime(text));

    [Theory]
    [InlineData("novalue")]
    [InlineData("=value")]
    [InlineData("")]
    public void ParseTags_InvalidTag_Throws(string tag)
    {
        var ex = Assert.Throws<ArgumentException>(() => SecretCommandInputs.ParseTags([tag]));

        Assert.Equal(TagsMessage, ex.Message);
    }

    [Fact]
    public void ParseTags_NameValue_ReturnsPair() =>
        Assert.Equal(new Dictionary<string, string> { ["k"] = "v" }, SecretCommandInputs.ParseTags(["k=v"]));

    [Fact]
    public void ParseTags_Null_ReturnsNull() =>
        Assert.Null(SecretCommandInputs.ParseTags(null));
}