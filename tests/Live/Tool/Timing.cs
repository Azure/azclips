using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace Live.Tool;

/// <summary>
/// Step timer shared by every phase of the live test.
/// <para>
/// The whole test runs as a SINGLE GitHub Actions step, so the run UI only ever shows
/// one duration for the lot. These rows attribute that duration to the phase actually
/// spending it. Port of <c>Scripts/lib/timing.sh</c>; the marker format is a contract
/// with the bash half still running on the VM, so it must not drift.
/// </para>
/// </summary>
internal sealed class Timing
{
    private const string MarkerPrefix = "MI_LIVE_TIMING:";

    private readonly List<Row> _rows = [];
    private readonly Stopwatch _wall = Stopwatch.StartNew();
    private string _currentLabel = string.Empty;
    private long _currentStart;

    private readonly record struct Row(string Label, long Ms, bool Detail);

    /// <summary>Format milliseconds the way the bash version did: 91234 -> "1m31.2s".</summary>
    public static string Format(long ms)
    {
        var sign = ms < 0 ? "-" : string.Empty;
        ms = Math.Abs(ms);
        var s = ms / 1000;
        var tenths = ms % 1000 / 100;
        return s >= 60
            ? $"{sign}{s / 60}m{s % 60}.{tenths}s"
            : $"{sign}{s}.{tenths}s";
    }

    public void Start(string label)
    {
        _currentLabel = label;
        _currentStart = _wall.ElapsedMilliseconds;
        Console.WriteLine($"==> {label} ...");
    }

    public void End()
    {
        var ms = _wall.ElapsedMilliseconds - _currentStart;
        Record(_currentLabel, ms);
        Console.WriteLine($"    [{Format(ms)}] {_currentLabel}");
    }

    /// <summary>Time an action, recording the row even when it throws.</summary>
    public T Timed<T>(string label, Func<T> action)
    {
        Start(label);
        try
        {
            return action();
        }
        finally
        {
            End();
        }
    }

    public void Timed(string label, Action action) =>
        Timed<object?>(label, () => { action(); return null; });

    /// <summary>Append a row measured elsewhere (e.g. parsed out of the xUnit XML report).</summary>
    public void Record(string label, long ms, bool detail = false) => _rows.Add(new Row(label, ms, detail));

    /// <summary>Milliseconds of the most recently recorded row.</summary>
    public long LastMs => _rows.Count > 0 ? _rows[^1].Ms : 0;

    internal int RowCount => _rows.Count;

    internal long MsFor(string label) => _rows.First(r => r.Label == label).Ms;

    internal bool IsDetail(string label) => _rows.First(r => r.Label == label).Detail;

    /// <summary>Drop recorded rows so one run can print more than one independent table.</summary>
    public void Reset()
    {
        _rows.Clear();
        _wall.Restart();
    }

    /// <summary>
    /// Rebuild a table from the markers a remote script printed.
    /// <para>
    /// This is how VM-side timings survive the trip home: <c>az vm run-command</c> gives
    /// the orchestrator nothing but the guest's stdout.
    /// </para>
    /// </summary>
    public void RecordFromMarkers(string output)
    {
        foreach (var raw in output.Split('\n'))
        {
            var line = raw.Trim();
            var at = line.IndexOf(MarkerPrefix, StringComparison.Ordinal);
            if (at < 0)
            {
                continue;
            }

            var parts = line[(at + MarkerPrefix.Length)..].Split('|');
            if (parts.Length < 2 || !long.TryParse(parts[1], out var ms))
            {
                continue;
            }

            var detail = parts.Length > 2 && parts[2].Trim() == "1";
            Record(parts[0], ms, detail);
        }
    }

    /// <summary>
    /// Print the table. <paramref name="totalMsOverride"/> replaces the wall clock for
    /// tables rebuilt from markers, where our own clock is meaningless.
    /// </summary>
    public void Summary(string title, long? totalMsOverride = null)
    {
        if (_rows.Count == 0)
        {
            return;
        }

        var total = totalMsOverride ?? _wall.ElapsedMilliseconds;
        var width = Math.Max(22, _rows.Max(r => r.Label.Length + (r.Detail ? 4 : 0)) + 2);
        var accounted = _rows.Where(r => !r.Detail).Sum(r => r.Ms);

        Console.WriteLine();
        Console.WriteLine($"===== {title} =====");
        foreach (var r in _rows)
        {
            if (r.Detail)
            {
                Console.WriteLine($"    {r.Label.PadRight(width - 4)}  {Format(r.Ms),8}");
                continue;
            }

            var pct = total > 0 ? r.Ms * 100 / total : 0;
            Console.WriteLine($"  {r.Label.PadRight(width)}  {Format(r.Ms),8}  {pct,3}%");
        }

        Console.WriteLine($"  {new string('-', width)}  --------");
        Console.WriteLine($"  {"measured".PadRight(width)}  {Format(accounted),8}");
        Console.WriteLine($"  {"other (untimed)".PadRight(width)}  {Format(total - accounted),8}");
        Console.WriteLine($"  {"TOTAL".PadRight(width)}  {Format(total),8}");
        Console.WriteLine();

        WriteStepSummary(title, total, accounted);
    }

    /// <summary>Append a markdown copy to the workflow run's summary page.</summary>
    private void WriteStepSummary(string title, long total, long accounted)
    {
        var path = Environment.GetEnvironmentVariable("GITHUB_STEP_SUMMARY");
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        var md = new StringBuilder();
        md.Append(CultureInfo.InvariantCulture, $"### {title}\n\n");
        md.Append("| Step | Duration | Share |\n|---|--:|--:|\n");
        foreach (var r in _rows)
        {
            if (r.Detail)
            {
                md.Append(CultureInfo.InvariantCulture, $"| &nbsp;&nbsp;&nbsp;&nbsp;{r.Label} | {Format(r.Ms)} | |\n");
                continue;
            }

            var pct = total > 0 ? r.Ms * 100 / total : 0;
            md.Append(CultureInfo.InvariantCulture, $"| **{r.Label}** | {Format(r.Ms)} | {pct}% |\n");
        }

        md.Append(CultureInfo.InvariantCulture, $"| **other (untimed)** | {Format(total - accounted)} | |\n");
        md.Append(CultureInfo.InvariantCulture, $"| **TOTAL** | **{Format(total)}** | |\n\n");

        File.AppendAllText(path, md.ToString());
    }
}
