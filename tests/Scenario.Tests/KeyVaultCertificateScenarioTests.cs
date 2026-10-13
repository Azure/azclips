using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.KeyVault.Customization;
using Microsoft.Azure.Azclips.Extensions.KeyVault.Models;
using Tests.Recording;

namespace Scenario.Tests;

[Collection("Scenario Tests")]
public class KeyVaultCertificateScenarioTests
{
    private const string Root = "https://fixture.vault.azure.net";
    private const string Version = "api-version=2025-07-01";

    [Theory]
    [InlineData(ClientType.CLI, null)]
    [InlineData(ClientType.CLI, "false")]
    [InlineData(ClientType.CLI, "true")]
    [InlineData(ClientType.PowerShell, null)]
    [InlineData(ClientType.PowerShell, "false")]
    [InlineData(ClientType.PowerShell, "true")]
    public void IncludePendingPreservesOmittedAndExplicitBooleanValues(ClientType clientType, string? includePending)
    {
        string query = includePending is null ? string.Empty : $"&includePending={includePending}";
        var record = new SessionRecord
        {
            Entries = [new SessionRecordEntry
            {
                Request = new RecordedRequest { Method = "GET", Uri = $"{Root}/certificates?{Version}{query}" },
                Response = new RecordedResponse { StatusCode = 200, Body = "{\"value\":[]}" }
            }]
        };
        var interceptor = new RecordPlaybackInterceptor(RecorderMode.Playback, record);
        using var host = new ScenarioHost(clientType, interceptor, extensions: [ExtensionUnderTest.KeyVault]);
        if (clientType == ClientType.CLI)
        {
            string[] arguments = includePending is null ? [] : ["--include-pending", includePending];
            host.RunCli(["keyvault", "certificate", "list", "--vault-name", "fixture", .. arguments]);
        }
        else
        {
            var parameters = new Dictionary<string, object> { ["VaultName"] = "fixture" };
            if (includePending is not null) parameters["IncludePendingInput"] = bool.Parse(includePending);
            host.RunPowerShell("Get-AzKeyVaultCertificate_List", parameters);
        }
        Assert.Equal(record.Entries[0].Request.Uri, Assert.Single(interceptor.Sent).Uri);
    }

