// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;

namespace Microsoft.Azure.Azclips.Extensions.ManagedServiceIdentity.Customization;

internal static class FederatedCredentialValidator
{
    public static void ValidateClaimsMatchingExpression(int? version, string? value)
    {
        if ((version is not null) != !string.IsNullOrWhiteSpace(value))
        {
            throw AzclipsErrorException.InvalidInput(
                "Both 'ClaimsMatchingExpressionVersion' and 'ClaimsMatchingExpressionValue' must be specified together.");
        }
    }
}
