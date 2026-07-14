// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;
using Azclips.Help.Tool.Validation;
using Core.Abstractions;

namespace Azclips.Help.Tool
{
    /// <summary>
    /// Emits <c>src/CLI/Help/Docs/&lt;verb&gt;/&lt;seg1&gt;/&lt;seg2&gt;.md</c> stubs (nested layout)
    /// plus <c>index.md</c> at the verb root and every intermediate group node. Naming
    /// mirrors <see cref="HelpCompletenessValidator"/>; existing files are left untouched
    /// so authors can re-run safely.
    /// </summary>
    public static class Scaffolder
    {
        /// <summary>Scaffolds a single verb's help folder.</summary>
        public static int Scaffold(string repoRoot, string verb, IExtensionIndex extensionIndex)
        {
            var verbFolder = HelpCommandKey.ToExtensionFolder(verb);
            var helpDir = Core.Cli.Help.HelpPaths.CliVerbHelpDir(repoRoot, verbFolder);
            Directory.CreateDirectory(helpDir);

            // Scope the routed table to this verb and strip the verb prefix; keys arrive as
            // "verb" / "verb sub" and emit as "" / "sub" / "sub sub" for the nested layout.
            var scopedKeys = ScopeKeysToVerb(extensionIndex.GetCommandTable(ClientType.CLI), verb)
                .OrderBy(static kv => kv.Key, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var created = 0;
            var skipped = 0;

            var indexPath = Path.Combine(helpDir, "index.md");
            if (TryWrite(indexPath, BuildGroupIndexTemplate(verb, groupKey: string.Empty)))
            {
                created++;
            }
            else
            {
                skipped++;
            }

            // Emit intermediate group index.md files (e.g. "group" → deployment/group/index.md)
            // before leaf .md files so directories exist and authors see the whole tree.
            var groupPrefixes = scopedKeys
                .SelectMany(kv => EnumerateGroupPrefixes(kv.Key))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(static prefix => prefix, StringComparer.OrdinalIgnoreCase);
            foreach (var prefix in groupPrefixes)
            {
                var groupFile = HelpCommandKey.ToGroupIndexFileName(prefix);
                var path = Path.Combine(helpDir, groupFile.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                if (TryWrite(path, BuildGroupIndexTemplate(verb, prefix)))
                {
                    created++;
                }
                else
                {
                    skipped++;
                }
            }

            foreach (var (subKey, metadata) in scopedKeys)
            {
                if (subKey.Length == 0)
                {
                    // Root command (key == verb) is rendered by index.md above.
                    continue;
                }

                // Multi-word sub-keys (e.g. "group create" → "group/create.md") are normalized by ToHelpFileName.
                var relative = HelpCommandKey.ToHelpFileName(subKey);
                var path = Path.Combine(helpDir, relative.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                if (TryWrite(path, BuildCommandTemplate(verb, subKey, metadata)))
                {
                    created++;
                }
                else
                {
                    skipped++;
                }
            }

            Console.WriteLine($"Scaffold verb '{verb}': {created} created, {skipped} skipped (already existed) in {Path.GetRelativePath(repoRoot, helpDir)}");
            return 0;
        }

        /// <summary>
        /// Enumerates every proper prefix of a scoped command key. <c>"group create"</c>
        /// yields <c>"group"</c>; <c>"deployment group create"</c> yields <c>"deployment"</c>
        /// and <c>"deployment group"</c>. Mirrors <see cref="HelpCompletenessValidator"/>.
        /// </summary>
        private static IEnumerable<string> EnumerateGroupPrefixes(string commandKey)
        {
            if (string.IsNullOrWhiteSpace(commandKey))
            {
                yield break;
            }

            var segments = commandKey.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            for (var i = 1; i < segments.Length; i++)
            {
                yield return string.Join(' ', segments, 0, i);
            }
        }

        /// <summary>
        /// Filters the routed command table to entries owned by <paramref name="verb"/>,
        /// stripping the verb prefix (empty string for the verb root).
        /// </summary>
        private static IEnumerable<KeyValuePair<string, CommandMetadata>> ScopeKeysToVerb(
            IReadOnlyDictionary<string, CommandMetadata> rawTable,
            string verb)
        {
            var prefix = verb + " ";
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var (rawKey, metadata) in rawTable)
            {
                var key = HelpCommandKey.Normalize(rawKey);
                string? scoped = null;

                if (key.Equals(verb, StringComparison.OrdinalIgnoreCase))
                {
                    scoped = string.Empty;
                }
                else if (key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    scoped = key[prefix.Length..];
                }

                if (scoped is not null && seen.Add(scoped))
                {
                    yield return new KeyValuePair<string, CommandMetadata>(scoped, metadata);
                }
            }
        }

        // Returns true if the file was written, false if it already existed.
        private static bool TryWrite(string path, string content)
        {
            if (File.Exists(path))
            {
                return false;
            }

            File.WriteAllText(path, content);
            return true;
        }

        private static string BuildGroupIndexTemplate(string verb, string groupKey)
        {
            // When groupKey is empty the file is the verb root (Docs/<verb>/index.md);
            // otherwise it is an intermediate group (Docs/<verb>/<groupKey>/index.md,
            // rendered as `az <verb> <groupKey>`).
            var displayPath = string.IsNullOrEmpty(groupKey) ? verb : verb + " " + groupKey;

            var sb = new StringBuilder();
            sb.AppendLine("---");
            sb.AppendLine($"summary: TODO \u2014 one-sentence description of the {displayPath} command group.");
            sb.AppendLine("status: ga");
            sb.AppendLine("schema: 1.0.0");
            sb.AppendLine("online version:");
            sb.AppendLine("---");
            sb.AppendLine();
            sb.AppendLine($"Description: TODO \u2014 describe what the `az {displayPath}` command group does.");
            sb.AppendLine();
            sb.AppendLine($"### Discover {displayPath} commands");
            sb.AppendLine();
            sb.AppendLine("```bash");
            sb.AppendLine($"az {displayPath} --help");
            sb.AppendLine("```");
            return sb.ToString();
        }

        private static string BuildCommandTemplate(string verb, string subKey, CommandMetadata metadata)
        {
            var sb = new StringBuilder();
            sb.AppendLine("---");
            sb.AppendLine($"summary: TODO — short summary of `az {verb} {subKey}`.");
            sb.AppendLine("status: ga");
            sb.AppendLine("schema: 1.0.0");
            sb.AppendLine("online version:");
            sb.AppendLine("---");
            sb.AppendLine();
            sb.AppendLine($"Description: TODO — describe what `az {verb} {subKey}` does and when to use it.");
            sb.AppendLine();
            sb.AppendLine($"### {subKey} a resource");
            sb.AppendLine();
            sb.AppendLine("```bash");
            sb.Append($"az {verb} {subKey}");

            foreach (var (paramName, paramMeta) in metadata.Parameters
                .Where(static kv => kv.Value.IsRequired)
                .OrderBy(static kv => kv.Key, StringComparer.OrdinalIgnoreCase))
            {
                sb.Append($" --{paramName} <{paramMeta.ParameterType.Name.ToLowerInvariant()}>");
            }

            sb.AppendLine();
            sb.AppendLine("```");
            return sb.ToString();
        }
    }
}
