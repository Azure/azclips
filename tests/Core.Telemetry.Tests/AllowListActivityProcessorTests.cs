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

    // --- Tag filtering ---------------------------------------------------

    [Fact]
    public void OnEnd_ShouldStripDisallowedTags_OnAllowedActivity()
    {
        using var harness = new FilterHarness(
            allowed: new[] { AllowedName },
            allowedTagKeys: new[] { "keep" });

        using (var a = harness.Source.StartActivity(AllowedName, ActivityKind.Server))
        {
            a!.SetTag("keep", "ok");
            a!.SetTag("drop", "secret");
        }

        var exported = Assert.Single(harness.Exported);
        Assert.Equal("ok", exported.GetTagItem("keep"));
        Assert.Null(exported.GetTagItem("drop"));
    }

    [Fact]
    public void OnEnd_ShouldNotStripTags_OnBlockedActivity()
    {
        // Blocked activities are dropped entirely by the exporter, so the
        // processor must NOT spend time mutating their tags. This guards
        // against accidental work on the hot path for non-exported spans.
        using var harness = new FilterHarness(
            allowed: new[] { AllowedName },
            allowedTagKeys: new[] { "keep" });

        Activity? captured;
        using (captured = harness.Source.StartActivity(BlockedName, ActivityKind.Internal))
        {
            captured!.SetTag("keep", "ok");
            captured!.SetTag("drop", "secret");
        }

        Assert.NotNull(captured);
        Assert.False(captured!.Recorded);
        // Tags on the in-memory Activity are untouched; only Recorded was cleared.
        Assert.Equal("ok", captured.GetTagItem("keep"));
        Assert.Equal("secret", captured.GetTagItem("drop"));
        Assert.Empty(harness.Exported);
    }

    [Fact]
    public void OnEnd_ShouldUseOrdinalComparison_ForTagKeyMatch()
    {
        using var harness = new FilterHarness(
            allowed: new[] { AllowedName },
            allowedTagKeys: new[] { "keep" });

        using (var a = harness.Source.StartActivity(AllowedName, ActivityKind.Server))
        {
            a!.SetTag("KEEP", "case-mismatch");
        }

        var exported = Assert.Single(harness.Exported);
        Assert.Null(exported.GetTagItem("KEEP"));
    }

    [Fact]
    public void OnEnd_ShouldNotFilterTags_WhenTagAllowlistIsNull()
    {
        // Legacy single-arg ctor must continue to behave as activity-only filter.
        using var harness = new FilterHarness(
            allowed: new[] { AllowedName },
            allowedTagKeys: null);

        using (var a = harness.Source.StartActivity(AllowedName, ActivityKind.Server))
        {
            a!.SetTag("any", "value");
            a!.SetTag("Id", "should-survive-when-no-tag-filter");
        }

        var exported = Assert.Single(harness.Exported);
        Assert.Equal("value", exported.GetTagItem("any"));
        Assert.Equal("should-survive-when-no-tag-filter", exported.GetTagItem("Id"));
    }

    [Fact]
    public void TelemetryAllowList_ShouldExcludeIdTag_ForPrivacy()
    {
        // The "Id" tag is a SHA-256 of (primary MAC + machine name) which is
        // pseudonymized personal data under GDPR / Microsoft data classification.
        // It must never be re-added to the default tag allowlist without an
        // explicit privacy review.
        Assert.DoesNotContain("Id", TelemetryAllowList.TagKeys);
    }

    [Fact]
    public void TelemetryAllowList_ShouldAllowCommandTelemetryTags()
    {
        // Sanity check: the per-command tags we actually need for product
        // insight must be present in the default tag allowlist.
        Assert.Contains("command", TelemetryAllowList.TagKeys);
        Assert.Contains("parameter", TelemetryAllowList.TagKeys);
        Assert.Contains("clientType", TelemetryAllowList.TagKeys);
        Assert.Contains("productVersion", TelemetryAllowList.TagKeys);
    }

    [Fact]
    public void AddTelemetryAllowListFilter_ShouldStripIdTag_FromAllowedActivity()
    {
        // End-to-end check using the production AddTelemetryAllowListFilter()
        // extension: the "Id" tag set on an allowed activity must be stripped
        // before reaching the exporter.
        const string allowed = "CLI.Activity.Root";

        var exported = new List<Activity>();
        var sourceName = "cirrus-allowlist-tag-strip-" + Guid.NewGuid().ToString("N");
        using var source = new ActivitySource(sourceName);
        using var provider = Sdk.CreateTracerProviderBuilder()
            .AddSource(sourceName)
            .AddTelemetryAllowListFilter()
            .AddProcessor(new SimpleActivityExportProcessor(new CapturingExporter(exported)))
            .Build();

        using (var a = source.StartActivity(allowed, ActivityKind.Server))
        {
            a!.SetTag("Id", "machine-fingerprint");
            a!.SetTag("command", "login");
        }

        var single = Assert.Single(exported);
        Assert.Null(single.GetTagItem("Id"));
        Assert.Equal("login", single.GetTagItem("command"));
    }

    // ---------------------------------------------------------------------

    private sealed class FilterHarness : IDisposable
    {
        private readonly TracerProvider _provider;

        public FilterHarness(IEnumerable<string> allowed)
            : this(allowed, allowedTagKeys: null)
        {
        }

        public FilterHarness(IEnumerable<string> allowed, IEnumerable<string>? allowedTagKeys)
        {
            var name = "cirrus-allowlist-" + Guid.NewGuid().ToString("N");
            Source = new ActivitySource(name);
            Exported = new List<Activity>();

            _provider = Sdk.CreateTracerProviderBuilder()
                .AddSource(name)
                .AddProcessor(new AllowListActivityProcessor(allowed, allowedTagKeys))
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
