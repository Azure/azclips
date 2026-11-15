using System.Text.Json;
using Microsoft.Azure.Azclips.Extensions.KeyVault.Customization;
using static Extensions.KeyVault.Tests.KeyVaultCommandTestHelpers;

namespace Extensions.KeyVault.Tests;

public class SetSecretCommandTests
{
    [Fact]
    public async Task InheritedAttributesArePreserved()
    {
        var command = new SetSecretCommand
        {
            SecretName = "test", Value = "fixture", SecretAttributesEnabled = false,
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SecureInputPreservesEmbeddedNullAndUnicode(bool withMetadata)
    {
        const string value = "fixture\0\u00e9";
        using var secure = new System.Security.SecureString();
        foreach (char character in value) secure.AppendChar(character);
        var command = new SetSecretCommand { SecretName = "test", SecureValue = secure };
        if (withMetadata) command.ContentType = "text/plain";
        Initialize(command, "{}", async request =>
        {
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
            Assert.Equal(value, body.RootElement.GetProperty("value").GetString());
        });
        await command.ProcessAsync();
        Assert.Null(command.Value);
        Assert.Null(command.SecureValue);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FileInputPreservesTrailingNewline(bool withMetadata)
    {
        string file = Path.GetTempFileName();
        try
        {
            await System.IO.File.WriteAllTextAsync(file, "fixture-value\n");
            var command = new SetSecretCommand { SecretName = "test", File = file };
            if (withMetadata) command.ContentType = "text/plain";
            Initialize(command, "{}", async request =>
            {
                using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
                Assert.Equal("fixture-value\n", body.RootElement.GetProperty("value").GetString());
            });
            await command.ProcessAsync();
        }
        finally { System.IO.File.Delete(file); }
    }

    [Fact]
    public async Task SetSendsValueButReturnsOnlyMetadata()
    {
        var command = new SetSecretCommand { SecretName = "test", Value = "private-fixture", Enabled = "false" };
        string? requestBody = null;
        var output = Initialize(command, "{\"value\":\"private-fixture\",\"id\":\"fixture-id\"}", async request =>
        {
            requestBody = await request.Content!.ReadAsStringAsync();
            Assert.Equal(HttpMethod.Put, request.Method);
        });
        await command.ProcessAsync();
        using var json = JsonDocument.Parse(requestBody!);
        Assert.Equal("private-fixture", json.RootElement.GetProperty("value").GetString());
        Assert.False(json.RootElement.GetProperty("attributes").GetProperty("enabled").GetBoolean());
        Assert.False(json.RootElement.GetProperty("attributes").TryGetProperty("exp", out _));
        Assert.DoesNotContain("private-fixture", JsonSerializer.Serialize(output));
        Assert.IsType<SecretMetadata>(Assert.Single(output));
        Assert.Null(command.Value);
    }

    [Theory]
    [InlineData("value-only")]
    [InlineData("file-only")]
    [InlineData("secure-only")]
    public async Task AllowsExactlyOneSecretValueSource(string mode)
    {
        string? filePath = null;
        try
        {
            var command = new SetSecretCommand { SecretName = "test" };
            switch (mode)
            {
                case "value-only":
                    command.Value = "plain-value";
                    break;
                case "file-only":
                    filePath = Path.GetTempFileName();
                    await System.IO.File.WriteAllTextAsync(filePath, "file-value");
                    command.File = filePath;
                    break;
                case "secure-only":
                    using (var secure = new System.Security.SecureString())
                    {
                        foreach (char character in "secure-value") secure.AppendChar(character);
                        command.SecureValue = secure.Copy();
                    }
                    break;
            }

            var output = Initialize(command, "{}", _ => Task.CompletedTask);
            await command.ProcessAsync();
            Assert.IsType<SecretMetadata>(Assert.Single(output));
        }
        finally
        {
            if (filePath is not null && System.IO.File.Exists(filePath))
            {
                System.IO.File.Delete(filePath);
            }
        }
    }

    [Fact]
    public Task MultipleSecretValueSources_ThrowsBeforeNetwork() =>
        AssertRejectedBeforeNetworkAsync(new SetSecretCommand { SecretName = "test", Value = "plain-value", File = "dummy-path" },
            "Specify exactly one secret value source: value, file or SecureString.");

    [Fact]
    public Task SecretName_IsValidated() =>
        AssertRejectedBeforeNetworkAsync(new SetSecretCommand { SecretName = "bad_name", Value = "fixture" },
            "Secret names must contain 1-127 letters, digits or hyphens.");

    [Fact]
    public Task Enabled_IsValidated() =>
        AssertRejectedBeforeNetworkAsync(new SetSecretCommand { SecretName = "test", Value = "fixture", Enabled = "maybe" },
            "Enabled must be true or false.");

    [Fact]
    public Task Expires_IsValidated() =>
        AssertRejectedBeforeNetworkAsync(new SetSecretCommand { SecretName = "test", Value = "fixture", Expires = "not-a-date" },
            "Dates must be UTC timestamps between 1970 and January 2038 supported by the selected API.");

    [Fact]
    public Task NotBefore_IsValidated() =>
        AssertRejectedBeforeNetworkAsync(new SetSecretCommand { SecretName = "test", Value = "fixture", NotBefore = "not-a-date" },
            "Dates must be UTC timestamps between 1970 and January 2038 supported by the selected API.");

    [Fact]
    public Task Tags_IsValidated() =>
        AssertRejectedBeforeNetworkAsync(new SetSecretCommand { SecretName = "test", Value = "fixture", TagsInput = ["novalue"] },
            "Tags must use NAME=VALUE syntax.");

    [Theory]
    [InlineData(25 * 1024 + 1, 'a')]
    [InlineData(12801, '\u00e9')]
    public Task ValueOver25KiB_ThrowsBeforeNetwork(int length, char character) =>
        AssertRejectedBeforeNetworkAsync(new SetSecretCommand { SecretName = "test", Value = new string(character, length) },
            "Secret values must not exceed 25 KiB.");

    [Fact]
    public async Task ValueOfExactly25KiB_IsAccepted()
    {
        var command = new SetSecretCommand { SecretName = "test", Value = new string('a', 25 * 1024) };
        var output = Initialize(command, "{}", _ => Task.CompletedTask);

        await command.ProcessAsync();

        Assert.IsType<SecretMetadata>(Assert.Single(output));
    }

    [Fact]
    public async Task FileOver25KiB_ThrowsBeforeNetwork()
    {
        string file = Path.GetTempFileName();
        try
        {
            await System.IO.File.WriteAllTextAsync(file, new string('a', 25 * 1024 + 1));
            await AssertRejectedBeforeNetworkAsync(new SetSecretCommand { SecretName = "test", File = file },
                "Secret values must not exceed 25 KiB.");
        }
        finally { System.IO.File.Delete(file); }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task UnreadableOrInvalidUtf8File_ThrowsBeforeNetwork(bool exists)
    {
        string file = exists ? Path.GetTempFileName() : Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".missing");
        try
        {
            if (exists) await System.IO.File.WriteAllBytesAsync(file, [0x66, 0xFF, 0xFE, 0x66]);
            await AssertRejectedBeforeNetworkAsync(new SetSecretCommand { SecretName = "test", File = file },
                "The secret file could not be read as UTF-8.");
        }
        finally { System.IO.File.Delete(file); }
    }
}
