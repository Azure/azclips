using System.Text;
using System.Text.RegularExpressions;

namespace Core.Agent.Tests.Snapshot;

/// <summary>
/// Captures the EventRenderer's OnText + OnEvent stream as a normalized
/// transcript suitable for snapshot comparison. Normalization removes
/// fields that drift between runs but aren't structurally meaningful:
/// elapsed milliseconds, TTFT seconds, token counts, and UUIDs. The shape
/// of the event sequence + the human-readable identifiers (tool name,
/// success vs. error) remain — those are what tests care about.
/// </summary>
internal sealed class TranscriptCollector
{
    private readonly StringBuilder _sb = new();
    private readonly StringBuilder _textBuf = new();

    public Action<AgentEvent> OnEvent => Capture;
    public Action<string> OnText => CaptureText;

    public string ToTranscript()
    {
        FlushText();
        return Normalize(_sb.ToString());
    }

    private void CaptureText(string chunk)
    {
        // Coalesce streamed chunks so a multi-delta turn shows as ONE Text line
        // (otherwise the snapshot would be dominated by chunk boundaries that
        // depend on how the SDK happened to slice the response).
        _textBuf.Append(chunk);
    }

    private void Capture(AgentEvent ev)
    {
        FlushText();
        _sb.AppendLine(Format(ev));
    }

    private void FlushText()
    {
        if (_textBuf.Length == 0) return;
        var text = _textBuf.ToString().TrimEnd('\r', '\n');
        if (text.Length > 0)
        {
            _sb.AppendLine($"Text len={text.Length}");
        }
        _textBuf.Clear();
    }

    private static string Format(AgentEvent ev) => ev switch
    {
        ToolStarted t      => $"ToolStarted name={t.Name} args={t.ArgsJson} callId={t.CallId}",
        ToolCompleted t    => $"ToolCompleted name={t.Name} isError={t.IsError} result={Compact(t.FullResult)} callId={t.CallId}",
        ToolFailed t       => $"ToolFailed name={t.Name} error={Compact(t.Error)} callId={t.CallId}",
        LlmFirstToken _    => "LlmFirstToken ttft=<NORMALIZED>",
        LlmTurnCompleted _ => "LlmTurnCompleted in=<NORMALIZED> out=<NORMALIZED> cached=<NORMALIZED> elapsed=<NORMALIZED>",
        _ => ev.GetType().Name
    };

    private static string Compact(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        // One-line, max 80 chars — keeps assertions tight without losing
        // identity information.
        var oneLine = s.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return oneLine.Length > 80 ? oneLine[..80] + "…" : oneLine;
    }

    private static readonly Regex UuidRe = new(
        @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}",
        RegexOptions.Compiled);

    private static string Normalize(string transcript)
    {
        // CallIds in our tests are deterministic (we set them ourselves), but
        // belt-and-suspenders: scrub any UUID that slips in.
        return UuidRe.Replace(transcript, "<UUID>");
    }
}
