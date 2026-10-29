using System.Net;
using System.Xml.Linq;
using Core.Abstractions;
using Core.DependencyInjection;
using Tests.Recording;

namespace Scenario.Tests;

/// <summary>
/// Record-mode setup that azclips has no command for: creating a container and taking a blob
/// snapshot. The requests use the signed-in profile's token and bypass the interceptor, so they
/// never reach a recording; playback reads the results back from the recorded command responses.
/// </summary>
internal static class StorageBlobLiveSetup
{
    private const string Version = "2023-11-03";

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

    /// <summary>Deletes every blob and snapshot left in the container, so a re-record starts empty.</summary>
    public static void EmptyContainer(string account, string container)
    {
        using var list = Send(HttpMethod.Get, $"https://{account}.blob.core.windows.net/{container}?restype=container&comp=list");
        list.EnsureSuccessStatusCode();
        var names = XDocument.Parse(list.Content.ReadAsStringAsync().GetAwaiter().GetResult())
            .Descendants("Blob").Select(blob => blob.Element("Name")!.Value).Distinct().ToList();
        foreach (var name in names)
        {
            string path = string.Join("/", name.Split('/').Select(Uri.EscapeDataString));
            using var delete = Send(HttpMethod.Delete, $"https://{account}.blob.core.windows.net/{container}/{path}", ("x-ms-delete-snapshots", "include"));
            if (delete.StatusCode != HttpStatusCode.Accepted && delete.StatusCode != HttpStatusCode.NotFound)
                throw new InvalidOperationException($"Cleanup of '{name}' failed with HTTP {(int)delete.StatusCode}.");
        }
    }

    private static HttpResponseMessage Send(HttpMethod method, string uri, params (string Name, string Value)[] headers)
    {
        var profile = AzclipsServiceProvider.GetRequiredService<IProfileService>().GetActiveProfileAsync().GetAwaiter().GetResult()
            ?? throw new InvalidOperationException("No active profile; sign in with the azclips CLI before recording.");
        var token = AzclipsServiceProvider.GetRequiredService<IAuthService>()
            .AcquireTokenSilentlyAsync(["https://storage.azure.com/.default"], profile.TenantId, profile: profile).GetAwaiter().GetResult();
        using var client = new HttpClient();
        using var request = new HttpRequestMessage(method, uri);
        request.Headers.Add("Authorization", "Bearer " + token.AccessToken);
        request.Headers.Add("x-ms-version", Version);
        foreach (var (name, value) in headers) request.Headers.Add(name, value);
        if (method == HttpMethod.Put) request.Content = new ByteArrayContent([]);
        return client.Send(request);
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
