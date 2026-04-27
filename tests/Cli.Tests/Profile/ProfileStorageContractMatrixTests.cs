using Xunit;

namespace Cli.Tests.Profile;

public class ProfileStorageContractMatrixTests
{
    public static TheoryData<string> StorageMatrix => new()
    {
        "Profile schema contains required fields",
        "Profile names are normalized case-insensitively",
        "Active pointer references normalized key",
        "Invalid profile JSON is skipped during list",
        "Atomic writes preserve valid profile files",
        "No token material is stored in profile metadata"
    };

    [Theory(Skip = "Matrix scaffold only. Implement storage assertions once profile store wiring lands.")]
    [MemberData(nameof(StorageMatrix))]
    public void StorageContract_ShouldMatchSpec(string scenario)
    {
        Assert.False(string.IsNullOrWhiteSpace(scenario));
    }
}