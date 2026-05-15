using System.ComponentModel;
using System.Text;
using Core.Agent.Models;
using Microsoft.Extensions.AI;

namespace Core.Agent.Tools;

/// <summary>
/// SDK-side wrapper around the host-provided <see cref="RunCommandExecutor"/>.
/// Exposes a single <c>run_command</c> <see cref="AIFunction"/> the Copilot SDK
/// can invoke. The host (CLI / TUI) owns the tee (stdout / stderr / Information
/// channels live to console + buffered for the model) — we just package the
/// <see cref="ExecutionOutcome"/> back into the labeled plaintext shape the
/// agent prompt expects.
/// </summary>
public static class RunCommandTool
{
    public delegate Task<ExecutionOutcome> RunCommandExecutor(string commandLine, CancellationToken ct);

    public static AIFunction Build(RunCommandExecutor executor) =>
        AIFunctionFactory.Create(
            async (
                [Description("The complete azv2 command line to execute, e.g. \"azv2 group create --subscription-id X --name Y --location eastus\". MUST be runnable as-is — no placeholders like <sub-id>.")]
                string command,
                CancellationToken ct) =>
            {
                var outcome = await executor(command, ct).ConfigureAwait(false);

                // Labeled plaintext for the model. Anthropic guidance: tool
                // results to model = plain text, NOT JSON. Structured rendering
                // is the host's job (already done via the tee).
                var sb = new StringBuilder();
                sb.Append(outcome.Success ? "SUCCESS · exit " : "FAILURE · exit ")
                  .Append(outcome.ExitCode).Append('\n');
                sb.Append("command: ").Append(outcome.Command).Append('\n');
                if (!string.IsNullOrWhiteSpace(outcome.Stdout))
                {
                    sb.Append("output:\n").Append(outcome.Stdout.TrimEnd()).Append('\n');
                }
                if (!string.IsNullOrWhiteSpace(outcome.ErrorMessage))
                {
                    sb.Append("error: ").Append(outcome.ErrorMessage).Append('\n');
                }
                return sb.ToString();
            },
            name: "run_command",
            description: "Execute a cirrus (azv2) command line on the user's machine after they approve it. Returns labeled plaintext with exit code, stdout (incl. interactive prompts like device-code links), and any error message.");
}
