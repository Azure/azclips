// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Text.Json;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Engine;
using Microsoft.Azure.Azclips.Extensions.KeyVault.Customization;
using Moq;
using static Cli.Tests.Commands.CommandTestHelpers;
using KeyVaultIndex = Microsoft.Azure.Azclips.Extensions.KeyVault.ExtensionIndex;

namespace Cli.Tests.Commands.KeyVault;

/// <summary>
/// Runs Key Vault secret and certificate CLI commands through the real engine against canned vault responses and checks
/// what each command writes, or the error it throws. The HTTP wire is a strict mock, so a request a test did not set
/// up a response for throws <see cref="MockException"/>.
/// </summary>
public sealed class KeyVaultCommandResponseTests : IDisposable
{
    private const string ExtensionName = "KeyVault";
    private const string VaultName = "myvault";
    private const string SecretName = "app-secret";
    private const string SecretValue = "s3cret";
    private const string CertificateName = "web-cert";
    private const string Version = "abc123";
    private const string ApiVersion = "2025-07-01";
    private const string VaultUrl = $"https://{VaultName}.vault.azure.net";
    private const string SecretPath = $"/secrets/{SecretName}";
    private const string CertificatePath = $"/certificates/{CertificateName}";
    private const string SecretUrl = VaultUrl + SecretPath;
    private const string CertificateUrl = VaultUrl + CertificatePath;
    private const string DeletedSeconds = "1700000000";
    private const string PurgeSeconds = "1707776000";
    private const string SecretBundleWithTags =
        $$$"""{"id":"{{{SecretUrl}}}/{{{Version}}}","value":"{{{SecretValue}}}","contentType":"text/plain","attributes":{"enabled":true},"tags":{"env":"dev"}}""";
    private const string DeletedSecretBundle =
        $$"""{"id":"{{SecretUrl}}/{{Version}}","recoveryId":"{{VaultUrl}}/deletedsecrets/{{SecretName}}","deletedDate":{{DeletedSeconds}},"scheduledPurgeDate":{{PurgeSeconds}}}""";
    private const string CertificateBundleWithTags =
        $$$"""{"id":"{{{CertificateUrl}}}/{{{Version}}}","x5t":"_-8","attributes":{"enabled":true,"created":1700000000},"tags":{"env":"dev"}}""";
    private const string DeletedCertificateBundle =
        $$"""{"id":"{{CertificateUrl}}/{{Version}}","recoveryId":"{{VaultUrl}}/deletedcertificates/{{CertificateName}}","deletedDate":{{DeletedSeconds}},"scheduledPurgeDate":{{PurgeSeconds}}}""";
    private const string CompletedOperation = $$$"""{"id":"{{{CertificateUrl}}}/pending","status":"completed","issuer":{"name":"Self"},"target":"{{{CertificateUrl}}}"}""";
    private const string PendingOperation = $$$"""{"id":"{{{CertificateUrl}}}/pending","status":"inProgress","issuer":{"name":"Self"}}""";
    private const string SelfSignedPolicy = """{"issuer":{"name":"Self"},"x509_props":{"subject":"CN=contoso.com"}}""";
    private const string UnknownIssuerPolicy = """{"issuer":{"name":"Unknown"},"x509_props":{"subject":"CN=a"}}""";
    private const string ValidityPolicy = """{"x509_props":{"validity_months":24}}""";
    private const string ForbiddenError = """{"error":{"code":"Forbidden","message":"Caller is not authorized."}}""";
    private const string AccessDeniedMessage = "Access denied. Check the resource permissions and network access rules.";
    private const string NotFoundMessage = "Request failed with HTTP 404. Response details are omitted because the request contains sensitive data.";

    private readonly Mock<ISendAsync> _mockWire = new(MockBehavior.Strict);
    private readonly List<object> _output = [];
    private readonly string _missingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
    private readonly Engine _engine;

    public KeyVaultCommandResponseTests() => _engine = CreateEngine(typeof(KeyVaultIndex).Assembly, _mockWire);

    public void Dispose() => _engine.Dispose();

    // ── secret set ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_SecretSet_WritesSecretMetadataId()
    {
        _mockWire.RespondTo(HttpMethod.Put, SecretUrl, HttpStatusCode.OK, SecretBundleWithTags);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["keyvault", "secret", "set", "--vault-name", VaultName, "--name", SecretName, "--value", SecretValue]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal($"{SecretUrl}/{Version}", Assert.IsType<SecretMetadata>(Assert.Single(_output)).Id);
    }

