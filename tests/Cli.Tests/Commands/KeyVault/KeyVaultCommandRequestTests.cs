// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Engine;
using Moq;
using static Cli.Tests.Commands.CommandTestHelpers;
using KeyVaultIndex = Microsoft.Azure.Azclips.Extensions.KeyVault.ExtensionIndex;

namespace Cli.Tests.Commands.KeyVault;

/// <summary>
/// Runs Key Vault secret and certificate CLI commands through the real engine and checks the request each one sends to
/// the vault: method, path, decoded query values and JSON body. The wire mock is strict, so an unexpected request fails the test.
/// </summary>
public sealed class KeyVaultCommandRequestTests : IDisposable
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
    private const string SecretBundle = $$$"""{"id":"{{{SecretUrl}}}/{{{Version}}}","value":"{{{SecretValue}}}","attributes":{"enabled":true}}""";
    private const string DeletedSecretBundle = $$"""{"id":"{{SecretUrl}}/{{Version}}","recoveryId":"{{VaultUrl}}/deletedsecrets/{{SecretName}}"}""";
    private const string CertificateBundle = $$$"""{"id":"{{{CertificateUrl}}}/{{{Version}}}","attributes":{"enabled":true}}""";
    private const string CompletedOperation = $$$"""{"id":"{{{CertificateUrl}}}/pending","status":"completed","issuer":{"name":"Self"}}""";
    private const string PendingOperation = $$$"""{"id":"{{{CertificateUrl}}}/pending","status":"inProgress","issuer":{"name":"Self"}}""";
    private const string SelfSignedPolicy = """{"issuer":{"name":"Self"},"x509_props":{"subject":"CN=contoso.com"}}""";

    private readonly Mock<ISendAsync> _mockWire = new(MockBehavior.Strict);
    private readonly List<object> _output = [];
    private readonly string _missingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
    private readonly Engine _engine;

    public KeyVaultCommandRequestTests() => _engine = CreateEngine(typeof(KeyVaultIndex).Assembly, _mockWire);

    public void Dispose() => _engine.Dispose();

    // Each vault command with the arguments it needs and a response it accepts.
    public static TheoryData<string[], string> GetVaultCommands() => new()
    {
        { ["keyvault", "secret", "set", "--vault-name", VaultName, "--name", SecretName, "--value", SecretValue], SecretBundle },
        { ["keyvault", "secret", "show", "--vault-name", VaultName, "--name", SecretName], SecretBundle },
        { ["keyvault", "secret", "list", "--vault-name", VaultName], EmptyList },
        { ["keyvault", "secret", "list-versions", "--vault-name", VaultName, "--name", SecretName], EmptyList },
        { ["keyvault", "secret", "set-attributes", "--vault-name", VaultName, "--name", SecretName, "--enabled", "false"], SecretBundle },
        { ["keyvault", "secret", "delete", "--vault-name", VaultName, "--name", SecretName, "--yes"], DeletedSecretBundle },
        { ["keyvault", "certificate", "create", "--vault-name", VaultName, "--name", CertificateName, "--policy", SelfSignedPolicy], CompletedOperation },
        { ["keyvault", "certificate", "show", "--vault-name", VaultName, "--name", CertificateName], CertificateBundle },
        { ["keyvault", "certificate", "list", "--vault-name", VaultName], EmptyList },
        { ["keyvault", "certificate", "list-versions", "--vault-name", VaultName, "--name", CertificateName], EmptyList },
        { ["keyvault", "certificate", "set-attributes", "--vault-name", VaultName, "--name", CertificateName, "--enabled", "false"], CertificateBundle },
        { ["keyvault", "certificate", "delete", "--vault-name", VaultName, "--name", CertificateName, "--yes"], CertificateBundle },
    };

    [Theory]
    [MemberData(nameof(GetVaultCommands))]
    public async Task RunAsync_VaultCommand_SendsDataPlaneApiVersion(string[] commandLine, string responseBody)
    {
        _mockWire.RespondToAnyRequest(responseBody);
        CommandInvocation invocation = CreateInvocation(ExtensionName, commandLine);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal(ApiVersion, Assert.Single(_mockWire.SentRequests()).QueryValue("api-version"));
    }

    // ── vault selection ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_VaultName_SendsToVaultNameHost()
    {
        _mockWire.RespondTo(HttpMethod.Get, $"{SecretUrl}/", HttpStatusCode.OK, SecretBundle);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["keyvault", "secret", "show", "--vault-name", VaultName, "--name", SecretName]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal($"{VaultName}.vault.azure.net", Assert.Single(_mockWire.SentRequests()).RequestUri!.Host);
    }

    [Fact]
    public async Task RunAsync_VaultUrlWithTrailingSlash_SendsToVaultUrlWithoutDoubleSlash()
    {
        _mockWire.RespondTo(HttpMethod.Get, $"https://othervault.vault.azure.net{SecretPath}/", HttpStatusCode.OK, SecretBundle);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["keyvault", "secret", "show", "--vault-url", "https://othervault.vault.azure.net/", "--name", SecretName]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal($"https://othervault.vault.azure.net{SecretPath}/", Assert.Single(_mockWire.SentRequests()).RequestUri!.GetLeftPart(UriPartial.Path));
    }

    // ── secret set ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_SecretSet_SendsPutToSecretPath()
    {
        _mockWire.RespondTo(HttpMethod.Put, SecretUrl, HttpStatusCode.OK, SecretBundle);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["keyvault", "secret", "set", "--vault-name", VaultName, "--name", SecretName, "--value", SecretValue]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal([$"PUT {SecretPath}?api-version={ApiVersion}"], _mockWire.FullRequestLines());
    }

    [Fact]
    public async Task RunAsync_SecretSetValue_SendsValueOnlyBody()
    {
        List<string?> sentBodies = _mockWire.RespondTo(HttpMethod.Put, SecretUrl, HttpStatusCode.OK, SecretBundle);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["keyvault", "secret", "set", "--vault-name", VaultName, "--name", SecretName, "--value", SecretValue]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal("""{"value":"s3cret"}""", Assert.Single(sentBodies));
    }

    [Fact]
    public async Task RunAsync_SecretSetAllOptions_SendsTagsContentTypeAndUnixSecondAttributes()
    {
        List<string?> sentBodies = _mockWire.RespondTo(HttpMethod.Put, SecretUrl, HttpStatusCode.OK, SecretBundle);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            [
                "keyvault", "secret", "set",
                "--vault-name", VaultName,
                "--name", SecretName,
                "--value", SecretValue,
                "--tags", "env=dev",
                "--content-type", "text/plain",
                "--enabled", "false",
                "--not-before", "2025-01-01T00:00:00Z",
                "--expires", "2030-01-01T00:00:00Z",
            ]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal("""{"value":"s3cret","tags":{"env":"dev"},"contentType":"text/plain","attributes":{"enabled":false,"nbf":1735689600,"exp":1893456000}}""", Assert.Single(sentBodies));
    }

    [Fact]
    public async Task RunAsync_SecretSetTagValueWithEquals_SplitsAtFirstEquals()
    {
        List<string?> sentBodies = _mockWire.RespondTo(HttpMethod.Put, SecretUrl, HttpStatusCode.OK, SecretBundle);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["keyvault", "secret", "set", "--vault-name", VaultName, "--name", SecretName, "--value", SecretValue, "--tags", "team=a=b"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal("""{"value":"s3cret","tags":{"team":"a=b"}}""", Assert.Single(sentBodies));
    }

    [Fact]
    public async Task RunAsync_SecretSetRepeatedTags_SendsAllTags()
    {
        List<string?> sentBodies = _mockWire.RespondTo(HttpMethod.Put, SecretUrl, HttpStatusCode.OK, SecretBundle);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            [
                "keyvault", "secret", "set",
                "--vault-name", VaultName,
                "--name", SecretName,
                "--value", SecretValue,
                "--tags", "env=dev",
                "--tags", "team=x",
            ]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal("""{"value":"s3cret","tags":{"env":"dev","team":"x"}}""", Assert.Single(sentBodies));
    }

    [Fact]
    public async Task RunAsync_SecretSetTagWithoutValue_SendsEmptyTagValue()
    {
        List<string?> sentBodies = _mockWire.RespondTo(HttpMethod.Put, SecretUrl, HttpStatusCode.OK, SecretBundle);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["keyvault", "secret", "set", "--vault-name", VaultName, "--name", SecretName, "--value", SecretValue, "--tags", "env="]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal("""{"value":"s3cret","tags":{"env":""}}""", Assert.Single(sentBodies));
    }

    // ── secret show ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_SecretShow_SendsGetToSecretPathWithTrailingSlash()
    {
        _mockWire.RespondTo(HttpMethod.Get, $"{SecretUrl}/", HttpStatusCode.OK, SecretBundle);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["keyvault", "secret", "show", "--vault-name", VaultName, "--name", SecretName]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        // Current behaviour: without --version the empty version segment leaves a trailing slash, which Key Vault accepts as "latest".
        Assert.Equal([$"GET {SecretPath}/?api-version={ApiVersion}"], _mockWire.FullRequestLines());
    }

    [Fact]
    public async Task RunAsync_SecretShowVersion_SendsGetToVersionPath()
    {
        _mockWire.RespondTo(HttpMethod.Get, $"{SecretUrl}/{Version}", HttpStatusCode.OK, SecretBundle);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["keyvault", "secret", "show", "--vault-name", VaultName, "--name", SecretName, "--version", Version]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal([$"GET {SecretPath}/{Version}?api-version={ApiVersion}"], _mockWire.FullRequestLines());
    }

    // ── secret list ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_SecretList_SendsGetToSecretsPath()
    {
        _mockWire.RespondTo(HttpMethod.Get, $"{VaultUrl}/secrets", HttpStatusCode.OK, EmptyList);
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["keyvault", "secret", "list", "--vault-name", VaultName]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal([$"GET /secrets?api-version={ApiVersion}"], _mockWire.FullRequestLines());
    }

    [Fact]
    public async Task RunAsync_SecretListMaxResults_SendsMaxResultsQuery()
    {
        _mockWire.RespondTo(HttpMethod.Get, $"{VaultUrl}/secrets", HttpStatusCode.OK, EmptyList);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["keyvault", "secret", "list", "--vault-name", VaultName, "--maxresults", "5"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal("5", Assert.Single(_mockWire.SentRequests()).QueryValue("maxresults"));
    }

    [Fact]
    public async Task RunAsync_SecretListNextLink_FollowsNextLinkAsGiven()
    {
        _mockWire.RespondInOrder(
            $$"""{"value":[],"nextLink":"{{VaultUrl}}/secrets?api-version={{ApiVersion}}&$skiptoken=page2"}""",
            EmptyList);
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["keyvault", "secret", "list", "--vault-name", VaultName]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal([$"GET /secrets?api-version={ApiVersion}", $"GET /secrets?api-version={ApiVersion}&$skiptoken=page2"], _mockWire.FullRequestLines());
    }

    // ── secret list-versions ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_SecretListVersions_SendsGetToVersionsPath()
    {
        _mockWire.RespondTo(HttpMethod.Get, $"{SecretUrl}/versions", HttpStatusCode.OK, EmptyList);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["keyvault", "secret", "list-versions", "--vault-name", VaultName, "--name", SecretName]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal([$"GET {SecretPath}/versions?api-version={ApiVersion}"], _mockWire.FullRequestLines());
    }

    [Fact]
    public async Task RunAsync_SecretListVersionsMaxResults_SendsMaxResultsQuery()
    {
        _mockWire.RespondTo(HttpMethod.Get, $"{SecretUrl}/versions", HttpStatusCode.OK, EmptyList);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["keyvault", "secret", "list-versions", "--vault-name", VaultName, "--name", SecretName, "--maxresults", "3"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal("3", Assert.Single(_mockWire.SentRequests()).QueryValue("maxresults"));
    }

    // ── secret set-attributes ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_SecretSetAttributes_SendsPatchToSecretPath()
    {
        _mockWire.RespondTo(HttpMethod.Patch, $"{SecretUrl}/", HttpStatusCode.OK, SecretBundle);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["keyvault", "secret", "set-attributes", "--vault-name", VaultName, "--name", SecretName, "--enabled", "false"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal([$"PATCH {SecretPath}/?api-version={ApiVersion}"], _mockWire.FullRequestLines());
    }

    [Fact]
    public async Task RunAsync_SecretSetAttributesEnabled_SendsOnlyEnabledAttribute()
    {
        List<string?> sentBodies = _mockWire.RespondTo(HttpMethod.Patch, $"{SecretUrl}/", HttpStatusCode.OK, SecretBundle);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["keyvault", "secret", "set-attributes", "--vault-name", VaultName, "--name", SecretName, "--enabled", "false"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal("""{"attributes":{"enabled":false}}""", Assert.Single(sentBodies));
    }

    [Fact]
    public async Task RunAsync_SecretSetAttributesVersion_SendsPatchToVersionPath()
    {
        _mockWire.RespondTo(HttpMethod.Patch, $"{SecretUrl}/{Version}", HttpStatusCode.OK, SecretBundle);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["keyvault", "secret", "set-attributes", "--vault-name", VaultName, "--name", SecretName, "--version", Version, "--enabled", "true"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal([$"PATCH {SecretPath}/{Version}?api-version={ApiVersion}"], _mockWire.FullRequestLines());
    }

    [Fact]
    public async Task RunAsync_SecretSetAttributesAllOptions_SendsContentTypeAttributesAndTags()
    {
        List<string?> sentBodies = _mockWire.RespondTo(HttpMethod.Patch, $"{SecretUrl}/", HttpStatusCode.OK, SecretBundle);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            [
                "keyvault", "secret", "set-attributes",
                "--vault-name", VaultName,
                "--name", SecretName,
                "--content-type", "text/plain",
                "--not-before", "2025-01-01T00:00:00Z",
                "--expires", "2030-01-01T00:00:00Z",
                "--tags", "env=prod",
            ]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal("""{"contentType":"text/plain","attributes":{"nbf":1735689600,"exp":1893456000},"tags":{"env":"prod"}}""", Assert.Single(sentBodies));
    }

    // ── secret delete ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_SecretDelete_SendsDeleteToSecretPath()
    {
        _mockWire.RespondTo(HttpMethod.Delete, SecretUrl, HttpStatusCode.OK, DeletedSecretBundle);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["keyvault", "secret", "delete", "--vault-name", VaultName, "--name", SecretName, "--yes"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal([$"DELETE {SecretPath}?api-version={ApiVersion}"], _mockWire.FullRequestLines());
    }

    // ── certificate create ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_CertificateCreateCompleted_SendsSinglePostToCreatePath()
    {
        _mockWire.RespondTo(HttpMethod.Post, $"{CertificateUrl}/create", HttpStatusCode.Accepted, CompletedOperation);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["keyvault", "certificate", "create", "--vault-name", VaultName, "--name", CertificateName, "--policy", SelfSignedPolicy]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal([$"POST {CertificatePath}/create?api-version={ApiVersion}"], _mockWire.FullRequestLines());
    }

    [Fact]
    public async Task RunAsync_CertificateCreateSnakeCasePolicy_SendsPolicyInBody()
    {
        List<string?> sentBodies = _mockWire.RespondTo(HttpMethod.Post, $"{CertificateUrl}/create", HttpStatusCode.Accepted, CompletedOperation);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            [
                "keyvault", "certificate", "create",
                "--vault-name", VaultName,
                "--name", CertificateName,
                "--policy", """{"issuer":{"name":"Self"},"x509_props":{"subject":"CN=contoso.com","validity_months":12},"key_props":{"kty":"RSA","key_size":2048}}""",
            ]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal("""{"policy":{"key_props":{"kty":"RSA","key_size":2048},"x509_props":{"subject":"CN=contoso.com","validity_months":12},"issuer":{"name":"Self"}}}""", Assert.Single(sentBodies));
    }

    [Fact]
    public async Task RunAsync_CertificateCreateRestApiPolicyNames_SendsSnakeCasePolicy()
    {
        List<string?> sentBodies = _mockWire.RespondTo(HttpMethod.Post, $"{CertificateUrl}/create", HttpStatusCode.Accepted, CompletedOperation);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            [
                "keyvault", "certificate", "create",
                "--vault-name", VaultName,
                "--name", CertificateName,
                "--policy", """{"issuerParameters":{"name":"Self","certificateType":"OV-SSL"},"x509CertificateProperties":{"subject":"CN=a","subjectAlternativeNames":{"dnsNames":["a.com"]},"validityInMonths":6},"keyProperties":{"keyType":"EC","curve":"P-256"},"lifetimeActions":[{"trigger":{"daysBeforeExpiry":30},"action":{"actionType":"AutoRenew"}}]}""",
            ]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal(
            """{"policy":{"key_props":{"kty":"EC","crv":"P-256"},"x509_props":{"subject":"CN=a","sans":{"dns_names":["a.com"]},"validity_months":6},"lifetime_actions":[{"trigger":{"days_before_expiry":30},"action":{"action_type":"AutoRenew"}}],"issuer":{"name":"Self","cty":"OV-SSL"}}}""",
            Assert.Single(sentBodies));
    }

    [Fact]
    public async Task RunAsync_CertificateCreatePowerShellPolicyNames_SendsSnakeCasePolicy()
    {
        List<string?> sentBodies = _mockWire.RespondTo(HttpMethod.Post, $"{CertificateUrl}/create", HttpStatusCode.Accepted, CompletedOperation);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            [
                "keyvault", "certificate", "create",
                "--vault-name", VaultName,
                "--name", CertificateName,
                "--policy", """{"IssuerName":"Self","SubjectName":"CN=a","DnsNames":["a.com"],"Kty":"RSA","RenewAtPercentageLifetime":80}""",
            ]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal(
            """{"policy":{"key_props":{"kty":"RSA"},"x509_props":{"subject":"CN=a","sans":{"dns_names":["a.com"]}},"lifetime_actions":[{"trigger":{"lifetime_percentage":80},"action":{"action_type":"AutoRenew"}}],"issuer":{"name":"Self"}}}""",
            Assert.Single(sentBodies));
    }

    [Fact]
    public async Task RunAsync_CertificateCreateAttributesAndTags_SendsThemAfterPolicy()
    {
        List<string?> sentBodies = _mockWire.RespondTo(HttpMethod.Post, $"{CertificateUrl}/create", HttpStatusCode.Accepted, CompletedOperation);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            [
                "keyvault", "certificate", "create",
                "--vault-name", VaultName,
                "--name", CertificateName,
                "--policy", """{"issuer":{"name":"Self"},"x509_props":{"subject":"CN=a"}}""",
                "--enabled", "false",
                "--expires", "2030-01-01T00:00:00Z",
                "--not-before", "2025-01-01T00:00:00Z",
                "--tags", "env=dev",
            ]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal(
            """{"policy":{"x509_props":{"subject":"CN=a"},"issuer":{"name":"Self"}},"attributes":{"enabled":false,"nbf":1735689600,"exp":1893456000},"tags":{"env":"dev"}}""",
            Assert.Single(sentBodies));
    }

    [Fact]
    public async Task RunAsync_CertificateCreateInProgress_PollsPendingOperation()
    {
        _mockWire.RespondWithHeaders(HttpMethod.Post, $"{CertificateUrl}/create", HttpStatusCode.Accepted, PendingOperation, "Retry-After: 0");
        _mockWire.RespondTo(HttpMethod.Get, $"{CertificateUrl}/pending", HttpStatusCode.OK, CompletedOperation);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["keyvault", "certificate", "create", "--vault-name", VaultName, "--name", CertificateName, "--policy", SelfSignedPolicy]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal([$"POST {CertificatePath}/create?api-version={ApiVersion}", $"GET {CertificatePath}/pending?api-version={ApiVersion}"], _mockWire.FullRequestLines());
    }

    [Fact]
    public async Task RunAsync_CertificateCreateInProgressNoWait_DoesNotPoll()
    {
        _mockWire.RespondTo(HttpMethod.Post, $"{CertificateUrl}/create", HttpStatusCode.Accepted, PendingOperation);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["keyvault", "certificate", "create", "--vault-name", VaultName, "--name", CertificateName, "--policy", SelfSignedPolicy, "--no-wait"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal([$"POST {CertificatePath}/create?api-version={ApiVersion}"], _mockWire.FullRequestLines());
    }

    [Fact]
    public async Task RunAsync_CertificateCreateUnknownIssuer_DoesNotPoll()
    {
        _mockWire.RespondTo(HttpMethod.Post, $"{CertificateUrl}/create", HttpStatusCode.Accepted,
            $$$"""{"id":"{{{CertificateUrl}}}/pending","status":"inProgress","issuer":{"name":"Unknown"}}""");
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            [
                "keyvault", "certificate", "create",
                "--vault-name", VaultName,
                "--name", CertificateName,
                "--policy", """{"issuer":{"name":"Unknown"},"x509_props":{"subject":"CN=a"}}""",
            ]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal([$"POST {CertificatePath}/create?api-version={ApiVersion}"], _mockWire.FullRequestLines());
    }

    // ── certificate show ───────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_CertificateShow_SendsGetToCertificatePathWithTrailingSlash()
    {
        _mockWire.RespondTo(HttpMethod.Get, $"{CertificateUrl}/", HttpStatusCode.OK, CertificateBundle);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["keyvault", "certificate", "show", "--vault-name", VaultName, "--name", CertificateName]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal([$"GET {CertificatePath}/?api-version={ApiVersion}"], _mockWire.FullRequestLines());
    }

    [Fact]
    public async Task RunAsync_CertificateShowVersion_SendsGetToVersionPath()
    {
        _mockWire.RespondTo(HttpMethod.Get, $"{CertificateUrl}/{Version}", HttpStatusCode.OK, CertificateBundle);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["keyvault", "certificate", "show", "--vault-name", VaultName, "--name", CertificateName, "--version", Version]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal([$"GET {CertificatePath}/{Version}?api-version={ApiVersion}"], _mockWire.FullRequestLines());
    }

    // ── certificate list ───────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_CertificateList_SendsGetToCertificatesPath()
    {
        _mockWire.RespondTo(HttpMethod.Get, $"{VaultUrl}/certificates", HttpStatusCode.OK, EmptyList);
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["keyvault", "certificate", "list", "--vault-name", VaultName]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal([$"GET /certificates?api-version={ApiVersion}"], _mockWire.FullRequestLines());
    }

    [Fact]
    public async Task RunAsync_CertificateListMaxResults_SendsMaxResultsQuery()
    {
        _mockWire.RespondTo(HttpMethod.Get, $"{VaultUrl}/certificates", HttpStatusCode.OK, EmptyList);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["keyvault", "certificate", "list", "--vault-name", VaultName, "--maxresults", "10"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal("10", Assert.Single(_mockWire.SentRequests()).QueryValue("maxresults"));
    }

    [Theory]
    [InlineData("true")]
    [InlineData("false")]
    public async Task RunAsync_CertificateListIncludePending_SendsIncludePendingQuery(string includePending)
    {
        _mockWire.RespondTo(HttpMethod.Get, $"{VaultUrl}/certificates", HttpStatusCode.OK, EmptyList);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["keyvault", "certificate", "list", "--vault-name", VaultName, "--include-pending", includePending]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal(includePending, Assert.Single(_mockWire.SentRequests()).QueryValue("includePending"));
    }

    [Fact]
    public async Task RunAsync_CertificateListNextLink_FollowsNextLinkAsGiven()
    {
        _mockWire.RespondInOrder(
            $$"""{"value":[],"nextLink":"{{VaultUrl}}/certificates?api-version={{ApiVersion}}&$skiptoken=page2"}""",
            EmptyList);
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["keyvault", "certificate", "list", "--vault-name", VaultName]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal([$"GET /certificates?api-version={ApiVersion}", $"GET /certificates?api-version={ApiVersion}&$skiptoken=page2"], _mockWire.FullRequestLines());
    }

    // ── certificate list-versions ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_CertificateListVersions_SendsGetToVersionsPath()
    {
        _mockWire.RespondTo(HttpMethod.Get, $"{CertificateUrl}/versions", HttpStatusCode.OK, EmptyList);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["keyvault", "certificate", "list-versions", "--vault-name", VaultName, "--name", CertificateName]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal([$"GET {CertificatePath}/versions?api-version={ApiVersion}"], _mockWire.FullRequestLines());
    }

    [Fact]
    public async Task RunAsync_CertificateListVersionsMaxResults_SendsMaxResultsQuery()
    {
        _mockWire.RespondTo(HttpMethod.Get, $"{CertificateUrl}/versions", HttpStatusCode.OK, EmptyList);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["keyvault", "certificate", "list-versions", "--vault-name", VaultName, "--name", CertificateName, "--maxresults", "3"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal("3", Assert.Single(_mockWire.SentRequests()).QueryValue("maxresults"));
    }

    // ── certificate set-attributes ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_CertificateSetAttributes_SendsPatchToCertificatePath()
    {
        _mockWire.RespondTo(HttpMethod.Patch, $"{CertificateUrl}/", HttpStatusCode.OK, CertificateBundle);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["keyvault", "certificate", "set-attributes", "--vault-name", VaultName, "--name", CertificateName, "--enabled", "false"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal([$"PATCH {CertificatePath}/?api-version={ApiVersion}"], _mockWire.FullRequestLines());
    }

    [Fact]
    public async Task RunAsync_CertificateSetAttributesEnabled_SendsOnlyEnabledAttribute()
    {
        List<string?> sentBodies = _mockWire.RespondTo(HttpMethod.Patch, $"{CertificateUrl}/", HttpStatusCode.OK, CertificateBundle);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["keyvault", "certificate", "set-attributes", "--vault-name", VaultName, "--name", CertificateName, "--enabled", "false"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal("""{"attributes":{"enabled":false}}""", Assert.Single(sentBodies));
    }

    [Fact]
    public async Task RunAsync_CertificateSetAttributesVersion_SendsPatchToVersionPath()
    {
        _mockWire.RespondTo(HttpMethod.Patch, $"{CertificateUrl}/{Version}", HttpStatusCode.OK, CertificateBundle);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            [
                "keyvault", "certificate", "set-attributes",
                "--vault-name", VaultName,
                "--name", CertificateName,
                "--version", Version,
                "--enabled", "true",
            ]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal([$"PATCH {CertificatePath}/{Version}?api-version={ApiVersion}"], _mockWire.FullRequestLines());
    }

    [Fact]
    public async Task RunAsync_CertificateSetAttributesDatesAndTags_SendsAttributesAndTags()
    {
        List<string?> sentBodies = _mockWire.RespondTo(HttpMethod.Patch, $"{CertificateUrl}/", HttpStatusCode.OK, CertificateBundle);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            [
                "keyvault", "certificate", "set-attributes",
                "--vault-name", VaultName,
                "--name", CertificateName,
                "--not-before", "2025-01-01T00:00:00Z",
                "--expires", "2030-01-01T00:00:00Z",
                "--tags", "env=prod",
            ]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal("""{"attributes":{"nbf":1735689600,"exp":1893456000},"tags":{"env":"prod"}}""", Assert.Single(sentBodies));
    }

    [Fact]
    public async Task RunAsync_CertificateSetAttributesPolicyOnly_PatchesPolicyThenGetsCertificate()
    {
        _mockWire.RespondTo(HttpMethod.Patch, $"{CertificateUrl}/policy", HttpStatusCode.OK, """{"x509_props":{"validity_months":24}}""");
        _mockWire.RespondTo(HttpMethod.Get, $"{CertificateUrl}/", HttpStatusCode.OK, CertificateBundle);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            [
                "keyvault", "certificate", "set-attributes",
                "--vault-name", VaultName,
                "--name", CertificateName,
                "--policy", """{"x509_props":{"validity_months":24}}""",
            ]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal([$"PATCH {CertificatePath}/policy?api-version={ApiVersion}", $"GET {CertificatePath}/?api-version={ApiVersion}"], _mockWire.FullRequestLines());
    }

    [Fact]
    public async Task RunAsync_CertificateSetAttributesPolicy_SendsPolicyAsPatchBody()
    {
        List<string?> sentBodies = _mockWire.RespondTo(HttpMethod.Patch, $"{CertificateUrl}/policy", HttpStatusCode.OK, """{"x509_props":{"validity_months":24}}""");
        _mockWire.RespondTo(HttpMethod.Get, $"{CertificateUrl}/", HttpStatusCode.OK, CertificateBundle);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            [
                "keyvault", "certificate", "set-attributes",
                "--vault-name", VaultName,
                "--name", CertificateName,
                "--policy", """{"x509_props":{"validity_months":24}}""",
            ]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal("""{"x509_props":{"validity_months":24}}""", Assert.Single(sentBodies));
    }

    [Fact]
    public async Task RunAsync_CertificateSetAttributesPolicyAndEnabled_PatchesPolicyThenCertificate()
    {
        _mockWire.RespondTo(HttpMethod.Patch, $"{CertificateUrl}/policy", HttpStatusCode.OK, """{"x509_props":{"validity_months":24}}""");
        _mockWire.RespondTo(HttpMethod.Patch, $"{CertificateUrl}/", HttpStatusCode.OK, CertificateBundle);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            [
                "keyvault", "certificate", "set-attributes",
                "--vault-name", VaultName,
                "--name", CertificateName,
                "--policy", """{"x509_props":{"validity_months":24}}""",
                "--enabled", "true",
            ]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal([$"PATCH {CertificatePath}/policy?api-version={ApiVersion}", $"PATCH {CertificatePath}/?api-version={ApiVersion}"], _mockWire.FullRequestLines());
    }

    // ── certificate download ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_CertificateDownload_SendsGetToCertificatePath()
    {
        _mockWire.RespondTo(HttpMethod.Get, $"{CertificateUrl}/", HttpStatusCode.OK, CertificateBundle);
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

        // The canned certificate has no "cer", so the command stops before writing the file.
        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal([$"GET {CertificatePath}/?api-version={ApiVersion}"], _mockWire.FullRequestLines());
    }

    [Fact]
    public async Task RunAsync_CertificateDownloadVersion_SendsGetToVersionPath()
    {
        _mockWire.RespondTo(HttpMethod.Get, $"{CertificateUrl}/{Version}", HttpStatusCode.OK, CertificateBundle);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            [
                "keyvault", "certificate", "download",
                "--vault-name", VaultName,
                "--name", CertificateName,
                "--version", Version,
                "--file", Path.Combine(_missingDirectory, $"{CertificateName}.pem"),
            ]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        // The canned certificate has no "cer", so the command stops before writing the file.
        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal([$"GET {CertificatePath}/{Version}?api-version={ApiVersion}"], _mockWire.FullRequestLines());
    }

    // ── certificate delete ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_CertificateDelete_SendsDeleteToCertificatePath()
    {
        _mockWire.RespondTo(HttpMethod.Delete, CertificateUrl, HttpStatusCode.OK, CertificateBundle);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["keyvault", "certificate", "delete", "--vault-name", VaultName, "--name", CertificateName, "--yes"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal([$"DELETE {CertificatePath}?api-version={ApiVersion}"], _mockWire.FullRequestLines());
    }
}
