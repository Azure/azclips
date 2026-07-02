// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Management.Automation;
using System.Reflection;

namespace Azclips.Help.Tool.Validation
{
    /// <summary>
    /// Static metadata for one cmdlet: <c>Verb-Noun</c> name, the union of <c>[Parameter]</c>
    /// names and <c>[Alias]</c> entries across parameter sets, required-mandatory-everywhere
    /// names, and parameter type map.
    /// </summary>
    public sealed record CmdletMetadata(
        string VerbNoun,
        HashSet<string> ParameterNames,
        Dictionary<string, string> ParameterAliases,
        HashSet<string> RequiredParameters,
        Dictionary<string, Type> ParameterTypes);

    /// <summary>
    /// Reflection-based catalogue of every cmdlet exposed by Microsoft Azure cmdlet assemblies.
    /// Built once per Help-tool run.
    /// Parameter names are unioned across all parameter sets to avoid false positives in
    /// examples that mix sets.
    /// </summary>
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
