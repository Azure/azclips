using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Authentication;
using Core.Runtime;
using Core.Runtime.Handler;
using Moq;
using Xunit;

namespace Cli.Tests;

/// <summary>
/// Managed identity login E2E tests with a mock IMDS endpoint.
/// <para>
/// These tests isolate the CLI subprocess's state with <c>AZCLIPS_HOME</c>, so login and logout
/// never touch the developer's real profile store on any platform.
/// </para>
/// </summary>
[Collection("Azclips Service Provider Tests")]
public class ManagedIdentityE2ETests : IAsyncLifetime
{
    // Resolve artifacts against the configuration these tests were built with,
    // so the suite works whether the solution is built Debug (local/GitHub) or
    // Release (distribution pipeline).
#if DEBUG
    private const string BuildConfiguration = "Debug";
#else
    private const string BuildConfiguration = "Release";
#endif

    private MockImdsServer? _imds;
    private static readonly ILoggingService Logger = Mock.Of<ILoggingService>();
    private static readonly ITelemetryService Telemetry = Mock.Of<ITelemetryService>();
    // unsigned JWT with the tenant id claim; avoids a token literal in source
    private static readonly string MockAccessToken = BuildMockJwt(MockTenantId);
    private const string MockTenantId = "72f988bf-86f1-41af-91ab-2d7cd011db47";
    private const string MockSubscriptionId = "a1b2c3d4-5e6f-7a8b-9c0d-1e2f3a4b5c6d";
    private const string MockClientId = "b4c5d6e7-1234-4abc-8def-0123456789ab";