    [Fact]
    public async Task RunAsync_SecretSet_DoesNotWriteSecretValue()
    {
        _mockWire.RespondTo(HttpMethod.Put, SecretUrl, HttpStatusCode.OK, SecretBundleWithTags);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["keyvault", "secret", "set", "--vault-name", VaultName, "--name", SecretName, "--value", SecretValue]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.DoesNotContain(SecretValue, JsonSerializer.Serialize(Assert.Single(_output)));
    }

    // ── secret show ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_SecretShow_WritesSecretMetadataTags()
    {
        _mockWire.RespondTo(HttpMethod.Get, $"{SecretUrl}/", HttpStatusCode.OK, SecretBundleWithTags);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["keyvault", "secret", "show", "--vault-name", VaultName, "--name", SecretName]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal(new Dictionary<string, string> { ["env"] = "dev" }, Assert.IsType<SecretMetadata>(Assert.Single(_output)).Tags);
    }

    [Fact]
    public async Task RunAsync_SecretShow_DoesNotWriteSecretValue()
    {
        _mockWire.RespondTo(HttpMethod.Get, $"{SecretUrl}/", HttpStatusCode.OK, SecretBundleWithTags);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["keyvault", "secret", "show", "--vault-name", VaultName, "--name", SecretName]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.DoesNotContain(SecretValue, JsonSerializer.Serialize(Assert.Single(_output)));
    }

