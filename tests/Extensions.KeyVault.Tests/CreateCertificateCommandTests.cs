using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.KeyVault.Customization;
using Microsoft.Azure.Azclips.Extensions.KeyVault.Models;
using Moq;
using static Extensions.KeyVault.Tests.KeyVaultCommandTestHelpers;

namespace Extensions.KeyVault.Tests;

public class CreateCertificateCommandTests
{
    private const string SelfIssuedPolicy = """{"issuer":{"name":"Self"}}""";
    private const string CompletedOperation = "{\"status\":\"completed\"}";
    // The operation names an attacker-controlled target so polling must not follow it.
    private const string InProgressOperation = "{\"status\":\"inProgress\",\"target\":\"https://attacker.example/\"}";
    private const string NullPowerShellPolicy = """{"IssuerName":"Self","Kty":null,"SubjectName":null,"DnsNames":null,"Emails":null,"UserPrincipalNames":null,"RenewAtPercentageLifetime":null}""";
    private const string CreateRequest = "POST https://fixture.vault.azure.net/certificates/test/create?api-version=2025-07-01";
    private const string PendingRequest = "GET https://fixture.vault.azure.net/certificates/test/pending?api-version=2025-07-01";
    private const string NameMessage = "Certificate names must contain 1-127 letters, digits or hyphens.";
    private const string EnabledMessage = "Enabled must be true or false.";
    private const string DateMessage = "Dates must be UTC timestamps between 1970 and January 2038 supported by the selected API.";
    private const string TagsMessage = "Tags must use NAME=VALUE syntax.";
    private const string PolicyMessage = "Policy must be a JSON object using supported classic CLI or REST policy fields without conflicting values.";

    // Strict: a request the test did not set up throws MockException, so a test that gets its expected
    // exception also proves no unexpected request was sent.
    private readonly Mock<IRuntimeHttpPipeline> _mockPipeline = CreatePipelineMock();

    [Theory]
    [InlineData("""{"issuer_parameters":{"name":"Self"},"key_properties":{"key_type":"RSA","key_size":2048,"exportable":false},"secret_properties":{"content_type":"application/x-pkcs12"},"x509_certificate_properties":{"subject":"CN=fixture","validity_in_months":12,"subject_alternative_names":{"dns_names":["fixture.example"]}}}""")]
    [InlineData("""{"issuerParameters":{"name":"Self"},"keyProperties":{"keyType":"RSA","keySize":2048,"exportable":false},"secretProperties":{"contentType":"application/x-pkcs12"},"x509CertificateProperties":{"subject":"CN=fixture","validityInMonths":12,"subjectAlternativeNames":{"dnsNames":["fixture.example"]}}}""")]
    [InlineData("""{"issuer":{"name":"Self"},"key_props":{"kty":"RSA","key_size":2048,"exportable":false},"secret_props":{"contentType":"application/x-pkcs12"},"x509_props":{"subject":"CN=fixture","validity_months":12,"sans":{"dns_names":["fixture.example"]}}}""")]
    public async Task ProcessAsync_ClassicOrRestPolicyShape_SendsNormalizedRestPolicy(string policy)
    {
        var command = new CreateCertificateCommand { CertificateName = "test", Policy = policy };
        List<string?> sentBodies = _mockPipeline.RespondWith(CompletedOperation);
        Initialize(command, _mockPipeline);

        await command.ProcessAsync();

        Assert.Equal(("Self", "RSA", false, 12, "fixture.example"), PolicySummary(sentBodies.Single()));
    }

    [Fact]
    public async Task ProcessAsync_ConflictingPolicyAliases_ThrowsArgumentException()
    {
        var command = new CreateCertificateCommand
        {
            CertificateName = "test", Policy = """{"issuer":{"name":"Self"},"issuer_parameters":{"name":"Unknown"}}"""
        };
        Initialize(command, _mockPipeline);

        Exception? exception = await Record.ExceptionAsync(command.ProcessAsync);

        Assert.Equal(PolicyMessage, Assert.IsType<ArgumentException>(exception).Message);
    }

    [Fact]
    public async Task ProcessAsync_NullPowerShellPolicyFields_SendsIssuerName()
    {
        var command = new CreateCertificateCommand { CertificateName = "test", Policy = NullPowerShellPolicy };
        List<string?> sentBodies = _mockPipeline.RespondWith(CompletedOperation);
        Initialize(command, _mockPipeline);

        await command.ProcessAsync();

        Assert.Equal("Self", JsonNode.Parse(sentBodies.Single()!)!["policy"]!["issuer"]!["name"]!.GetValue<string>());
    }

