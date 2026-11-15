using System.Text.Json;
using Microsoft.Azure.Azclips.Extensions.KeyVault.Customization;
using static Extensions.KeyVault.Tests.KeyVaultCommandTestHelpers;

namespace Extensions.KeyVault.Tests;

public class UpdateSecretCommandTests
{
    [Fact]
    public async Task InheritedAttributesArePreserved()
    {
        var command = new UpdateSecretCommand
        {
            SecretName = "test", SecretAttributesEnabled = false,
            SecretAttributesNotBefore = 1700000000, SecretAttributesExpires = 1800000000,
            Tags = new Dictionary<string, string> { ["source"] = "inherited" }
        };
        Initialize(command, "{}", async request =>
        {
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
            var attributes = body.RootElement.GetProperty("attributes");
            Assert.False(attributes.GetProperty("enabled").GetBoolean());
            Assert.Equal(1700000000, attributes.GetProperty("nbf").GetInt32());
            Assert.Equal(1800000000, attributes.GetProperty("exp").GetInt32());
            Assert.Equal("inherited", body.RootElement.GetProperty("tags").GetProperty("source").GetString());
        });
        await command.ProcessAsync();
    }

    [Fact]
    public async Task InheritedEmptyTagsAreAnUpdate()
    {
        var command = new UpdateSecretCommand { SecretName = "test", Tags = new Dictionary<string, string>() };
        Initialize(command, "{}", async request =>
        {
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
            Assert.Empty(body.RootElement.GetProperty("tags").EnumerateObject());
        });
        await command.ProcessAsync();
    }

    [Fact]
    public async Task ExplicitConvenienceInputOverridesOnlyThatAttribute()
    {
        var command = new UpdateSecretCommand
        {
            SecretName = "test", SecretAttributesEnabled = false, Enabled = "true",
            SecretAttributesExpires = 1800000000
        };
        Initialize(command, "{}", async request =>
        {
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
            Assert.True(body.RootElement.GetProperty("attributes").GetProperty("enabled").GetBoolean());
            Assert.Equal(1800000000, body.RootElement.GetProperty("attributes").GetProperty("exp").GetInt32());
        });
        await command.ProcessAsync();
    }

    [Fact]
    public async Task UpdateDoesNotSendValueOrUnspecifiedAttributes()
    {
        var command = new UpdateSecretCommand { SecretName = "test", Enabled = "false" };
        Initialize(command, "{}", async request =>
        {
            Assert.Equal(HttpMethod.Patch, request.Method);
            using var json = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
            Assert.False(json.RootElement.TryGetProperty("value", out _));
            Assert.False(json.RootElement.TryGetProperty("tags", out _));
            Assert.False(json.RootElement.GetProperty("attributes").GetProperty("enabled").GetBoolean());
            Assert.Single(json.RootElement.GetProperty("attributes").EnumerateObject());
        });
        await command.ProcessAsync();
    }

    [Fact]
    public Task SecretName_IsValidated() =>
        AssertRejectedBeforeNetworkAsync(new UpdateSecretCommand { SecretName = "bad_name", Enabled = "true" },
            "Secret names must contain 1-127 letters, digits or hyphens.");

    [Fact]
    public Task SecretVersion_IsValidated() =>
        AssertRejectedBeforeNetworkAsync(new UpdateSecretCommand { SecretName = "test", SecretVersion = "bad-version", Enabled = "true" },
            "Secret version must be an alphanumeric version identifier.");

    [Fact]
    public Task Enabled_IsValidated() =>
        AssertRejectedBeforeNetworkAsync(new UpdateSecretCommand { SecretName = "test", Enabled = "maybe" },
            "Enabled must be true or false.");

    [Fact]
    public Task Expires_IsValidated() =>
        AssertRejectedBeforeNetworkAsync(new UpdateSecretCommand { SecretName = "test", Expires = "not-a-date" },
            "Dates must be UTC timestamps between 1970 and January 2038 supported by the selected API.");

    [Fact]
    public Task NotBefore_IsValidated() =>
        AssertRejectedBeforeNetworkAsync(new UpdateSecretCommand { SecretName = "test", NotBefore = "not-a-date" },
            "Dates must be UTC timestamps between 1970 and January 2038 supported by the selected API.");

    [Fact]
    public Task Tags_IsValidated() =>
        AssertRejectedBeforeNetworkAsync(new UpdateSecretCommand { SecretName = "test", TagsInput = ["novalue"] },
            "Tags must use NAME=VALUE syntax.");

    [Fact]
    public Task NoChanges_ThrowsBeforeNetwork() =>
        AssertRejectedBeforeNetworkAsync(new UpdateSecretCommand { SecretName = "test" },
            "Specify at least one attribute, content type or tag update.");
}
