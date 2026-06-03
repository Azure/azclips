// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Management.Automation;
using System.Reflection;

namespace Cirrus.Help.Tool.Validation
{
    /// <summary>
    /// Static metadata for a single cmdlet: canonical <c>Verb-Noun</c> name plus the union of
    /// <c>[Parameter]</c> property names and their <c>[Alias]</c> entries across all parameter sets.
    /// </summary>
    /// <param name="VerbNoun">Canonical name (e.g. <c>Set-CirrusConfig</c>).</param>
    /// <param name="ParameterNames">Declared <c>[Parameter]</c> property names. Case-insensitive set.</param>
    /// <param name="ParameterAliases">
    /// Alias → canonical parameter name. Aliases come from <c>[Alias]</c> attributes on
    /// <c>[Parameter]</c>-decorated properties. Case-insensitive keys.
    /// </param>
    /// <param name="RequiredParameters">
    /// Parameters that are <c>Mandatory = true</c> across <c>AllParameterSets</c>, meaning the
    /// example must always supply them regardless of which set is invoked. We deliberately do
    /// not flag set-specific mandatory params to avoid false positives in cross-set examples.
    /// </param>
    /// <param name="ParameterTypes">
    /// Canonical parameter name → declaring CLR property type (with <see cref="Nullable{T}"/>
    /// unwrapped). Used by the value-type validator.
    /// </param>
    public sealed record CmdletMetadata(
        string VerbNoun,
        HashSet<string> ParameterNames,
        Dictionary<string, string> ParameterAliases,
        HashSet<string> RequiredParameters,
        Dictionary<string, Type> ParameterTypes);

    /// <summary>
    /// Reflection-based catalogue of every cmdlet exposed by the Cirrus cmdlet assemblies
    /// (<c>Microsoft.Azure.Cirrus.Cmdlets.*.dll</c>). Built once per Help-tool run and consumed
    /// by <see cref="PowerShellExampleSyntaxValidator"/>.
    /// </summary>
    /// <remarks>
    /// We intentionally union parameter names across all <c>ParameterSetName</c> values. The
    /// alternative — tracking parameter sets — would let us reject "you can't use -X and -Y
    /// together", but it can also produce false positives in examples that legitimately mix
    /// parameters from a default set with parameters from another set. Keeping the union is
    /// the conservative choice for now.
    /// </remarks>
    public sealed class CmdletIndex
    {
        private readonly Dictionary<string, CmdletMetadata> _cmdlets =
            new(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyDictionary<string, CmdletMetadata> All => _cmdlets;

        public bool TryGet(string verbNoun, out CmdletMetadata metadata) =>
            _cmdlets.TryGetValue(verbNoun, out metadata!);

        /// <summary>
        /// Reflect over the supplied assemblies and add every <c>[Cmdlet]</c>-decorated public type
        /// to the index. Duplicate <c>Verb-Noun</c> entries across assemblies are ignored (last
        /// writer wins, but our cmdlet names are unique by convention).
        /// </summary>
        public void AddFromAssembly(Assembly assembly)
        {
            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                // Tolerate partial type-load failures (transitive dependency missing) — process
                // only the types we could load.
                types = ex.Types.Where(t => t is not null).Cast<Type>().ToArray();
            }

            foreach (var type in types)
            {
                // Accept top-level public types AND nested public types (the latter is how unit
                // tests declare fixture cmdlets). Abstract base cmdlets are skipped.
                var visible = type.IsPublic || type.IsNestedPublic;
                if (!visible || type.IsAbstract)
                {
                    continue;
                }

                var cmdletAttr = type.GetCustomAttribute<CmdletAttribute>();
                if (cmdletAttr is null)
                {
                    continue;
                }

                var verbNoun = $"{cmdletAttr.VerbName}-{cmdletAttr.NounName}";
                var (names, aliases, required, paramTypes) = ExtractParameters(type);

                _cmdlets[verbNoun] = new CmdletMetadata(verbNoun, names, aliases, required, paramTypes);
            }
        }

        private static (HashSet<string> Names, Dictionary<string, string> Aliases, HashSet<string> Required, Dictionary<string, Type> Types) ExtractParameters(Type cmdletType)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var required = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var types = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);

            // Walk the inheritance chain so we pick up parameters declared on base classes too.
            for (var t = cmdletType; t is not null && t != typeof(object); t = t.BaseType)
            {
                var properties = t.GetProperties(
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

                foreach (var prop in properties)
                {
                    var parameterAttrs = prop.GetCustomAttributes<ParameterAttribute>().ToList();
                    if (parameterAttrs.Count == 0)
                    {
                        continue;
                    }

                    names.Add(prop.Name);
                    types[prop.Name] = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;

                    // A parameter is "always required" only when it is Mandatory in the
                    // AllParameterSets sentinel (or when the cmdlet declares no other sets and
                    // the default attribute is Mandatory). Treating set-specific Mandatory as
                    // required would falsely flag examples that pick a different set.
                    if (parameterAttrs.Any(a => a.Mandatory
                        && string.Equals(a.ParameterSetName, ParameterAttribute.AllParameterSets, StringComparison.Ordinal)))
                    {
                        required.Add(prop.Name);
                    }

                    foreach (var aliasAttr in prop.GetCustomAttributes<AliasAttribute>())
                    {
                        foreach (var alias in aliasAttr.AliasNames)
                        {
                            aliases[alias] = prop.Name;
                        }
                    }
                }
            }

            return (names, aliases, required, types);
        }
    }
}
