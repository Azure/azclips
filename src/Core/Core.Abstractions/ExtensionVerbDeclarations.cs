// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Core.Abstractions
{
    /// <summary>
    /// Resolves extension verbs from <see cref="ExtensionVerbAttribute"/> declarations.
    /// </summary>
    public static class ExtensionVerbDeclarations
    {
        /// <summary>
        /// Returns all distinct verbs declared on the runtime type of <paramref name="index"/>.
        /// Distinctness is case-insensitive; first-seen casing is preserved.
        /// </summary>
        public static IReadOnlyList<string> GetDeclaredVerbs(IExtensionIndex index)
        {
            ArgumentNullException.ThrowIfNull(index);
            return GetDeclaredVerbs(index.GetType());
        }

        /// <summary>
        /// Returns all distinct verbs declared via <see cref="ExtensionVerbAttribute"/> on
        /// <paramref name="indexType"/>. Distinctness is case-insensitive; first-seen casing is
        /// preserved.
        /// </summary>
        public static IReadOnlyList<string> GetDeclaredVerbs(Type indexType)
        {
            ArgumentNullException.ThrowIfNull(indexType);

            var verbs = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var attribute in indexType.GetCustomAttributes(typeof(ExtensionVerbAttribute), inherit: false).Cast<ExtensionVerbAttribute>())
            {
                if (seen.Add(attribute.Verb))
                {
                    verbs.Add(attribute.Verb);
                }
            }

            return verbs;
        }
    }
}