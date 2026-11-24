// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Text.Json;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Engine;
using Microsoft.Azure.Azclips.Extensions.ManagedServiceIdentity.Models;
using Moq;
using static Cli.Tests.Commands.CommandTestHelpers;
using ManagedServiceIdentityIndex = Microsoft.Azure.Azclips.Extensions.ManagedServiceIdentity.ExtensionIndex;

namespace Cli.Tests.Commands.ManagedIdentity;

/// <summary>
/// Runs managed identity and federated credential CLI commands through the real engine against canned ARM responses and
/// checks what each command writes, or the error it throws. The HTTP wire is a strict mock, so a request a test did not
/// set up a response for throws <see cref="MockException"/>.
/// </summary>
public sealed class ManagedIdentityCommandResponseTests : IDisposable
{
    private const string ExtensionName = "ManagedServiceIdentity";
    private const string GroupName = "rg";
    private const string IdentityName = "id1";
    private const string SecondIdentityName = "id2";
    private const string CredentialName = "fic1";
    private const string SecondCredentialName = "fic2";
    private const string Location = "westus";
    private const string PrincipalId = "p";
    private const string ClientId = "c";
    private const string Issuer = "https://issuer";
    private const string Subject = "sub1";
    private const string UpdatedSubject = "sub2";
    private const string ApiVersion = "2025-05-31-preview";
    private const string IdentitiesProvider = "providers/Microsoft.ManagedIdentity/userAssignedIdentities";
    private const string GroupIdentitiesPath = $"/subscriptions/{SubscriptionId}/resourceGroups/{GroupName}/{IdentitiesProvider}";
    private const string IdentityPath = $"{GroupIdentitiesPath}/{IdentityName}";
    private const string SecondIdentityPath = $"{GroupIdentitiesPath}/{SecondIdentityName}";
    private const string CredentialsPath = $"{IdentityPath}/federatedIdentityCredentials";
    private const string CredentialPath = $"{CredentialsPath}/{CredentialName}";
    private const string SecondCredentialPath = $"{CredentialsPath}/{SecondCredentialName}";
    private const string IdentityUrl = ArmUrl + IdentityPath;
    private const string SubscriptionIdentitiesUrl = $"{ArmUrl}/subscriptions/{SubscriptionId}/{IdentitiesProvider}";
    private const string CredentialsUrl = ArmUrl + CredentialsPath;
    private const string CredentialUrl = ArmUrl + CredentialPath;
    private const string IdentityBody = $$$"""{"id":"{{{IdentityPath}}}","name":"{{{IdentityName}}}","type":"Microsoft.ManagedIdentity/userAssignedIdentities","location":"{{{Location}}}","tags":{"env":"dev"},"properties":{"tenantId":"t","principalId":"{{{PrincipalId}}}","clientId":"{{{ClientId}}}","isolationScope":"None"},"systemData":{"createdBy":"x"}}""";
    private const string ProdTagsIdentityBody = $$$"""{"id":"{{{IdentityPath}}}","name":"{{{IdentityName}}}","type":"Microsoft.ManagedIdentity/userAssignedIdentities","location":"{{{Location}}}","tags":{"env":"prod"},"properties":{"tenantId":"t","principalId":"{{{PrincipalId}}}","clientId":"{{{ClientId}}}","isolationScope":"None"},"systemData":{"createdBy":"x"}}""";
    private const string SecondIdentityBody = $$"""{"id":"{{SecondIdentityPath}}","name":"{{SecondIdentityName}}"}""";
    private const string CredentialBody = $$$"""{"id":"{{{CredentialPath}}}","name":"{{{CredentialName}}}","type":"Microsoft.ManagedIdentity/userAssignedIdentities/federatedIdentityCredentials","properties":{"issuer":"{{{Issuer}}}","subject":"{{{Subject}}}","audiences":["api://AzureADTokenExchange"]}}""";
    private const string UpdatedCredentialBody = $$$"""{"id":"{{{CredentialPath}}}","name":"{{{CredentialName}}}","type":"Microsoft.ManagedIdentity/userAssignedIdentities/federatedIdentityCredentials","properties":{"issuer":"{{{Issuer}}}","subject":"{{{UpdatedSubject}}}","audiences":["api://AzureADTokenExchange"]}}""";

    private readonly Mock<ISendAsync> _mockWire = new(MockBehavior.Strict);
    private readonly List<object> _output = [];
    private readonly Engine _engine;

    public ManagedIdentityCommandResponseTests() => _engine = CreateEngine(typeof(ManagedServiceIdentityIndex).Assembly, _mockWire);

    public void Dispose() => _engine.Dispose();

    // ── identity create ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_IdentityCreate_WritesCreatedIdentityClientId()
    {
        _mockWire.RespondTo(HttpMethod.Put, IdentityUrl, HttpStatusCode.Created, IdentityBody);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["identity", "create", "-n", IdentityName, "-g", GroupName, "-l", Location]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal(ClientId, Assert.IsType<UserAssignedIdentity>(Assert.Single(_output)).Properties!.ClientId);
    }

