using Core.Configuration.Abstractions;
using Core.Configuration.Implementation;
using Core.Configuration.Schema;

namespace Cli.Tests;

public class ConfigurationSchemaTests
{
    private readonly IConfigurationSchema _schema = new ConfigurationSchema();

    [Fact]
    public void GetAllKeys_ShouldContainCoreLoggingLevel()
    {
        var keys = _schema.GetAllKeys();

        Assert.Contains(keys, k => string.Equals(k.Key, "core.logging.level", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void IsKnownKey_ShouldReturnFalse_ForUnknownKey()
    {
        var known = _schema.IsKnownKey("core.notARealKey");

        Assert.False(known);
    }

    [Fact]
    public void ValidateKey_ShouldRejectUnknownKey_WhenForceIsFalse()
    {
        var validator = new DefaultConfigurationValidator(_schema);

        var result = validator.ValidateKey("core.notARealKey", force: false);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void ValidateKey_ShouldAllowUnknownKey_WhenForceIsTrue()
    {
        var validator = new DefaultConfigurationValidator(_schema);

        var result = validator.ValidateKey("core.notARealKey", force: true);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateValue_ShouldRejectInvalidAllowedValue()
    {
        var validator = new DefaultConfigurationValidator(_schema);

        var result = validator.ValidateValue("core.logging.level", "verbose");

        Assert.False(result.IsValid);
    }

    [Fact]
    public void ValidateValue_ShouldAcceptValidAllowedValue()
    {
        var validator = new DefaultConfigurationValidator(_schema);

        var result = validator.ValidateValue("core.logging.level", "debug");

        Assert.True(result.IsValid);
    }
}
