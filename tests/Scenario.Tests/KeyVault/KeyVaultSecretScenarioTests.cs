using System.Security;
using System.Text.Json;
using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.KeyVault.Customization;
using Tests.Recording;

namespace Scenario.Tests;

[Collection("Scenario Tests")]
public class KeyVaultSecretScenarioTests
{
    [Fact]
    public void CliConversionDoesNotLogSecretValues()
    {
        var logger = new CapturingLogger();
        var parser = new Core.Cli.PropertyParser(logger);
        Assert.Equal("private-fixture", parser.ConvertToType("value", "private-fixture", typeof(string)));
        Assert.DoesNotContain(logger.Messages, message => message.Contains("private-fixture"));
    }

    private const string Root = "https://fixture.vault.azure.net";
    private sealed class CapturingLogger : ILoggingService
    {
        public List<string> Messages { get; } = [];
        public void Log(string message) => Messages.Add(message);
        public void Info(string message) => Messages.Add(message);
        public void Verbose(string message) => Messages.Add(message);
        public void Debug(string message) => Messages.Add(message);
        public void Warn(string message) => Messages.Add(message);
        public void Error(string message) => Messages.Add(message);
    }
    private const string Version = "api-version=2025-07-01";
    private const string Bundle = "{\"id\":\"https://fixture.vault.azure.net/secrets/test/version1\",\"value\":\"fixture-value\",\"attributes\":{\"enabled\":false}}";

    [Theory]
    [InlineData(ClientType.CLI)]
    [InlineData(ClientType.PowerShell)]
    public void SyntheticSecretLifecycleThroughRealBinding(ClientType clientType)
    {
        var record = new SessionRecord { Metadata = { ["source"] = "Synthetic fixture, not a live Azure recording" } };
        Add("PUT", $"/secrets/test?{Version}", Bundle);
        Add("GET", $"/secrets/test/?{Version}", Bundle);
        Add("GET", $"/secrets/test/?{Version}", Bundle);
        Add("GET", $"/secrets?{Version}", Page("one", $"{Root}/secrets?{Version}&skiptoken=next"));
        Add("GET", $"/secrets?{Version}&skiptoken=next", Page("two"));
        Add("GET", $"/secrets/test/versions?{Version}", Page("version1"));
        Add("PATCH", $"/secrets/test/?{Version}", Bundle);
        Add("DELETE", $"/secrets/test?{Version}", "{\"value\":\"fixture-value\",\"recoveryId\":\"recovery\"}");
        var interceptor = new RecordPlaybackInterceptor(RecorderMode.Playback, record);
        using var host = new ScenarioHost(clientType, interceptor, extensions: [ExtensionUnderTest.KeyVault]);
        using var secure = new SecureString();
        foreach (char character in "fixture-value") secure.AppendChar(character);

        var set = Run("set", "Set-AzKeyVaultSecret", ["--name", "test", "--value", "fixture-value", "--enabled", "false"],
            new() { ["SecretName"] = "test", ["SecureValue"] = secure, ["Enabled"] = "false" });
        Assert.IsType<SecretMetadata>(Assert.Single(set));
        Assert.DoesNotContain("fixture-value", JsonSerializer.Serialize(set));
        var show = Run("show", "Get-AzKeyVaultSecret_Get", ["--name", "test"], new() { ["SecretName"] = "test" });
        Assert.IsType<SecretMetadata>(Assert.Single(show));
        var plaintext = Run("show", "Get-AzKeyVaultSecret_Get", ["--name", "test", "--as-plain-text"],
            new() { ["SecretName"] = "test", ["AsPlainText"] = true });
        Assert.Equal("fixture-value", Assert.Single(plaintext));
        var list = Run("list", "Get-AzKeyVaultSecret_List", [], new());
        Assert.Equal(2, Assert.IsAssignableFrom<IEnumerable<SecretMetadata>>(Assert.Single(list)).Count());
        var versions = Run("list-versions", "Get-AzKeyVaultSecret_Versions", ["--name", "test"], new() { ["SecretName"] = "test" });
        Assert.Single(Assert.IsAssignableFrom<IEnumerable<SecretMetadata>>(Assert.Single(versions)));
        Run("set-attributes", "Update-AzKeyVaultSecret", ["--name", "test", "--enabled", "false"],
            new() { ["SecretName"] = "test", ["Enabled"] = "false" });
        var deleted = Run("delete", "Remove-AzKeyVaultSecret", ["--name", "test", "--yes"],
            new() { ["SecretName"] = "test", ["Yes"] = true });
        Assert.Equal("recovery", Assert.IsType<SecretMetadata>(Assert.Single(deleted)).RecoveryId);
        Assert.Equal(8, interceptor.Sent.Count);
        Assert.DoesNotContain("fixture-value", JsonSerializer.Serialize(deleted));

        using var update = JsonDocument.Parse(interceptor.Sent.Single(request => request.Method == "PATCH").Body!);
        Assert.False(update.RootElement.TryGetProperty("value", out _));
        Assert.Single(update.RootElement.EnumerateObject());

        IReadOnlyList<object> Run(string cli, string ps, string[] args, Dictionary<string, object> parameters)
        {
            parameters["VaultName"] = "fixture";
            return clientType == ClientType.CLI
                ? host.RunCli(["keyvault", "secret", cli, "--vault-name", "fixture", .. args])
                : host.RunPowerShell(ps, parameters);
        }

        void Add(string method, string route, string body) => record.Entries.Add(new SessionRecordEntry
        {
            Request = new RecordedRequest { Method = method, Uri = Root + route },
            Response = new RecordedResponse { StatusCode = 200, Body = body }
        });
    }

