// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using System.Runtime.InteropServices;
using NuGet.Versioning;

namespace Core.Runtime
{
    /// <summary>
    /// Enforces <c>[CirrusCompatibility(MinCoreVersion, MaxCoreVersion)]</c> ranges against a
    /// caller-supplied <em>current</em> core version. Pure, callable from both install
    /// (hard fail) and load (warn-and-skip) sites.
    /// </summary>
    /// <remarks>
    /// Per <c>docs/spec/extension-management.md §6</c>:
    /// <list type="bullet">
    ///   <item>Missing attribute &#x2192; assumed compatible. Tolerates pre-attribute extensions.</item>
    ///   <item>Either bound missing &#x2192; that side is unbounded.</item>
    ///   <item>Unparseable bounds &#x2192; ignored (with a reason in the result), don't fail-closed
    ///         on a typo because that would brick the user's working install.</item>
    ///   <item>Versions are SemVer with pre-release ordering, parsed via <see cref="NuGetVersion"/>.</item>
    /// </list>
    /// <para>
    /// The current core version is supplied by the caller as a string. Establishing a single
    /// host-version source-of-truth is deferred (see vNext list in the spec) — until then,
    /// callers pass a literal that the host team bumps when a breaking change ships.
    /// </para>
    /// <para>
    /// <see cref="CheckAssembly"/> reads the attribute via <see cref="MetadataLoadContext"/>
    /// (no execution), mirroring <c>ExtensionVerbReader</c>'s pattern. This is essential at
    /// install time: a malicious or buggy extension shouldn't be able to run code during
    /// compatibility validation.
    /// </para>
    /// </remarks>
    public static class CompatibilityGuard
    {
        // Match by FullName so we don't need to load Core.Abstractions into the metadata-only
        // context. Keep in sync with CirrusCompatibilityAttribute.
        private const string AttributeFullName = "Core.Abstractions.CirrusCompatibilityAttribute";

        /// <summary>
        /// Result of a compatibility check.
        /// </summary>
        /// <param name="IsCompatible">True if the candidate is compatible with <c>currentCoreVersion</c>.</param>
        /// <param name="Reason">Human-readable reason when <see cref="IsCompatible"/> is <c>false</c>,
        /// or a non-fatal advisory note (e.g. unparseable bound was ignored) otherwise.</param>
        public readonly record struct Result(bool IsCompatible, string? Reason);

        /// <summary>
        /// Pure compatibility check. Determines whether an extension declaring
        /// [<paramref name="minCoreVersion"/>, <paramref name="maxCoreVersion"/>] is compatible
        /// with the host running <paramref name="currentCoreVersion"/>.
        /// </summary>
        /// <param name="minCoreVersion">Minimum supported core version, or <c>null</c> for no lower bound.</param>
        /// <param name="maxCoreVersion">Maximum supported core version, or <c>null</c> for no upper bound.</param>
        /// <param name="currentCoreVersion">The host's current core version.</param>
        /// <returns>A <see cref="Result"/>. Always returns compatible when both bounds are missing
        /// or unparseable; failure is only signalled when <paramref name="currentCoreVersion"/> is
        /// definitively outside a parseable range.</returns>
        public static Result Check(string? minCoreVersion, string? maxCoreVersion, string currentCoreVersion)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(currentCoreVersion);

            if (!NuGetVersion.TryParse(currentCoreVersion, out var current))
            {
                // Caller messed up. This is a host bug, not an extension bug; tolerate it by
                // passing — better than blocking every install on a malformed host literal.
                return new Result(true, $"current core version '{currentCoreVersion}' is not a valid SemVer; compatibility check skipped");
            }

            // Track an advisory note when we ignore a malformed bound. Only the *last* advisory
            // wins (rare; no reason to allocate a list for both bounds being garbage).
            string? advisory = null;

