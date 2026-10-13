using System.Net;

namespace Microsoft.Azure.Azclips.ExtensionRuntime;

public static class SensitiveHttpDiagnostics
{
    private static readonly HttpRequestOptionsKey<bool> SuppressDetails = new("Azclips.SuppressSensitiveHttpDetails");

    public static void MarkSensitive(this HttpRequestMessage request) => request.Options.Set(SuppressDetails, true);

    public static bool HasSensitiveDiagnostics(this HttpRequestMessage? request) =>
        request is not null && request.Options.TryGetValue(SuppressDetails, out bool sensitive) && sensitive;

    public static string FailureMessage(HttpStatusCode statusCode) => statusCode switch
    {
        HttpStatusCode.Unauthorized => "Authentication failed. Check the profile, tenant and token audience.",
        HttpStatusCode.Forbidden => "Access denied. Check the vault's data-plane permissions and network access rules. Ask an administrator to review role assignments, such as Key Vault Secrets User for reading or Key Vault Secrets Officer for lifecycle operations. An Azure management role alone does not grant secret access.",
        _ => $"Request failed with HTTP {(int)statusCode}. Response details are omitted because the request contains sensitive data."
    };
}