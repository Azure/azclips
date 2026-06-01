// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Core.Abstractions
{
    /// <summary>
    /// Declares the range of Cirrus core (host) versions an extension is compatible with.
    /// Applied at the assembly level on extension DLLs.
    /// </summary>
    /// <remarks>
    /// Phase 10 #36 (<c>docs/spec/extension-management.md §7</c>): the host enforces this range
    /// at install time (hard fail, exit 7) and at load time (warn-and-skip). Both fields are
    /// optional — a missing or unparseable bound is treated as "no bound on that side"
    /// (tolerant default, mirroring az-cli's <c>ext_compat_with_cli</c>). A missing attribute
    /// entirely is treated as compatible: extensions built before this attribute existed
    /// continue to load. Versions are SemVer with pre-release ordering, parsed via NuGet's
    /// <c>NuGetVersion</c> at the host side; the attribute itself stores raw strings to keep
    /// <c>Core.Abstractions</c> dependency-free.
    /// <para>
    /// Example usage on an extension's <c>AssemblyInfo.cs</c> (or any source file):
    /// </para>
    /// <code>
    /// [assembly: CirrusCompatibility(MinCoreVersion = "1.0.0", MaxCoreVersion = "2.0.0")]
    /// </code>
    /// </remarks>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
    public sealed class CirrusCompatibilityAttribute : Attribute
    {
        /// <summary>
        /// Minimum core version this extension supports, inclusive. <c>null</c> = no lower bound.
        /// </summary>
        public string? MinCoreVersion { get; init; }

        /// <summary>
        /// Maximum core version this extension supports, inclusive. <c>null</c> = no upper bound.
        /// </summary>
        public string? MaxCoreVersion { get; init; }
    }
}
