// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;
using Cirrus.Help.Tool.Validation;
using Core.Abstractions;

namespace Cirrus.Help.Tool
{
    /// <summary>
    /// Emits <c>src/CLI/Help/Docs/&lt;verb&gt;/&lt;sub&gt;.md</c> stubs (and a sibling
    /// <c>index.md</c>) for every routed command under a given CLI verb. Folder and file
    /// naming mirrors <see cref="HelpCompletenessValidator"/> and the runtime markdown
    /// lookup, so a scaffolded folder validates immediately. Existing files are left
    /// untouched so authors can re-run the scaffolder safely after adding new commands.
    /// </summary>
    public static class Scaffolder
    {
        /// <summary>
        /// Scaffolds a single verb's help folder.
        /// </summary>
        /// <param name="repoRoot">Repository root (folder containing <c>src/</c>).</param>
        /// <param name="verb">Lowercase CLI verb (e.g. <c>vm</c>, <c>disk</c>, <c>group</c>).</param>
        /// <param name="extensionIndex">Index that contributes <paramref name="verb"/>. Multi-verb extensions are scoped via <see cref="ScopeKeysToVerb"/>.</param>
        public static int Scaffold(string repoRoot, string verb, IExtensionIndex extensionIndex)
        {
            var verbFolder = HelpCommandKey.ToExtensionFolder(verb);
            var helpDir = Path.Combine(repoRoot, "src", "CLI", "Help", "Docs", verbFolder);
            Directory.CreateDirectory(helpDir);

            // Scope the routed table down to commands belonging to this verb and strip
            // the verb prefix. Keys arrive as "verb" / "verb sub"; we emit "" / "sub"
            // so the file names match the runtime lookup (index.md, sub.md).
            var scopedKeys = ScopeKeysToVerb(extensionIndex.GetCommandTable(ClientType.CLI), verb);

            var created = 0;
            var skipped = 0;

            var indexPath = Path.Combine(helpDir, "index.md");
            if (TryWrite(indexPath, BuildIndexTemplate(verb)))
            {
                created++;
            }
            else
            {
                skipped++;
            }

            foreach (var (subKey, metadata) in scopedKeys.OrderBy(static kv => kv.Key, StringComparer.OrdinalIgnoreCase))
            {
                if (subKey.Length == 0)
                {
                    // Root command (key == verb) is rendered by index.md above.
                    continue;
                }

                // Multi-word sub-keys (rare; e.g. "config get" stripped to "get" is single-word,
                // but a hypothetical "vm scale set" stripped to "scale set" becomes scale-set.md)
                // are normalized by ToHelpFileName.
                var path = Path.Combine(helpDir, HelpCommandKey.ToHelpFileName(subKey));
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
        /// Filters the full routed command table down to entries owned by <paramref name="verb"/>
        /// and yields them with the verb prefix stripped (empty string for the verb root).
        /// Mirrors the scoping done by <see cref="HelpCompletenessValidator"/>.
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

        private static string BuildIndexTemplate(string verb)
        {
            var sb = new StringBuilder();
            sb.AppendLine("---");
            sb.AppendLine($"summary: TODO \u2014 one-sentence description of the {verb} command group.");
            sb.AppendLine("status: ga");
            sb.AppendLine("schema: 1.0.0");
            sb.AppendLine("online version:");
            sb.AppendLine("---");
            sb.AppendLine();
            sb.AppendLine($"Description: TODO \u2014 describe what the `azv2 {verb}` command group does.");
            sb.AppendLine();
            sb.AppendLine($"### Discover {verb} commands");
            sb.AppendLine();
            sb.AppendLine("```bash");
            sb.AppendLine($"azv2 {verb} --help");
            sb.AppendLine("```");
            return sb.ToString();
        }

        private static string BuildCommandTemplate(string verb, string subKey, CommandMetadata metadata)
        {
            var sb = new StringBuilder();
            sb.AppendLine("---");
            sb.AppendLine($"summary: TODO — short summary of `azv2 {verb} {subKey}`.");
            sb.AppendLine("status: ga");
            sb.AppendLine("schema: 1.0.0");
            sb.AppendLine("online version:");
            sb.AppendLine("---");
            sb.AppendLine();
            sb.AppendLine($"Description: TODO — describe what `azv2 {verb} {subKey}` does and when to use it.");
            sb.AppendLine();
            sb.AppendLine($"### {subKey} a resource");
            sb.AppendLine();
            sb.AppendLine("```bash");
            sb.Append($"azv2 {verb} {subKey}");

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