    [Fact]
    public async Task ProcessAsync_NullPowerShellPolicyFields_OmitsEmptyRestSections()
    {
        var command = new CreateCertificateCommand { CertificateName = "test", Policy = NullPowerShellPolicy };
        List<string?> sentBodies = _mockPipeline.RespondWith(CompletedOperation);
        Initialize(command, _mockPipeline);

        await command.ProcessAsync();

        Assert.Empty(JsonNode.Parse(sentBodies.Single()!)!["policy"]!.AsObject().Select(p => p.Key).Intersect(["key_props", "x509_props", "lifetime_actions"]));
    }

    [Theory]
    [InlineData(false, "Self", new[] { CreateRequest, PendingRequest })]
    [InlineData(true, "Self", new[] { CreateRequest })]
    [InlineData(false, "Unknown", new[] { CreateRequest })]
    public async Task ProcessAsync_InProgressOperation_PollsPendingOperationOnlyWhenWaiting(bool noWait, string issuer, string[] expectedRequests)
    {
        var command = CreateInProgressCommand(noWait: noWait, issuer: issuer);
        _mockPipeline.RespondWith(InProgressOperation, CompletedOperation);
        Initialize(command, _mockPipeline);

        await command.ProcessAsync();

        Assert.Equal(expectedRequests, _mockPipeline.RequestLines());
    }

    [Theory]
    [InlineData(false, "Self", "completed")]
    [InlineData(true, "Self", "inProgress")]
    [InlineData(false, "Unknown", "inProgress")]
    public async Task ProcessAsync_InProgressOperation_WritesLastOperationStatus(bool noWait, string issuer, string expectedStatus)
    {
        var command = CreateInProgressCommand(noWait: noWait, issuer: issuer);
        _mockPipeline.RespondWith(InProgressOperation, CompletedOperation);
        var output = Initialize(command, _mockPipeline);

        await command.ProcessAsync();

        Assert.Equal([expectedStatus], output.Cast<CertificateOperationResult>().Select(r => r.Status));
    }

    [Theory]
    [InlineData(false, "Self")]
    [InlineData(true, "Self")]
    [InlineData(false, "Unknown")]
    public async Task ProcessAsync_InheritedAttributes_SendsAttributesInCreateRequest(bool noWait, string issuer)
    {
        var command = CreateInProgressCommand(noWait: noWait, issuer: issuer);
        List<string?> sentBodies = _mockPipeline.RespondWith(InProgressOperation, CompletedOperation);
        Initialize(command, _mockPipeline);

        await command.ProcessAsync();

        Assert.Equal((false, 1800000000), AttributesSummary(sentBodies[0]));
    }

    [Theory]
    [InlineData("failed", "Certificate creation failed. Sensitive service details omitted.")]
    [InlineData("canceled", "Certificate creation canceled. Sensitive service details omitted.")]
    [InlineData("cancelled", "Certificate creation cancelled. Sensitive service details omitted.")]
    [InlineData("unexpected", "The certificate operation returned an unsupported status.")]
    public async Task ProcessAsync_FailedOrUnknownStatus_ThrowsInvalidOperationException(string state, string message)
    {
        var command = new CreateCertificateCommand { CertificateName = "test", Policy = SelfIssuedPolicy };
        _mockPipeline.RespondWith(FailedOperation(state));
        Initialize(command, _mockPipeline);

        Exception? exception = await Record.ExceptionAsync(command.ProcessAsync);

        Assert.Equal(message, Assert.IsType<InvalidOperationException>(exception).Message);
    }

    [Theory]
    [InlineData("failed")]
    [InlineData("canceled")]
    [InlineData("cancelled")]
    [InlineData("unexpected")]
    public async Task ProcessAsync_FailedOrUnknownStatus_OmitsServiceErrorDetailsFromException(string state)
    {
        var command = new CreateCertificateCommand { CertificateName = "test", Policy = SelfIssuedPolicy };
        _mockPipeline.RespondWith(FailedOperation(state));
        Initialize(command, _mockPipeline);

        Exception? exception = await Record.ExceptionAsync(command.ProcessAsync);

        Assert.DoesNotContain("private-fixture", exception!.ToString());
    }

