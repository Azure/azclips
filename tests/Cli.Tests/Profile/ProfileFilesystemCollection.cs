using Xunit;

namespace Cli.Tests.Profile;

[CollectionDefinition("ProfileFilesystem", DisableParallelization = true)]
public sealed class ProfileFilesystemCollection
{
    // Marker: forces Profile* tests that touch the real ~/.cirrus/ directory to run sequentially.
}
