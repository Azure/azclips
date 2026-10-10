// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace Microsoft.Azure.Azclips.Extensions.Resources
{
    /// <summary>
    /// Thin wrapper over the Bicep CLI used to compile local <c>.bicep</c> templates and
    /// <c>.bicepparam</c> parameter files to ARM JSON before they are inlined into a deployment
    /// request body. Compilation is delegated to the installed Bicep CLI (it is not reimplemented
    /// here), matching how <c>az deployment</c> and <c>New-AzResourceGroupDeployment</c> behave.
    /// </summary>
    internal static class BicepCompiler
    {
        /// <summary>
        /// Compiles a <c>.bicep</c> template to ARM template JSON via <c>bicep build &lt;path&gt; --stdout</c>.
        /// </summary>
        /// <param name="bicepFilePath">The path to the local <c>.bicep</c> file.</param>
        /// <returns>The compiler's ARM template JSON (stdout).</returns>
        public static string BuildTemplate(string bicepFilePath) =>
            Run("build", bicepFilePath);

        /// <summary>
        /// Compiles a <c>.bicepparam</c> parameters file via <c>bicep build-params &lt;path&gt; --stdout</c>.
        /// </summary>
        /// <param name="bicepParamFilePath">The path to the local <c>.bicepparam</c> file.</param>
        /// <returns>
        /// The compiler's stdout, a JSON envelope shaped like
        /// <c>{ "parametersJson": "&lt;stringified ARM params file&gt;", "templateJson": ..., "templateSpecId": ... }</c>.
        /// </returns>
        public static string BuildParams(string bicepParamFilePath) =>
            Run("build-params", bicepParamFilePath);

        /// <summary>
        /// Legacy CLI <c>_build_bicepparam_file</c>: optional <c>--bicep-file</c> and inline overrides passed
        /// through the <c>BICEP_PARAMETERS_OVERRIDES</c> environment variable.
        /// </summary>
        public static string BuildParams(string bicepParamFilePath, string? bicepFilePath, string? overridesJson) =>
            Run("build-params", bicepParamFilePath, bicepFilePath, overridesJson);

        private static string Run(string command, string filePath, string? bicepFilePath = null, string? overridesJson = null)
        {
            string executable = LocateBicepExecutable();

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = executable,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                // Bicep emits UTF-8; force UTF-8 decoding so diagnostics and JSON don't get mojibake.
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };
            // Pass arguments as separate list entries (never a single concatenated command line) so
            // paths with spaces are handled safely and there is no shell interpolation / injection.
            startInfo.ArgumentList.Add(command);
            startInfo.ArgumentList.Add(filePath);
            if (bicepFilePath is not null)
            {
                startInfo.ArgumentList.Add("--bicep-file");
                startInfo.ArgumentList.Add(bicepFilePath);
            }
            startInfo.ArgumentList.Add("--stdout");
            if (overridesJson is not null)
            {
                startInfo.Environment["BICEP_PARAMETERS_OVERRIDES"] = overridesJson;
            }

            using Process process = new Process { StartInfo = startInfo };

            try
            {
                process.Start();
            }
            catch (Win32Exception ex)
            {
                throw new InvalidOperationException(
                    "Bicep CLI not found. Install Bicep (https://aka.ms/bicep-install) or pass a compiled ARM JSON template.",
                    ex);
            }

            // Start draining stdout and stderr concurrently, then wait for exit. Reading them
            // serially can deadlock: a serial ReadToEnd() on stdout blocks until the child closes
            // stdout, but if the child first fills the redirected stderr pipe buffer it blocks
            // writing stderr and never finishes stdout — both sides hang. bicep build --stdout can
            // emit large ARM JSON on stdout AND diagnostics on stderr, so both pipes must be drained
            // at the same time. Starting both async reads before awaiting them keeps both buffers
            // moving so neither pipe can fill and stall the child.
            Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
            Task<string> stderrTask = process.StandardError.ReadToEndAsync();
            process.WaitForExit();
            string stdout = stdoutTask.GetAwaiter().GetResult();
            string stderr = stderrTask.GetAwaiter().GetResult();

            if (process.ExitCode != 0)
            {
                // Surface the Bicep compiler diagnostics (stderr) so the user can fix their source.
                // Never include the file contents.
                string diagnostics = string.IsNullOrWhiteSpace(stderr) ? stdout : stderr;
                throw new InvalidOperationException(
                    $"Bicep compilation failed (exit code {process.ExitCode}):{Environment.NewLine}{diagnostics}");
            }

            return stdout;
        }

        /// <summary>
        /// Locates the Bicep executable, preferring one on PATH, then falling back to the standard
        /// az-bundled location (<c>~/.azure/bin/bicep[.exe]</c>). Returns the bare command name when
        /// nothing is found so the OS resolver runs and a missing-CLI failure surfaces a clear error.
        /// </summary>
        private static string LocateBicepExecutable()
        {
            bool isWindows = OperatingSystem.IsWindows();
            string commandName = isWindows ? "bicep.exe" : "bicep";

            string? onPath = FindOnPath(commandName);
            if (onPath is not null)
            {
                return onPath;
            }

            string? home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrEmpty(home))
            {
                string azBundled = Path.Combine(home, ".azure", "bin", commandName);
                if (File.Exists(azBundled))
                {
                    return azBundled;
                }
            }

            // Fall back to the bare command name; if it cannot be resolved, Process.Start throws
            // Win32Exception and Run() turns that into the actionable "Bicep CLI not found" error.
            return commandName;
        }

        private static string? FindOnPath(string commandName)
        {
            string? pathVar = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrEmpty(pathVar))
            {
                return null;
            }

            foreach (string dir in pathVar.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                string candidate;
                try
                {
                    candidate = Path.Combine(dir.Trim(), commandName);
                }
                catch (ArgumentException)
                {
                    // Skip malformed PATH entries (invalid path chars).
                    continue;
                }

                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }
    }
}
