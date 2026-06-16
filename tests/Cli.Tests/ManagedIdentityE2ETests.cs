using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Authentication;
using Core.Runtime.Handler;
using Tests.Common.Stubs;
using Xunit;

namespace Cli.Tests;

/// <summary>
/// End-to-end test for managed identity login flow with mocked IMDS endpoint.
/// Tests: logout → login --identity → verify context.json → execute command with token
/// </summary>
[Collection("Azclips Service Provider Tests")]
public class ManagedIdentityE2ETests : IAsyncLifetime
{
    private MockImdsServer? _imds;
    private const string MockAccessToken = "eyJ0eXAiOiJKV1QiLCJhbGciOiJSUzI1NiIsIng1dCI6Ik1UTXNNVUl6UWpVM01qVkJRVEkyTlRjNU9EUkNOMFU0TkRKQ05rRkNRamRDT0RNMk5qazBSUSIsImtpZCI6Ik1UTXNNVUl6UWpVM01qVkJRVEkyTlRjNU9EUkNOMFU0TkRKQ05rRkNRamRDT0RNMk5qazBSUSJ9.eyJhdWQiOiJodHRwczovL21hbmFnZW1lbnQuYXp1cmUuY29tIiwiaXNzIjoiaHR0cHM6Ly9zdHMud2luZG93cy5uZXQvNzJmOTg4YmYtODZmMS00MWFmLTkxYWItMmQ3Y2QwMTFkYjQ3LyIsImlhdCI6MTcxODgwMDAwMCwibmJmIjoxNzE4ODAwMDAwLCJleHAiOjE3MTg4MDM2MDAsImFjciI6IjEiLCJhaW8iOiJBU1FBMi84WEFBQUFjaXJydXMtdGVzdC1tb2NrLXRva2VuIiwiYXBwaWQiOiJiNGM1ZDZlNy0xMjM0LTRhYmMtOGRlZi0wMTIzNDU2Nzg5YWIiLCJhcHBpZGFjciI6IjIiLCJpZHAiOiJodHRwczovL3N0cy53aW5kb3dzLm5ldC83MmY5ODhiZi04NmYxLTQxYWYtOTFhYi0yZDdjZDAxMWRiNDcvIiwib2lkIjoiYzdkOGU5ZjAtMjM0NS00YmNkLTkwMTItMzQ1Njc4OWFiY2RlIiwic3ViIjoiZDBlMWYyYTMtMzQ1Ni00Y2RlLWExMjMtNDU2Nzg5YWJjZGVmIiwidGlkIjoiNzJmOTg4YmYtODZmMS00MWFmLTkxYWItMmQ3Y2QwMTFkYjQ3IiwidmVyIjoiMS4wIn0.MOCK_SIGNATURE";
    private const string MockTenantId = "72f988bf-86f1-41af-91ab-2d7cd011db47";
    private const string MockSubscriptionId = "a1b2c3d4-5e6f-7a8b-9c0d-1e2f3a4b5c6d";
    private const string MockClientId = "b4c5d6e7-1234-4abc-8def-0123456789ab";

    public async Task InitializeAsync()
    {
        // For TRUE E2E, verify extensions are built
        // Test runs from: tests/Cli.Tests/bin/Debug/net10.0/
        // Extensions are in: artifacts/Debug/Microsoft.Azure.Azclips.Extensions.*/
        // So we need to go up 5 levels: ../../../../.. then down to artifacts/Debug
        var debugDir = Path.GetFullPath(
            Path.Combine(
                AppContext.BaseDirectory,
                "..", "..", "..", "..", "..", "artifacts", "Debug"
            )
        );

        var loginExtensionDir = Path.Combine(debugDir, "Microsoft.Azure.Azclips.Extensions.Login");

        if (!Directory.Exists(loginExtensionDir))
        {
            throw new InvalidOperationException(
                $"Login extension not found at {loginExtensionDir}. " +
                $"Run `dotnet build tests/Cli.Tests/Cli.Tests.csproj` (or `dotnet test`) to regenerate it.");
        }

        Console.WriteLine($"Extensions found in: {debugDir}");
        Console.WriteLine($"   - Login extension: {loginExtensionDir}");

        // Start mock IMDS server and redirect Azure MI discovery to it.
        _imds = new MockImdsServer(MockAccessToken, MockClientId);
        Environment.SetEnvironmentVariable("AZURE_POD_IDENTITY_AUTHORITY_HOST", _imds.Url);
    }

    public async Task DisposeAsync()
    {
        // Clean up environment
        Environment.SetEnvironmentVariable("AZURE_POD_IDENTITY_AUTHORITY_HOST", null);

        // Stop mock server
        if (_imds != null)
        {
            await _imds.DisposeAsync();
        }
    }

