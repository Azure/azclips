using System.Net;
using System.Text.Json;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Runtime.Handler;
using Microsoft.Azure.Azclips.Extensions.KeyVault.Customization;
using Microsoft.Azure.Azclips.Extensions.KeyVault.Processors;
using Moq;

namespace Extensions.KeyVault.Tests;

public class SecretCommandTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InheritedAttributesArePreserved(bool update)
    {
        KeyVaultDataPlaneProcessorBase command = update
            ? new UpdateSecretCommand
            {
                SecretName = "test", SecretAttributesEnabled = false,
                SecretAttributesNotBefore = 1700000000, SecretAttributesExpires = 1800000000,
                Tags = new Dictionary<string, string> { ["source"] = "inherited" }
            }
            : new SetSecretCommand
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

    [Theory]
    [InlineData(false, null)]
    [InlineData(true, null)]
    [InlineData(false, 1)]
    [InlineData(true, 1)]
    public async Task ListsRespectInheritedPageSize(bool versions, int? pageSize)
    {
        KeyVaultDataPlaneProcessorBase command = versions
            ? new ListSecretVersionsCommand { SecretName = "test", Maxresults = pageSize }
            : new ListSecretsCommand { Maxresults = pageSize };
        Initialize(command, "{\"value\":[]}", request =>
        {
            Assert.Equal(pageSize.HasValue, request.RequestUri!.Query.Contains("maxresults="));
            if (pageSize.HasValue) Assert.Contains("maxresults=1", request.RequestUri.Query);
            return Task.CompletedTask;
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
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShowRequiresOptInToReturnPlaintext(bool plaintext)
    {
        var command = new ShowSecretCommand { SecretName = "test", SecretVersion = "version1", AsPlainText = plaintext };
        var output = Initialize(command, "{\"value\":\"private-fixture\"}", request =>
        {
            Assert.EndsWith("/secrets/test/version1?api-version=2025-07-01", request.RequestUri!.AbsoluteUri);
            return Task.CompletedTask;
        });
        await command.ProcessAsync();
        if (plaintext) Assert.Equal("private-fixture", Assert.Single(output));
        else Assert.IsType<SecretMetadata>(Assert.Single(output));
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
    public async Task DeleteRequiresConfirmationAndPreservesRecoveryMetadata()
    {
        var command = new RemoveSecretCommand { SecretName = "test" };
        int requests = 0;
        var output = Initialize(command, "{\"value\":\"private-fixture\",\"recoveryId\":\"recovery\",\"deletedDate\":123}", request =>
        {
            requests++;
            Assert.Equal(HttpMethod.Delete, request.Method);
            return Task.CompletedTask;
        });
        await Assert.ThrowsAsync<ArgumentException>(() => command.ProcessAsync());
        Assert.Equal(0, requests);
        command.Yes = true;
        await command.ProcessAsync();
        Assert.Equal("recovery", Assert.IsType<SecretMetadata>(Assert.Single(output)).RecoveryId);
        Assert.DoesNotContain("private-fixture", JsonSerializer.Serialize(output));
    }

    [Fact]
    public async Task RecoverSendsBodylessPostAndReturnsOnlyMetadata()
    {
        var command = new RecoverSecretCommand { SecretName = "test" };
        int requests = 0;
        var output = Initialize(command,
            "{\"id\":\"https://fixture.vault.azure.net/secrets/test/version1\",\"value\":\"private-fixture\",\"attributes\":{\"enabled\":false}}",
            request =>
            {
                requests++;
                Assert.Equal(HttpMethod.Post, request.Method);
                Assert.Equal("https://fixture.vault.azure.net/deletedsecrets/test/recover?api-version=2025-07-01", request.RequestUri!.AbsoluteUri);
                Assert.Null(request.Content);
                return Task.CompletedTask;
            });

        await command.ProcessAsync();

        Assert.Equal(1, requests);
        var metadata = Assert.IsType<SecretMetadata>(Assert.Single(output));
        Assert.Equal("https://fixture.vault.azure.net/secrets/test/version1", metadata.Id);
        Assert.False(metadata.Attributes!.Enabled);
        Assert.DoesNotContain("private-fixture", JsonSerializer.Serialize(output));
    }

    [Theory]
    [InlineData("")]
    [InlineData("../test")]
    [InlineData("test*")]
    [InlineData("test%2Frecover")]
    public async Task RecoverRejectsInvalidNamesBeforeNetwork(string name)
    {
        var command = new RecoverSecretCommand { SecretName = name };
        var output = Initialize(command, "{}", _ => throw new InvalidOperationException("Unexpected request."));

        await Assert.ThrowsAsync<ArgumentException>(() => command.ProcessAsync());

        Assert.Empty(output);
    }

    private static List<object> Initialize(KeyVaultDataPlaneProcessorBase command, string body, Func<HttpRequestMessage, Task> inspect)
    {
        var publisher = new Mock<IEventPublisher>();
        publisher.SetReturnsDefault(Task.CompletedTask);
        var pipeline = new Mock<IRuntimeHttpPipeline>();
        pipeline.Setup(value => value.SendAsync(It.IsAny<HttpRequestMessage>(), publisher.Object))
            .Returns(async (HttpRequestMessage request, IEventPublisher _) =>
            {
                await inspect(request);
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body), RequestMessage = request };
            });
        var output = new List<object>();
        command.VaultName = "fixture";
        command.Init(new ProcessorContext(pipeline.Object, publisher.Object, new AuthHandler(),
            new OutputHandler { WriteOutput = (value, _) => { output.Add(value); return Task.CompletedTask; } },
            new Profile { TenantId = "fixture-tenant" }));
        return output;
    }
}