    [Theory]
    [InlineData("{\"value\":\"private-fixture\",\"pwd\":\"private-fixture\"}")]
    [InlineData("{\"error\":{\"message\":\"private-fixture\"}}")]
    [InlineData("{broken private-fixture")]
    public void RecordingSanitizerNeverKeepsSensitivePayload(string body)
    {
        Assert.DoesNotContain("private-fixture", new SensitiveJsonSanitizer().Sanitize(body));
    }

    private static string Page(string id, string? next = null) =>
        JsonSerializer.Serialize(new { value = new[] { new { id } }, nextLink = next });

    [Theory]
    [InlineData("https://attacker.example/secrets")]
    [InlineData("https://other.vault.azure.net/secrets")]
    public void HostilePaginationNeverReachesTheRecorder(string next)
    {
        var record = new SessionRecord
        {
            Entries = [new SessionRecordEntry
            {
                Request = new RecordedRequest { Method = "GET", Uri = $"{Root}/secrets?{Version}" },
                Response = new RecordedResponse { StatusCode = 200, Body = Page("one", next) }
            }]
        };
        var interceptor = new RecordPlaybackInterceptor(RecorderMode.Playback, record);
        using var host = new ScenarioHost(ClientType.CLI, interceptor, extensions: [ExtensionUnderTest.KeyVault]);
        var error = Assert.ThrowsAny<Exception>(() => host.RunCli("keyvault", "secret", "list", "--vault-name", "fixture"));
        Assert.Contains("selected vault's secrets or certificates endpoint", error.ToString());
        Assert.Single(interceptor.Sent);
    }

    [Theory]
    [InlineData(ClientType.CLI)]
    [InlineData(ClientType.PowerShell)]
    public void EngineUsesInjectedSensitiveTransport(ClientType clientType)
    {
        using var backend = new VaultBackend();
        var record = new SessionRecord();
        var interceptor = new RecordPlaybackInterceptor(RecorderMode.Record, record);
        using var host = new ScenarioHost(clientType, interceptor, backendHandler: backend, extensions: [ExtensionUnderTest.KeyVault]);
        var output = clientType == ClientType.CLI
            ? host.RunCli("keyvault", "secret", "show", "--vault-name", "fixture", "--name", "test")
            : host.RunPowerShell("Get-AzKeyVaultSecret_Get", new Dictionary<string, object> { ["VaultName"] = "fixture", ["SecretName"] = "test" });
        Assert.Equal(1, backend.Calls);
        Assert.IsType<SecretMetadata>(Assert.Single(output));
        Assert.Single(record.Entries);
        Assert.DoesNotContain("fixture-value", JsonSerializer.Serialize(record));
    }

    [Theory]
    [InlineData(System.Net.HttpStatusCode.OK)]
    [InlineData(System.Net.HttpStatusCode.Forbidden)]
    [InlineData(System.Net.HttpStatusCode.NotFound)]
    [InlineData(System.Net.HttpStatusCode.Conflict)]
    public void PowerShellRecoveryUsesProtectedTransportAndSafeOutput(System.Net.HttpStatusCode statusCode)
    {
        using var backend = new RecoveryBackend(statusCode);
        var record = new SessionRecord();
        var interceptor = new RecordPlaybackInterceptor(RecorderMode.Record, record);
        using var host = new ScenarioHost(ClientType.PowerShell, interceptor, backendHandler: backend, extensions: [ExtensionUnderTest.KeyVault]);
        var parameters = new Dictionary<string, object> { ["VaultName"] = "fixture", ["Name"] = "test", ["Confirm"] = false };

        if (statusCode == System.Net.HttpStatusCode.OK)
        {
            var output = host.RunPowerShellCmdlet<Microsoft.Azure.Azclips.Cmdlets.KeyVault.UndoAzKeyVaultSecretRemovalCmdlet>(parameters);
            var metadata = Assert.IsType<SecretMetadata>(Assert.Single(output));
            Assert.Equal($"{Root}/secrets/test/version1", metadata.Id);
            Assert.False(metadata.Attributes!.Enabled);
            Assert.DoesNotContain("fixture-value", JsonSerializer.Serialize(output));
        }
        else
        {
            var error = Assert.ThrowsAny<Exception>(() => host.RunPowerShellCmdlet<Microsoft.Azure.Azclips.Cmdlets.KeyVault.UndoAzKeyVaultSecretRemovalCmdlet>(parameters));
            Assert.DoesNotContain("fixture-value", error.ToString());
        }

        Assert.Equal(1, backend.Calls);
        Assert.Single(record.Entries);
        Assert.DoesNotContain("fixture-value", JsonSerializer.Serialize(record));
    }

    private sealed class RecoveryBackend(System.Net.HttpStatusCode statusCode) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Calls++;
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal($"{Root}/deletedsecrets/test/recover?{Version}", request.RequestUri!.AbsoluteUri);
            Assert.Null(request.Content);
            return Task.FromResult(new HttpResponseMessage(statusCode)
            {
                RequestMessage = request,
                Content = new StringContent(statusCode == System.Net.HttpStatusCode.OK
                    ? Bundle : "{\"error\":{\"message\":\"fixture-value\"}}")
            });
        }
    }

    private sealed class VaultBackend : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Calls++;
            Assert.Equal("fixture.vault.azure.net", request.RequestUri!.Host);
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                RequestMessage = request, Content = new StringContent(Bundle)
            });
        }
    }
}