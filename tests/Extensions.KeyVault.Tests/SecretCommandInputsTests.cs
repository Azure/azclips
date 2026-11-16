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
    public void ValidateName_InvalidName_ThrowsArgumentException(string name)
    {
        Exception? exception = Record.Exception(() => SecretCommandInputs.ValidateName(name));

        Assert.Equal(NameMessage, Assert.IsType<ArgumentException>(exception).Message);
    }

    [Fact]
    public void ValidateName_NameOf128Characters_ThrowsArgumentException()
    {
        string name = new('a', 128);

        Exception? exception = Record.Exception(() => SecretCommandInputs.ValidateName(name));

        Assert.Equal(NameMessage, Assert.IsType<ArgumentException>(exception).Message);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(127)]
    public void ValidateName_NameAtLengthBoundary_DoesNotThrow(int length)
    {
        string name = new('a', length);

        Exception? exception = Record.Exception(() => SecretCommandInputs.ValidateName(name));

        Assert.Null(exception);
    }

    // Current behaviour: no null guard, so the framework ArgumentNullException surfaces.
    [Fact]
    public void ValidateName_Null_ThrowsArgumentNullException()
    {
        string name = null!;

        Exception? exception = Record.Exception(() => SecretCommandInputs.ValidateName(name));

        Assert.Equal("Value cannot be null. (Parameter 'input')", Assert.IsType<ArgumentNullException>(exception).Message);
    }

    // Current behaviour: the pattern's '$' anchor also matches before a single trailing newline.
    [Fact]
    public void ValidateName_SingleTrailingNewline_DoesNotThrow()
    {
        string name = "test\n";

        Exception? exception = Record.Exception(() => SecretCommandInputs.ValidateName(name));

        Assert.Null(exception);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("bad-version")]
    [InlineData("v\u00e9")]
    public void ValidateVersion_InvalidVersion_ThrowsArgumentException(string version)
    {
        Exception? exception = Record.Exception(() => SecretCommandInputs.ValidateVersion(version));

        Assert.Equal(VersionMessage, Assert.IsType<ArgumentException>(exception).Message);
    }

    [Fact]
    public void ValidateVersion_VersionOf65Characters_ThrowsArgumentException()
    {
        string version = new('a', 65);

        Exception? exception = Record.Exception(() => SecretCommandInputs.ValidateVersion(version));

        Assert.Equal(VersionMessage, Assert.IsType<ArgumentException>(exception).Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("a")]
    public void ValidateVersion_NullOrValid_DoesNotThrow(string? version)
    {
        Exception? exception = Record.Exception(() => SecretCommandInputs.ValidateVersion(version));

        Assert.Null(exception);
    }

    [Fact]
    public void ValidateVersion_VersionOf64Characters_DoesNotThrow()
    {
        string version = new('a', 64);

        Exception? exception = Record.Exception(() => SecretCommandInputs.ValidateVersion(version));

        Assert.Null(exception);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(26)]
    public void ValidatePageSize_OutOfRange_ThrowsArgumentException(int pageSize)
    {
        Exception? exception = Record.Exception(() => SecretCommandInputs.ValidatePageSize(pageSize));

        Assert.Equal(PageSizeMessage, Assert.IsType<ArgumentException>(exception).Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(1)]
    [InlineData(25)]
    public void ValidatePageSize_InRangeOrNull_DoesNotThrow(int? pageSize)
    {
        Exception? exception = Record.Exception(() => SecretCommandInputs.ValidatePageSize(pageSize));

        Assert.Null(exception);
    }

    [Theory]
    [InlineData("maybe")]
    [InlineData("")]
    [InlineData("1")]
    public void ParseEnabled_InvalidValue_ThrowsArgumentException(string enabled)
    {
        Exception? exception = Record.Exception(() => SecretCommandInputs.ParseEnabled(enabled));

        Assert.Equal(EnabledMessage, Assert.IsType<ArgumentException>(exception).Message);
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    [InlineData(null, null)]
    public void ParseEnabled_ValidOrNull_ReturnsValue(string? enabled, bool? expected)
    {
        bool? result = SecretCommandInputs.ParseEnabled(enabled);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("not-a-date")]
    [InlineData("")]
    [InlineData("1969-12-31T23:59:59Z")]
    [InlineData("2038-01-19T03:14:08Z")]
    public void ParseTime_InvalidOrOutOfRangeDate_ThrowsArgumentException(string text)
    {
        Exception? exception = Record.Exception(() => SecretCommandInputs.ParseTime(text));

        Assert.Equal(DateMessage, Assert.IsType<ArgumentException>(exception).Message);
    }

    [Theory]
    [InlineData("1970-01-01T00:00:00Z", 0)]
    [InlineData("2038-01-19T03:14:07Z", int.MaxValue)]
    [InlineData(null, null)]
    public void ParseTime_BoundaryOrNull_ReturnsUnixSeconds(string? text, int? expected)
    {
        int? result = SecretCommandInputs.ParseTime(text);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("novalue")]
    [InlineData("=value")]
    [InlineData("")]
    public void ParseTags_InvalidTag_ThrowsArgumentException(string tag)
    {
        Exception? exception = Record.Exception(() => SecretCommandInputs.ParseTags([tag]));

        Assert.Equal(TagsMessage, Assert.IsType<ArgumentException>(exception).Message);
    }

    [Fact]
    public void ParseTags_NameValue_ReturnsPair()
    {
        string[] tags = ["k=v"];

        var result = SecretCommandInputs.ParseTags(tags);

        Assert.Equal(new Dictionary<string, string> { ["k"] = "v" }, result);
    }

    [Fact]
    public void ParseTags_Null_ReturnsNull()
    {
        string[]? tags = null;

        var result = SecretCommandInputs.ParseTags(tags);

        Assert.Null(result);
    }
}