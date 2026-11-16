using Microsoft.Azure.Azclips.Extensions.KeyVault.Customization;

namespace Extensions.KeyVault.Tests;

public class CertificateCommandInputsTests
{
    private const string NameMessage = "Certificate names must contain 1-127 letters, digits or hyphens.";
    private const string VersionMessage = "Certificate version must be an alphanumeric version identifier.";

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("bad_name")]
    [InlineData("caf\u00e9")]
    public void Validate_InvalidName_ThrowsArgumentException(string? name)
    {
        Exception? exception = Record.Exception(() => CertificateCommandInputs.Validate(name!));

        Assert.Equal(NameMessage, Assert.IsType<ArgumentException>(exception).Message);
    }

    [Fact]
    public void Validate_NameOf128Characters_ThrowsArgumentException()
    {
        string name = new('a', 128);

        Exception? exception = Record.Exception(() => CertificateCommandInputs.Validate(name));

        Assert.Equal(NameMessage, Assert.IsType<ArgumentException>(exception).Message);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(127)]
    public void Validate_NameAtLengthBoundary_DoesNotThrow(int length)
    {
        string name = new('a', length);

        Exception? exception = Record.Exception(() => CertificateCommandInputs.Validate(name));

        Assert.Null(exception);
    }

    // Current behaviour: the pattern's '$' anchor also matches before a single trailing newline.
    [Fact]
    public void Validate_NameWithSingleTrailingNewline_DoesNotThrow()
    {
        string name = "test\n";

        Exception? exception = Record.Exception(() => CertificateCommandInputs.Validate(name));

        Assert.Null(exception);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("bad-version")]
    [InlineData("v\u00e9")]
    public void Validate_InvalidVersion_ThrowsArgumentException(string version)
    {
        Exception? exception = Record.Exception(() => CertificateCommandInputs.Validate("test", version));

        Assert.Equal(VersionMessage, Assert.IsType<ArgumentException>(exception).Message);
    }

    [Fact]
    public void Validate_VersionOf65Characters_ThrowsArgumentException()
    {
        string version = new('a', 65);

        Exception? exception = Record.Exception(() => CertificateCommandInputs.Validate("test", version));

        Assert.Equal(VersionMessage, Assert.IsType<ArgumentException>(exception).Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("a")]
    public void Validate_NullOrValidVersion_DoesNotThrow(string? version)
    {
        Exception? exception = Record.Exception(() => CertificateCommandInputs.Validate("test", version));

        Assert.Null(exception);
    }

    [Fact]
    public void Validate_VersionOf64Characters_DoesNotThrow()
    {
        string version = new('a', 64);

        Exception? exception = Record.Exception(() => CertificateCommandInputs.Validate("test", version));

        Assert.Null(exception);
    }
}