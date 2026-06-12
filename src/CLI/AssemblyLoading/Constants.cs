namespace Core.Cli
{
    internal static class Constants
    {
        internal const string cirrusPrefix = "Microsoft.Azure.Cirrus.Extensions.";

        /// <summary>
        /// Strips <see cref="cirrusPrefix"/> from the front of <paramref name="packageId"/> if
        /// present, using a case-insensitive comparison. Used at every dispatch site that
        /// hands a "short" extension name to the loader (which re-prepends the prefix).
        /// </summary>
        /// <remarks>
        /// Registry keys are stored with <see cref="System.StringComparer.OrdinalIgnoreCase"/>
        /// and NuGet package ids are case-insensitive on the wire, so a registry that holds
        /// e.g. <c>microsoft.azure.cirrus.extensions.compute</c> would not match an ordinal
        /// (case-sensitive) prefix check; the loader would then re-prepend the prefix and
        /// look for <c>Microsoft.Azure.Cirrus.Extensions.microsoft.azure.cirrus.extensions.compute</c>.
        /// </remarks>
        internal static string StripCirrusPrefix(string packageId)
            => packageId.StartsWith(cirrusPrefix, System.StringComparison.OrdinalIgnoreCase)
                ? packageId[cirrusPrefix.Length..]
                : packageId;
    }
}