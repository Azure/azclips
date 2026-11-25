// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Cmdlets.Config.Tests;

// The cmdlets resolve the engine from the static AzclipsServiceProvider, which these tests replace.
[CollectionDefinition("Azclips Service Provider Tests", DisableParallelization = true)]
public sealed class AzclipsServiceProviderCollection;
