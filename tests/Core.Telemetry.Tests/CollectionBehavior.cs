// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Xunit;

// DataBoundaryResolverTests' TestHome mutates the process-wide AZCLIPS_HOME environment
// variable (HomePaths.HomeOverrideVariable) for the duration of each test. That variable is
// read by HomePaths.GetDefaultLocalAppDataRoot(), which many other tests in this assembly
// depend on directly or indirectly (TraceDirectoryProviderTests, DataBoundaryCache-backed
// tests, etc.). [CollectionDefinition(..., DisableParallelization = true)] on
// DataBoundaryResolverTests only serializes tests WITHIN that named collection — by default
// xUnit still runs DIFFERENT collections in this same assembly/process concurrently, so an
// unrelated test could observe the temporary override and fail intermittently depending on
// scheduling. Disabling parallelization for the whole assembly closes that gap without
// requiring every test that happens to touch HomePaths to remember to opt into a shared
// collection.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