    [Fact]
    public void CreateTimeoutCancelsAnInFlightRequest()
    {
        using var backend = new CertificateBackend(async (_, token) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            throw new InvalidOperationException("Cancellation was not observed.");
        });
        var record = new SessionRecord();
        var interceptor = new RecordPlaybackInterceptor(RecorderMode.Record, record);
        using var host = new ScenarioHost(ClientType.CLI, interceptor, backendHandler: backend, extensions: [ExtensionUnderTest.KeyVault]);
        var elapsed = System.Diagnostics.Stopwatch.StartNew();
        var error = Assert.ThrowsAny<Exception>(() => host.RunCli("keyvault", "certificate", "create", "--vault-name", "fixture", "--name", "test", "--policy", "{}", "--timeout", "1"));
        Assert.Contains("timed out", error.ToString());
        Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(5), $"One-second timeout took {elapsed.Elapsed}.");
        Assert.Equal(1, backend.Calls);
    }

    [Theory]
    [InlineData(ClientType.CLI)]
    [InlineData(ClientType.PowerShell)]
    public void ImportUsesSensitiveTransportAndRedactsRecording(ClientType clientType)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=fixture", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        string file = Path.GetTempFileName();
        string passwordFile = Path.GetTempFileName();
        const string password = "synthetic-private-password";
        using var secure = new System.Security.SecureString();
        foreach (char character in password) secure.AppendChar(character);
        try
        {
            File.WriteAllBytes(file, certificate.Export(X509ContentType.Pfx, password));
            File.WriteAllText(passwordFile, password);
            string? imported = null;
            using var backend = new CertificateBackend(async (message, token) =>
            {
                using var json = JsonDocument.Parse(await message.Content!.ReadAsStringAsync(token));
                Assert.Equal(password, json.RootElement.GetProperty("pwd").GetString());
                imported = json.RootElement.GetProperty("value").GetString();
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK) { RequestMessage = message, Content = new StringContent("{}") };
            });
            var record = new SessionRecord();
            var interceptor = new RecordPlaybackInterceptor(RecorderMode.Record, record);
            using var host = new ScenarioHost(clientType, interceptor, backendHandler: backend, extensions: [ExtensionUnderTest.KeyVault]);
            var output = clientType == ClientType.CLI
                ? host.RunCli("keyvault", "certificate", "import", "--vault-name", "fixture", "--name", "test", "--file", file, "--password-file", passwordFile)
                : host.RunPowerShell("Import-AzKeyVaultCertificate", new Dictionary<string, object> { ["VaultName"] = "fixture", ["CertificateName"] = "test", ["File"] = file, ["SecurePassword"] = secure });
            Assert.Equal(1, backend.Calls);
            Assert.Single(record.Entries);
            Assert.NotNull(imported);
            Assert.DoesNotContain(password, JsonSerializer.Serialize(record));
            Assert.DoesNotContain(imported, JsonSerializer.Serialize(record));
            Assert.DoesNotContain(imported, JsonSerializer.Serialize(output));
        }
        finally { File.Delete(file); File.Delete(passwordFile); }
    }

    [Theory]
    [InlineData(ClientType.CLI)]
    [InlineData(ClientType.PowerShell)]
    public void SyntheticCertificateLifecycleThroughRealBinding(ClientType clientType)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=fixture", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        string file = Path.GetTempFileName();
        string download = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".pem");
        try
        {
            File.WriteAllText(file, certificate.ExportCertificatePem() + "\n" + key.ExportPkcs8PrivateKeyPem());
            string bundle = JsonSerializer.Serialize(new { id = Root + "/certificates/test/version1", cer = certificate.RawData, x5t = "-_8" });
            var record = new SessionRecord { Metadata = { ["source"] = "Synthetic fixture, not a live Azure recording" } };
            Add("POST", "/certificates/test/create", "{\"status\":\"inProgress\"}");
            Add("POST", "/certificates/test/import", bundle);
            Add("GET", "/certificates/test/", bundle);
            Add("GET", "/certificates", "{\"value\":[{\"id\":\"one\"}],\"nextLink\":\"" + Root + "/certificates?" + Version + "&skiptoken=next\"}");
            Add("GET", "/certificates", "{\"value\":[{\"id\":\"two\"}]}", "&skiptoken=next");
            Add("GET", "/certificates/test/versions", "{\"value\":[{\"id\":\"version1\"}]}");
            Add("PATCH", "/certificates/test/", bundle);
            Add("GET", "/certificates/test/", bundle);
            Add("DELETE", "/certificates/test", "{\"recoveryId\":\"recovery\"}");
            var interceptor = new RecordPlaybackInterceptor(RecorderMode.Playback, record);
            using var host = new ScenarioHost(clientType, interceptor, extensions: [ExtensionUnderTest.KeyVault]);
            const string policy = "{\"issuer\":{\"name\":\"Unknown\"}}";
            Assert.IsType<CertificateOperation>(Assert.Single(Run("create", "Add-AzKeyVaultCertificate", ["--name", "test", "--policy", policy], new() { ["CertificateName"] = "test", ["Policy"] = policy })));
            Assert.IsType<CertificateBundle>(Assert.Single(Run("import", "Import-AzKeyVaultCertificate", ["--name", "test", "--file", file], new() { ["CertificateName"] = "test", ["File"] = file })));
            Assert.IsType<CertificateBundle>(Assert.Single(Run("show", "Get-AzKeyVaultCertificate_Get", ["--name", "test"], new() { ["CertificateName"] = "test" })));
            Assert.Equal(2, Assert.IsAssignableFrom<IEnumerable<CertificateItem>>(Assert.Single(Run("list", "Get-AzKeyVaultCertificate_List", [], new()))).Count());
            Assert.Single(Assert.IsAssignableFrom<IEnumerable<CertificateItem>>(Assert.Single(Run("list-versions", "Get-AzKeyVaultCertificate_Versions", ["--name", "test"], new() { ["CertificateName"] = "test" }))));
            Run("set-attributes", "Update-AzKeyVaultCertificate", ["--name", "test", "--enabled", "false"], new() { ["CertificateName"] = "test", ["Enabled"] = "false" });
            Assert.IsType<CertificateDownloadResult>(Assert.Single(Run("download", "Export-AzKeyVaultCertificate", ["--name", "test", "--file", download], new() { ["CertificateName"] = "test", ["File"] = download })));
            Assert.Equal("recovery", Assert.IsType<DeletedCertificateBundle>(Assert.Single(Run("delete", "Remove-AzKeyVaultCertificate", ["--name", "test", "--yes"], new() { ["CertificateName"] = "test", ["Yes"] = true }))).RecoveryId);
            Assert.Equal(9, interceptor.Sent.Count);
            Assert.All(interceptor.Sent, sent => Assert.Contains("/certificates", sent.Uri));
            using var exported = X509Certificate2.CreateFromPem(File.ReadAllText(download));
            Assert.Equal(certificate.Thumbprint, exported.Thumbprint);
            Assert.False(exported.HasPrivateKey);

            IReadOnlyList<object> Run(string cli, string ps, string[] arguments, Dictionary<string, object> parameters)
            {
                parameters["VaultName"] = "fixture";
                return clientType == ClientType.CLI ? host.RunCli(["keyvault", "certificate", cli, "--vault-name", "fixture", .. arguments]) : host.RunPowerShell(ps, parameters);
            }

            void Add(string method, string route, string body, string query = "") => record.Entries.Add(new SessionRecordEntry
            {
                Request = new RecordedRequest { Method = method, Uri = Root + route + "?" + Version + query },
                Response = new RecordedResponse { StatusCode = 200, Body = body }
            });
        }
        finally { File.Delete(file); File.Delete(download); }
    }

    [Theory]
    [InlineData("https://attacker.example/certificates")]
    [InlineData("https://other.vault.azure.net/certificates")]
    [InlineData("https://fixture.vault.azure.net/keys")]
    public void HostilePaginationIsRejectedBeforeSending(string next)
    {
        var record = new SessionRecord
        {
            Entries = [new SessionRecordEntry
            {
                Request = new RecordedRequest { Method = "GET", Uri = $"{Root}/certificates?{Version}" },
                Response = new RecordedResponse { StatusCode = 200, Body = JsonSerializer.Serialize(new { value = new[] { new { id = "one" } }, nextLink = next }) }
            }]
        };
        var interceptor = new RecordPlaybackInterceptor(RecorderMode.Playback, record);
        using var host = new ScenarioHost(ClientType.CLI, interceptor, extensions: [ExtensionUnderTest.KeyVault]);
        var error = Assert.ThrowsAny<Exception>(() => host.RunCli("keyvault", "certificate", "list", "--vault-name", "fixture"));
        Assert.Contains("selected vault", error.ToString());
        Assert.Single(interceptor.Sent);
    }

    [Theory]
    [InlineData(System.Net.HttpStatusCode.OK)]
    [InlineData(System.Net.HttpStatusCode.Forbidden)]
    [InlineData(System.Net.HttpStatusCode.NotFound)]
    [InlineData(System.Net.HttpStatusCode.Conflict)]
    public void PowerShellRecoveryUsesProtectedTransportAndDoesNotExposeSecretMaterial(System.Net.HttpStatusCode statusCode)
    {
        const string sensitive = "private-fixture";
        using var backend = new CertificateBackend((message, _) =>
        {
            Assert.Equal(HttpMethod.Post, message.Method);
            Assert.Equal($"{Root}/deletedcertificates/test/recover?{Version}", message.RequestUri!.AbsoluteUri);
            Assert.Null(message.Content);
            return Task.FromResult(new HttpResponseMessage(statusCode)
            {
                RequestMessage = message,
                Content = new StringContent(statusCode == System.Net.HttpStatusCode.OK
                    ? JsonSerializer.Serialize(new { id = Root + "/certificates/test/version1", sid = Root + "/secrets/test/version1", cer = new byte[] { 1, 2, 3 }, x5t = "-_8", value = sensitive, pwd = sensitive })
                    : JsonSerializer.Serialize(new { error = new { message = sensitive } }))
            });
        });
        var record = new SessionRecord();
        var interceptor = new RecordPlaybackInterceptor(RecorderMode.Record, record);
        using var host = new ScenarioHost(ClientType.PowerShell, interceptor, backendHandler: backend, extensions: [ExtensionUnderTest.KeyVault]);
        var parameters = new Dictionary<string, object> { ["VaultName"] = "fixture", ["CertificateName"] = "test" };

        if (statusCode == System.Net.HttpStatusCode.OK)
        {
            var output = host.RunPowerShell("Undo-AzKeyVaultCertificateRemoval", parameters);
            var certificate = Assert.IsType<CertificateBundle>(Assert.Single(output));
            Assert.Equal($"{Root}/certificates/test/version1", certificate.Id);
            Assert.Equal(new byte[] { 1, 2, 3 }, certificate.Cer);
            Assert.Equal(new byte[] { 251, 255 }, certificate.X509Thumbprint);
            Assert.DoesNotContain(sensitive, JsonSerializer.Serialize(output));
        }
        else
        {
            var error = Assert.ThrowsAny<Exception>(() => host.RunPowerShell("Undo-AzKeyVaultCertificateRemoval", parameters));
            if (statusCode == System.Net.HttpStatusCode.Forbidden)
            {
                Assert.Contains("Access denied.", error.ToString());
                Assert.Contains("Key Vault Certificates Officer", error.ToString());
            }
            else Assert.Contains($"HTTP {(int)statusCode}", error.ToString());
            Assert.DoesNotContain(sensitive, error.ToString());
        }

        Assert.Equal(1, backend.Calls);
        Assert.Single(record.Entries);
        Assert.DoesNotContain(sensitive, JsonSerializer.Serialize(record));
    }

    private sealed class CertificateBackend(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Calls++;
            return send(request, token);
        }
    }
}