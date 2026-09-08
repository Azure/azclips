using System.Text.Json;
using Core.Agent.Common;
using Core.Agent.Hosting;

namespace Core.Agent.Plan;

/// <summary>
/// Parses and validates the args the LLM passes to <c>propose_plan</c> and
/// <c>submit_response</c>. All methods are pure — no session state, no I/O —
/// so tests can drive them directly without hosting the SDK.
/// </summary>
internal static class PlanArgsParser
{
    private static readonly string[] ValidSubmitStatuses = ["completed", "rejected", "failed"];

    // Returns null when submit_response args are valid, else an error message.
    // Keeps validation in the hook (not the tool body) so a bad call becomes a
    // deny + additionalContext correction prompt instead of a framework exception.
    // internal for direct unit testing (see SubmitResponseValidationTests).
    internal static string? ValidateSubmitResponse(object? args)
    {
        var status = ArgsNormalizer.TryGetString(args, "status");
        var message = ArgsNormalizer.TryGetString(args, "message");

        if (string.IsNullOrWhiteSpace(status))
            return "missing required field 'status' (expected completed | rejected | failed)";
        if (!ValidSubmitStatuses.Contains(status, StringComparer.OrdinalIgnoreCase))
            return $"unknown status '{status}'; valid: completed | rejected | failed";

        var isReject = string.Equals(status, "rejected", StringComparison.OrdinalIgnoreCase);
        var isFail   = string.Equals(status, "failed",   StringComparison.OrdinalIgnoreCase);
        if ((isReject || isFail) && string.IsNullOrWhiteSpace(message))
            return $"'message' is required when status='{status}'";
        if (isReject && !message!.StartsWith("OUT_OF_SCOPE_", StringComparison.OrdinalIgnoreCase))
            return "rejected 'message' must start with OUT_OF_SCOPE_";

        return null;
    }

    // Returns null when propose_plan args are valid, else an error message. Same
    // hook-not-body validation rationale as submit_response. internal for tests.
    internal static string? ValidateProposePlan(object? args)
    {
        if (!ArgsNormalizer.TryGetArray(args, "issues", out var issues) || issues.GetArrayLength() == 0)
            return "issues must have at least one point";
        foreach (var issue in issues.EnumerateArray())
        {
            if (issue.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(issue.GetString()))
                return "each issue must be a non-empty string";
        }

        if (!ArgsNormalizer.TryGetArray(args, "steps", out var steps) || steps.GetArrayLength() == 0)
            return "steps must have at least one entry";

        int i = 0;
        foreach (var step in steps.EnumerateArray())
        {
            if (step.ValueKind != JsonValueKind.Object)
                return $"step {i} is not an object";
            if (!HasStringProp(step, "label"))
                return $"step {i} missing required field 'label'";
            if (!HasStringProp(step, "cmd"))
                return $"step {i} missing required field 'cmd'";
            i++;
        }

        return null;
    }

    private static bool HasStringProp(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out var v) &&
        v.ValueKind == JsonValueKind.String &&
        !string.IsNullOrWhiteSpace(v.GetString());

    // Parses the (already-validated) steps array into the host's PlanStep view
    // for rendering. Missing/odd fields degrade gracefully — validation has
    // already guaranteed label+cmd are present for a plan that reaches approval.
    // A step is destructive when the model said so OR its command contains a
    // danger verb (host-side double-safety). internal for unit testing.
    internal static IReadOnlyList<ConsoleApprovalPrompt.PlanStep>? ExtractPlanSteps(object? args)
    {
        if (!ArgsNormalizer.TryGetArray(args, "steps", out var steps)) return null;

        var list = new List<ConsoleApprovalPrompt.PlanStep>();
        int fallbackId = 1;
        foreach (var step in steps.EnumerateArray())
        {
            if (step.ValueKind != JsonValueKind.Object) { fallbackId++; continue; }
            var id = step.TryGetProperty("id", out var idEl)
                && idEl.ValueKind == JsonValueKind.Number
                && idEl.TryGetInt32(out var parsed) ? parsed : fallbackId;
            var label = step.TryGetProperty("label", out var l) ? l.GetString() ?? string.Empty : string.Empty;
            var cmd = step.TryGetProperty("cmd", out var c) ? c.GetString() ?? string.Empty : string.Empty;
            // destructive is a schema boolean — trust it. A danger verb in the
            // command is an independent safety backstop, not a format fallback.
            var modelDestructive = step.TryGetProperty("destructive", out var d)
                && d.ValueKind == JsonValueKind.True;
            var destructive = modelDestructive || HasDangerVerb(cmd);
            list.Add(new ConsoleApprovalPrompt.PlanStep(id, label, cmd, destructive));
            fallbackId++;
        }
        return list.Count > 0 ? list : null;
    }

