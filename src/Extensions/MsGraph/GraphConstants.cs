// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.Azclips.Extensions.MsGraph;

/// <summary>Microsoft Graph auth-scope constants.</summary>
internal static class GraphConstants
{
    /// <summary>OAuth scope requested when acquiring a Microsoft Graph token.</summary>
    public static readonly string[] GraphScopes = ["https://graph.microsoft.com/.default"];
}
