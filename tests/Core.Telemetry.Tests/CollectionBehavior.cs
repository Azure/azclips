// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Xunit;

// Storage-path tests change AZCLIPS_HOME, and cache-failure tests capture Console.Error.
// Serialize this assembly so other tests cannot observe these process-wide overrides.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