    // Parses an array of {flag, description} (placeholders) into the host's
    // UserInputNeed view. Returns null when absent or empty. internal for tests.
    internal static IReadOnlyList<ConsoleApprovalPrompt.UserInputNeed>? ExtractUserInputNeeds(object? args, string field)
    {
        if (!ArgsNormalizer.TryGetArray(args, field, out var needs)) return null;

        var list = new List<ConsoleApprovalPrompt.UserInputNeed>();
        foreach (var need in needs.EnumerateArray())
        {
            if (need.ValueKind != JsonValueKind.Object) continue;
            var flag = need.TryGetProperty("flag", out var n) && n.ValueKind == JsonValueKind.String
                ? n.GetString() : null;
            var description = need.TryGetProperty("description", out var d) && d.ValueKind == JsonValueKind.String
                ? d.GetString() ?? string.Empty : string.Empty;
            if (string.IsNullOrWhiteSpace(flag)) continue;
            list.Add(new ConsoleApprovalPrompt.UserInputNeed(flag!, description));
        }
        return list.Count > 0 ? list : null;
    }

    // Parses the propose_plan issues array (what-went-wrong points) into a string
    // list. Skips non-string / blank entries. Returns null when absent or empty.
    // internal for unit testing.
    internal static IReadOnlyList<string>? ExtractIssues(object? args)
    {
        if (!ArgsNormalizer.TryGetArray(args, "issues", out var issues)) return null;

        var list = new List<string>();
        foreach (var issue in issues.EnumerateArray())
        {
            if (issue.ValueKind == JsonValueKind.String && issue.GetString() is { Length: > 0 } s
                && !string.IsNullOrWhiteSpace(s))
                list.Add(s);
        }
        return list.Count > 0 ? list : null;
    }

    // Parses the propose_plan defaults array ({flag, note}) into the host's
    // ChosenDefault view — flags the agent chose/derived. The value is NOT taken
    // here; it's read from the step command at render time. Returns null when
    // absent or empty. internal for unit testing.
    internal static IReadOnlyList<ConsoleApprovalPrompt.ChosenDefault>? ExtractChosenDefaults(object? args)
    {
        if (!ArgsNormalizer.TryGetArray(args, "defaults", out var defaults)) return null;

        var list = new List<ConsoleApprovalPrompt.ChosenDefault>();
        foreach (var d in defaults.EnumerateArray())
        {
            if (d.ValueKind != JsonValueKind.Object) continue;
            // Prefer "flag"; tolerate the old "name" key so a model still emitting
            // the previous shape degrades to the flag name rather than dropping.
            var flag = d.TryGetProperty("flag", out var f) && f.ValueKind == JsonValueKind.String ? f.GetString()
                     : d.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String ? n.GetString()
                     : null;
            var note = d.TryGetProperty("note", out var nt) && nt.ValueKind == JsonValueKind.String ? nt.GetString() ?? string.Empty : string.Empty;
            if (string.IsNullOrWhiteSpace(flag)) continue;
            list.Add(new ConsoleApprovalPrompt.ChosenDefault(flag!, note));
        }
        return list.Count > 0 ? list : null;
    }

    // Danger verbs that mark a command destructive even when the model didn't.
    // Matched as whole words so "undelete" or a resource named "removexyz" in a
    // path don't trip it; case-insensitive.
    private static readonly string[] DangerVerbs =
        ["delete", "remove", "purge", "destroy", "rm", "drop", "wipe", "erase", "format", "uninstall"];

    internal static bool HasDangerVerb(string cmd)
    {
        if (string.IsNullOrWhiteSpace(cmd)) return false;
        foreach (var token in cmd.Split([' ', '\t', '\n', '\r', '-', '/'], StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (var verb in DangerVerbs)
                if (string.Equals(token, verb, StringComparison.OrdinalIgnoreCase))
                    return true;
        }
        return false;
    }
}