    [Fact]
    public async Task ProcessAsync_RetryAfterBeyondTimeout_ThrowsTimeoutException()
    {
        var command = new CreateCertificateCommand { CertificateName = "test", Policy = "{}", TimeoutSeconds = 1 };
        _mockPipeline.RespondWith(_ => InProgressWithRetryAfterAsync());
        Initialize(command, _mockPipeline);

        Exception? exception = await Record.ExceptionAsync(command.ProcessAsync);

        Assert.Equal("Certificate creation timed out. The server operation may still be pending.", Assert.IsType<TimeoutException>(exception).Message);
    }

    [Fact]
    public async Task ProcessAsync_RetryAfterBeyondTimeout_SendsNoPollRequest()
    {
        var command = new CreateCertificateCommand { CertificateName = "test", Policy = "{}", TimeoutSeconds = 1 };
        _mockPipeline.RespondWith(_ => InProgressWithRetryAfterAsync());
        Initialize(command, _mockPipeline);

        await Record.ExceptionAsync(command.ProcessAsync);

        Assert.Equal([CreateRequest], _mockPipeline.RequestLines());
    }

    [Fact]
    public async Task ProcessAsync_CancelledAfterCreate_ThrowsOperationCanceledException()
    {
        using var cancellation = new CancellationTokenSource();
        var command = new CreateCertificateCommand { CertificateName = "test", Policy = "{}" };
        _mockPipeline.RespondWith(_ => { cancellation.Cancel(); return Response("{\"status\":\"inProgress\"}"); });
        Initialize(command, _mockPipeline, cancellation.Token);

        Exception? exception = await Record.ExceptionAsync(command.ProcessAsync);

        Assert.IsAssignableFrom<OperationCanceledException>(exception);
    }

    [Fact]
    public async Task ProcessAsync_CancelledAfterCreate_SendsNoPollRequest()
    {
        using var cancellation = new CancellationTokenSource();
        var command = new CreateCertificateCommand { CertificateName = "test", Policy = "{}" };
        _mockPipeline.RespondWith(_ => { cancellation.Cancel(); return Response("{\"status\":\"inProgress\"}"); });
        Initialize(command, _mockPipeline, cancellation.Token);

        await Record.ExceptionAsync(command.ProcessAsync);

        Assert.Equal([CreateRequest], _mockPipeline.RequestLines());
    }

    [Fact]
    public async Task ProcessAsync_ClassicAtFilePolicy_SendsPolicyFromFile()
    {
        using TempFile file = TempFile.CreateEmpty();
        await File.WriteAllTextAsync(file.Path, """{"issuer_parameters":{"name":"Self"}}""");
        var command = new CreateCertificateCommand { CertificateName = "test", Policy = "@" + file.Path };
        List<string?> sentBodies = _mockPipeline.RespondWith(CompletedOperation);
        Initialize(command, _mockPipeline);

        await command.ProcessAsync();

        Assert.Equal("Self", JsonNode.Parse(sentBodies.Single()!)!["policy"]!["issuer"]!["name"]!.GetValue<string>());
    }

    [Fact]
    public async Task ProcessAsync_InvalidCertificateName_ThrowsArgumentException()
    {
        var command = new CreateCertificateCommand { CertificateName = "bad_name", Policy = SelfIssuedPolicy };
        Initialize(command, _mockPipeline);

        Exception? exception = await Record.ExceptionAsync(command.ProcessAsync);

        Assert.Equal(NameMessage, Assert.IsType<ArgumentException>(exception).Message);
    }

    [Fact]
    public async Task ProcessAsync_InvalidEnabled_ThrowsArgumentException()
    {
        var command = new CreateCertificateCommand { CertificateName = "test", Policy = SelfIssuedPolicy, Enabled = "maybe" };
        Initialize(command, _mockPipeline);

        Exception? exception = await Record.ExceptionAsync(command.ProcessAsync);

        Assert.Equal(EnabledMessage, Assert.IsType<ArgumentException>(exception).Message);
    }

    [Fact]
    public async Task ProcessAsync_InvalidExpires_ThrowsArgumentException()
    {
        var command = new CreateCertificateCommand { CertificateName = "test", Policy = SelfIssuedPolicy, Expires = "not-a-date" };
        Initialize(command, _mockPipeline);

        Exception? exception = await Record.ExceptionAsync(command.ProcessAsync);

        Assert.Equal(DateMessage, Assert.IsType<ArgumentException>(exception).Message);
    }

