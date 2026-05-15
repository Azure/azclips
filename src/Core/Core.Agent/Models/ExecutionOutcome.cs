using System.Text.Json.Serialization;

namespace Core.Agent.Models;

// Envelope the caller pushes back into the workflow after the user
// acts on an executable_command. The workflow's ConditionGroup branches
// on Success: false → diagnose handoff, true → continue chat loop.

public sealed record ExecutionOutcome(
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("command")] string Command,
    [property: JsonPropertyName("exit_code")] int ExitCode,
    [property: JsonPropertyName("error_message")] string? ErrorMessage = null,
    [property: JsonPropertyName("stdout")] string? Stdout = null);
