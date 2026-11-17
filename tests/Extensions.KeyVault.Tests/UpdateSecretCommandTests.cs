using System.Text.Json.Nodes;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.KeyVault.Customization;
using Moq;
using static Extensions.KeyVault.Tests.KeyVaultCommandTestHelpers;

namespace Extensions.KeyVault.Tests;

public class UpdateSecretCommandTests
{
    private const string DateMessage = "Dates must be UTC timestamps between 1970 and January 2038 supported by the selected API.";

    // Strict: a request the test did not set up throws MockException, so a test that gets its expected
    // exception also proves no unexpected request was sent.
    private readonly Mock<IRuntimeHttpPipeline> _mockPipeline = CreatePipelineMock();

    [Fact]
    public async Task ProcessAsync_InheritedAttributesAndTags_SendsThemInBody()
    {
        var command = new UpdateSecretCommand
        {
            SecretName = "test", SecretAttributesEnabled = false,
            SecretAttributesNotBefore = 1700000000, SecretAttributesExpires = 1800000000,
            Tags = new Dictionary<string, string> { ["source"] = "inherited" }
        };
        List<string?> sentBodies = _mockPipeline.RespondWith("{}");
        Initialize(command, _mockPipeline);

        await command.ProcessAsync();

        Assert.Equal((false, 1700000000, 1800000000, "inherited"), AttributesAndTagSummary(sentBodies.Single()));
    }

    [Fact]
    public async Task ProcessAsync_InheritedEmptyTags_SendsEmptyTags()
    {
        var command = new UpdateSecretCommand { SecretName = "test", Tags = new Dictionary<string, string>() };
        List<string?> sentBodies = _mockPipeline.RespondWith("{}");
        Initialize(command, _mockPipeline);

        await command.ProcessAsync();

        Assert.Empty(JsonNode.Parse(sentBodies.Single()!)!["tags"]!.AsObject());
    }

    [Fact]
    public async Task ProcessAsync_EnabledWithInheritedAttributes_OverridesOnlyEnabled()
    {
        var command = new UpdateSecretCommand
        {
            SecretName = "test", SecretAttributesEnabled = false, Enabled = "true",
            SecretAttributesExpires = 1800000000
        };
        List<string?> sentBodies = _mockPipeline.RespondWith("{}");
        Initialize(command, _mockPipeline);

        await command.ProcessAsync();

        Assert.Equal((true, 1800000000), EnabledAndExpires(sentBodies.Single()));
    }

    [Fact]
    public async Task ProcessAsync_EnabledOnly_SendsPatchRequest()
    {
        var command = new UpdateSecretCommand { SecretName = "test", Enabled = "false" };
        _mockPipeline.RespondWith("{}");
        Initialize(command, _mockPipeline);

        await command.ProcessAsync();

        Assert.Equal([HttpMethod.Patch], _mockPipeline.SentRequests().Select(r => r.Method));
    }

    [Fact]
    public async Task ProcessAsync_EnabledOnly_OmitsValueAndTags()
    {
        var command = new UpdateSecretCommand { SecretName = "test", Enabled = "false" };
        List<string?> sentBodies = _mockPipeline.RespondWith("{}");
        Initialize(command, _mockPipeline);

        await command.ProcessAsync();

        Assert.Empty(JsonNode.Parse(sentBodies.Single()!)!.AsObject().Select(p => p.Key).Intersect(["value", "tags"]));
    }

    [Fact]
    public async Task ProcessAsync_EnabledOnly_SendsOnlyEnabledAttribute()
    {
        var command = new UpdateSecretCommand { SecretName = "test", Enabled = "false" };
        List<string?> sentBodies = _mockPipeline.RespondWith("{}");
        Initialize(command, _mockPipeline);

        await command.ProcessAsync();

        Assert.Equal("""{"enabled":false}""", JsonNode.Parse(sentBodies.Single()!)!["attributes"]!.ToJsonString());
    }

