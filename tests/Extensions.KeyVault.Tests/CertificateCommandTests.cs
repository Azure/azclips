using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Abstractions.Runtime.Handler;
using Microsoft.Azure.Azclips.Extensions.KeyVault.Customization;
using Microsoft.Azure.Azclips.Extensions.KeyVault.Models;
using Microsoft.Azure.Azclips.Extensions.KeyVault.Processors;
using Moq;

namespace Extensions.KeyVault.Tests;

public class CertificateCommandTests
{
    [Theory]
    [InlineData("""{"issuer_parameters":{"name":"Self"},"key_properties":{"key_type":"RSA","key_size":2048,"exportable":false},"secret_properties":{"content_type":"application/x-pkcs12"},"x509_certificate_properties":{"subject":"CN=fixture","validity_in_months":12,"subject_alternative_names":{"dns_names":["fixture.example"]}}}""")]
    [InlineData("""{"issuerParameters":{"name":"Self"},"keyProperties":{"keyType":"RSA","keySize":2048,"exportable":false},"secretProperties":{"contentType":"application/x-pkcs12"},"x509CertificateProperties":{"subject":"CN=fixture","validityInMonths":12,"subjectAlternativeNames":{"dnsNames":["fixture.example"]}}}""")]
    [InlineData("""{"issuer":{"name":"Self"},"key_props":{"kty":"RSA","key_size":2048,"exportable":false},"secret_props":{"contentType":"application/x-pkcs12"},"x509_props":{"subject":"CN=fixture","validity_months":12,"sans":{"dns_names":["fixture.example"]}}}""")]
    public async Task CreateAcceptsClassicAndRestPolicyShapes(string policy)
    {
        var command = new CreateCertificateCommand { CertificateName = "test", Policy = policy };
        Initialize(command, async request =>
        {
            using var document = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
            var actual = document.RootElement.GetProperty("policy");
            Assert.Equal("Self", actual.GetProperty("issuer").GetProperty("name").GetString());
            Assert.Equal("RSA", actual.GetProperty("key_props").GetProperty("kty").GetString());
            Assert.False(actual.GetProperty("key_props").GetProperty("exportable").GetBoolean());
            Assert.Equal(12, actual.GetProperty("x509_props").GetProperty("validity_months").GetInt32());
            Assert.Equal("fixture.example", actual.GetProperty("x509_props").GetProperty("sans").GetProperty("dns_names")[0].GetString());
            return await Response("{\"status\":\"completed\"}");
        });
        await command.ProcessAsync();
    }

    [Fact]
    public async Task ConflictingPolicyAliasesAreRejectedBeforeSending()
    {
        var command = new CreateCertificateCommand
        {
            CertificateName = "test", Policy = """{"issuer":{"name":"Self"},"issuer_parameters":{"name":"Unknown"}}"""
        };
        Initialize(command, _ => throw new InvalidOperationException("Unexpected HTTP request."));
        await Assert.ThrowsAsync<ArgumentException>(() => command.ProcessAsync());
    }

    [Fact]
    public async Task NullPowerShellPolicyFieldsDoNotCreateEmptyRestSections()
    {
        var command = new CreateCertificateCommand
        {
            CertificateName = "test",
            Policy = """{"IssuerName":"Self","Kty":null,"SubjectName":null,"DnsNames":null,"Emails":null,"UserPrincipalNames":null,"RenewAtPercentageLifetime":null}"""
        };
        Initialize(command, async request =>
        {
            using var document = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
            var policy = document.RootElement.GetProperty("policy");
            Assert.Equal("Self", policy.GetProperty("issuer").GetProperty("name").GetString());
            Assert.False(policy.TryGetProperty("key_props", out _));
            Assert.False(policy.TryGetProperty("x509_props", out _));
            Assert.False(policy.TryGetProperty("lifetime_actions", out _));
            return await Response("{\"status\":\"completed\"}");
        });
        await command.ProcessAsync();
    }

