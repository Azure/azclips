// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Azclips.Help.Tool.Validation
{
    /// <summary>
    /// Helpers for converting between routed command keys (e.g. "config get") and
    /// the markdown help filenames that back them (e.g. "config/get.md"). The path
    /// layout is nested — one directory segment per command-tree level — with
    /// <c>index.md</c> at every group node (verb root and intermediate sub-groups).
    /// See docs/spec/help-system.md §4.
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
        /// Maps a normalized command key to its leaf help filename (forward-slash relative path).
        /// Empty key → <c>"index.md"</c>. <c>"config get"</c> → <c>"config/get.md"</c>.
        /// <c>"deployment group create"</c> → <c>"deployment/group/create.md"</c>.
        /// </summary>
        /// <remarks>
        /// Intermediate group index files (e.g. <c>"config/index.md"</c>) are derived
        /// separately by <see cref="ToGroupIndexFileName"/> — call sites that need to
        /// enumerate every expected file for a routed key should call both.
        /// </remarks>
        public static string ToHelpFileName(string commandKey) =>
            string.IsNullOrWhiteSpace(commandKey)
                ? "index.md"
                : commandKey.Trim().ToLowerInvariant().Replace(' ', '/') + ".md";

        /// <summary>
        /// Maps a normalized command key to its group-index filename (forward-slash relative path).
        /// Empty key → <c>"index.md"</c>. <c>"deployment group"</c> → <c>"deployment/group/index.md"</c>.
        /// </summary>
        public static string ToGroupIndexFileName(string commandKey) =>
            string.IsNullOrWhiteSpace(commandKey)
                ? "index.md"
                : commandKey.Trim().ToLowerInvariant().Replace(' ', '/') + "/index.md";

        /// <summary>
        /// Inverse of <see cref="ToHelpFileName"/> / <see cref="ToGroupIndexFileName"/>.
        /// <c>"index.md"</c> → <c>""</c>; <c>"config/get.md"</c> → <c>"config get"</c>;
        /// <c>"deployment/group/index.md"</c> → <c>"deployment group"</c>.
        /// </summary>
        public static string FromHelpFileName(string fileName)
        {
            var normalized = fileName.Replace('\\', '/');

            if (normalized.Equals("index.md", StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            if (normalized.EndsWith("/index.md", StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized[..^"/index.md".Length];
            }
            else if (normalized.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized[..^3];
            }

            return normalized.Replace('/', ' ');
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
