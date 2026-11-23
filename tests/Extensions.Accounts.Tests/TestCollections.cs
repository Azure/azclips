// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Extensions.Accounts.Tests;

// Tests that initialize the process-wide AzclipsServiceProvider must not run in parallel.
[CollectionDefinition("Azclips Service Provider Tests", DisableParallelization = true)]
public sealed class AzclipsServiceProviderCollectionDefinition
{
}