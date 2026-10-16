using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Core.Abstractions;
using Microsoft.Azure.Azclips.Cmdlets.KeyVault;
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
    [InlineData(false)]
    [InlineData(true)]
    public void PowerShellAcceptsClassicPolicyObjectsAndOnlyWaitsWhenRequested(bool wait)
    {
        using var backend = new CertificateBackend(async (message, token) =>
        {
            bool creating = message.Method == HttpMethod.Post;
            if (creating)
            {
                using var body = JsonDocument.Parse(await message.Content!.ReadAsStringAsync(token));
                var policy = body.RootElement.GetProperty("policy");
                Assert.Equal("Self", policy.GetProperty("issuer").GetProperty("name").GetString());
                Assert.Equal("RSA", policy.GetProperty("key_props").GetProperty("kty").GetString());
                Assert.False(policy.GetProperty("key_props").GetProperty("reuse_key").GetBoolean());
                Assert.Equal(80, policy.GetProperty("lifetime_actions")[0].GetProperty("trigger").GetProperty("lifetime_percentage").GetInt32());
            }
            var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                RequestMessage = message,
                Content = new StringContent(JsonSerializer.Serialize(new { id = Root + "/certificates/test/pending", status = creating ? "inProgress" : "completed", csr = "AQID" }))
            };
            response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.Zero);
            return response;
        });
        var policyObject = new System.Management.Automation.PSObject();
        foreach (var (name, value) in new (string, object)[]
        {
            ("IssuerName", "Self"), ("Kty", "RSA"), ("KeySize", 2048), ("ReuseKeyOnRenewal", false),
            ("SecretContentType", "application/x-pkcs12"), ("SubjectName", "CN=fixture"), ("ValidityInMonths", 12),
            ("RenewAtPercentageLifetime", 80), ("DnsNames", new[] { "fixture.example" })
        }) policyObject.Properties.Add(new System.Management.Automation.PSNoteProperty(name, value));
        using var host = new ScenarioHost(ClientType.PowerShell, new RecordPlaybackInterceptor(RecorderMode.Record, new SessionRecord()),
            backendHandler: backend, extensions: [ExtensionUnderTest.KeyVault]);
        var parameters = new Dictionary<string, object>
        {
            ["VaultName"] = "fixture", ["CertificateName"] = "test", ["CertificatePolicy"] = policyObject, ["Confirm"] = false
        };
        if (wait) parameters["Wait"] = true;
        var result = Assert.IsType<CertificateOperationResult>(Assert.Single(host.RunPowerShellCmdlet<AddAzKeyVaultCertificateCmdlet>(parameters)));
        Assert.Equal(wait ? "completed" : "inProgress", result.Status);
        Assert.Equal(wait ? 2 : 1, backend.Calls);
        Assert.Equal("AQID", result.CertificateSigningRequest);
        Assert.Equal("test", result.Name);
        Assert.Equal("fixture", result.VaultName);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void PowerShellMutationsRequirePassThruForOutput(bool deleting, bool passThru)
    {
        using var backend = new CertificateBackend((message, _) =>
        {
            Assert.Equal(deleting ? HttpMethod.Delete : HttpMethod.Patch, message.Method);
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                RequestMessage = message,
                Content = new StringContent("{\"id\":\"" + Root + "/certificates/test/version1\",\"attributes\":{\"enabled\":false},\"recoveryId\":\"recovery\"}")
            });
        });
        using var host = new ScenarioHost(ClientType.PowerShell, new RecordPlaybackInterceptor(RecorderMode.Record, new SessionRecord()),
            backendHandler: backend, extensions: [ExtensionUnderTest.KeyVault]);
        var parameters = new Dictionary<string, object> { ["VaultName"] = "fixture", ["Name"] = "test", ["Confirm"] = false, ["PassThru"] = passThru };
        if (!deleting) parameters["Enabled"] = false;
        var output = deleting ? host.RunPowerShellCmdlet<RemoveAzKeyVaultCertificateCmdlet>(parameters)
            : host.RunPowerShellCmdlet<UpdateAzKeyVaultCertificateCmdlet>(parameters);
        Assert.Equal(1, backend.Calls);
        if (!passThru) Assert.Empty(output);
        else Assert.False(Assert.IsAssignableFrom<CertificateResult>(Assert.Single(output)).Enabled);
    }

    [Fact]
    public void PowerShellReturnedPolicyCanBeEditedAndReused()
    {
        using var backend = new CertificateBackend(async (message, token) =>
        {
            if (message.Method == HttpMethod.Patch)
            {
                Assert.EndsWith("/certificates/test/policy?" + Version, message.RequestUri!.AbsoluteUri);
                using var body = JsonDocument.Parse(await message.Content!.ReadAsStringAsync(token));
                Assert.Equal(24, body.RootElement.GetProperty("x509_props").GetProperty("validity_months").GetInt32());
                Assert.Equal(75, body.RootElement.GetProperty("lifetime_actions")[0].GetProperty("trigger").GetProperty("lifetime_percentage").GetInt32());
                Assert.False(body.RootElement.TryGetProperty("id", out _));
                Assert.False(body.RootElement.TryGetProperty("key_props", out _));
                Assert.False(body.RootElement.GetProperty("attributes").TryGetProperty("created", out _));
                Assert.False(body.RootElement.GetProperty("attributes").GetProperty("enabled").GetBoolean());
            }
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                RequestMessage = message,
                Content = new StringContent("""{"id":"https://fixture.vault.azure.net/certificates/test/version1","policy":{"id":"https://fixture.vault.azure.net/certificates/test/policy","issuer":{"name":"Self"},"attributes":{"created":1700000000,"enabled":true},"x509_props":{"validity_months":12},"lifetime_actions":[{"action":{"action_type":"AutoRenew"},"trigger":{"lifetime_percentage":80}}]}}""")
            };
        });
        using var host = new ScenarioHost(ClientType.PowerShell, new RecordPlaybackInterceptor(RecorderMode.Record, new SessionRecord()),
            backendHandler: backend, extensions: [ExtensionUnderTest.KeyVault]);
        var certificate = Assert.IsType<CertificateResult>(Assert.Single(host.RunPowerShellCmdlet<GetAzKeyVaultCertificateCmdlet>(
            new Dictionary<string, object> { ["VaultName"] = "fixture", ["Name"] = "test" })));
        certificate.Policy!.ValidityInMonths = 24;
        certificate.Policy.RenewAtPercentageLifetime = 75;
        certificate.Policy.Enabled = false;
        host.RunPowerShellCmdlet<UpdateAzKeyVaultCertificateCmdlet>(new Dictionary<string, object>
        {
            ["VaultName"] = "fixture", ["Name"] = "test", ["PolicyObject"] = certificate.Policy, ["Confirm"] = false
        });
        Assert.Equal(3, backend.Calls);
    }

    [Fact]
    public void PowerShellCertificateResultHasClassicPropertiesAndPublicX509View()
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=fixture", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        using var backend = new CertificateBackend((message, _) => Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            RequestMessage = message,
            Content = new StringContent(JsonSerializer.Serialize(new
            {
                id = Root + "/certificates/test/version1", cer = certificate.RawData,
                x5t = System.Buffers.Text.Base64Url.EncodeToString(certificate.GetCertHash()),
                kid = Root + "/keys/test/version1", sid = Root + "/secrets/test/version1",
                attributes = new { enabled = false, created = 1700000000, exp = 1800000000 },
                policy = new { issuer = new { name = "Self" }, x509_props = new { subject = "CN=fixture", validity_months = 12 } }
            }))
        }));
        using var host = new ScenarioHost(ClientType.PowerShell, new RecordPlaybackInterceptor(RecorderMode.Record, new SessionRecord()),
            backendHandler: backend, extensions: [ExtensionUnderTest.KeyVault]);
        var result = Assert.IsType<CertificateResult>(Assert.Single(host.RunPowerShellCmdlet<GetAzKeyVaultCertificateCmdlet>(
            new Dictionary<string, object> { ["VaultName"] = "fixture", ["Name"] = "test" })));
        Assert.Equal("fixture", result.VaultName);
        Assert.Equal("test", result.Name);
        Assert.Equal("version1", result.Version);
        Assert.Equal(certificate.Thumbprint, result.Thumbprint);
        Assert.False(result.Enabled);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1800000000).UtcDateTime, result.Expires);
        Assert.Equal("Self", result.Policy!.IssuerName);
        Assert.Equal(12, result.Policy.ValidityInMonths);
        using var publicCertificate = result.Certificate;
        Assert.NotNull(publicCertificate);
        Assert.Same(publicCertificate, result.Certificate);
        Assert.False(publicCertificate.HasPrivateKey);
    }

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
    [InlineData(ClientType.CLI, "PFX", false)]
    [InlineData(ClientType.CLI, "PFXNoPassword", false)]
    [InlineData(ClientType.CLI, "PEM", false)]
    [InlineData(ClientType.CLI, "EncryptedPEM", false)]
    [InlineData(ClientType.PowerShell, "PFX", false)]
    [InlineData(ClientType.PowerShell, "PFXNoPassword", false)]
    [InlineData(ClientType.PowerShell, "PEM", false)]
    [InlineData(ClientType.PowerShell, "EncryptedPEM", false)]
    [InlineData(ClientType.CLI, "PFX", true)]
    [InlineData(ClientType.CLI, "PFXNoPassword", true)]
    [InlineData(ClientType.CLI, "PEM", true)]
    [InlineData(ClientType.CLI, "EncryptedPEM", true)]
    [InlineData(ClientType.PowerShell, "PFX", true)]
    [InlineData(ClientType.PowerShell, "PFXNoPassword", true)]
    [InlineData(ClientType.PowerShell, "PEM", true)]
    [InlineData(ClientType.PowerShell, "EncryptedPEM", true)]
    [InlineData(ClientType.CLI, "PFX", false, true)]
    [InlineData(ClientType.CLI, "EncryptedPEM", false, true)]
    public void ImportUsesSensitiveTransportAndRedactsRecording(ClientType clientType, string format, bool ellipticCurve, bool usePasswordArgument = false)
    {
        using AsymmetricAlgorithm key = ellipticCurve ? ECDsa.Create(ECCurve.NamedCurves.nistP256) : RSA.Create(2048);
        var request = ellipticCurve
            ? new CertificateRequest("CN=fixture", (ECDsa)key, HashAlgorithmName.SHA256)
            : new CertificateRequest("CN=fixture", (RSA)key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        string file = Path.GetTempFileName();
        string passwordFile = Path.GetTempFileName();
        const string password = "synthetic-private-password";
        using var secure = new System.Security.SecureString();
        foreach (char character in password) secure.AppendChar(character);
        bool encrypted = format is "PFX" or "EncryptedPEM";
        try
        {
            byte[] input = format.StartsWith("PFX", StringComparison.Ordinal)
                ? certificate.Export(X509ContentType.Pfx, encrypted ? password : null)
                : System.Text.Encoding.UTF8.GetBytes(certificate.ExportCertificatePem() + "\n" + (encrypted
                    ? key.ExportEncryptedPkcs8PrivateKeyPem(password, new PbeParameters(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA256, 1000))
                    : key.ExportPkcs8PrivateKeyPem()));
            File.WriteAllBytes(file, input);
            CryptographicOperations.ZeroMemory(input);
            File.WriteAllText(passwordFile, password);
            string? imported = null;
            using var backend = new CertificateBackend(async (message, token) =>
            {
                using var json = JsonDocument.Parse(await message.Content!.ReadAsStringAsync(token));
                if (encrypted) Assert.Equal(password, json.RootElement.GetProperty("pwd").GetString());
                else Assert.False(json.RootElement.TryGetProperty("pwd", out _));
                imported = json.RootElement.GetProperty("value").GetString();
                Assert.Equal(format.StartsWith("PFX", StringComparison.Ordinal) ? "application/x-pkcs12" : "application/x-pem-file",
                    json.RootElement.GetProperty("policy").GetProperty("secret_props").GetProperty("contentType").GetString());
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    RequestMessage = message,
                    Content = new StringContent(JsonSerializer.Serialize(new { id = Root + "/certificates/test/version1", cer = certificate.RawData, x5t = "-_8" }))
                };
            });
            var record = new SessionRecord();
            var interceptor = new RecordPlaybackInterceptor(RecorderMode.Record, record);
            using var host = new ScenarioHost(clientType, interceptor, backendHandler: backend, extensions: [ExtensionUnderTest.KeyVault]);
            var parameters = new Dictionary<string, object> { ["VaultName"] = "fixture", ["Name"] = "test", ["FilePath"] = file, ["Confirm"] = false };
            if (encrypted) parameters["Password"] = secure;
            string[] passwordArguments = !encrypted ? [] : usePasswordArgument
                ? ["--password", password] : ["--password-file", passwordFile];
            var output = clientType == ClientType.CLI
                ? host.RunCli(["keyvault", "certificate", "import", "--vault-name", "fixture", "--name", "test", "--file", file, .. passwordArguments])
                : host.RunPowerShellCmdlet<ImportAzKeyVaultCertificateCmdlet>(parameters);
            Assert.Equal(certificate.RawData, Assert.IsType<CertificateResult>(Assert.Single(output)).Cer);
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
    [InlineData(ClientType.CLI, false)]
    [InlineData(ClientType.CLI, true)]
    [InlineData(ClientType.PowerShell, false)]
    [InlineData(ClientType.PowerShell, true)]
    public void WrongImportPasswordIsRejectedBeforeTransport(ClientType clientType, bool pem)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=fixture", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        string file = Path.GetTempFileName();
        using var wrongPassword = new System.Security.SecureString();
        foreach (char character in "wrong-private-password") wrongPassword.AppendChar(character);
        try
        {
            byte[] input = pem
                ? System.Text.Encoding.UTF8.GetBytes(certificate.ExportCertificatePem() + "\n" + key.ExportEncryptedPkcs8PrivateKeyPem(
                    "correct-password", new PbeParameters(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA256, 1000)))
                : certificate.Export(X509ContentType.Pfx, "correct-password");
            File.WriteAllBytes(file, input);
            CryptographicOperations.ZeroMemory(input);
            using var backend = new CertificateBackend((_, _) => throw new InvalidOperationException("Unexpected HTTP request."));
            var record = new SessionRecord();
            using var host = new ScenarioHost(clientType, new RecordPlaybackInterceptor(RecorderMode.Record, record), backendHandler: backend, extensions: [ExtensionUnderTest.KeyVault]);
            var error = Assert.ThrowsAny<Exception>(() =>
            {
                if (clientType == ClientType.CLI)
                    host.RunCli("keyvault", "certificate", "import", "--vault-name", "fixture", "--name", "test", "--file", file, "--password", "wrong-private-password");
                else
                    host.RunPowerShellCmdlet<ImportAzKeyVaultCertificateCmdlet>(new Dictionary<string, object>
                    {
                        ["VaultName"] = "fixture", ["Name"] = "test", ["FilePath"] = file, ["Password"] = wrongPassword, ["Confirm"] = false
                    });
            });
            Assert.Contains("certificate file or password is invalid", error.ToString());
            Assert.DoesNotContain("wrong-private-password", error.ToString());
            Assert.Equal(0, backend.Calls);
            Assert.Empty(record.Entries);
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void ConflictingCliPasswordSourcesAreRejectedBeforeReadingFiles()
    {
        using var backend = new CertificateBackend((_, _) => throw new InvalidOperationException("Unexpected HTTP request."));
        var record = new SessionRecord();
        using var host = new ScenarioHost(ClientType.CLI, new RecordPlaybackInterceptor(RecorderMode.Record, record), backendHandler: backend, extensions: [ExtensionUnderTest.KeyVault]);
        var error = Assert.ThrowsAny<Exception>(() => host.RunCli("keyvault", "certificate", "import", "--vault-name", "fixture", "--name", "test",
            "--file", "missing.pfx", "--password", "private-password", "--password-file", "missing-password.txt"));
        Assert.Contains("Specify only one certificate password source", error.ToString());
        Assert.DoesNotContain("private-password", error.ToString());
        Assert.Equal(0, backend.Calls);
        Assert.Empty(record.Entries);
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
            Assert.IsType<CertificateOperationResult>(Assert.Single(Run("create", "Add-AzKeyVaultCertificate", ["--name", "test", "--policy", policy], new() { ["CertificateName"] = "test", ["Policy"] = policy })));
            Assert.IsType<CertificateResult>(Assert.Single(Run("import", "Import-AzKeyVaultCertificate", ["--name", "test", "--file", file], new() { ["CertificateName"] = "test", ["File"] = file })));
            Assert.IsType<CertificateResult>(Assert.Single(Run("show", "Get-AzKeyVaultCertificate_Get", ["--name", "test"], new() { ["CertificateName"] = "test" })));
            Assert.Equal(2, Assert.IsAssignableFrom<IEnumerable<CertificateMetadata>>(Assert.Single(Run("list", "Get-AzKeyVaultCertificate_List", [], new()))).Count());
            Assert.Single(Assert.IsAssignableFrom<IEnumerable<CertificateMetadata>>(Assert.Single(Run("list-versions", "Get-AzKeyVaultCertificate_Versions", ["--name", "test"], new() { ["CertificateName"] = "test" }))));
            Run("set-attributes", "Update-AzKeyVaultCertificate", ["--name", "test", "--enabled", "false"], new() { ["CertificateName"] = "test", ["Enabled"] = "false" });
            Assert.IsType<CertificateDownloadResult>(Assert.Single(Run("download", "Export-AzKeyVaultCertificate", ["--name", "test", "--file", download], new() { ["CertificateName"] = "test", ["File"] = download })));
            Assert.Equal("recovery", Assert.IsType<DeletedCertificateResult>(Assert.Single(Run("delete", "Remove-AzKeyVaultCertificate", ["--name", "test", "--yes"], new() { ["CertificateName"] = "test", ["Yes"] = true }))).RecoveryId);
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
        var parameters = new Dictionary<string, object> { ["VaultName"] = "fixture", ["Name"] = "test", ["Confirm"] = false };

        if (statusCode == System.Net.HttpStatusCode.OK)
        {
            var output = host.RunPowerShellCmdlet<UndoAzKeyVaultCertificateRemovalCmdlet>(parameters);
            var certificate = Assert.IsType<CertificateResult>(Assert.Single(output));
            Assert.Equal($"{Root}/certificates/test/version1", certificate.Id);
            Assert.Equal(new byte[] { 1, 2, 3 }, certificate.Cer);
            Assert.Equal(new byte[] { 251, 255 }, certificate.X509Thumbprint);
            Assert.DoesNotContain(sensitive, JsonSerializer.Serialize(output));
        }
        else
        {
            var error = Assert.ThrowsAny<Exception>(() => host.RunPowerShellCmdlet<UndoAzKeyVaultCertificateRemovalCmdlet>(parameters));
            if (statusCode == System.Net.HttpStatusCode.Forbidden)
            {
                Assert.Contains("Access denied.", error.ToString());
                Assert.Contains("Check the resource permissions and network access rules.", error.ToString());
                Assert.DoesNotContain("Key Vault", error.Message);
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