            if (!string.IsNullOrWhiteSpace(minCoreVersion))
            {
                if (NuGetVersion.TryParse(minCoreVersion, out var min))
                {
                    if (current < min)
                    {
                        return new Result(false,
                            $"requires core >= {min} but current is {current}");
                    }
                }
                else
                {
                    advisory = $"MinCoreVersion '{minCoreVersion}' is not a valid SemVer; ignored";
                }
            }

            if (!string.IsNullOrWhiteSpace(maxCoreVersion))
            {
                if (NuGetVersion.TryParse(maxCoreVersion, out var max))
                {
                    if (current > max)
                    {
                        return new Result(false,
                            $"requires core <= {max} but current is {current}");
                    }
                }
                else
                {
                    advisory = $"MaxCoreVersion '{maxCoreVersion}' is not a valid SemVer; ignored";
                }
            }

            return new Result(true, advisory);
        }

        /// <summary>
        /// Inspect every DLL in <paramref name="extensionDirectory"/> for an assembly-level
        /// <c>[CirrusCompatibility]</c> attribute and check it against
        /// <paramref name="currentCoreVersion"/>. Returns the first decisive result encountered;
        /// if no attribute is found on any DLL, returns compatible (per the tolerant default).
        /// </summary>
        /// <remarks>
        /// Uses <see cref="MetadataLoadContext"/> + <see cref="CustomAttributeData"/>; never
        /// executes extension code. Mirrors <c>ExtensionVerbReader</c>'s reader pattern. IO and
        /// reflection failures are swallowed and treated as "no attribute found" — we don't want
        /// a transient file-lock or a corrupt DLL elsewhere in the package to cascade into a
        /// false compatibility violation.
        /// </remarks>
        public static Result CheckAssembly(string extensionDirectory, string currentCoreVersion)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(extensionDirectory);
            ArgumentException.ThrowIfNullOrWhiteSpace(currentCoreVersion);

            if (!Directory.Exists(extensionDirectory))
            {
                throw new DirectoryNotFoundException($"Extension directory not found: {extensionDirectory}");
            }

            var dllPaths = Directory.GetFiles(extensionDirectory, "*.dll", SearchOption.TopDirectoryOnly);
            if (dllPaths.Length == 0)
            {
                return new Result(true, null);
            }

            var runtimeAssemblies = Directory.GetFiles(RuntimeEnvironment.GetRuntimeDirectory(), "*.dll");
            var hostAssemblies = Directory.GetFiles(AppContext.BaseDirectory, "*.dll");
            var allPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in runtimeAssemblies) allPaths.Add(p);
            foreach (var p in hostAssemblies) allPaths.Add(p);
            foreach (var p in dllPaths) allPaths.Add(p);

            var resolver = new PathAssemblyResolver(allPaths);
            using var mlc = new MetadataLoadContext(resolver);

            foreach (var dllPath in dllPaths)
            {
                Assembly assembly;
                try
                {
                    assembly = mlc.LoadFromAssemblyPath(dllPath);
                }
                catch (BadImageFormatException)
                {
                    continue;
                }
                catch (FileLoadException)
                {
                    continue;
                }

                CustomAttributeData? compatAttr;
                try
                {
                    compatAttr = assembly.GetCustomAttributesData()
                        .FirstOrDefault(a => a.AttributeType.FullName == AttributeFullName);
                }
                catch
                {
                    continue;
                }

                if (compatAttr is null)
                {
                    continue;
                }

                // The attribute uses init-only properties, so values arrive in NamedArguments,
                // not constructor arguments. Both nullable strings.
                string? minCore = null;
                string? maxCore = null;
                foreach (var named in compatAttr.NamedArguments)
                {
                    var value = named.TypedValue.Value as string;
                    if (string.Equals(named.MemberName, "MinCoreVersion", StringComparison.Ordinal))
                    {
                        minCore = value;
                    }
                    else if (string.Equals(named.MemberName, "MaxCoreVersion", StringComparison.Ordinal))
                    {
                        maxCore = value;
                    }
                }

                return Check(minCore, maxCore, currentCoreVersion);
            }

            // No DLL declared the attribute. Treat as compatible per the tolerant default.
            return new Result(true, null);
        }
    }
}
