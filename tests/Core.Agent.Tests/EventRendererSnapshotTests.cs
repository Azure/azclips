using Core.Agent.Tests.Snapshot;
using GitHub.Copilot.SDK;
using Xunit;

namespace Core.Agent.Tests;

/// <summary>
/// Snapshot tests for EventRenderer's SessionEvent → AgentEvent translation.
///
/// We don't stub the LLM (the Copilot SDK runs the agentic loop inside the
/// daemon — there's no IChatClient seam to inject into). Instead we feed
/// hand-crafted SessionEvent sequences directly to EventRenderer.Forward
/// and assert on the transcript it produces. The unit under test is the
/// ~130-line translation layer in src/Core/Core.Agent/EventRenderer.cs;
/// the SDK daemon is treated as a black box per plan-v3 §3.
///
/// Borrows the snapshot infrastructure pattern from main-repo spike
/// 9159959c (SnapshotAssert / TranscriptCollector / .snapshots/) but
/// substitutes SessionEvent-stub harness in place of that commit's
/// MAF-specific IChatClient stub.
/// </summary>
public class EventRendererSnapshotTests
{
    private static (EventRenderer renderer, TranscriptCollector capture) MakeRig()
    {
        var capture = new TranscriptCollector();
        var renderer = new EventRenderer
        {
            OnEvent = capture.OnEvent,
            OnText  = capture.OnText,
        };
        return (renderer, capture);
    }

    [Fact]
    public void HappyPath_StreamingToolUsage_ProducesExpectedSequence()
    {
        var (renderer, capture) = MakeRig();

        // Simulate the canonical happy turn: TurnStart → first delta → more
        // deltas → ToolStart → ToolComplete → Usage.
        renderer.Forward(new AssistantTurnStartEvent { Data = new AssistantTurnStartData { TurnId = "t1" } });
        renderer.Forward(new AssistantMessageDeltaEvent { Data = new AssistantMessageDeltaData { DeltaContent = "Hello ", MessageId = "m1" } });
        renderer.Forward(new AssistantMessageDeltaEvent { Data = new AssistantMessageDeltaData { DeltaContent = "world.", MessageId = "m1" } });
        renderer.Forward(new ToolExecutionStartEvent
        {
            Data = new ToolExecutionStartData
            {
                ToolName = "run_command",
                ToolCallId = "call1",
                Arguments = "{\"command\":\"azv2 group list\"}",
            }
        });
        renderer.Forward(new ToolExecutionCompleteEvent
        {
            Data = new ToolExecutionCompleteData
            {
                ToolCallId = "call1",
                Success = true,
                Result = new ToolExecutionCompleteResult { Content = "SUCCESS · exit 0\noutput: rg1, rg2" },
            }
        });
        renderer.Forward(new AssistantUsageEvent
        {
            Data = new AssistantUsageData
            {
                Model = "test-model",
                InputTokens = 1500,
                OutputTokens = 42,
                CacheReadTokens = 1200,
            }
        });

        SnapshotAssert.Matches("event-renderer-happy-path", capture.ToTranscript());
    }

    [Fact]
    public void DedupOnAggregate_StreamingFollowedByAssistantMessage_OnlyEmitsStreamingText()
    {
        var (renderer, capture) = MakeRig();

        // Provider streams deltas, then sends the aggregate AssistantMessage.
        // EventRenderer should NOT double-render the aggregate (it would be
        // identical text appearing twice in the UI).
        renderer.Forward(new AssistantTurnStartEvent { Data = new AssistantTurnStartData { TurnId = "t1" } });
        renderer.Forward(new AssistantMessageDeltaEvent { Data = new AssistantMessageDeltaData { DeltaContent = "Streamed text.", MessageId = "m1" } });
        renderer.Forward(new AssistantMessageEvent { Data = new AssistantMessageData { Content = "Streamed text.", MessageId = "m1" } });
        renderer.Forward(new AssistantUsageEvent { Data = new AssistantUsageData { Model = "test-model", InputTokens = 100, OutputTokens = 10 } });

        SnapshotAssert.Matches("event-renderer-dedup-when-streamed", capture.ToTranscript());
    }

    [Fact]
    public void NoStreaming_AssistantMessageRendersAggregate()
    {
        var (renderer, capture) = MakeRig();

        // BYOM / non-streaming providers send only AssistantMessage (no deltas).
        // EventRenderer should fall back to rendering the aggregate so the
        // user actually sees text.
        renderer.Forward(new AssistantTurnStartEvent { Data = new AssistantTurnStartData { TurnId = "t1" } });
        renderer.Forward(new AssistantMessageEvent { Data = new AssistantMessageData { Content = "Aggregate-only response.", MessageId = "m1" } });
        renderer.Forward(new AssistantUsageEvent { Data = new AssistantUsageData { Model = "test-model", InputTokens = 100, OutputTokens = 10 } });

        SnapshotAssert.Matches("event-renderer-aggregate-fallback", capture.ToTranscript());
    }

    [Fact]
    public void ToolFailure_EmitsToolFailedWithError()
    {
        var (renderer, capture) = MakeRig();

        renderer.Forward(new AssistantTurnStartEvent { Data = new AssistantTurnStartData { TurnId = "t1" } });
        renderer.Forward(new ToolExecutionStartEvent
        {
            Data = new ToolExecutionStartData
            {
                ToolName = "run_command",
                ToolCallId = "call1",
                Arguments = "{\"command\":\"azv2 group show --name nope\"}",
            }
        });
        renderer.Forward(new ToolExecutionCompleteEvent
        {
            Data = new ToolExecutionCompleteData
            {
                ToolCallId = "call1",
                Success = false,
                Error = new ToolExecutionCompleteError { Message = "ResourceNotFound: 'nope'" },
            }
        });

        SnapshotAssert.Matches("event-renderer-tool-failure", capture.ToTranscript());
    }
}
