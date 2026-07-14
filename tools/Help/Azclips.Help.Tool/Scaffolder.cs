// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;
using Azclips.Help.Tool.Validation;
using Core.Abstractions;

namespace Azclips.Help.Tool
{
    /// <summary>
    /// Emits nested help markdown stubs for a verb: <c>index.md</c> at the verb root and
    /// every intermediate group, plus one <c>&lt;path&gt;.md</c> per routed leaf. Existing
    /// files are left untouched so authors can re-run safely.
    /// </summary>
    public static class Scaffolder
    {
        /// <summary>Scaffolds a single verb's help folder.</summary>
        public static int Scaffold(string repoRoot, string verb, IExtensionIndex extensionIndex)
        {
            var verbFolder = HelpCommandKey.ToExtensionFolder(verb);
            var helpDir = Core.Cli.Help.HelpPaths.CliVerbHelpDir(repoRoot, verbFolder);
            Directory.CreateDirectory(helpDir);

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

            // Emit intermediate group index.md files before leaves so directories exist.
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
                    // Verb root is handled by index.md above.
                    continue;
                }

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

        /// <summary>Yields every proper prefix of a scoped command key (mirrors <see cref="HelpCompletenessValidator"/>).</summary>
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