    [Fact]
    public async Task RunAsync_IdentityCreateConflict_ThrowsStatusUrlAndBody()
    {
        _mockWire.RespondTo(HttpMethod.Put, IdentityUrl, HttpStatusCode.Conflict, """{"error":{"code":"Conflict","message":"Identity id1 already exists."}}""");
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["identity", "create", "-n", IdentityName, "-g", GroupName, "-l", Location]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal(
            $$$"""409 Conflict from {{{IdentityUrl}}}?api-version={{{ApiVersion}}}: {"error":{"code":"Conflict","message":"Identity id1 already exists."}}""",
            Assert.IsType<HttpRequestException>(exception).Message);
    }

    // ── identity show ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_IdentityShow_WritesIdentityPrincipalId()
    {
        _mockWire.RespondTo(HttpMethod.Get, IdentityUrl, HttpStatusCode.OK, IdentityBody);
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["identity", "show", "-n", IdentityName, "-g", GroupName]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal(PrincipalId, Assert.IsType<UserAssignedIdentity>(Assert.Single(_output)).Properties!.PrincipalId);
    }

    [Fact]
    public async Task RunAsync_IdentityShowTwoIds_WritesArrayOfIdentities()
    {
        _mockWire.RespondTo(HttpMethod.Get, IdentityUrl, HttpStatusCode.OK, IdentityBody);
        _mockWire.RespondTo(HttpMethod.Get, ArmUrl + SecondIdentityPath, HttpStatusCode.OK, SecondIdentityBody);
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["identity", "show", "--ids", IdentityPath, SecondIdentityPath]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal([IdentityName, SecondIdentityName], Assert.IsType<UserAssignedIdentity[]>(Assert.Single(_output)).Select(i => i.Name));
    }

    [Fact]
    public async Task RunAsync_IdentityShowNotFound_ThrowsStatusUrlAndBody()
    {
        _mockWire.RespondTo(HttpMethod.Get, IdentityUrl, HttpStatusCode.NotFound, """{"error":{"code":"ResourceNotFound","message":"Identity id1 was not found."}}""");
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["identity", "show", "-n", IdentityName, "-g", GroupName]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal(
            $$$"""404 Not Found from {{{IdentityUrl}}}?api-version={{{ApiVersion}}}: {"error":{"code":"ResourceNotFound","message":"Identity id1 was not found."}}""",
            Assert.IsType<HttpRequestException>(exception).Message);
    }