    [Fact]
    public async Task RecoverSendsBodylessPostAndReturnsPublicCertificateWithoutFetchingSecret()
    {
        var command = new RecoverCertificateCommand { CertificateName = "test" };
        int calls = 0;
        var output = Initialize(command, request =>
        {
            calls++;
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("https://fixture.vault.azure.net/deletedcertificates/test/recover?api-version=2025-07-01", request.RequestUri!.AbsoluteUri);
            Assert.Null(request.Content);
            return Response("{\"id\":\"https://fixture.vault.azure.net/certificates/test/version1\",\"sid\":\"https://fixture.vault.azure.net/secrets/test/version1\",\"cer\":\"AQID\",\"x5t\":\"-_8\",\"attributes\":{\"enabled\":false},\"value\":\"private-fixture\",\"pwd\":\"private-fixture\"}");
        });

        await command.ProcessAsync();

        Assert.Equal(1, calls);
        var certificate = Assert.IsType<CertificateResult>(Assert.Single(output));
        Assert.Equal("https://fixture.vault.azure.net/certificates/test/version1", certificate.Id);
        Assert.Equal(new byte[] { 1, 2, 3 }, certificate.Cer);
        Assert.Equal(new byte[] { 251, 255 }, certificate.X509Thumbprint);
        Assert.DoesNotContain("private-fixture", JsonSerializer.Serialize(output));
    }

    [Theory]
    [InlineData("")]
    [InlineData("../test")]
    [InlineData("test*")]
    [InlineData("test%2Frecover")]
    public async Task RecoverRejectsInvalidNamesBeforeNetwork(string name)
    {
        var command = new RecoverCertificateCommand { CertificateName = name };
        var output = Initialize(command, _ => throw new InvalidOperationException("Unexpected request."));

        await Assert.ThrowsAsync<ArgumentException>(() => command.ProcessAsync());

        Assert.Empty(output);
    }

    [Theory]
    [InlineData(false, "Self", 2)]
    [InlineData(true, "Self", 1)]
    [InlineData(false, "Unknown", 1)]
    public async Task CreatePollsOnlyWhenAppropriate(bool noWait, string issuer, int expectedCalls)
    {
        var command = new CreateCertificateCommand
        {
            CertificateName = "test", NoWait = noWait,
            CertificatePolicy = new CertificatePolicy { IssuerParametersName = issuer },
            CertificateAttributesEnabled = false, CertificateAttributesExpires = 1800000000
        };
        int calls = 0;
        var output = Initialize(command, async request =>
        {
            if (++calls == 1)
            {
                Assert.Equal(HttpMethod.Post, request.Method);
                using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
                Assert.False(body.RootElement.GetProperty("attributes").GetProperty("enabled").GetBoolean());
                Assert.Equal(1800000000, body.RootElement.GetProperty("attributes").GetProperty("exp").GetInt32());
                return await Response("{\"status\":\"inProgress\",\"target\":\"https://attacker.example/\"}");
            }
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.EndsWith("/certificates/test/pending?api-version=2025-07-01", request.RequestUri!.AbsoluteUri);
            return await Response("{\"status\":\"completed\"}");
        });
        await command.ProcessAsync();
        Assert.Equal(expectedCalls, calls);
        Assert.Equal(expectedCalls == 2 ? "completed" : "inProgress", Assert.IsType<CertificateOperationResult>(Assert.Single(output)).Status);
    }

