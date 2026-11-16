// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.Azclips.ExtensionRuntime;

/// <summary>
/// Marks best-effort requests a command sends alongside its own requests, such as deployment progress checks. How many are
/// sent depends on timing, so test recordings leave them out.
/// </summary>
public static class BackgroundHttpRequests
{
    private static readonly HttpRequestOptionsKey<bool> Background = new("Azclips.BackgroundRequest");

    public static void MarkBackground(this HttpRequestMessage request) => request.Options.Set(Background, true);

    public static bool IsBackground(this HttpRequestMessage? request) =>
        request is not null && request.Options.TryGetValue(Background, out bool background) && background;
}
