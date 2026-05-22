// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Core.Abstractions
{
    /// <summary>
    /// Declares the top-level verb (command group name) that an extension claims.
    /// </summary>
    /// <remarks>
    /// Shape A of Cirrus extension management: exactly one verb per extension, one extension per verb.
    /// The verb is the single source of truth used by:
    /// <list type="bullet">
    ///   <item>the installer (<c>extension add</c>), to detect conflicts before extracting files;</item>
    ///   <item>the dispatcher, to route <c>azv2 &lt;verb&gt; ...</c> invocations to the right extension;</item>
    ///   <item>the build-time hint-table generator, to produce <c>verb-hints.json</c> shipped with the host.</item>
    /// </list>
    /// Verb matching is case-insensitive (<see cref="System.StringComparer.OrdinalIgnoreCase"/>).
    /// See <c>docs/spec/extension-management.md</c> for the full design.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class ExtensionVerbAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new <see cref="ExtensionVerbAttribute"/>.
        /// </summary>
        /// <param name="verb">The top-level verb this extension claims (e.g. <c>"vm"</c>, <c>"group"</c>).</param>
        public ExtensionVerbAttribute(string verb)
        {
            if (string.IsNullOrWhiteSpace(verb))
            {
                throw new ArgumentException("Verb must be a non-empty, non-whitespace string.", nameof(verb));
            }

            Verb = verb;
        }

        /// <summary>
        /// The top-level verb claimed by the extension.
        /// </summary>
        public string Verb { get; }
    }
}
