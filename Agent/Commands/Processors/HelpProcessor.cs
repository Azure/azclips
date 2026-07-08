// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Cli.Help;
using Core.DependencyInjection;
using Core.Runtime;

namespace Microsoft.Azure.Azclips.Extensions.Agent.Processors
{
    /// <summary>
    /// `az agent tool help` — the CLI-side (product-side) help catalog for LLM consumption.
    ///
    /// Walks every installed extension (built-in seed + `az extension add` registry) via
    /// <see cref="IAssemblyLoadingService.LoadAllExtensions"/>, loads each assembly's
    /// <see cref="IExtensionIndex"/> instance, runs Daniel's
    /// <see cref="ExtensionHelpManifestBuilder"/> pipeline to project the per-verb command
    /// tables + packaged markdown into <see cref="HelpManifest"/> / <see cref="HelpIndex"/>,
    /// then serializes the result as JSON to stdout.
    ///
    /// The agent-side <c>HelpTools</c> shells out to this command (`az agent tool help`
    /// / `az agent tool help --name &lt;x&gt;`) — the LLM never re-implements this logic.
    /// A user installing a new extension after startup shows up on the very next help call
    /// because we re-enumerate every time; no cached catalog to invalidate.
    /// </summary>
    public class HelpProcessor : AgentProcessorBase
    {
        [ArgumentInfo(Required = false, Description = @"Command name to look up, e.g. 'vm create'. Omit to list all known commands.", SerializedName = @"name")]
        public string? Name { get; set; }

        private static readonly JsonSerializerOptions s_json = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        };

        public override async Task ProcessAsync()
        {
            await Publisher.Publish(Events.ProcessorProcessAsyncStart);

            var assemblyLoader = AzclipsServiceProvider.GetRequiredService<IAssemblyLoadingService>();
            var manifests = BuildAllManifests(assemblyLoader);

            string json;
            if (string.IsNullOrWhiteSpace(Name))
            {
                var index = ExtensionHelpManifestBuilder.BuildIndex(manifests);
                json = JsonSerializer.Serialize(index, s_json);
            }
            else
            {
                var command = FindCommand(manifests, Name);
                json = command is null
                    ? JsonSerializer.Serialize(new { error = "not_found", name = Name, suggestions = FindSuggestions(manifests, Name) }, s_json)
                    : JsonSerializer.Serialize(command, s_json);
            }

            await WriteOutput(json);
            await Publisher.Publish(Events.ProcessorProcessAsyncEnd);
        }

        /// <summary>
        /// Enumerates every installed extension and produces one HelpManifest per
        /// (extension, verb) pair. Extensions that can't be loaded or don't have an
        /// IExtensionIndex are skipped silently — one bad extension shouldn't take
        /// out `az agent tool help` for the whole catalog.
        /// </summary>
        internal static IList<HelpManifest> BuildAllManifests(IAssemblyLoadingService assemblyLoader)
        {
            var manifests = new List<HelpManifest>();
            var loaded = assemblyLoader.LoadAllExtensions();
            if (loaded is null) return manifests;

            foreach (var (info, loadAssembly) in loaded)
            {
                try
                {
                    var assembly = loadAssembly();
                    var index = assembly.GetExtensionIndex();
                    manifests.AddRange(ExtensionHelpManifestBuilder.BuildManifests(index, info.Version));
                }
                catch
                {
                    // Skip extensions that fail to load / have no IExtensionIndex / declare no
                    // verbs. Help is best-effort: one broken extension shouldn't nuke the
                    // whole catalog. (The extension's own broken state will surface elsewhere,
                    // e.g. discovery.)
                    continue;
                }
            }

            return manifests;
        }

        /// <summary>
        /// Looks up a HelpCommand across all manifests by cliName or psName (case-insensitive,
        /// same behavior as the old static-JSON lookup).
        /// </summary>
        internal static HelpCommand? FindCommand(IEnumerable<HelpManifest> manifests, string name)
        {
            foreach (var manifest in manifests)
            {
                foreach (var command in manifest.Commands)
                {
                    if (string.Equals(command.CliName, name, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(command.PsName, name, StringComparison.OrdinalIgnoreCase))
                    {
                        return command;
                    }
                }
            }
            return null;
        }

        // On a not_found, offer commands whose cliName/psName contains the query
        // (or vice versa) so a partial or slightly-off name — e.g. "vm" -> "vm create",
        // "create" -> "vm create-basic" — can still be recovered by the caller/model
        // instead of a dead end. Capped to keep the payload small.
        internal static IReadOnlyList<string> FindSuggestions(IEnumerable<HelpManifest> manifests, string name)
        {
            const int max = 8;
            var q = name.Trim();
            if (q.Length == 0) return System.Array.Empty<string>();

            var hits = new List<string>();
            foreach (var manifest in manifests)
            {
                foreach (var command in manifest.Commands)
                {
                    foreach (var candidate in new[] { command.CliName, command.PsName })
                    {
                        if (string.IsNullOrEmpty(candidate)) continue;
                        if (candidate.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                            q.Contains(candidate, StringComparison.OrdinalIgnoreCase))
                        {
                            if (!hits.Contains(candidate, StringComparer.OrdinalIgnoreCase))
                                hits.Add(candidate);
                        }
                    }
                    if (hits.Count >= max) return hits;
                }
            }
            return hits;
        }
    }
}
