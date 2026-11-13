using System.Text.Json;
using Microsoft.Azure.Azclips.Extensions.KeyVault.Customization;
using Microsoft.Azure.Azclips.Extensions.KeyVault.Models;
using static Extensions.KeyVault.Tests.KeyVaultCommandTestHelpers;

namespace Extensions.KeyVault.Tests;

public class CreateCertificateCommandTests
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

    [Fact]
    public Task CertificateName_IsValidated() =>
        AssertRejectedBeforeNetworkAsync(new CreateCertificateCommand { CertificateName = "bad_name", Policy = """{"issuer":{"name":"Self"}}""" },
            "Certificate names must contain 1-127 letters, digits or hyphens.");

    [Fact]
    public Task Enabled_IsValidated() =>
        AssertRejectedBeforeNetworkAsync(new CreateCertificateCommand { CertificateName = "test", Policy = """{"issuer":{"name":"Self"}}""", Enabled = "maybe" },
            "Enabled must be true or false.");

    [Fact]
    public Task Expires_IsValidated() =>
        AssertRejectedBeforeNetworkAsync(new CreateCertificateCommand { CertificateName = "test", Policy = """{"issuer":{"name":"Self"}}""", Expires = "not-a-date" },
            "Dates must be UTC timestamps between 1970 and January 2038 supported by the selected API.");

    [Fact]
    public Task NotBefore_IsValidated() =>
        AssertRejectedBeforeNetworkAsync(new CreateCertificateCommand { CertificateName = "test", Policy = """{"issuer":{"name":"Self"}}""", NotBefore = "not-a-date" },
            "Dates must be UTC timestamps between 1970 and January 2038 supported by the selected API.");

    [Fact]
    public Task Tags_IsValidated() =>
        AssertRejectedBeforeNetworkAsync(new CreateCertificateCommand { CertificateName = "test", Policy = """{"issuer":{"name":"Self"}}""", TagsInput = ["novalue"] },
            "Tags must use NAME=VALUE syntax.");

    [Fact]
    public Task Policy_IsValidated() =>
        AssertRejectedBeforeNetworkAsync(new CreateCertificateCommand { CertificateName = "test", Policy = "[]" },
            "Policy must be a JSON object using supported classic CLI or REST policy fields without conflicting values.");

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(3601)]
    public Task TimeoutOutOfRange_ThrowsBeforeNetwork(int timeout) =>
        AssertRejectedBeforeNetworkAsync(
            new CreateCertificateCommand { CertificateName = "test", Policy = """{"issuer":{"name":"Self"}}""", TimeoutSeconds = timeout },
            "Timeout must be between 1 and 3600 seconds.");

    [Theory]
    [InlineData(1)]
    [InlineData(3600)]
    public async Task TimeoutAtBoundary_IsAccepted(int timeout)
    {
        var command = new CreateCertificateCommand { CertificateName = "test", Policy = """{"issuer":{"name":"Self"}}""", TimeoutSeconds = timeout };
        var output = Initialize(command, _ => Response("{\"status\":\"completed\"}"));

        await command.ProcessAsync();

        Assert.Equal("completed", Assert.IsType<CertificateOperationResult>(Assert.Single(output)).Status);
    }

    [Fact]
    public Task WithoutPolicy_ThrowsBeforeNetwork() =>
        AssertRejectedBeforeNetworkAsync(new CreateCertificateCommand { CertificateName = "test" },
            "An explicit certificate policy is required.");
}
