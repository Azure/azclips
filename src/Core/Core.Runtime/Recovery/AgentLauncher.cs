// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using Core.Abstractions;
using Core.Abstractions.Recovery;

namespace Core.Runtime.Recovery;

/// <summary>Locates and starts the azclips-agent process for error recovery.</summary>
public static class AgentLauncher
{
    public const string AgentModeEnvVar = "AZCLIPS_IN_AGENT_MODE";
    // Forwards the user's real cwd to the agent (whose working dir we override to
    // the install dir for probing). Read by EnvironmentProber on the agent side by
    // the same literal name — rename both together.
    public const string CallerCwdEnvVar = "AZCLIPS_CALLER_CWD";

    /// <summary>
    /// Builds the spawn shape for azclips-agent.exe. Separate from the launch itself so
    /// tests can assert the environment block without starting a process.
    /// </summary>
    public static ProcessStartInfo BuildAgentStartInfo(
        string agentExe,
        string inputHandle,
        TelemetryInvocationContext context,
        string fileId,
        string? callerDirectory = null)
    {
        var psi = new ProcessStartInfo
        {
            FileName = agentExe,
            // Set the working directory to the agent exe's own directory so relative
            // additionalProbingPaths in azclips-agent.runtimeconfig.json resolve correctly.
            // .NET resolves those at CLR startup, before any managed code runs, relative to
            // the working directory rather than the exe location — so it must be set here
            // and cannot be corrected from inside the agent. The user's real cwd is
            // forwarded via AZCLIPS_CALLER_CWD below.
            WorkingDirectory = Path.GetDirectoryName(agentExe)!,
            // Inherit stdio so streaming tokens, the y/n prompt, and device-code links all
            // show live in the user's terminal.
            UseShellExecute = false,
            RedirectStandardOutput = false,
            RedirectStandardError = false,
            RedirectStandardInput = false,
        };

        psi.Environment[AgentModeEnvVar] = "1";
        // Best-effort: GetCurrentDirectory can throw (e.g. the cwd was deleted), and
        // forwarding it is only a hint — skip it rather than fail the launch.
        if (!string.IsNullOrEmpty(callerDirectory))
            psi.Environment[CallerCwdEnvVar] = callerDirectory;
        else
        {
            try { psi.Environment[CallerCwdEnvVar] = Directory.GetCurrentDirectory(); }
            catch { /* leave CallerCwdEnvVar unset; the agent falls back to its own cwd */ }
        }

        TelemetryLayerEnvironment.SetAgentLaunch(psi.Environment, context, fileId);

        psi.ArgumentList.Add("--agent-mode");
        psi.ArgumentList.Add(RecoveryRequest.InputPipeArgument);
        psi.ArgumentList.Add(inputHandle);

        return psi;
    }

    public static string JoinShellQuoted(string[] args)
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < args.Length; i++)
        {
            if (i > 0) sb.Append(' ');
            var a = args[i];
            if (a.Length == 0 || a.Contains(' ') || a.Contains('\t') || a.Contains('"'))
                sb.Append('"').Append(a.Replace("\"", "\\\"")).Append('"');
            else
                sb.Append(a);
        }
        return sb.ToString();
    }

    public static string? FindAgentExe()
    {
        var baseDir = AppContext.BaseDirectory;
        foreach (var candidate in new[]
        {
            // Packaged layout: azclips-agent.exe lives next to az.exe (hybrid
            // layout — see stage_agent). The two share the runtime pack + all
            // shared NuGet dlls; agent-only deps + prompts + copilot native
            // live under Extensions/Microsoft.Azure.Azclips.Extensions.Agent/.
            Path.Combine(baseDir, "azclips-agent.exe"),
            Path.Combine(baseDir, "azclips-agent"),
            // Local dev layout: `dotnet build` publishes each project to its
            // own artifacts/Debug/<ExtName>/ directory.
            Path.Combine(baseDir, "..", "Core.Agent", "azclips-agent.exe"),
            Path.Combine(baseDir, "..", "Core.Agent", "azclips-agent"),
        })
        {
            if (File.Exists(candidate)) return Path.GetFullPath(candidate);
        }
        return null;
    }
}