    [Fact]
    public async Task RunAsync_SecretShowAsPlainText_WritesOnlySecretValue()
    {
        _mockWire.RespondTo(HttpMethod.Get, $"{SecretUrl}/", HttpStatusCode.OK, SecretBundleWithTags);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["keyvault", "secret", "show", "--vault-name", VaultName, "--name", SecretName, "--as-plain-text"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal(SecretValue, Assert.Single(_output));
    }

    [Fact]
    public async Task RunAsync_SecretShowAsPlainTextFalse_WritesSecretMetadata()
    {
        _mockWire.RespondTo(HttpMethod.Get, $"{SecretUrl}/", HttpStatusCode.OK, SecretBundleWithTags);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["keyvault", "secret", "show", "--vault-name", VaultName, "--name", SecretName, "--as-plain-text", "false"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.IsType<SecretMetadata>(Assert.Single(_output));
    }

    [Fact]
    public async Task RunAsync_SecretShowAsPlainTextWithoutValue_WritesEmptyString()
    {
        _mockWire.RespondTo(HttpMethod.Get, $"{SecretUrl}/", HttpStatusCode.OK, $$"""{"id":"{{SecretUrl}}/{{Version}}"}""");
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["keyvault", "secret", "show", "--vault-name", VaultName, "--name", SecretName, "--as-plain-text"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        // Current behaviour: a bundle without a value is written as an empty string rather than reported as missing.
        Assert.Equal(string.Empty, Assert.Single(_output));
    }

    [Fact]
    public async Task RunAsync_SecretShowNotFound_ThrowsWithoutResponseDetails()
    {
        _mockWire.RespondTo(HttpMethod.Get, $"{SecretUrl}/", HttpStatusCode.NotFound,
            $$$"""{"error":{"code":"SecretNotFound","message":"A secret with (name/id) {{{SecretName}}} was not found in this key vault."}}""");
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["keyvault", "secret", "show", "--vault-name", VaultName, "--name", SecretName]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal(NotFoundMessage, Assert.IsType<HttpRequestException>(exception).Message);
    }

    [Fact]
    public async Task RunAsync_SecretShowForbidden_ThrowsAccessDenied()
    {
        _mockWire.RespondTo(HttpMethod.Get, $"{SecretUrl}/", HttpStatusCode.Forbidden, ForbiddenError);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["keyvault", "secret", "show", "--vault-name", VaultName, "--name", SecretName]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal(AccessDeniedMessage, Assert.IsType<HttpRequestException>(exception).Message);
    }

    [Fact]
    public async Task RunAsync_SecretShowInvalidJson_ThrowsDeserializationError()
    {
        _mockWire.RespondTo(HttpMethod.Get, $"{SecretUrl}/", HttpStatusCode.OK, "not json");
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["keyvault", "secret", "show", "--vault-name", VaultName, "--name", SecretName]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal("Failed to deserialize response body into SecretBundle.", Assert.IsType<InvalidOperationException>(exception).Message);
    }

    // ── secret list ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_SecretListTwoPages_WritesSecretsFromBothPages()
    {
        _mockWire.RespondInOrder(
            $$"""{"value":[{"id":"{{VaultUrl}}/secrets/one"}],"nextLink":"{{VaultUrl}}/secrets?api-version={{ApiVersion}}&$skiptoken=page2"}""",
            $$"""{"value":[{"id":"{{VaultUrl}}/secrets/two"}]}""");
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["keyvault", "secret", "list", "--vault-name", VaultName]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        // Current behaviour: secret lists are written as List<SecretMetadata> while certificate lists are CertificateMetadata[].
        Assert.Equal([$"{VaultUrl}/secrets/one", $"{VaultUrl}/secrets/two"], Assert.IsType<List<SecretMetadata>>(Assert.Single(_output)).Select(s => s.Id));
    }

    [Fact]
    public async Task RunAsync_SecretListEmptyValue_WritesEmptyList()
    {
        _mockWire.RespondTo(HttpMethod.Get, $"{VaultUrl}/secrets", HttpStatusCode.OK, EmptyList);
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["keyvault", "secret", "list", "--vault-name", VaultName]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Empty(Assert.IsType<List<SecretMetadata>>(Assert.Single(_output)));
    }

    [Fact]
    public async Task RunAsync_SecretListBodyWithoutValue_WritesEmptyList()
    {
        _mockWire.RespondTo(HttpMethod.Get, $"{VaultUrl}/secrets", HttpStatusCode.OK, "{}");
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["keyvault", "secret", "list", "--vault-name", VaultName]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Empty(Assert.IsType<List<SecretMetadata>>(Assert.Single(_output)));
    }

    [Fact]
    public async Task RunAsync_SecretListForbidden_ThrowsAccessDenied()
    {
        _mockWire.RespondTo(HttpMethod.Get, $"{VaultUrl}/secrets", HttpStatusCode.Forbidden, ForbiddenError);
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["keyvault", "secret", "list", "--vault-name", VaultName]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal(AccessDeniedMessage, Assert.IsType<HttpRequestException>(exception).Message);
    }

    // ── secret list-versions ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_SecretListVersions_WritesVersionIds()
    {
        _mockWire.RespondTo(HttpMethod.Get, $"{SecretUrl}/versions", HttpStatusCode.OK,
            $$"""{"value":[{"id":"{{SecretUrl}}/v1"},{"id":"{{SecretUrl}}/v2"}]}""");
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["keyvault", "secret", "list-versions", "--vault-name", VaultName, "--name", SecretName]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal([$"{SecretUrl}/v1", $"{SecretUrl}/v2"], Assert.IsType<List<SecretMetadata>>(Assert.Single(_output)).Select(s => s.Id));
    }

    // ── secret set-attributes ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_SecretSetAttributes_WritesUpdatedEnabledAttribute()
    {
        _mockWire.RespondTo(HttpMethod.Patch, $"{SecretUrl}/", HttpStatusCode.OK,
            $$$"""{"id":"{{{SecretUrl}}}/{{{Version}}}","attributes":{"enabled":false}}""");
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["keyvault", "secret", "set-attributes", "--vault-name", VaultName, "--name", SecretName, "--enabled", "false"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.False(Assert.IsType<SecretMetadata>(Assert.Single(_output)).Attributes!.Enabled);
    }

    [Fact]
    public async Task RunAsync_SecretSetAttributesResponseWithValue_DoesNotWriteSecretValue()
    {
        _mockWire.RespondTo(HttpMethod.Patch, $"{SecretUrl}/", HttpStatusCode.OK, SecretBundleWithTags);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["keyvault", "secret", "set-attributes", "--vault-name", VaultName, "--name", SecretName, "--enabled", "true"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.DoesNotContain(SecretValue, JsonSerializer.Serialize(Assert.Single(_output)));
    }

    // ── secret delete ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_SecretDelete_WritesRecoveryId()
    {
        _mockWire.RespondTo(HttpMethod.Delete, SecretUrl, HttpStatusCode.OK, DeletedSecretBundle);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["keyvault", "secret", "delete", "--vault-name", VaultName, "--name", SecretName, "--yes"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal($"{VaultUrl}/deletedsecrets/{SecretName}", Assert.IsType<SecretMetadata>(Assert.Single(_output)).RecoveryId);
    }

    [Fact]
    public async Task RunAsync_SecretDelete_WritesScheduledPurgeDateAsUnixSeconds()
    {
        _mockWire.RespondTo(HttpMethod.Delete, SecretUrl, HttpStatusCode.OK, DeletedSecretBundle);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["keyvault", "secret", "delete", "--vault-name", VaultName, "--name", SecretName, "--yes"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        // Current behaviour: secret dates stay Unix seconds while certificate dates are DateTimeOffset.
        Assert.Equal(1707776000, Assert.IsType<SecretMetadata>(Assert.Single(_output)).ScheduledPurgeDate);
    }

    // ── certificate create ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_CertificateCreateCompleted_WritesCompletedOperation()
    {
        _mockWire.RespondTo(HttpMethod.Post, $"{CertificateUrl}/create", HttpStatusCode.Accepted, CompletedOperation);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["keyvault", "certificate", "create", "--vault-name", VaultName, "--name", CertificateName, "--policy", SelfSignedPolicy]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal("completed", Assert.IsType<CertificateOperationResult>(Assert.Single(_output)).Status);
    }

    [Fact]
    public async Task RunAsync_CertificateCreatePolledToCompletion_WritesPolledOperation()
    {
        _mockWire.RespondWithHeaders(HttpMethod.Post, $"{CertificateUrl}/create", HttpStatusCode.Accepted, PendingOperation, "Retry-After: 0");
        _mockWire.RespondTo(HttpMethod.Get, $"{CertificateUrl}/pending", HttpStatusCode.OK, CompletedOperation);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["keyvault", "certificate", "create", "--vault-name", VaultName, "--name", CertificateName, "--policy", SelfSignedPolicy]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal("completed", Assert.IsType<CertificateOperationResult>(Assert.Single(_output)).Status);
    }

    [Fact]
    public async Task RunAsync_CertificateCreateNoWait_WritesInProgressOperation()
    {
        _mockWire.RespondTo(HttpMethod.Post, $"{CertificateUrl}/create", HttpStatusCode.Accepted, PendingOperation);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["keyvault", "certificate", "create", "--vault-name", VaultName, "--name", CertificateName, "--policy", SelfSignedPolicy, "--no-wait"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal("inProgress", Assert.IsType<CertificateOperationResult>(Assert.Single(_output)).Status);
    }

    [Fact]
    public async Task RunAsync_CertificateCreateUnknownIssuer_WritesInProgressOperation()
    {
        _mockWire.RespondTo(HttpMethod.Post, $"{CertificateUrl}/create", HttpStatusCode.Accepted, PendingOperation);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["keyvault", "certificate", "create", "--vault-name", VaultName, "--name", CertificateName, "--policy", UnknownIssuerPolicy]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal("inProgress", Assert.IsType<CertificateOperationResult>(Assert.Single(_output)).Status);
    }

    [Theory]
    [InlineData("failed")]
    [InlineData("canceled")]
    [InlineData("cancelled")]
    public async Task RunAsync_CertificateCreateTerminalFailure_ThrowsWithoutServiceDetails(string status)
    {
        _mockWire.RespondTo(HttpMethod.Post, $"{CertificateUrl}/create", HttpStatusCode.Accepted,
            $$$"""{"id":"{{{CertificateUrl}}}/pending","status":"{{{status}}}","status_details":"Issuer rejected the request.","issuer":{"name":"Self"}}""");
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["keyvault", "certificate", "create", "--vault-name", VaultName, "--name", CertificateName, "--policy", SelfSignedPolicy]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal($"Certificate creation {status}. Sensitive service details omitted.", Assert.IsType<InvalidOperationException>(exception).Message);
    }

    [Fact]
    public async Task RunAsync_CertificateCreateUnsupportedStatus_ThrowsUnsupportedStatus()
    {
        _mockWire.RespondTo(HttpMethod.Post, $"{CertificateUrl}/create", HttpStatusCode.Accepted,
            $$$"""{"id":"{{{CertificateUrl}}}/pending","status":"queued","issuer":{"name":"Self"}}""");
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["keyvault", "certificate", "create", "--vault-name", VaultName, "--name", CertificateName, "--policy", SelfSignedPolicy]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal("The certificate operation returned an unsupported status.", Assert.IsType<InvalidOperationException>(exception).Message);
    }

    [Fact]
    public async Task RunAsync_CertificateCreateInvalidJson_ThrowsInvalidResponse()
    {
        _mockWire.RespondTo(HttpMethod.Post, $"{CertificateUrl}/create", HttpStatusCode.Accepted, "not json");
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["keyvault", "certificate", "create", "--vault-name", VaultName, "--name", CertificateName, "--policy", SelfSignedPolicy]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal("The certificate operation response is invalid. Sensitive details omitted.", Assert.IsType<InvalidOperationException>(exception).Message);
    }

    // ── certificate show ───────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_CertificateShow_WritesNameFromId()
    {
        _mockWire.RespondTo(HttpMethod.Get, $"{CertificateUrl}/", HttpStatusCode.OK, CertificateBundleWithTags);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["keyvault", "certificate", "show", "--vault-name", VaultName, "--name", CertificateName]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal(CertificateName, Assert.IsType<CertificateResult>(Assert.Single(_output)).Name);
    }

    [Fact]
    public async Task RunAsync_CertificateShow_WritesThumbprintAsHex()
    {
        _mockWire.RespondTo(HttpMethod.Get, $"{CertificateUrl}/", HttpStatusCode.OK, CertificateBundleWithTags);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["keyvault", "certificate", "show", "--vault-name", VaultName, "--name", CertificateName]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal("FFEF", Assert.IsType<CertificateResult>(Assert.Single(_output)).X509ThumbprintHex);
    }

    [Fact]
    public async Task RunAsync_CertificateShow_WritesUnixSecondsAsDates()
    {
        _mockWire.RespondTo(HttpMethod.Get, $"{CertificateUrl}/", HttpStatusCode.OK, CertificateBundleWithTags);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["keyvault", "certificate", "show", "--vault-name", VaultName, "--name", CertificateName]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1700000000), Assert.IsType<CertificateResult>(Assert.Single(_output)).Attributes.Created);
    }

    [Fact]
    public async Task RunAsync_CertificateShowNotFound_ThrowsWithoutResponseDetails()
    {
        _mockWire.RespondTo(HttpMethod.Get, $"{CertificateUrl}/", HttpStatusCode.NotFound,
            $$$"""{"error":{"code":"CertificateNotFound","message":"A certificate with (name/id) {{{CertificateName}}} was not found in this key vault."}}""");
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["keyvault", "certificate", "show", "--vault-name", VaultName, "--name", CertificateName]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal(NotFoundMessage, Assert.IsType<HttpRequestException>(exception).Message);
    }

    // ── certificate list ───────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_CertificateListTwoPages_WritesCertificatesFromBothPages()
    {
        _mockWire.RespondInOrder(
            $$"""{"value":[{"id":"{{VaultUrl}}/certificates/one"}],"nextLink":"{{VaultUrl}}/certificates?api-version={{ApiVersion}}&$skiptoken=page2"}""",
            $$"""{"value":[{"id":"{{VaultUrl}}/certificates/two"}]}""");
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["keyvault", "certificate", "list", "--vault-name", VaultName]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal(["one", "two"], Assert.IsType<CertificateMetadata[]>(Assert.Single(_output)).Select(c => c.Name));
    }

    [Fact]
    public async Task RunAsync_CertificateListEmptyValue_WritesEmptyArray()
    {
        _mockWire.RespondTo(HttpMethod.Get, $"{VaultUrl}/certificates", HttpStatusCode.OK, EmptyList);
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["keyvault", "certificate", "list", "--vault-name", VaultName]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Empty(Assert.IsType<CertificateMetadata[]>(Assert.Single(_output)));
    }

    [Fact]
    public async Task RunAsync_CertificateList_WritesEmptySubject()
    {
        _mockWire.RespondTo(HttpMethod.Get, $"{VaultUrl}/certificates", HttpStatusCode.OK, $$"""{"value":[{"id":"{{VaultUrl}}/certificates/one"}]}""");
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["keyvault", "certificate", "list", "--vault-name", VaultName]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        // Current behaviour: list items carry no subject, so every item is written with an empty Subject.
        Assert.Equal(string.Empty, Assert.Single(Assert.IsType<CertificateMetadata[]>(Assert.Single(_output))).Subject);
    }

    // ── certificate list-versions ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_CertificateListVersions_WritesVersionIds()
    {
        _mockWire.RespondTo(HttpMethod.Get, $"{CertificateUrl}/versions", HttpStatusCode.OK,
            $$"""{"value":[{"id":"{{CertificateUrl}}/v1"},{"id":"{{CertificateUrl}}/v2"}]}""");
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["keyvault", "certificate", "list-versions", "--vault-name", VaultName, "--name", CertificateName]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal([$"{CertificateUrl}/v1", $"{CertificateUrl}/v2"], Assert.IsType<CertificateMetadata[]>(Assert.Single(_output)).Select(c => c.Id));
    }

    // ── certificate set-attributes ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_CertificateSetAttributes_WritesUpdatedEnabledAttribute()
    {
        _mockWire.RespondTo(HttpMethod.Patch, $"{CertificateUrl}/", HttpStatusCode.OK,
            $$$"""{"id":"{{{CertificateUrl}}}/{{{Version}}}","attributes":{"enabled":false}}""");
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["keyvault", "certificate", "set-attributes", "--vault-name", VaultName, "--name", CertificateName, "--enabled", "false"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.False(Assert.IsType<CertificateResult>(Assert.Single(_output)).Attributes.Enabled);
    }

    [Fact]
    public async Task RunAsync_CertificateSetAttributesPolicyOnly_WritesCertificateFromFollowUpGet()
    {
        _mockWire.RespondTo(HttpMethod.Patch, $"{CertificateUrl}/policy", HttpStatusCode.OK, ValidityPolicy);
        _mockWire.RespondTo(HttpMethod.Get, $"{CertificateUrl}/", HttpStatusCode.OK, CertificateBundleWithTags);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            [
                "keyvault", "certificate", "set-attributes",
                "--vault-name", VaultName,
                "--name", CertificateName,
                "--policy", ValidityPolicy,
            ]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal($"{CertificateUrl}/{Version}", Assert.IsType<CertificateResult>(Assert.Single(_output)).Id);
    }

    // ── certificate download ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_CertificateDownloadWithoutCer_ThrowsNoPublicCertificate()
    {
        _mockWire.RespondTo(HttpMethod.Get, $"{CertificateUrl}/", HttpStatusCode.OK, CertificateBundleWithTags);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            [
                "keyvault", "certificate", "download",
                "--vault-name", VaultName,
                "--name", CertificateName,
                "--file", Path.Combine(_missingDirectory, $"{CertificateName}.pem"),
            ]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal("The response contains no public certificate to download.", Assert.IsType<InvalidOperationException>(exception).Message);
    }

    [Fact]
    public async Task RunAsync_CertificateDownloadInvalidCer_ThrowsInvalidPublicCertificate()
    {
        _mockWire.RespondTo(HttpMethod.Get, $"{CertificateUrl}/", HttpStatusCode.OK,
            $$"""{"id":"{{CertificateUrl}}/{{Version}}","cer":"AQID"}""");
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            [
                "keyvault", "certificate", "download",
                "--vault-name", VaultName,
                "--name", CertificateName,
                "--file", Path.Combine(_missingDirectory, $"{CertificateName}.pem"),
            ]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal("The response contains an invalid public certificate.", Assert.IsType<InvalidOperationException>(exception).Message);
    }

    // ── certificate delete ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_CertificateDelete_WritesRecoveryId()
    {
        _mockWire.RespondTo(HttpMethod.Delete, CertificateUrl, HttpStatusCode.OK, DeletedCertificateBundle);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["keyvault", "certificate", "delete", "--vault-name", VaultName, "--name", CertificateName, "--yes"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal($"{VaultUrl}/deletedcertificates/{CertificateName}", Assert.IsType<DeletedCertificateResult>(Assert.Single(_output)).RecoveryId);
    }

    [Fact]
    public async Task RunAsync_CertificateDelete_WritesScheduledPurgeDateAsDate()
    {
        _mockWire.RespondTo(HttpMethod.Delete, CertificateUrl, HttpStatusCode.OK, DeletedCertificateBundle);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["keyvault", "certificate", "delete", "--vault-name", VaultName, "--name", CertificateName, "--yes"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1707776000), Assert.IsType<DeletedCertificateResult>(Assert.Single(_output)).ScheduledPurgeDate);
    }
}
