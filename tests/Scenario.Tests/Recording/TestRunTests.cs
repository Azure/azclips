using Tests.Recording;

namespace Scenario.Tests;

public class TestRunTests
{
    [Fact]
    public void RunId_IsStable_AndEightHex()
    {
        Assert.Matches("^[0-9a-f]{8}$", TestRun.RunId);
        Assert.Equal(TestRun.RunId, TestRun.RunId);
    }

    [Fact]
    public void ScopedName_SuffixesOnlyInRecordMode()
    {
        Assert.Equal($"scenario-test-rg-t{TestRun.RunId}", TestRun.ScopedName("scenario-test-rg", RecorderMode.Record));
        Assert.Equal("scenario-test-rg", TestRun.ScopedName("scenario-test-rg", RecorderMode.Playback));
    }

    [Fact]
    public void Sanitisers_StripTheScopedSuffix_Everywhere()
    {
        var scoped = TestRun.ScopedName("scenario-test-rg", RecorderMode.Record);
        var sanitized = RecordingSanitizers.Default.Apply(
            $"/subscriptions/x/resourceGroups/{scoped}?api-version=1 {{\"name\":\"{scoped}\"}}");

        Assert.DoesNotContain(scoped, sanitized);
        Assert.Contains("resourceGroups/scenario-test-rg?", sanitized);
        Assert.Contains("\"name\":\"scenario-test-rg\"", sanitized);
    }

    [Fact]
    public void Sanitisers_LeaveLookalikes_Alone()
    {
        // No t marker, a nine character hex run, and a name outside the scenario prefix.
        foreach (var value in new[]
        {
            "scenario-test-rg-3f8a21c9",
            "scenario-test-rg-t3f8a21c9a",
            "other-rg-t3f8a21c9",
        })
        {
            Assert.Equal(value, RecordingSanitizers.Default.Apply(value));
        }
    }
}
