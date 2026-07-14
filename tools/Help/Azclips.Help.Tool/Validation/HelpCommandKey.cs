// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Azclips.Help.Tool.Validation
{
    /// <summary>
    /// Helpers for converting between routed command keys (e.g. "config get") and
    /// the nested markdown help filenames that back them (e.g. "config/get.md",
    /// "config/index.md"). One directory segment per command-tree level.
    /// </summary>
    public static class HelpCommandKey
    {
        /// <summary>Trims and lowercases a routed command key; empty input → empty.</summary>
        public static string Normalize(string? commandKey) =>
            string.IsNullOrWhiteSpace(commandKey)
                ? string.Empty
                : commandKey.Trim().ToLowerInvariant();

        /// <summary>
        /// Maps a normalized command key to its leaf help filename (forward-slash relative path).
        /// Empty key → <c>"index.md"</c>. <c>"config get"</c> → <c>"config/get.md"</c>.
        /// </summary>
        public static string ToHelpFileName(string commandKey) =>
            string.IsNullOrWhiteSpace(commandKey)
                ? "index.md"
                : commandKey.Trim().ToLowerInvariant().Replace(' ', '/') + ".md";

        /// <summary>
        /// Maps a normalized command key to its group-index filename.
        /// <c>"deployment group"</c> → <c>"deployment/group/index.md"</c>.
        /// </summary>
        public static string ToGroupIndexFileName(string commandKey) =>
            string.IsNullOrWhiteSpace(commandKey)
                ? "index.md"
                : commandKey.Trim().ToLowerInvariant().Replace(' ', '/') + "/index.md";

        /// <summary>
        /// Inverse of <see cref="ToHelpFileName"/> / <see cref="ToGroupIndexFileName"/>.
        /// <c>"config/get.md"</c> → <c>"config get"</c>;
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

        /// <summary>Lowercases an extension name for use as an on-disk folder name.</summary>
        public static string ToExtensionFolder(string extensionName)
        {
            return string.IsNullOrWhiteSpace(extensionName)
                ? extensionName
                : extensionName.ToLowerInvariant();
        }

        /// <summary>Yields every proper prefix of a routed command key. Each prefix corresponds to a group node whose <c>index.md</c> is required.</summary>
        public static IEnumerable<string> EnumerateGroupPrefixes(string commandKey)
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
    }
}
