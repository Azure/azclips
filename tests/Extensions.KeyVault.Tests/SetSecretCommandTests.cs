using System.Security;
using System.Text.Json;
using System.Text.Json.Nodes;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.KeyVault.Customization;
using Moq;
using static Extensions.KeyVault.Tests.KeyVaultCommandTestHelpers;

namespace Extensions.KeyVault.Tests;

public class SetSecretCommandTests
{
    private const string SecureValue = "fixture\0\u00e9";
    private const string DateMessage = "Dates must be UTC timestamps between 1970 and January 2038 supported by the selected API.";
    private const string SizeMessage = "Secret values must not exceed 25 KiB.";
    private const string UnreadableFileMessage = "The secret file could not be read as UTF-8.";

    // Strict: a request the test did not set up throws MockException, so a test that gets its expected
    // exception also proves no unexpected request was sent.
    private readonly Mock<IRuntimeHttpPipeline> _mockPipeline = CreatePipelineMock();

    [Fact]
    public async Task ProcessAsync_InheritedAttributesAndTags_SendsThemInBody()
    {
        var command = new SetSecretCommand
        {
            SecretName = "test", Value = "fixture", SecretAttributesEnabled = false,
            SecretAttributesNotBefore = 1700000000, SecretAttributesExpires = 1800000000,
            Tags = new Dictionary<string, string> { ["source"] = "inherited" }
        };
        List<string?> sentBodies = _mockPipeline.RespondWith("{}");
        Initialize(command, _mockPipeline);

        await command.ProcessAsync();

        Assert.Equal((false, 1700000000, 1800000000, "inherited"), AttributesAndTagSummary(sentBodies.Single()));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("text/plain")]
    public async Task ProcessAsync_SecureValue_SendsEmbeddedNullAndUnicode(string? contentType)
    {
        using SecureString secureValue = SecureStringOf(SecureValue);
        var command = new SetSecretCommand { SecretName = "test", SecureValue = secureValue, ContentType = contentType };
        List<string?> sentBodies = _mockPipeline.RespondWith("{}");
        Initialize(command, _mockPipeline);

        await command.ProcessAsync();

        Assert.Equal(SecureValue, JsonNode.Parse(sentBodies.Single()!)!["value"]!.GetValue<string>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("text/plain")]
    public async Task ProcessAsync_SecureValue_ClearsValue(string? contentType)
    {
        using SecureString secureValue = SecureStringOf(SecureValue);
        var command = new SetSecretCommand { SecretName = "test", SecureValue = secureValue, ContentType = contentType };
        _mockPipeline.RespondWith("{}");
        Initialize(command, _mockPipeline);

        await command.ProcessAsync();

        Assert.Null(command.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("text/plain")]
    public async Task ProcessAsync_SecureValue_ClearsSecureValue(string? contentType)
    {
        using SecureString secureValue = SecureStringOf(SecureValue);
        var command = new SetSecretCommand { SecretName = "test", SecureValue = secureValue, ContentType = contentType };
        _mockPipeline.RespondWith("{}");
        Initialize(command, _mockPipeline);

        await command.ProcessAsync();

        Assert.Null(command.SecureValue);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("text/plain")]
    public async Task ProcessAsync_FileWithTrailingNewline_SendsTrailingNewline(string? contentType)
    {
        using TempFile file = TempFile.CreateEmpty();
        await File.WriteAllTextAsync(file.Path, "fixture-value\n");
        var command = new SetSecretCommand { SecretName = "test", File = file.Path, ContentType = contentType };
        List<string?> sentBodies = _mockPipeline.RespondWith("{}");
        Initialize(command, _mockPipeline);

        await command.ProcessAsync();

        Assert.Equal("fixture-value\n", JsonNode.Parse(sentBodies.Single()!)!["value"]!.GetValue<string>());
    }

    [Fact]
    public async Task ProcessAsync_Value_SendsPutRequest()
    {
        var command = new SetSecretCommand { SecretName = "test", Value = "private-fixture", Enabled = "false" };
        _mockPipeline.RespondWith("{\"value\":\"private-fixture\",\"id\":\"fixture-id\"}");
        Initialize(command, _mockPipeline);

        await command.ProcessAsync();

        Assert.Equal([HttpMethod.Put], _mockPipeline.SentRequests().Select(r => r.Method));
    }

    [Fact]
    public async Task ProcessAsync_Value_SendsValueInBody()
    {
        var command = new SetSecretCommand { SecretName = "test", Value = "private-fixture", Enabled = "false" };
        List<string?> sentBodies = _mockPipeline.RespondWith("{\"value\":\"private-fixture\",\"id\":\"fixture-id\"}");
        Initialize(command, _mockPipeline);

        await command.ProcessAsync();

        Assert.Equal("private-fixture", JsonNode.Parse(sentBodies.Single()!)!["value"]!.GetValue<string>());
    }

    [Fact]
    public async Task ProcessAsync_EnabledFalse_SendsEnabledFalse()
    {
        var command = new SetSecretCommand { SecretName = "test", Value = "private-fixture", Enabled = "false" };
        List<string?> sentBodies = _mockPipeline.RespondWith("{\"value\":\"private-fixture\",\"id\":\"fixture-id\"}");
        Initialize(command, _mockPipeline);

        await command.ProcessAsync();

        Assert.False(JsonNode.Parse(sentBodies.Single()!)!["attributes"]!["enabled"]!.GetValue<bool>());
    }

    [Fact]
    public async Task ProcessAsync_NoExpires_OmitsExpiresAttribute()
    {
        var command = new SetSecretCommand { SecretName = "test", Value = "private-fixture", Enabled = "false" };
        List<string?> sentBodies = _mockPipeline.RespondWith("{\"value\":\"private-fixture\",\"id\":\"fixture-id\"}");
        Initialize(command, _mockPipeline);

        await command.ProcessAsync();

        Assert.False(JsonNode.Parse(sentBodies.Single()!)!["attributes"]!.AsObject().ContainsKey("exp"));
    }

    [Fact]
    public async Task ProcessAsync_ResponseWithValue_OmitsValueFromOutput()
    {
        var command = new SetSecretCommand { SecretName = "test", Value = "private-fixture", Enabled = "false" };
        _mockPipeline.RespondWith("{\"value\":\"private-fixture\",\"id\":\"fixture-id\"}");
        var output = Initialize(command, _mockPipeline);

        await command.ProcessAsync();

        Assert.DoesNotContain("private-fixture", JsonSerializer.Serialize(output));
    }

    [Fact]
    public async Task ProcessAsync_ResponseWithValue_WritesMetadata()
    {
        var command = new SetSecretCommand { SecretName = "test", Value = "private-fixture", Enabled = "false" };
        _mockPipeline.RespondWith("{\"value\":\"private-fixture\",\"id\":\"fixture-id\"}");
        var output = Initialize(command, _mockPipeline);

        await command.ProcessAsync();

        Assert.IsType<SecretMetadata>(output.Single());
    }

    [Fact]
    public async Task ProcessAsync_Value_ClearsValue()
    {
        var command = new SetSecretCommand { SecretName = "test", Value = "private-fixture", Enabled = "false" };
        _mockPipeline.RespondWith("{\"value\":\"private-fixture\",\"id\":\"fixture-id\"}");
        Initialize(command, _mockPipeline);

        await command.ProcessAsync();

        Assert.Null(command.Value);
    }

    [Fact]
    public async Task ProcessAsync_ValueOnly_WritesMetadata()
    {
        var command = new SetSecretCommand { SecretName = "test", Value = "plain-value" };
        _mockPipeline.RespondWith("{}");
        var output = Initialize(command, _mockPipeline);

        await command.ProcessAsync();

        Assert.IsType<SecretMetadata>(output.Single());
    }

    [Fact]
    public async Task ProcessAsync_FileOnly_WritesMetadata()
    {
        using TempFile file = TempFile.CreateEmpty();
        await File.WriteAllTextAsync(file.Path, "file-value");
        var command = new SetSecretCommand { SecretName = "test", File = file.Path };
        _mockPipeline.RespondWith("{}");
        var output = Initialize(command, _mockPipeline);

        await command.ProcessAsync();

        Assert.IsType<SecretMetadata>(output.Single());
    }

    [Fact]
    public async Task ProcessAsync_SecureValueOnly_WritesMetadata()
    {
        using SecureString secureValue = SecureStringOf("secure-value");
        var command = new SetSecretCommand { SecretName = "test", SecureValue = secureValue };
        _mockPipeline.RespondWith("{}");
        var output = Initialize(command, _mockPipeline);

        await command.ProcessAsync();

        Assert.IsType<SecretMetadata>(output.Single());
    }

    [Fact]
    public async Task ProcessAsync_ValueAndFile_ThrowsArgumentException()
    {
        var command = new SetSecretCommand { SecretName = "test", Value = "plain-value", File = "dummy-path" };
        Initialize(command, _mockPipeline);

        Exception? exception = await Record.ExceptionAsync(command.ProcessAsync);

        Assert.Equal("Specify exactly one secret value source: value, file or SecureString.", Assert.IsType<ArgumentException>(exception).Message);
    }

    [Fact]
    public async Task ProcessAsync_InvalidSecretName_ThrowsArgumentException()
    {
        var command = new SetSecretCommand { SecretName = "bad_name", Value = "fixture" };
        Initialize(command, _mockPipeline);

        Exception? exception = await Record.ExceptionAsync(command.ProcessAsync);

        Assert.Equal("Secret names must contain 1-127 letters, digits or hyphens.", Assert.IsType<ArgumentException>(exception).Message);
    }

    [Fact]
    public async Task ProcessAsync_InvalidEnabled_ThrowsArgumentException()
    {
        var command = new SetSecretCommand { SecretName = "test", Value = "fixture", Enabled = "maybe" };
        Initialize(command, _mockPipeline);

        Exception? exception = await Record.ExceptionAsync(command.ProcessAsync);

        Assert.Equal("Enabled must be true or false.", Assert.IsType<ArgumentException>(exception).Message);
    }

    [Fact]
    public async Task ProcessAsync_InvalidExpires_ThrowsArgumentException()
    {
        var command = new SetSecretCommand { SecretName = "test", Value = "fixture", Expires = "not-a-date" };
        Initialize(command, _mockPipeline);

        Exception? exception = await Record.ExceptionAsync(command.ProcessAsync);

        Assert.Equal(DateMessage, Assert.IsType<ArgumentException>(exception).Message);
    }

    [Fact]
    public async Task ProcessAsync_InvalidNotBefore_ThrowsArgumentException()
    {
        var command = new SetSecretCommand { SecretName = "test", Value = "fixture", NotBefore = "not-a-date" };
        Initialize(command, _mockPipeline);

        Exception? exception = await Record.ExceptionAsync(command.ProcessAsync);

        Assert.Equal(DateMessage, Assert.IsType<ArgumentException>(exception).Message);
    }

    [Fact]
    public async Task ProcessAsync_TagWithoutValue_ThrowsArgumentException()
    {
        var command = new SetSecretCommand { SecretName = "test", Value = "fixture", TagsInput = ["novalue"] };
        Initialize(command, _mockPipeline);

        Exception? exception = await Record.ExceptionAsync(command.ProcessAsync);

        Assert.Equal("Tags must use NAME=VALUE syntax.", Assert.IsType<ArgumentException>(exception).Message);
    }

    [Theory]
    [InlineData(25 * 1024 + 1, 'a')]
    [InlineData(12801, '\u00e9')]
    public async Task ProcessAsync_ValueOver25KiB_ThrowsArgumentException(int length, char character)
    {
        var command = new SetSecretCommand { SecretName = "test", Value = new string(character, length) };
        Initialize(command, _mockPipeline);

        Exception? exception = await Record.ExceptionAsync(command.ProcessAsync);

        Assert.Equal(SizeMessage, Assert.IsType<ArgumentException>(exception).Message);
    }

    [Fact]
    public async Task ProcessAsync_ValueOfExactly25KiB_WritesMetadata()
    {
        var command = new SetSecretCommand { SecretName = "test", Value = new string('a', 25 * 1024) };
        _mockPipeline.RespondWith("{}");
        var output = Initialize(command, _mockPipeline);

        await command.ProcessAsync();

        Assert.IsType<SecretMetadata>(output.Single());
    }

    [Fact]
    public async Task ProcessAsync_FileOver25KiB_ThrowsArgumentException()
    {
        using TempFile file = TempFile.CreateEmpty();
        await File.WriteAllTextAsync(file.Path, new string('a', 25 * 1024 + 1));
        var command = new SetSecretCommand { SecretName = "test", File = file.Path };
        Initialize(command, _mockPipeline);

        Exception? exception = await Record.ExceptionAsync(command.ProcessAsync);

        Assert.Equal(SizeMessage, Assert.IsType<ArgumentException>(exception).Message);
    }

    [Fact]
    public async Task ProcessAsync_InvalidUtf8File_ThrowsArgumentException()
    {
        using TempFile file = TempFile.CreateEmpty();
        await File.WriteAllBytesAsync(file.Path, [0x66, 0xFF, 0xFE, 0x66]);
        var command = new SetSecretCommand { SecretName = "test", File = file.Path };
        Initialize(command, _mockPipeline);

        Exception? exception = await Record.ExceptionAsync(command.ProcessAsync);

        Assert.Equal(UnreadableFileMessage, Assert.IsType<ArgumentException>(exception).Message);
    }

    [Fact]
    public async Task ProcessAsync_MissingFile_ThrowsArgumentException()
    {
        using TempFile file = TempFile.ReservePath(".missing");
        var command = new SetSecretCommand { SecretName = "test", File = file.Path };
        Initialize(command, _mockPipeline);

        Exception? exception = await Record.ExceptionAsync(command.ProcessAsync);

        Assert.Equal(UnreadableFileMessage, Assert.IsType<ArgumentException>(exception).Message);
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
}