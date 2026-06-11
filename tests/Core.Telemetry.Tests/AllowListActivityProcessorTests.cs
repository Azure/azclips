using System.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Trace;

namespace Core.Telemetry.Tests;

public class AllowListActivityProcessorTests
{
    private const string AllowedName = "Allowed.Activity.Root";
    private const string BlockedName = "Blocked.Activity.Child";

    [Fact]
    public void OnEnd_ShouldKeepRecordedFlag_WhenOperationNameIsAllowed()
    {
        using var harness = new FilterHarness(new[] { AllowedName });

        using (harness.Source.StartActivity(AllowedName, ActivityKind.Server))
        {
            // dispose triggers OnEnd
        }

        var exported = Assert.Single(harness.Exported);
        Assert.Equal(AllowedName, exported.OperationName);
        Assert.True(exported.Recorded, "Allowed activities must retain the Recorded flag.");
    }

    [Fact]
    public void OnEnd_ShouldClearRecordedFlag_WhenOperationNameIsNotAllowed()
    {
        using var harness = new FilterHarness(new[] { AllowedName });

        Activity? captured;
        using (captured = harness.Source.StartActivity(BlockedName, ActivityKind.Internal))
        {
            // dispose triggers OnEnd
        }

        Assert.NotNull(captured);
        Assert.False(captured!.Recorded, "Blocked activities must have the Recorded flag cleared.");
        Assert.Empty(harness.Exported);
    }

    [Fact]
    public void OnEnd_ShouldOnlyExportAllowedActivities_WhenMixed()
    {
        using var harness = new FilterHarness([AllowedName]);

        using (harness.Source.StartActivity(AllowedName, ActivityKind.Server)) { }
        using (harness.Source.StartActivity(BlockedName, ActivityKind.Internal)) { }
        using (harness.Source.StartActivity(AllowedName, ActivityKind.Server)) { }

        Assert.Equal(2, harness.Exported.Count);
        Assert.All(harness.Exported, a => Assert.Equal(AllowedName, a.OperationName));
    }

    [Fact]
    public void OnEnd_ShouldNotExportChild_WhenChildOperationIsNotAllowed_EvenIfParentAllowed()
    {
        using var harness = new FilterHarness([AllowedName]);

        using (harness.Source.StartActivity(AllowedName, ActivityKind.Server))
        using (harness.Source.StartActivity(BlockedName, ActivityKind.Internal))
        {
            // both end when leaving the using blocks
        }

        // Parent is allowed; child name is not. Filter is per-activity, not per-trace.
        var exported = Assert.Single(harness.Exported);
        Assert.Equal(AllowedName, exported.OperationName);
    }

    [Fact]
    public void OnEnd_ShouldUseOrdinalComparison_ForOperationNameMatch()
    {
        using var harness = new FilterHarness([AllowedName]);
        using (harness.Source.StartActivity(AllowedName.ToLowerInvariant(), ActivityKind.Server)) { }
        Assert.Empty(harness.Exported);
    }

    [Fact]
    public void OnEnd_ShouldPreserveOtherTraceFlags_WhenClearingRecordedBit()
    {
        var parentContext = new ActivityContext(
            ActivityTraceId.CreateRandom(),
            ActivitySpanId.CreateRandom(),
            ActivityTraceFlags.Recorded | (ActivityTraceFlags)0x02, // 0x02 == Random (W3C)
            isRemote: true);

        using var harness = new FilterHarness([AllowedName]);

        Activity? captured;
        using (captured = harness.Source.StartActivity(BlockedName, ActivityKind.Internal, parentContext))
        {
        }

        Assert.NotNull(captured);
        Assert.False(captured!.Recorded, "Recorded must be cleared on blocked activities.");
        // The non-Recorded bits we provided in the parent context must still be set.
        Assert.True((captured.ActivityTraceFlags & (ActivityTraceFlags)0x02) == (ActivityTraceFlags)0x02,
            "Non-Recorded trace flags (e.g. Random) must be preserved.");
    }

    [Fact]
    public void AddTelemetryAllowListFilter_ShouldFilter_AgainstTelemetryAllowListNames()
    {
        const string allowed = "CLI.Activity.Root";
        Assert.Contains(allowed, TelemetryAllowList.Names);

        var exported = new List<Activity>();
        var sourceName = "cirrus-allowlist-ext-" + Guid.NewGuid().ToString("N");
        using var source = new ActivitySource(sourceName);
        using var provider = Sdk.CreateTracerProviderBuilder()
            .AddSource(sourceName)
            .AddTelemetryAllowListFilter()
            .AddProcessor(new SimpleActivityExportProcessor(new CapturingExporter(exported)))
            .Build();

        using (source.StartActivity(allowed, ActivityKind.Server)) { }
        using (source.StartActivity("Definitely.Not.Allowed", ActivityKind.Internal)) { }

        var single = Assert.Single(exported);
        Assert.Equal(allowed, single.OperationName);
    }

    [Fact]
    public void TelemetryAllowList_ShouldContainExpectedRootActivityNames()
    {
        // Guards the privacy-review allowlist invariants: the per-client root
        // intentional — this test just protects the foundational ones.
        Assert.Contains("CLI.Activity.Root", TelemetryAllowList.Names);
        Assert.Contains("PowerShell.Activity.Root", TelemetryAllowList.Names);        
    }
        
    private sealed class FilterHarness : IDisposable
    {
        private readonly TracerProvider _provider;

        public FilterHarness(IEnumerable<string> allowed)
        {
            var name = "cirrus-allowlist-" + Guid.NewGuid().ToString("N");
            Source = new ActivitySource(name);
            Exported = new List<Activity>();

            _provider = Sdk.CreateTracerProviderBuilder()
                .AddSource(name)
                .AddProcessor(new AllowListActivityProcessor(allowed))
                .AddProcessor(new SimpleActivityExportProcessor(new CapturingExporter(Exported)))
                .Build()!;
        }

        public ActivitySource Source { get; }

        public List<Activity> Exported { get; }

        public void Dispose()
        {
            _provider.Dispose();
            Source.Dispose();
        }
    }
     
    private sealed class CapturingExporter : BaseExporter<Activity>
    {
        private readonly List<Activity> _sink;

        public CapturingExporter(List<Activity> sink) => _sink = sink;

        public override ExportResult Export(in Batch<Activity> batch)
        {
            foreach (var activity in batch)
            {
                _sink.Add(activity);
            }
            return ExportResult.Success;
        }
    }
}
