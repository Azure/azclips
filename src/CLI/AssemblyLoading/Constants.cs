// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Cli
{
    internal static class Constants
    {
        internal const string azclipsExtensionPackagePrefix = ExtensionDiscoveryPaths.PackagePrefix;
        internal static readonly string[] extensionPackagePrefixes = ExtensionDiscoveryPaths.PackagePrefixes.ToArray();

        /// <summary>
        /// Strips a known extension package prefix from the front of <paramref name="packageId"/> if
        /// present, using a case-insensitive comparison. Used at every dispatch site that
        /// hands a "short" extension name to the loader (which re-prepends a known prefix).
        /// </summary>
        /// <remarks>
        /// Registry keys are stored with <see cref="System.StringComparer.OrdinalIgnoreCase"/>
        /// and NuGet package ids are case-insensitive on the wire, so a registry that holds
        /// e.g. <c>microsoft.azure.azclips.extensions.compute</c> would not match an ordinal
        /// (case-sensitive) prefix check; the loader would then re-prepend the prefix and
        /// look for <c>Microsoft.Azure.Azclips.Extensions.microsoft.azure.azclips.extensions.compute</c>.
        /// </remarks>
        internal static string StripKnownExtensionPrefix(string packageId)
        {
            foreach (var prefix in extensionPackagePrefixes)
            {
                if (packageId.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase))
                {
                    return packageId[prefix.Length..];
                }
            }

            return packageId;
        }
    }
}