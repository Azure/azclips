using System.Text.Json;
using System.Text.Json.Nodes;
using Tests.Recording;

namespace Scenario.Tests;

/// <summary>
/// Reads the network resources an orchestration created: what the run sent in each PUT, and what
/// ARM reported in the recorded GET that ends each long-running create. Bodies deserialize into
/// the Compute models the orchestration builds its requests from.
/// </summary>
internal static class RecordedArm
{
    private static readonly JsonSerializerOptions s_json = new() { PropertyNameCaseInsensitive = true };

    /// <summary>The body of the one PUT this run sent to the resource.</summary>
    public static T SentPut<T>(IReadOnlyList<CapturedHttpRequest> sent, string type, string name)
        => JsonSerializer.Deserialize<T>(sent.Single(r => r.Method == "PUT" && Targets(r.Uri, type, name)).Body!, s_json)!;

    /// <summary>
    /// The resource as ARM reported it once created: the first GET of it after its PUT, which must
    /// say Succeeded. The PUT's own response still says Updating. The models have no
    /// provisioningState, so that one field is read from the raw JSON.
    /// </summary>
    public static T FinalGet<T>(IReadOnlyList<SessionRecordEntry> entries, string type, string name)
    {
        var get = entries
            .SkipWhile(e => !(e.Request.Method == "PUT" && Targets(e.Request.Uri, type, name)))
            .Skip(1)
            .First(e => e.Request.Method == "GET"
                && Targets(e.Request.Uri, type, name)
                && e.Response.StatusCode == 200
                && JsonNode.Parse(e.Response.Body!)?["properties"]?["provisioningState"]?.GetValue<string>() == "Succeeded");
        Assert.Equal(200, get.Response.StatusCode);
        return JsonSerializer.Deserialize<T>(get.Response.Body!, s_json)!;
    }

    private static bool Targets(string uri, string type, string name)
        => new Uri(uri).AbsolutePath.EndsWith($"/Microsoft.Network/{type}/{name}", StringComparison.OrdinalIgnoreCase);
}
