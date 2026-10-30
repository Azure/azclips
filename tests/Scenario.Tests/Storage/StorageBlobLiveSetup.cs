using System.Net;
using Core.Abstractions;
using Core.DependencyInjection;
using Tests.Recording;

namespace Scenario.Tests;

/// <summary>
/// Record-mode setup that azclips has no command for: creating a file share, creating a container
/// and taking a blob snapshot. The requests use the signed-in profile's token and bypass the interceptor, so they
/// never reach a recording; playback reads the results back from the recorded command responses.
/// </summary>
internal static class StorageBlobLiveSetup
{
    private const string Version = "2023-11-03";
    private const string StorageScope = "https://storage.azure.com/.default";
    private const string ManagementScope = "https://management.azure.com/.default";

    /// <summary>Creates a file share through ARM.</summary>
    public static void CreateShare(string subscriptionId, string resourceGroup, string account, string share)
    {
        string uri = $"https://management.azure.com/subscriptions/{subscriptionId}/resourceGroups/{resourceGroup}" +
            $"/providers/Microsoft.Storage/storageAccounts/{account}/fileServices/default/shares/{share}?api-version=2025-08-01";
        using var client = new HttpClient();
        using var request = new HttpRequestMessage(HttpMethod.Put, uri);
        request.Headers.Add("Authorization", "Bearer " + AccessToken(ManagementScope));
        request.Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");
        using var response = client.Send(request);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Share setup failed with HTTP {(int)response.StatusCode}.");
    }

    public static void EnsureContainer(string account, string container)
    {
        using var response = Send(HttpMethod.Put, $"https://{account}.blob.core.windows.net/{container}?restype=container");
        if (response.StatusCode != HttpStatusCode.Created && response.StatusCode != HttpStatusCode.Conflict)
            throw new InvalidOperationException($"Container setup failed with HTTP {(int)response.StatusCode}.");
    }

    public static void CreateSnapshot(string account, string container, string blob)
    {
        using var response = Send(HttpMethod.Put, $"https://{account}.blob.core.windows.net/{container}/{blob}?comp=snapshot");
        if (response.StatusCode != HttpStatusCode.Created)
            throw new InvalidOperationException($"Snapshot setup failed with HTTP {(int)response.StatusCode}.");
    }

    private static HttpResponseMessage Send(HttpMethod method, string uri)
    {
        using var client = new HttpClient();
        using var request = new HttpRequestMessage(method, uri);
        request.Headers.Add("Authorization", "Bearer " + AccessToken(StorageScope));
        request.Headers.Add("x-ms-version", Version);
        if (method == HttpMethod.Put) request.Content = new ByteArrayContent([]);
        return client.Send(request);
    }

    private static string AccessToken(string scope)
    {
        var profile = AzclipsServiceProvider.GetRequiredService<IProfileService>().GetActiveProfileAsync().GetAwaiter().GetResult()
            ?? throw new InvalidOperationException("No active profile; sign in with the azclips CLI before recording.");
        return AzclipsServiceProvider.GetRequiredService<IAuthService>()
            .AcquireTokenSilentlyAsync([scope], profile.TenantId, profile: profile).GetAwaiter().GetResult().AccessToken;
    }
}

/// <summary>
/// Matches on method and URI with the client-generated block id removed: multi-block uploads stage
/// each block under a fresh random id, so the recorded and replayed ids never agree.
/// </summary>
internal sealed class BlockIdRequestMatcher : IRequestMatcher
{
    private static readonly System.Text.RegularExpressions.Regex s_blockId = new("(?<=[?&]blockid=)[^&]*");

    public string Description => "method + URI without blockid";

    public bool Matches(RecordedRequest recorded, CapturedHttpRequest outgoing)
        => string.Equals(recorded.Method, outgoing.Method, StringComparison.OrdinalIgnoreCase)
        && string.Equals(s_blockId.Replace(recorded.Uri, ""), s_blockId.Replace(outgoing.Uri, ""), StringComparison.OrdinalIgnoreCase);
}