    private static string BuildMockJwt(string tenantId)
    {
        static string B64Url(string value) =>
            Convert.ToBase64String(Encoding.UTF8.GetBytes(value)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        var header = B64Url("{\"alg\":\"none\",\"typ\":\"JWT\"}");
        var payload = B64Url($"{{\"tid\":\"{tenantId}\",\"aud\":\"https://management.azure.com\"}}");
        var signature = B64Url("mock-signature");
        return $"{header}.{payload}.{signature}";
    }

    // temp profile store so tests don't touch the real ~/.azclips
    private string _localAppDataDir = string.Empty;
    private string _profileDirectory = string.Empty;

    public async Task InitializeAsync()
    {
        _localAppDataDir = Path.Combine(Path.GetTempPath(), "azclips-mi-e2e", Guid.NewGuid().ToString("N"));
        _profileDirectory = Path.Combine(_localAppDataDir, HomePaths.DefaultHomeDirectoryName);
        Directory.CreateDirectory(_profileDirectory);

        var artifactsDir = ResolveArtifactsDirectory();

        var accountsExtensionDir = Path.Combine(artifactsDir, "Microsoft.Azure.Azclips.Extensions.Accounts");

        if (!Directory.Exists(accountsExtensionDir))
        {
            throw new InvalidOperationException(
                $"Accounts extension not found at {accountsExtensionDir}. " +
                $"Run `dotnet build tests/Cli.Tests/Cli.Tests.csproj` (or `dotnet test`) to regenerate it.");
        }

        Console.WriteLine($"Extensions found in: {artifactsDir}");
        Console.WriteLine($"   - Accounts extension: {accountsExtensionDir}");

        // route MSAL MI discovery to the mock IMDS server
        _imds = new MockImdsServer(MockAccessToken, MockClientId);
        Environment.SetEnvironmentVariable("AZURE_POD_IDENTITY_AUTHORITY_HOST", _imds.Url);
    }

    public async Task DisposeAsync()
    {
        Environment.SetEnvironmentVariable("AZURE_POD_IDENTITY_AUTHORITY_HOST", null);

        if (_imds != null)
        {
            await _imds.DisposeAsync();
        }

        // drop the temp store
        try
        {
            if (!string.IsNullOrEmpty(_localAppDataDir) && Directory.Exists(_localAppDataDir))
            {
                Directory.Delete(_localAppDataDir, recursive: true);
            }
        }
        catch
        {
            // ignore; OS cleans up temp eventually
        }
    }

    /// <summary>Runs the CLI subprocess against the mock IMDS endpoint.</summary>
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

        // route MI calls to the mock server
        psi.Environment["AZURE_POD_IDENTITY_AUTHORITY_HOST"] = _imds!.Url;

        psi.Environment[HomePaths.HomeOverrideVariable] = _profileDirectory;

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
    public async Task ManagedIdentity_E2E_SystemAssigned_ShouldLoginAndSaveProfile()
    {
        // Arrange
        ResetProfiles();
        var profileService = CreateProfileService();

        var cliBinary = ResolveCliBinary();

        Console.WriteLine($"Testing CLI binary: {cliBinary}");

        // Act
        var logoutExitCode = await RunCliAsync(cliBinary, "logout");
        Assert.Equal(0, logoutExitCode);

        // login with system-assigned MI
        var loginExitCode = await RunCliAsync(cliBinary, "login", "--identity", "--subscription-id", MockSubscriptionId);

        // Assert
        Assert.Equal(0, loginExitCode);

        // profile captures MI login
        var savedProfile = await profileService.GetActiveProfileAsync();
        Assert.NotNull(savedProfile);
        Assert.Equal(AuthMethod.ManagedIdentity, savedProfile!.AuthMethod);
        // managed identity does not persist a tenant id
        Assert.True(string.IsNullOrEmpty(savedProfile.TenantId));
        Assert.Equal(MockSubscriptionId, savedProfile.SubscriptionId);
        Assert.Equal("MSI@", savedProfile.ManagedIdentityId); // System-assigned

        // profile is written to the CLI's default location
        var profilesFile = Path.Combine(_profileDirectory, "profiles.json");
        Assert.True(File.Exists(profilesFile), $"Profiles file should exist at: {profilesFile}");
        var profilesJson = await File.ReadAllTextAsync(profilesFile);
        Assert.DoesNotContain(MockTenantId, profilesJson);
        Assert.Contains("ManagedIdentity", profilesJson);
        Assert.Contains("MSI@", profilesJson);
    }

    [Fact]
    public async Task ManagedIdentity_E2E_UserAssignedClientId_ShouldLoginAndSaveProfile()
    {
        // Arrange
        ResetProfiles();
        var profileService = CreateProfileService();

        var cliBinary = ResolveCliBinary();

        await RunCliAsync(cliBinary, "logout");

        // login with user-assigned MI
        var customClientId = "e5f6a7b8-4567-4def-b234-56789abcdef0";
        var loginExitCode = await RunCliAsync(cliBinary, "login", "--identity", "--subscription-id", MockSubscriptionId, "--client-id", customClientId);

        // Assert
        Assert.Equal(0, loginExitCode);

        var savedProfile = await profileService.GetActiveProfileAsync();
        Assert.NotNull(savedProfile);
        Assert.Equal(AuthMethod.ManagedIdentity, savedProfile!.AuthMethod);
        Assert.Equal($"MSIClient@{customClientId}", savedProfile.ManagedIdentityId);
        Assert.Equal(customClientId, savedProfile.ClientId);
    }



    [Fact]
    public async Task ManagedIdentity_E2E_CommandWithToken_ShouldUseTokenFromProfile()
    {
        // Exercise AuthHandler and verify it stamps the saved MI token on requests.

        // Arrange
        ResetProfiles();
        var profileService = CreateProfileService();

        var cliBinary = ResolveCliBinary();

        var loginExitCode = await RunCliAsync(cliBinary, "login", "--identity", "--subscription-id", MockSubscriptionId);
        Assert.Equal(0, loginExitCode);

        var savedProfile = await profileService.GetActiveProfileAsync();
        Assert.NotNull(savedProfile);
        Assert.Equal(AuthMethod.ManagedIdentity, savedProfile!.AuthMethod);

        var reauthExitCode = await RunCliAsync(cliBinary, "login", "--profile", savedProfile.Name);
        Assert.Equal(0, reauthExitCode);

        // same auth service commands use for silent refresh
        var msalOptions = new MsalClientOptions();
        var secretStore = new ServicePrincipalSecretStore();
        var authService = new MsalAuthService(
            Logger,
            Telemetry,
            profileService,
            new ClientMetadata(ClientType.CLI, MockClientId),
            new ManagedIdentityAuthenticator(new AzureCredentialFactory(), Logger, Telemetry),
            new FederatedTokenAuthenticator(Logger, Telemetry, secretStore, msalOptions),
            new ServicePrincipalSecretAuthenticator(Logger, Telemetry, secretStore, msalOptions),
            secretStore,
            msalOptions);

        // production handler that adds the bearer token
        var authHandler = new AuthHandler
        {
            Profile = savedProfile,
            AcquireTokenAsyncDefault = authService.AcquireTokenSilentlyAsync,
        };

        // Act
        HttpRequestMessage? capturedRequest = null;
        var terminal = new Mock<ISendAsync>();
        terminal.Setup(t => t.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<IEventPublisher>()))
            .Callback<HttpRequestMessage, IEventPublisher>((request, _) => capturedRequest = request)
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));
        var commandRequest = new HttpRequestMessage(
            HttpMethod.Get,
            $"https://management.azure.com/subscriptions/{MockSubscriptionId}/resourcegroups?api-version=2022-12-01");

        var publisher = new Mock<IEventPublisher>();
        publisher.SetReturnsDefault(Task.CompletedTask);
        await authHandler.SendAsync(commandRequest, publisher.Object, terminal.Object);

        // Assert
        Assert.NotNull(capturedRequest);
        Assert.NotNull(capturedRequest!.Headers.Authorization);
        Assert.Equal("Bearer", capturedRequest.Headers.Authorization!.Scheme);
        Assert.Equal(MockAccessToken, capturedRequest.Headers.Authorization.Parameter);
    }


    [Fact]
    public async Task ManagedIdentity_E2E_MultipleIdSelectors_ShouldFail()
    {
        // only one user-assigned MI selector is allowed
        var cliBinary = ResolveCliBinary();

        var clientId = "e5f6a7b8-4567-4def-b234-56789abcdef0";
        var objectId = "22222222-2222-2222-2222-222222222222";

        var exitCode = await RunCliAsync(
            cliBinary, "login", "--identity", "--client-id", clientId, "--object-id", objectId);

        Assert.NotEqual(0, exitCode);
    }

    [Fact]
    public async Task ManagedIdentity_MockImdsServer_RequiresMetadataHeader()
    {
        using var httpClient = new HttpClient();
        var request = new HttpRequestMessage(HttpMethod.Get,
            $"{_imds!.Url}/metadata/identity/oauth2/token?resource=https://management.azure.com");
        // omit Metadata header on purpose

        var response = await httpClient.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ManagedIdentity_MockImdsServer_HandlesClientIdParameter()
    {
        using var httpClient = new HttpClient();
        var customClientId = "f6a7b8c9-5678-4ef0-c345-6789abcdef01";
        var request = new HttpRequestMessage(HttpMethod.Get,
            $"{_imds!.Url}/metadata/identity/oauth2/token?resource=https://management.azure.com&client_id={customClientId}");
        request.Headers.Add("Metadata", "true");

        var response = await httpClient.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();
        using var tokenResponse = JsonSerializer.Deserialize<JsonDocument>(content);

        var clientId = tokenResponse?.RootElement.GetProperty("client_id").GetString();
        Assert.Equal(customClientId, clientId);
    }

    private static string ResolveCliBinary()
    {
        var cliBinary = Path.Combine(ResolveArtifactsDirectory(), "Core.Cli",
            OperatingSystem.IsWindows() ? "az.exe" : "az");

        if (!File.Exists(cliBinary))
        {
            throw new InvalidOperationException($"CLI binary not found: {cliBinary}");
        }

        return cliBinary;
    }

    /// <summary>
    /// Locates the <c>artifacts/&lt;Configuration&gt;</c> directory by walking up from the test
    /// assembly location, so the tests do not depend on a fixed directory depth. The distribution
    /// layout there (per-package extension subdirectories) is required for the CLI subprocess to
    /// resolve extensions such as Login.
    /// </summary>
    private static string ResolveArtifactsDirectory()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "artifacts", BuildConfiguration);
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException(
            $"Could not locate 'artifacts/{BuildConfiguration}' above {AppContext.BaseDirectory}. " +
            $"Run `dotnet build azclips.slnx` to produce it.");
    }

    private ProfileService CreateProfileService()
    {
        // same temp store the subprocess writes to
        return new ProfileService(Logger, _profileDirectory);
    }

    private void ResetProfiles()
    {
        // clean profile state between tests
        // Do not need to clean AT since MI do not store it in disk
        foreach (var fileName in new[] { "profiles.json", "active-profile" })
        {
            var path = Path.Combine(_profileDirectory, fileName);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

}