    /// <summary>
    /// Runs the actual CLI binary as a subprocess for TRUE E2E testing.
    /// Sets AZURE_POD_IDENTITY_AUTHORITY_HOST to point to our mock IMDS server.
    /// </summary>
    private async Task<int> RunCliAsync(string cliBinary, params string[] args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = cliBinary,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        // Set environment variable to redirect IMDS calls to our mock server
        psi.Environment["AZURE_POD_IDENTITY_AUTHORITY_HOST"] = _imds!.Url;

        foreach (var arg in args)
        {
            psi.ArgumentList.Add(arg);
        }

        using var process = new Process { StartInfo = psi };
        var output = new StringBuilder();
        var error = new StringBuilder();

        process.OutputDataReceived += (sender, e) =>
        {
            if (e.Data != null)
            {
                output.AppendLine(e.Data);
                Console.WriteLine($"[CLI OUT] {e.Data}");
            }
        };

        process.ErrorDataReceived += (sender, e) =>
        {
            if (e.Data != null)
            {
                error.AppendLine(e.Data);
                Console.WriteLine($"[CLI ERR] {e.Data}");
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync();

        return process.ExitCode;
    }

    [Fact]
    public async Task ManagedIdentity_E2E_SystemAssigned_ShouldLoginAndSaveContext()
    {
        // TRUE E2E: Run the actual CLI binary as a subprocess
        // This tests the real deployment layout and extension loading

        // Arrange - ensure clean state
        var contextService = CreateAuthContextService();
        await contextService.ClearContextAsync();

        // Find the actual CLI binary
        var cliBinary = Path.GetFullPath(
            Path.Combine(
                AppContext.BaseDirectory,
                "..", "..", "..", "..", "..", "artifacts", "Debug", "Core.Cli", "az"
            )
        );

        if (!File.Exists(cliBinary))
        {
            throw new InvalidOperationException($"CLI binary not found: {cliBinary}");
        }

        Console.WriteLine($"Testing CLI binary: {cliBinary}");

        // Act 1: Logout (clean state)
        var logoutExitCode = await RunCliAsync(cliBinary, "login", "logout");
        Assert.Equal(0, logoutExitCode);

        // Act 2: Login with managed identity (system-assigned)
        // Note: --tenant is NOT allowed with --identity (matches Azure CLI behavior)
        var loginExitCode = await RunCliAsync(cliBinary, "login", "--identity", "--subscription-id", MockSubscriptionId);

        // Assert: Login succeeded
        Assert.Equal(0, loginExitCode);

        // Assert: Context.json was created and contains MI token
        var savedContext = await contextService.GetCurrentContextAsync();
        Assert.NotNull(savedContext);
        Assert.Equal(LoginMode.ManagedIdentity, savedContext.Mode);
        // Tenant ID should be extracted from the MI token's "tid" claim
        Assert.Equal(MockTenantId, savedContext.TenantId);
        Assert.Equal(MockSubscriptionId, savedContext.SubscriptionId);
        Assert.Equal("MSI@", savedContext.ManagedIdentityId); // System-assigned

        // Verify the context was saved (uses default location, not test temp directory)
        var defaultContextDir = AuthContextService.BaseDirectory;
        var defaultContextFile = Path.Combine(defaultContextDir, "context.json");
        Assert.True(File.Exists(defaultContextFile), $"Context file should exist at: {defaultContextFile}");
        var contextJson = await File.ReadAllTextAsync(defaultContextFile);
        // Tenant ID extracted from MI token's "tid" claim
        Assert.Contains(MockTenantId, contextJson);
        Assert.Contains("ManagedIdentity", contextJson);
        Assert.Contains("MSI@", contextJson);
    }

    [Fact]
    public async Task ManagedIdentity_E2E_UserAssignedClientId_ShouldLoginAndSaveContext()
    {
        // TRUE E2E: Run the actual CLI binary as a subprocess

        // Arrange
        var contextService = CreateAuthContextService();
        await contextService.ClearContextAsync();

        // Find the actual CLI binary
        var cliBinary = Path.GetFullPath(
            Path.Combine(
                AppContext.BaseDirectory,
                "..", "..", "..", "..", "..", "artifacts", "Debug", "Core.Cli", "az"
            )
        );

        if (!File.Exists(cliBinary))
        {
            throw new InvalidOperationException($"CLI binary not found: {cliBinary}");
        }

        // Act 1: Logout
        await RunCliAsync(cliBinary, "login", "logout");

        // Act 2: Login with user-assigned MI (client ID)
        var customClientId = "e5f6a7b8-4567-4def-b234-56789abcdef0";
        var loginExitCode = await RunCliAsync(cliBinary, "login", "--identity", "--subscription-id", MockSubscriptionId, "--client-id", customClientId);

        // Assert
        Assert.Equal(0, loginExitCode);

        var savedContext = await contextService.GetCurrentContextAsync();
        Assert.NotNull(savedContext);
        Assert.Equal(LoginMode.ManagedIdentity, savedContext.Mode);
        Assert.Equal($"MSIClient@{customClientId}", savedContext.ManagedIdentityId);
    }



    [Fact]
    public async Task ManagedIdentity_E2E_CommandWithToken_ShouldUseTokenFromContext()
    {
        // Verifies that a command's outgoing request carries the managed-identity bearer
        // token established by `login --identity`. Instead of mocking a full ARM endpoint and
        // routing through a service extension, we exercise the production AuthHandler — the
        // component that attaches the token to every command request — and capture the request.

        // Arrange: clean state, then login with managed identity so the auth context is persisted.
        var contextService = CreateAuthContextService();
        await contextService.ClearContextAsync();

        var cliBinary = Path.GetFullPath(
            Path.Combine(
                AppContext.BaseDirectory,
                "..", "..", "..", "..", "..", "artifacts", "Debug", "Core.Cli", "az"
            )
        );

        if (!File.Exists(cliBinary))
        {
            throw new InvalidOperationException($"CLI binary not found: {cliBinary}");
        }

        var loginExitCode = await RunCliAsync(cliBinary, "login", "--identity", "--subscription-id", MockSubscriptionId);
        Assert.Equal(0, loginExitCode);

        var savedContext = await contextService.GetCurrentContextAsync();
        Assert.NotNull(savedContext);
        Assert.Equal(LoginMode.ManagedIdentity, savedContext.Mode);

        // Build the same auth service commands use to silently re-acquire the MI token from context.
        var logger = new StubLoggingService();
        var telemetry = new StubTelemetryService();
        var authService = new MsalAuthService(
            logger,
            telemetry,
            contextService,
            new MsalTokenCacheProvider(logger),
            new ManagedIdentityAuthenticator(new AzureCredentialFactory(), logger, telemetry));

        // AuthHandler is the production pipeline step that stamps the bearer token onto a request.
        var authHandler = new AuthHandler
        {
            AuthContext = savedContext,
            AcquireTokenAsyncDefault = authService.AcquireTokenSilentlyAsync,
        };

        // Act: run a representative command request through the handler, capturing the outgoing message.
        HttpRequestMessage? capturedRequest = null;
        var terminal = new CapturingSendAsync(request => capturedRequest = request);
        var commandRequest = new HttpRequestMessage(
            HttpMethod.Get,
            $"https://management.azure.com/subscriptions/{MockSubscriptionId}/resourcegroups?api-version=2022-12-01");

        await authHandler.SendAsync(commandRequest, new StubEventPublisher(), terminal);

        // Assert: the command request carries the MI token acquired from the saved login context.
        Assert.NotNull(capturedRequest);
        Assert.NotNull(capturedRequest!.Headers.Authorization);
        Assert.Equal("Bearer", capturedRequest.Headers.Authorization!.Scheme);
        Assert.Equal(MockAccessToken, capturedRequest.Headers.Authorization.Parameter);
    }


    [Fact]
    public async Task ManagedIdentity_E2E_MultipleIdSelectors_ShouldFail()
    {
        // Match Azure CLI's ManagedIdentityAuth.parse_ids: only one of
        // --client-id / --object-id / --resource-id may be supplied.
        var cliBinary = Path.GetFullPath(
            Path.Combine(
                AppContext.BaseDirectory,
                "..", "..", "..", "..", "..", "artifacts", "Debug", "Core.Cli", "az"
            )
        );

        if (!File.Exists(cliBinary))
        {
            throw new InvalidOperationException($"CLI binary not found: {cliBinary}");
        }

        var clientId = "e5f6a7b8-4567-4def-b234-56789abcdef0";
        var objectId = "22222222-2222-2222-2222-222222222222";

        var exitCode = await RunCliAsync(
            cliBinary, "login", "--identity", "--client-id", clientId, "--object-id", objectId);

        Assert.NotEqual(0, exitCode);
    }

    [Fact]
    public async Task ManagedIdentity_MockImdsServer_RequiresMetadataHeader()
    {
        // Test that IMDS mock enforces Metadata header
        using var httpClient = new HttpClient();
        var request = new HttpRequestMessage(HttpMethod.Get,
            $"{_imds!.Url}/metadata/identity/oauth2/token?resource=https://management.azure.com");
        // Deliberately omit Metadata header

        var response = await httpClient.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ManagedIdentity_MockImdsServer_HandlesClientIdParameter()
    {
        // Test that mock server returns correct client_id when specified
        using var httpClient = new HttpClient();
        var customClientId = "f6a7b8c9-5678-4ef0-c345-6789abcdef01";
        var request = new HttpRequestMessage(HttpMethod.Get,
            $"{_imds!.Url}/metadata/identity/oauth2/token?resource=https://management.azure.com&client_id={customClientId}");
        request.Headers.Add("Metadata", "true");

        var response = await httpClient.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();
        var tokenResponse = JsonSerializer.Deserialize<JsonDocument>(content);

        var clientId = tokenResponse?.RootElement.GetProperty("client_id").GetString();
        Assert.Equal(customClientId, clientId);
    }

    private IAuthContextService CreateAuthContextService()
    {
        // Create a real AuthContextService for testing
        // Note: This uses the real file system at the default context location
        var loggingService = new StubLoggingService();
        var telemetryService = new StubTelemetryService();
        return new Core.Authentication.AuthContextService(loggingService, telemetryService);
    }

    /// <summary>
    /// Terminal pipeline step that records the request it receives and returns an empty 200 response.
    /// </summary>
    private sealed class CapturingSendAsync(Action<HttpRequestMessage> capture) : ISendAsync
    {
        public Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, IEventPublisher callback)
        {
            capture(request);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
