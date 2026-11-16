// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.Extensions.ManagedServiceIdentity.Processors;

namespace Microsoft.Azure.Azclips.Extensions.ManagedServiceIdentity.Customization;

public class CreateFederatedCredentialProcessor_Orchestrator : CreateFederatedCredentialProcessor
{
    protected override Task BeforeProcessAsync()
    {
        FederatedCredentialValidator.ValidateClaimsMatchingExpression(ClaimsMatchingExpressionVersion, ClaimsMatchingExpressionValue);
        return base.BeforeProcessAsync();
    }
}
