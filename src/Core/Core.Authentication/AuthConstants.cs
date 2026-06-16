// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Authentication;

/// <summary>
/// Shared constants for Azure authentication.
/// </summary>
internal static class AuthConstants
{
    /// <summary>
    /// Default Azure Resource Manager (ARM) scope requested when acquiring a token.
    /// </summary>
    internal const string ArmDefaultScope = "https://management.core.windows.net//.default";
}