    [Theory]
    [InlineData("failed")]
    [InlineData("canceled")]
    [InlineData("cancelled")]
    [InlineData("unexpected")]
    public async Task CreateRejectsFailureOrUnknownStatusWithoutLeakingDetails(string state)
    {
        var command = new CreateCertificateCommand { CertificateName = "test", Policy = "{\"issuer\":{\"name\":\"Self\"}}" };
        Initialize(command, _ => Response(JsonSerializer.Serialize(new { status = state, error = new { message = "private-fixture" } })));
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => command.ProcessAsync());
        Assert.DoesNotContain("private-fixture", error.ToString());
    }

    [Fact]
    public async Task CreateTimesOutWhileHonoringRetryAfter()
    {
        var command = new CreateCertificateCommand { CertificateName = "test", Policy = "{}", TimeoutSeconds = 1 };
        int calls = 0;
        Initialize(command, async _ =>
        {
            calls++;
            var response = await Response("{\"status\":\"inProgress\"}");
            response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(10));
            return response;
        });
        await Assert.ThrowsAsync<TimeoutException>(() => command.ProcessAsync());
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task CreateCancellationStopsBeforePolling()
    {
        using var cancellation = new CancellationTokenSource();
        var command = new CreateCertificateCommand { CertificateName = "test", Policy = "{}" };
        int calls = 0;
        Initialize(command, _ => { calls++; cancellation.Cancel(); return Response("{\"status\":\"inProgress\"}"); }, cancellation.Token);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => command.ProcessAsync());
        Assert.Equal(1, calls);
    }

    [Theory]
    [InlineData(false, null)]
    [InlineData(true, null)]
    [InlineData(false, "version1")]
    [InlineData(true, "version1")]
    public async Task UpdatePolicyComposesNamedPolicyThenReadOrAttributes(bool attributes, string? version)
    {
        var command = new UpdateCertificateCommand { CertificateName = "test", CertificateVersion = version, Policy = "{\"x509_props\":{\"validity_months\":12}}" };
        if (attributes) { command.CertificateAttributesEnabled = false; command.Tags = new Dictionary<string, string>(); }
        int calls = 0;
        Initialize(command, async request =>
        {
            if (++calls == 1)
            {
                Assert.EndsWith("/certificates/test/policy?api-version=2025-07-01", request.RequestUri!.AbsoluteUri);
                Assert.Equal(HttpMethod.Patch, request.Method);
                using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
                Assert.Equal(12, body.RootElement.GetProperty("x509_props").GetProperty("validity_months").GetInt32());
            }
            else
            {
                Assert.Equal(attributes ? HttpMethod.Patch : HttpMethod.Get, request.Method);
                if (attributes)
                {
                    Assert.EndsWith($"/certificates/test/{version}?api-version=2025-07-01", request.RequestUri!.AbsoluteUri);
                    using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
                    Assert.False(body.RootElement.GetProperty("attributes").GetProperty("enabled").GetBoolean());
                    Assert.False(body.RootElement.TryGetProperty("policy", out _));
                    Assert.Empty(body.RootElement.GetProperty("tags").EnumerateObject());
                }
            }
            return await Response("{}");
        });
        await command.ProcessAsync();
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task InvalidPolicySendsNothing()
    {
        var command = new UpdateCertificateCommand { CertificateName = "test", Policy = "{\"typo\":true}" };
        Initialize(command, _ => throw new Exception("Network must not run."));
        await Assert.ThrowsAsync<ArgumentException>(() => command.ProcessAsync());
    }

    [Fact]
    public async Task ClassicAtFilePolicySyntaxIsSupported()
    {
        string file = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(file, """{"issuer_parameters":{"name":"Self"}}""");
            var command = new CreateCertificateCommand { CertificateName = "test", Policy = "@" + file };
            Initialize(command, async request =>
            {
                using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
                Assert.Equal("Self", body.RootElement.GetProperty("policy").GetProperty("issuer").GetProperty("name").GetString());
                return await Response("{\"status\":\"completed\"}");
            });
            await command.ProcessAsync();
        }
        finally { File.Delete(file); }
    }

    [Theory]
    [InlineData("PFX")]
    [InlineData("PEM")]
    [InlineData("EncryptedPEM")]
    public async Task ImportPreservesFileContentAndClearsSensitiveReferences(string format)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=fixture", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        const string password = "synthetic-password";
        byte[] bytes = format == "PFX" ? certificate.Export(X509ContentType.Pfx, password)
            : System.Text.Encoding.UTF8.GetBytes(certificate.ExportCertificatePem() + "\n" + (format == "PEM"
                ? key.ExportPkcs8PrivateKeyPem()
                : key.ExportEncryptedPkcs8PrivateKeyPem(password, new PbeParameters(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA256, 1000))));
        string file = Path.GetTempFileName();
        using var secure = new System.Security.SecureString();
        foreach (char character in password) secure.AppendChar(character);
        try
        {
            await File.WriteAllBytesAsync(file, bytes);
            var command = new ImportCertificateCommand { CertificateName = "test", File = file, SecurePassword = secure, CertificateAttributesEnabled = false };
            Initialize(command, async message =>
            {
                using var body = JsonDocument.Parse(await message.Content!.ReadAsStringAsync());
                Assert.Equal(password, body.RootElement.GetProperty("pwd").GetString());
                Assert.False(body.RootElement.GetProperty("attributes").GetProperty("enabled").GetBoolean());
                Assert.Equal(format == "PFX" ? Convert.ToBase64String(bytes) : System.Text.Encoding.UTF8.GetString(bytes), body.RootElement.GetProperty("value").GetString());
                return await Response("{}");
            });
            await command.ProcessAsync();
            Assert.Null(command.Base64EncodedCertificate);
            Assert.Null(command.Password);
            Assert.Null(command.SecurePassword);
        }
        finally { File.Delete(file); CryptographicOperations.ZeroMemory(bytes); }
    }

    [Fact]
    public async Task ImportFailureClearsPasswordAndDoesNotExposeIt()
    {
        string file = Path.GetTempFileName();
        try
        {
            await File.WriteAllBytesAsync(file, [1, 2, 3]);
            var command = new ImportCertificateCommand { CertificateName = "test", File = file, Password = "private-fixture" };
            Initialize(command, _ => throw new Exception("Network must not run."));
            var error = await Assert.ThrowsAsync<ArgumentException>(() => command.ProcessAsync());
            Assert.DoesNotContain("private-fixture", error.ToString());
            Assert.Null(command.Password);
            Assert.Null(command.Base64EncodedCertificate);
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public async Task ShowDecodesBase64UrlThumbprintWithoutRetrievingSecret()
    {
        var command = new ShowCertificateCommand { CertificateName = "test", CertificateVersion = "version1" };
        var output = Initialize(command, request =>
        {
            Assert.EndsWith("/certificates/test/version1?api-version=2025-07-01", request.RequestUri!.AbsoluteUri);
            return Response("{\"x5t\":\"-_8\",\"sid\":\"https://fixture.vault.azure.net/secrets/test\"}");
        });
        await command.ProcessAsync();
        Assert.Equal(new byte[] { 251, 255 }, Assert.IsType<CertificateResult>(Assert.Single(output)).X509Thumbprint);
    }

    [Theory]
    [InlineData(false, null)]
    [InlineData(true, null)]
    [InlineData(false, 1)]
    [InlineData(true, 1)]
    public async Task ListsPreserveInheritedPageSizeAndEnumeratePages(bool versions, int? pageSize)
    {
        KeyVaultDataPlaneProcessorBase command = versions
            ? new ListCertificateVersionsCommand { CertificateName = "test", Maxresults = pageSize }
            : new ListCertificatesCommand { Maxresults = pageSize, IncludePending = false };
        int calls = 0;
        var output = Initialize(command, request =>
        {
            if (++calls == 1)
            {
                Assert.Equal(pageSize.HasValue, request.RequestUri!.Query.Contains("maxresults="));
                if (!versions) Assert.Contains("includePending=false", request.RequestUri.Query);
                return Response("{\"value\":[{\"id\":\"one\"}],\"nextLink\":\"https://fixture.vault.azure.net/certificates?skiptoken=next\"}");
            }
            return Response("{\"value\":[{\"id\":\"two\"}]}");
        });
        await command.ProcessAsync();
        Assert.Equal(2, calls);
        Assert.Equal(2, Assert.IsAssignableFrom<IEnumerable<CertificateMetadata>>(Assert.Single(output)).Count());
    }

    [Fact]
    public async Task DeleteRequiresConfirmationAndReturnsRecoveryMetadata()
    {
        var command = new RemoveCertificateCommand { CertificateName = "test" };
        int calls = 0;
        var output = Initialize(command, request =>
        {
            calls++;
            Assert.Equal(HttpMethod.Delete, request.Method);
            return Response("{\"recoveryId\":\"recovery\",\"deletedDate\":123}");
        });
        await Assert.ThrowsAsync<ArgumentException>(() => command.ProcessAsync());
        Assert.Equal(0, calls);
        command.Yes = true;
        await command.ProcessAsync();
        Assert.Equal("recovery", Assert.IsType<DeletedCertificateResult>(Assert.Single(output)).RecoveryId);
        Assert.Equal(1, calls);
    }

    [Theory]
    [InlineData("DER")]
    [InlineData("PEM")]
    public async Task DownloadWritesOnlyPublicCertificateAndRefusesOverwrite(string encoding)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=fixture", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        string destination = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".cer");
        try
        {
            var command = new DownloadCertificateCommand { CertificateName = "test", File = destination, Encoding = encoding };
            int calls = 0;
            Initialize(command, _ => { calls++; return Response(JsonSerializer.Serialize(new { cer = certificate.RawData })); });
            await command.ProcessAsync();
            using var downloaded = encoding == "DER"
                ? X509CertificateLoader.LoadCertificateFromFile(destination)
                : X509Certificate2.CreateFromPem(await File.ReadAllTextAsync(destination));
            Assert.Equal(certificate.Thumbprint, downloaded.Thumbprint);
            Assert.False(downloaded.HasPrivateKey);
            await Assert.ThrowsAsync<IOException>(() => command.ProcessAsync());
            Assert.Equal(1, calls);
        }
        finally { File.Delete(destination); }
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"cer\":\"AQID\"}")]
    public async Task InvalidDownloadCreatesNoFile(string response)
    {
        string destination = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".cer");
        var command = new DownloadCertificateCommand { CertificateName = "test", File = destination };
        Initialize(command, _ => Response(response));
        await Assert.ThrowsAsync<InvalidOperationException>(() => command.ProcessAsync());
        Assert.False(File.Exists(destination));
    }

    private static Task<HttpResponseMessage> Response(string body) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) });

    internal static List<object> Initialize(KeyVaultDataPlaneProcessorBase command, Func<HttpRequestMessage, Task<HttpResponseMessage>> send, CancellationToken token = default)
    {
        var publisher = new Mock<IEventPublisher>();
        publisher.SetReturnsDefault(Task.CompletedTask);
        publisher.SetupGet(value => value.Token).Returns(token);
        publisher.SetupGet(value => value.Cancel).Returns(() => { });
        var pipeline = new Mock<IRuntimeHttpPipeline>();
        pipeline.SetupGet(value => value.PollingInterval).Returns(0);
        pipeline.Setup(value => value.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<IEventPublisher>()))
            .Returns((HttpRequestMessage request, IEventPublisher _) => send(request));
        var output = new List<object>();
        var outputHandler = new Mock<IOutputHandler>();
        outputHandler.SetupGet(value => value.WriteOutput).Returns((value, _) => { output.Add(value); return Task.CompletedTask; });
        command.VaultName = "fixture";
        command.Init(new ProcessorContext(pipeline.Object, publisher.Object, Mock.Of<IAuthHandler>(),
            outputHandler.Object,
            new Profile { TenantId = "fixture-tenant" }));
        return output;
    }
}