    [Fact]
    public async Task ProcessAsync_InvalidNotBefore_ThrowsArgumentException()
    {
        var command = new CreateCertificateCommand { CertificateName = "test", Policy = SelfIssuedPolicy, NotBefore = "not-a-date" };
        Initialize(command, _mockPipeline);

        Exception? exception = await Record.ExceptionAsync(command.ProcessAsync);

        Assert.Equal(DateMessage, Assert.IsType<ArgumentException>(exception).Message);
    }

    [Fact]
    public async Task ProcessAsync_TagWithoutValue_ThrowsArgumentException()
    {
        var command = new CreateCertificateCommand { CertificateName = "test", Policy = SelfIssuedPolicy, TagsInput = ["novalue"] };
        Initialize(command, _mockPipeline);

        Exception? exception = await Record.ExceptionAsync(command.ProcessAsync);

        Assert.Equal(TagsMessage, Assert.IsType<ArgumentException>(exception).Message);
    }

    [Fact]
    public async Task ProcessAsync_NonObjectPolicy_ThrowsArgumentException()
    {
        var command = new CreateCertificateCommand { CertificateName = "test", Policy = "[]" };
        Initialize(command, _mockPipeline);

        Exception? exception = await Record.ExceptionAsync(command.ProcessAsync);

        Assert.Equal(PolicyMessage, Assert.IsType<ArgumentException>(exception).Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(3601)]
    public async Task ProcessAsync_TimeoutOutOfRange_ThrowsArgumentException(int timeout)
    {
        var command = new CreateCertificateCommand { CertificateName = "test", Policy = SelfIssuedPolicy, TimeoutSeconds = timeout };
        Initialize(command, _mockPipeline);

        Exception? exception = await Record.ExceptionAsync(command.ProcessAsync);

        Assert.Equal("Timeout must be between 1 and 3600 seconds.", Assert.IsType<ArgumentException>(exception).Message);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3600)]
    public async Task ProcessAsync_TimeoutAtBoundary_WritesCompletedOperation(int timeout)
    {
        var command = new CreateCertificateCommand { CertificateName = "test", Policy = SelfIssuedPolicy, TimeoutSeconds = timeout };
        _mockPipeline.RespondWith(CompletedOperation);
        var output = Initialize(command, _mockPipeline);

        await command.ProcessAsync();

        Assert.Equal(["completed"], output.Cast<CertificateOperationResult>().Select(r => r.Status));
    }

    [Fact]
    public async Task ProcessAsync_NoPolicy_ThrowsArgumentException()
    {
        var command = new CreateCertificateCommand { CertificateName = "test" };
        Initialize(command, _mockPipeline);

        Exception? exception = await Record.ExceptionAsync(command.ProcessAsync);

        Assert.Equal("An explicit certificate policy is required.", Assert.IsType<ArgumentException>(exception).Message);
    }

    private CreateCertificateCommand CreateInProgressCommand(bool noWait, string issuer) =>
        new()
        {
            CertificateName = "test",
            NoWait = noWait,
            CertificatePolicy = new CertificatePolicy { IssuerParametersName = issuer },
            CertificateAttributesEnabled = false,
            CertificateAttributesExpires = 1800000000
        };


    private string FailedOperation(string state) =>
        JsonSerializer.Serialize(new { status = state, error = new { message = "private-fixture" } });


    private async Task<HttpResponseMessage> InProgressWithRetryAfterAsync()
    {
        HttpResponseMessage response = await Response("{\"status\":\"inProgress\"}");
        response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(10));
        return response;
    }

    private (string? Issuer, string? KeyType, bool Exportable, int ValidityMonths, string? DnsName) PolicySummary(string? sentBody)
    {
        JsonNode policy = JsonNode.Parse(sentBody!)!["policy"]!;
        return (
            policy["issuer"]!["name"]!.GetValue<string>(),
            policy["key_props"]!["kty"]!.GetValue<string>(),
            policy["key_props"]!["exportable"]!.GetValue<bool>(),
            policy["x509_props"]!["validity_months"]!.GetValue<int>(),
            policy["x509_props"]!["sans"]!["dns_names"]![0]!.GetValue<string>());
    }

    private (bool Enabled, int Expires) AttributesSummary(string? sentBody)
    {
        JsonNode attributes = JsonNode.Parse(sentBody!)!["attributes"]!;
        return (attributes["enabled"]!.GetValue<bool>(), attributes["exp"]!.GetValue<int>());
    }
}