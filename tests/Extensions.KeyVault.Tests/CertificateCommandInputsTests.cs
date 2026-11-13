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
    public void Validate_InvalidName_Throws(string? name)
    {
        var ex = Assert.Throws<ArgumentException>(() => CertificateCommandInputs.Validate(name!));

        Assert.Equal(NameMessage, ex.Message);
    }

    [Fact]
    public void Validate_NameOf128Characters_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => CertificateCommandInputs.Validate(new string('a', 128)));

        Assert.Equal(NameMessage, ex.Message);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(127)]
    public void Validate_NameAtLengthBoundary_DoesNotThrow(int length) =>
        Assert.Null(Record.Exception(() => CertificateCommandInputs.Validate(new string('a', length))));

    // Current behaviour: the pattern's '$' anchor also matches before a single trailing newline.
    [Fact]
    public void Validate_NameWithSingleTrailingNewline_DoesNotThrow() =>
        Assert.Null(Record.Exception(() => CertificateCommandInputs.Validate("test\n")));

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("bad-version")]
    [InlineData("v\u00e9")]
    public void Validate_InvalidVersion_Throws(string version)
    {
        var ex = Assert.Throws<ArgumentException>(() => CertificateCommandInputs.Validate("test", version));

        Assert.Equal(VersionMessage, ex.Message);
    }

    [Fact]
    public void Validate_VersionOf65Characters_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => CertificateCommandInputs.Validate("test", new string('a', 65)));

        Assert.Equal(VersionMessage, ex.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("a")]
    public void Validate_NullOrValidVersion_DoesNotThrow(string? version) =>
        Assert.Null(Record.Exception(() => CertificateCommandInputs.Validate("test", version)));

    [Fact]
    public void Validate_VersionOf64Characters_DoesNotThrow() =>
        Assert.Null(Record.Exception(() => CertificateCommandInputs.Validate("test", new string('a', 64))));
}