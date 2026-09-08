using Xunit;

namespace Scenario.Tests;

// Scenario hosts boot and dispose the process-wide AzclipsServiceProvider, so scenario tests
// must never run concurrently with each other.
[CollectionDefinition("Scenario Tests", DisableParallelization = true)]
public sealed class ScenarioTestCollectionDefinition
{
}
