// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Cirrus.Help.Tool.Validation
{
    /// <summary>
    /// Helpers for converting between routed command keys (e.g. "config get") and
    /// the markdown help filenames that back them (e.g. "config-get.md").
    /// </summary>
    public static class HelpCommandKey
    {
        /// <summary>
        /// Normalizes a routed command key: trims whitespace and lowercases.
        /// Empty/whitespace input becomes empty string (the root command).
        /// </summary>
        public static string Normalize(string? commandKey) =>
            string.IsNullOrWhiteSpace(commandKey)
                ? string.Empty
                : commandKey.Trim().ToLowerInvariant();

        /// <summary>
        /// Maps a normalized command key to its help filename.
        /// Empty key → "index.md". "config get" → "config-get.md".
        /// </summary>
        public static string ToHelpFileName(string commandKey) =>
            string.IsNullOrWhiteSpace(commandKey)
                ? "index.md"
                : commandKey.Replace(' ', '-').ToLowerInvariant() + ".md";

        /// <summary>
        /// Inverse of <see cref="ToHelpFileName"/>. "index.md" → "", "config-get.md" → "config get".
        /// </summary>
        public static string FromHelpFileName(string fileName)
        {
            if (fileName.Equals("index.md", StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            var normalized = fileName.Replace('\\', '/');
            if (normalized.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized[..^3];
            }

            return normalized.Replace('-', ' ');
        }

        /// <summary>
        /// Returns the on-disk folder name used for an extension's CLI help directory
        /// (e.g. <c>src/CLI/Help/Docs/&lt;folder&gt;/</c>). Folder names are lowercase to match
        /// the runtime lookup in <see cref="Core.Cli.Help.MarkdownCommandHelpProvider"/>.
        /// </summary>
        public static string ToExtensionFolder(string extensionName)
        {
            return string.IsNullOrWhiteSpace(extensionName)
                ? extensionName
                : extensionName.ToLowerInvariant();
        }
    }
}
