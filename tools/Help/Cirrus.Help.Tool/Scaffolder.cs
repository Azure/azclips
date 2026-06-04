// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;
using Cirrus.Help.Tool.Validation;
using Core.Abstractions;

namespace Cirrus.Help.Tool
{
    /// <summary>
    /// Emits <c>src/CLI/Help/&lt;extension&gt;/&lt;command&gt;.md</c> stubs (and a sibling
    /// <c>index.md</c>) for every entry in <see cref="IExtensionIndex.GetCommandTable"/> for
    /// <see cref="ClientType.CLI"/>. Existing files are left untouched so authors can re-run
    /// the scaffolder safely after adding new commands.
    /// </summary>
    public static class Scaffolder
    {
        public static int Scaffold(string repoRoot, string extensionName, IExtensionIndex extensionIndex, string? cliGroupToken)
        {
            var helpDir = Path.Combine(repoRoot, "src", "CLI", "Help", extensionName);
            Directory.CreateDirectory(helpDir);

            var groupToken = cliGroupToken ?? extensionName;
            var cliTable = extensionIndex.GetCommandTable(ClientType.CLI);

            var created = 0;
            var skipped = 0;

            var indexPath = Path.Combine(helpDir, "index.md");
            var firstCommandKey = cliTable.Keys
                .OrderBy(static k => k, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
            if (TryWrite(indexPath, BuildIndexTemplate(extensionName, groupToken, firstCommandKey)))
            {
                created++;
            }
            else
            {
                skipped++;
            }

            foreach (var (key, metadata) in cliTable.OrderBy(static kv => kv.Key, StringComparer.OrdinalIgnoreCase))
            {
                // Use the canonical filename mapping so multi-word routed keys
                // (e.g. "config get") become "config-get.md" and an empty key
                // wouldn't accidentally produce ".md" — keeps scaffolder output
                // in lockstep with HelpCompletenessValidator (PR #226 review).
                var path = Path.Combine(helpDir, HelpCommandKey.ToHelpFileName(key));
                if (TryWrite(path, BuildCommandTemplate(groupToken, key, metadata)))
                {
                    created++;
                }
                else
                {
                    skipped++;
                }
            }

            Console.WriteLine($"Scaffold {extensionName}: {created} created, {skipped} skipped (already existed) in {Path.GetRelativePath(repoRoot, helpDir)}");
            return 0;
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

        private static string BuildIndexTemplate(string extensionName, string groupToken, string? firstCommandKey)
        {
            _ = firstCommandKey; // reserved for future per-command discovery hints
            var sb = new StringBuilder();
            sb.AppendLine("---");
            sb.AppendLine($"summary: TODO \u2014 one-sentence description of the {extensionName} command group.");
            sb.AppendLine("status: ga");
            sb.AppendLine("schema: 1.0.0");
            sb.AppendLine("online version:");
            sb.AppendLine("---");
            sb.AppendLine();
            sb.AppendLine($"Description: TODO \u2014 describe what the `azv2 {groupToken}` command group does.");
            sb.AppendLine();
            sb.AppendLine($"### Discover {groupToken} commands");
            sb.AppendLine();
            sb.AppendLine("```bash");
            sb.AppendLine($"azv2 {groupToken} --help");
            sb.AppendLine("```");
            return sb.ToString();
        }

        private static string BuildCommandTemplate(string groupToken, string commandKey, CommandMetadata metadata)
        {
            var sb = new StringBuilder();
            sb.AppendLine("---");
            sb.AppendLine($"summary: TODO — short summary of `azv2 {groupToken} {commandKey}`.");
            sb.AppendLine("status: ga");
            sb.AppendLine("schema: 1.0.0");
            sb.AppendLine("online version:");
            sb.AppendLine("---");
            sb.AppendLine();
            sb.AppendLine($"Description: TODO — describe what `azv2 {groupToken} {commandKey}` does and when to use it.");
            sb.AppendLine();
            sb.AppendLine($"### {commandKey} a resource");
            sb.AppendLine();
            sb.AppendLine("```bash");
            sb.Append($"azv2 {groupToken} {commandKey}");

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