    // ── identity list ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_IdentityListTwoPages_WritesIdentitiesFromBothPages()
    {
        _mockWire.RespondInOrder(
            $$"""{"value":[{"id":"{{IdentityPath}}","name":"{{IdentityName}}"}],"nextLink":"{{SubscriptionIdentitiesUrl}}?api-version={{ApiVersion}}&$skiptoken=p2"}""",
            $$"""{"value":[{"id":"{{SecondIdentityPath}}","name":"{{SecondIdentityName}}"}]}""");
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["identity", "list"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal([IdentityName, SecondIdentityName], Assert.IsType<List<UserAssignedIdentity>>(Assert.Single(_output)).Select(i => i.Name));
    }

    [Fact]
    public async Task RunAsync_IdentityListEmptyValue_WritesEmptyList()
    {
        _mockWire.RespondTo(HttpMethod.Get, SubscriptionIdentitiesUrl, HttpStatusCode.OK, EmptyList);
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["identity", "list"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Empty(Assert.IsType<List<UserAssignedIdentity>>(Assert.Single(_output)));
    }

    // ── identity update ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_IdentityUpdate_WritesPatchResponse()
    {
        _mockWire.RespondTo(HttpMethod.Patch, IdentityUrl, HttpStatusCode.OK, ProdTagsIdentityBody);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["identity", "update", "-n", IdentityName, "-g", GroupName, "--tags", "env=prod"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal(new Dictionary<string, string> { ["env"] = "prod" }, Assert.IsType<UserAssignedIdentity>(Assert.Single(_output)).Tags);
    }

    [Fact]
    public async Task RunAsync_IdentityUpdateTwoIds_WritesArrayOfIdentities()
    {
        _mockWire.RespondTo(HttpMethod.Patch, IdentityUrl, HttpStatusCode.OK, IdentityBody);
        _mockWire.RespondTo(HttpMethod.Patch, ArmUrl + SecondIdentityPath, HttpStatusCode.OK, SecondIdentityBody);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["identity", "update", "--ids", IdentityPath, SecondIdentityPath, "--tags", "env=prod"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal([IdentityName, SecondIdentityName], Assert.IsType<UserAssignedIdentity[]>(Assert.Single(_output)).Select(i => i.Name));
    }

    // ── identity delete ────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(HttpStatusCode.OK)]
    [InlineData(HttpStatusCode.NoContent)]
    public async Task RunAsync_IdentityDeleteWithoutBody_WritesNothing(HttpStatusCode statusCode)
    {
        _mockWire.RespondTo(HttpMethod.Delete, IdentityUrl, statusCode);
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["identity", "delete", "-n", IdentityName, "-g", GroupName]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Empty(_output);
    }

    [Fact]
    public async Task RunAsync_IdentityDeleteWithBody_WritesBodyAsJson()
    {
        _mockWire.RespondTo(HttpMethod.Delete, IdentityUrl, HttpStatusCode.OK, """{"status":"Deleted"}""");
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["identity", "delete", "-n", IdentityName, "-g", GroupName]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal("""{"status":"Deleted"}""", Assert.IsType<JsonElement>(Assert.Single(_output)).GetRawText());
    }

    // ── federated-credential create ────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_CredentialCreate_WritesCreatedCredentialSubject()
    {
        _mockWire.RespondTo(HttpMethod.Put, CredentialUrl, HttpStatusCode.Created, CredentialBody);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            [
                "identity", "federated-credential", "create",
                "-g", GroupName,
                "--identity-name", IdentityName,
                "-n", CredentialName,
                "--issuer", Issuer,
                "--subject", Subject,
            ]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal(Subject, Assert.IsType<FederatedIdentityCredential>(Assert.Single(_output)).Properties!.Subject);
    }

    // ── federated-credential show ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_CredentialShow_WritesCredentialIssuer()
    {
        _mockWire.RespondTo(HttpMethod.Get, CredentialUrl, HttpStatusCode.OK, CredentialBody);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["identity", "federated-credential", "show", "-g", GroupName, "--identity-name", IdentityName, "-n", CredentialName]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal(Issuer, Assert.IsType<FederatedIdentityCredential>(Assert.Single(_output)).Properties!.Issuer);
    }

    // ── federated-credential list ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_CredentialListTwoPages_WritesCredentialsFromBothPages()
    {
        _mockWire.RespondInOrder(
            $$"""{"value":[{"id":"{{CredentialPath}}","name":"{{CredentialName}}"}],"nextLink":"{{CredentialsUrl}}?api-version={{ApiVersion}}&$skiptoken=p2"}""",
            $$"""{"value":[{"id":"{{SecondCredentialPath}}","name":"{{SecondCredentialName}}"}]}""");
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["identity", "federated-credential", "list", "-g", GroupName, "--identity-name", IdentityName]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal([CredentialName, SecondCredentialName], Assert.IsType<List<FederatedIdentityCredential>>(Assert.Single(_output)).Select(c => c.Name));
    }

    [Fact]
    public async Task RunAsync_CredentialListEmptyValue_WritesEmptyList()
    {
        _mockWire.RespondTo(HttpMethod.Get, CredentialsUrl, HttpStatusCode.OK, EmptyList);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["identity", "federated-credential", "list", "-g", GroupName, "--identity-name", IdentityName]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Empty(Assert.IsType<List<FederatedIdentityCredential>>(Assert.Single(_output)));
    }

    // ── federated-credential update ────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_CredentialUpdate_WritesPutResponse()
    {
        _mockWire.RespondTo(HttpMethod.Get, CredentialUrl, HttpStatusCode.OK, CredentialBody);
        _mockWire.RespondTo(HttpMethod.Put, CredentialUrl, HttpStatusCode.OK, UpdatedCredentialBody);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["identity", "federated-credential", "update", "-g", GroupName, "--identity-name", IdentityName, "-n", CredentialName, "--subject", UpdatedSubject]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal(UpdatedSubject, Assert.IsType<FederatedIdentityCredential>(Assert.Single(_output)).Properties!.Subject);
    }

    [Fact]
    public async Task RunAsync_CredentialUpdateNotFound_ThrowsStatusUrlAndBody()
    {
        _mockWire.RespondTo(HttpMethod.Get, CredentialUrl, HttpStatusCode.NotFound, """{"error":{"code":"NotFound","message":"Federated credential fic1 was not found."}}""");
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["identity", "federated-credential", "update", "-g", GroupName, "--identity-name", IdentityName, "-n", CredentialName, "--subject", UpdatedSubject]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal(
            $$$"""404 Not Found from {{{CredentialUrl}}}?api-version={{{ApiVersion}}}: {"error":{"code":"NotFound","message":"Federated credential fic1 was not found."}}""",
            Assert.IsType<HttpRequestException>(exception).Message);
    }

    // ── federated-credential delete ────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(HttpStatusCode.OK)]
    [InlineData(HttpStatusCode.NoContent)]
    public async Task RunAsync_CredentialDeleteWithoutBody_WritesNothing(HttpStatusCode statusCode)
    {
        _mockWire.RespondTo(HttpMethod.Delete, CredentialUrl, statusCode);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["identity", "federated-credential", "delete", "-g", GroupName, "--identity-name", IdentityName, "-n", CredentialName]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Empty(_output);
    }
}
