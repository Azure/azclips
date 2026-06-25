// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Core.Abstractions
{
    /// <summary>
    /// Declares a top-level verb (command group name) that an extension claims.
    /// </summary>
    /// <remarks>
    /// An extension may declare <em>one or more</em> top-level verbs by applying this
    /// attribute repeatedly to its
    /// <c>IExtensionIndex</c> implementer. Each declared verb becomes a namespace the extension
    /// contributes leaves to; every <c>s_cliTable</c> key's first token must match one of the
    /// declared verbs.
    /// <para>
    /// The verb is the source of truth used by:
    /// </para>
    /// <list type="bullet">
    ///   <item>the installer (<c>extension add</c>), to detect conflicts before extracting files;</item>
    ///   <item>the dispatcher, to route <c>az &lt;verb&gt; ...</c> invocations to the right extension;</item>
    ///   <item>the build-time hint-table generator, to produce <c>extension-hints.json</c> shipped with the host.</item>
    /// </list>
    /// Verb matching is case-insensitive (<see cref="System.StringComparer.OrdinalIgnoreCase"/>).
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
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
