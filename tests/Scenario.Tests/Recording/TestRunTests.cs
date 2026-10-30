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
    public void Sanitisers_StripTheRunId_FromTheStorageAccountName()
    {
        var account = $"azclipsdpt{TestRun.RunId}";
        var sanitized = RecordingSanitizers.Default.Apply(
            $"https://{account}.blob.core.windows.net/c?comp=list /storageAccounts/{account}/listKeys {{\"name\":\"{account}\"}}");

        Assert.DoesNotContain(account, sanitized);
        Assert.Contains("https://azclipsdp.blob.core.windows.net/c?", sanitized);
        Assert.Contains("/storageAccounts/azclipsdp/listKeys", sanitized);
        Assert.Contains("\"name\":\"azclipsdp\"", sanitized);
    }

    [Fact]
    public void Sanitisers_LeaveAccountLookalikes_Alone()
    {
        // Another account prefix, a nine character hex run, no t marker, and bare hex such as an etag or request id.
        foreach (var value in new[]
        {
            "otherdpt3f8a21c9",
            "azclipsdpt3f8a21c9a",
            "azclipsdp3f8a21c9",
            "\"0x8DE3F8A21C9\" t3f8a21c9 3f8a21c9-0000-0000-0000-000000000000",
        })
        {
            Assert.Equal(value, RecordingSanitizers.Default.Apply(value));
        }
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
