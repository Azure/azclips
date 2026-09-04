using Core.Abstractions;
using Xunit;

namespace Cli.Tests;

public class ProfileNameValidatorTests
{
    [Theory]
    [InlineData("prod")]
    [InlineData("my-prod.env_01")]
    [InlineData("a")]
    [InlineData("default")] // charset/length valid; reserved-ness is a separate rule
    public void IsValid_ShouldAccept_WellFormedNames(string name)
    {
        Assert.True(ProfileNameValidator.IsValid(name));
    }

    [Theory]
    [InlineData("")]
    [InlineData("my profile!")]
    [InlineData("has space")]
    [InlineData("bad/slash")]
    public void IsValid_ShouldReject_MalformedNames(string name)
    {
        Assert.False(ProfileNameValidator.IsValid(name));
    }

    [Fact]
    public void IsValid_ShouldReject_NamesLongerThan100()
    {
        var name = new string('a', 101);
        Assert.False(ProfileNameValidator.IsValid(name));
    }

    [Fact]
    public void IsValid_ShouldAccept_NameOfExactly100()
    {
        var name = new string('a', 100);
        Assert.True(ProfileNameValidator.IsValid(name));
    }

    [Theory]
    [InlineData("default", true)]
    [InlineData("DEFAULT", true)]
    [InlineData("prod", false)]
    public void IsReserved_ShouldDetectDefault_CaseInsensitively(string name, bool expected)
    {
        Assert.Equal(expected, ProfileNameValidator.IsReserved(name));
    }

    [Fact]
    public void Validate_ShouldThrow_OnMalformedName()
    {
        Assert.Throws<ArgumentException>(() => ProfileNameValidator.Validate("bad name!"));
    }

    [Fact]
    public void Validate_ShouldAllow_DefaultName()
    {
        // Validate enforces only charset/length; "default" is well-formed.
        ProfileNameValidator.Validate("default");
    }

    [Fact]
    public void ValidateForCreation_ShouldThrow_OnReservedDefaultName()
    {
        Assert.Throws<ArgumentException>(() => ProfileNameValidator.ValidateForCreation("default"));
    }

    [Fact]
    public void ValidateForCreation_ShouldAllow_NormalName()
    {
        ProfileNameValidator.ValidateForCreation("prod");
    }
}