    [Fact]
    public async Task ProcessAsync_InvalidSecretName_ThrowsArgumentException()
    {
        var command = new UpdateSecretCommand { SecretName = "bad_name", Enabled = "true" };
        Initialize(command, _mockPipeline);

        Exception? exception = await Record.ExceptionAsync(command.ProcessAsync);

        Assert.Equal("Secret names must contain 1-127 letters, digits or hyphens.", Assert.IsType<ArgumentException>(exception).Message);
    }

    [Fact]
    public async Task ProcessAsync_InvalidSecretVersion_ThrowsArgumentException()
    {
        var command = new UpdateSecretCommand { SecretName = "test", SecretVersion = "bad-version", Enabled = "true" };
        Initialize(command, _mockPipeline);

        Exception? exception = await Record.ExceptionAsync(command.ProcessAsync);

        Assert.Equal("Secret version must be an alphanumeric version identifier.", Assert.IsType<ArgumentException>(exception).Message);
    }

    [Fact]
    public async Task ProcessAsync_InvalidEnabled_ThrowsArgumentException()
    {
        var command = new UpdateSecretCommand { SecretName = "test", Enabled = "maybe" };
        Initialize(command, _mockPipeline);

        Exception? exception = await Record.ExceptionAsync(command.ProcessAsync);

        Assert.Equal("Enabled must be true or false.", Assert.IsType<ArgumentException>(exception).Message);
    }

    [Fact]
    public async Task ProcessAsync_InvalidExpires_ThrowsArgumentException()
    {
        var command = new UpdateSecretCommand { SecretName = "test", Expires = "not-a-date" };
        Initialize(command, _mockPipeline);

        Exception? exception = await Record.ExceptionAsync(command.ProcessAsync);

        Assert.Equal(DateMessage, Assert.IsType<ArgumentException>(exception).Message);
    }

    [Fact]
    public async Task ProcessAsync_InvalidNotBefore_ThrowsArgumentException()
    {
        var command = new UpdateSecretCommand { SecretName = "test", NotBefore = "not-a-date" };
        Initialize(command, _mockPipeline);

        Exception? exception = await Record.ExceptionAsync(command.ProcessAsync);

        Assert.Equal(DateMessage, Assert.IsType<ArgumentException>(exception).Message);
    }

    [Fact]
    public async Task ProcessAsync_TagWithoutValue_ThrowsArgumentException()
    {
        var command = new UpdateSecretCommand { SecretName = "test", TagsInput = ["novalue"] };
        Initialize(command, _mockPipeline);

        Exception? exception = await Record.ExceptionAsync(command.ProcessAsync);

        Assert.Equal("Tags must use NAME=VALUE syntax.", Assert.IsType<ArgumentException>(exception).Message);
    }

    [Fact]
    public async Task ProcessAsync_NoChanges_ThrowsArgumentException()
    {
        var command = new UpdateSecretCommand { SecretName = "test" };
        Initialize(command, _mockPipeline);

        Exception? exception = await Record.ExceptionAsync(command.ProcessAsync);

        Assert.Equal("Specify at least one attribute, content type or tag update.", Assert.IsType<ArgumentException>(exception).Message);
    }

    private (bool Enabled, int NotBefore, int Expires, string Source) AttributesAndTagSummary(string? sentBody)
    {
        JsonNode body = JsonNode.Parse(sentBody!)!;
        JsonNode attributes = body["attributes"]!;
        return (
            attributes["enabled"]!.GetValue<bool>(),
            attributes["nbf"]!.GetValue<int>(),
            attributes["exp"]!.GetValue<int>(),
            body["tags"]!["source"]!.GetValue<string>());
    }

    private (bool Enabled, int Expires) EnabledAndExpires(string? sentBody)
    {
        JsonNode attributes = JsonNode.Parse(sentBody!)!["attributes"]!;
        return (attributes["enabled"]!.GetValue<bool>(), attributes["exp"]!.GetValue<int>());
    }
}