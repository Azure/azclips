// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Cli;

using System.Collections;
using Core.Abstractions;

public class CLIArgumentParsingProvider(PropertyParser propertyParser, ILoggingService loggingService) : IArgumentParsingProvider
{
    public IDictionary<string, object> ParseArguments(IDictionary<string, object> args, IDictionary<string, ParameterMetadata> parameters, IDictionary<string, string>? parameterAliases)
    {
        Dictionary<string, object> parsedArguments = new(StringComparer.OrdinalIgnoreCase);

        // Precompute the canonical parameter names explicitly supplied via named flags so
        // positional binding can detect ambiguity (the same parameter given both positionally and
        // by flag, e.g. `az config set core.output.color --key foo --value bar`) deterministically,
        // regardless of dictionary iteration order.
        var explicitlyNamed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in args.Keys)
        {
            if (string.IsNullOrEmpty(key))
            {
                continue;
            }
            var resolvedKey = key;
            if (!parameters.ContainsKey(resolvedKey) && parameterAliases != null && parameterAliases.TryGetValue(resolvedKey, out var canonical))
            {
                resolvedKey = canonical;
            }
            if (parameters.TryGetValue(resolvedKey, out var metadata))
            {
                explicitlyNamed.Add(metadata.Name);
            }
        }

        foreach (var kvp in args)
        {
            string originalInput = kvp.Key;

            // Multiple positional tokens (e.g. `az config set core.output.color always`) are all
            // collected under the empty-string key by ExtensionParser. Bind each, in order, to the
            // canonical parameter declared for that position: position 0 via the conventional [""]
            // alias, subsequent positions via "1", "2", etc.
            if (string.IsNullOrEmpty(originalInput))
            {
                BindPositionalArguments(kvp.Value, parameters, parameterAliases, explicitlyNamed, parsedArguments);
                continue;
            }

            string key = originalInput;
            object value = kvp.Value;

            // Resolve aliases
            if (!parameters.ContainsKey(key) && parameterAliases != null && parameterAliases.TryGetValue(key, out var canonicalKey))
            {
                loggingService.Info($"Resolving alias '{key}' to canonical parameter '{canonicalKey}'");
                key = canonicalKey;
            }

            if (!parameters.TryGetValue(key, out var parameterMetadata))
            {
                throw new ArgumentException($"Unable to find parameter: {originalInput}");
            }
            //less likely to be null, but we should still check
            if (parameterMetadata == null)
            {
                throw new InvalidOperationException($"Parameter metadata is null for: {originalInput}");
            }
            try
            {
                parsedArguments[parameterMetadata.Name] = ParseArgument(originalInput, value, parameterMetadata.ParameterType);
            }
            catch (Exception ex)
            {
                throw new ArgumentException($"Failed to parse parameter '{originalInput}': {ex.Message}", ex);
            }
        }
        return parsedArguments;
    }

    private void BindPositionalArguments(object positionalSource, IDictionary<string, ParameterMetadata> parameters, IDictionary<string, string>? parameterAliases, HashSet<string> explicitlyNamed, Dictionary<string, object> parsedArguments)
    {
        var positionalValues = FlattenToStrings(positionalSource);
        for (int i = 0; i < positionalValues.Length; i++)
        {
            // Position 0 uses the conventional [""] alias key (already relied on by several
            // commands, e.g. `profile show <name>`); later positions use their 1-based index as
            // a string key, e.g. ["1"] = "value" for `config set <key> <value>`.
            var positionalKey = i == 0 ? string.Empty : i.ToString();
            if (parameterAliases == null || !parameterAliases.TryGetValue(positionalKey, out var canonicalKey) || !parameters.TryGetValue(canonicalKey, out var parameterMetadata))
            {
                throw new ArgumentException($"Unrecognized positional argument(s): {string.Join(" ", positionalValues.Skip(i))}");
            }

            if (explicitlyNamed.Contains(parameterMetadata.Name))
            {
                throw new ArgumentException($"Parameter '{parameterMetadata.InvocationName}' was specified both positionally and by flag ('--{parameterMetadata.InvocationName}').");
            }

            try
            {
                parsedArguments[parameterMetadata.Name] = propertyParser.ConvertToType(parameterMetadata.InvocationName, positionalValues[i], parameterMetadata.ParameterType);
            }
            catch (Exception ex)
            {
                throw new ArgumentException($"Failed to parse parameter '{parameterMetadata.InvocationName}': {ex.Message}", ex);
            }
        }
    }

    private object ParseArgument(string originalInput, object source, Type targetType)
    {
        // Already the expected type
        if (targetType.IsInstanceOfType(source))
        {
            return source;
        }

        // Collection target (string[], List<string>, string[][], List<List<string>>, etc.)
        // CLI raw value is List<List<string>>; convert it into the target collection shape.
        if (TryGetCollectionElementType(targetType, out var elementType))
        {
            return ConvertToCollection(originalInput, source, targetType, elementType!);
        }

        // Scalar target: flatten to string[] and take the first element
        var flattened = FlattenToStrings(source);
        if (flattened.Length == 0)
        {
            if (targetType == typeof(bool))
            {
                return true;
            }
            throw new ArgumentException($"Parameter '{originalInput}' requires a value.");
        }
        return propertyParser.ConvertToType(originalInput, flattened[0], targetType);
    }

    // List<List<string>> -> string[] (flattened)
    private static string[] FlattenToStrings(object source) =>
        source switch
        {
            List<List<string>> listOfLists => listOfLists.SelectMany(l => l).ToArray(),
            List<string> list => list.ToArray(),
            string[] arr => arr,
            string s => [s],
            IEnumerable<string> e => e.ToArray(),
            _ => [source?.ToString() ?? string.Empty],
        };

    private static bool TryGetCollectionElementType(Type type, out Type? elementType)
    {
        if (type == typeof(string))
        {
            elementType = null;
            return false;
        }

        if (type.IsArray)
        {
            elementType = type.GetElementType();
            return elementType is not null;
        }

        if (type.IsGenericType)
        {
            var ienum = new[] { type }
                .Concat(type.GetInterfaces())
                .FirstOrDefault(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IEnumerable<>));
            if (ienum is not null)
            {
                elementType = ienum.GetGenericArguments()[0];
                return true;
            }
        }

        elementType = null;
        return false;
    }

    private object ConvertToCollection(string originalInput, object source, Type targetType, Type elementType)
    {
        // Determine if elementType itself is a collection (nested case: List<List<string>>, string[][])
        bool nested = TryGetCollectionElementType(elementType, out var nestedElementType);

        var items = new List<object?>();

        if (nested)
        {
            // Build outer list of inner collections
            // Source can be List<List<string>>, List<string> (treat as single inner), or scalar (single inner of one)
            IEnumerable<IEnumerable<string>> outer = source switch
            {
                List<List<string>> listOfLists => listOfLists,
                List<string> list => [list],
                string s => [new List<string> { s }],
                _ => [new List<string> { source?.ToString() ?? string.Empty }],
            };
            foreach (var inner in outer)
            {
                items.Add(BuildInnerCollection(inner, elementType, nestedElementType!));
            }
        }
        else
        {
            // Flat collection: flatten everything to strings, then convert each to elementType
            foreach (var s in FlattenToStrings(source))
            {
                items.Add(ConvertScalar(originalInput, s, elementType));
            }
        }

        return Materialize(items, targetType, elementType);
    }

    private object BuildInnerCollection(IEnumerable<string> source, Type targetType, Type elementType)
    {
        var items = new List<object?>();
        foreach (var s in source)
        {
            items.Add(ConvertScalar(string.Empty, s, elementType));
        }
        return Materialize(items, targetType, elementType);
    }

    private object ConvertScalar(string originalInput, string value, Type targetType)
    {
        if (targetType == typeof(string))
        {
            return value;
        }
        return propertyParser.ConvertToType(originalInput, value, targetType);
    }

    private static object Materialize(IList<object?> items, Type targetType, Type elementType)
    {
        if (targetType.IsArray)
        {
            var array = Array.CreateInstance(elementType, items.Count);
            for (int i = 0; i < items.Count; i++)
            {
                array.SetValue(items[i], i);
            }
            return array;
        }

        var listType = typeof(List<>).MakeGenericType(elementType);
        var list = (IList)Activator.CreateInstance(listType)!;
        foreach (var item in items)
        {
            list.Add(item);
        }

        if (targetType.IsAssignableFrom(listType))
        {
            return list;
        }

        try
        {
            return Activator.CreateInstance(targetType, list)!;
        }
        catch
        {
            return list;
        }
    }

    // PowerShell should have validated most of cases
    // This method is mainly for CLI required, allowed values validation
    public IList<string> ValidateArguments(IDictionary<string, object> args, IDictionary<string, ParameterMetadata> parameters)
    {
        var errors = new List<string>();

        // Build lookup by original C# property name (args keys are already resolved to original names)
        var byName = parameters.ToDictionary(kvp => kvp.Value.Name, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase);

        // Check for unknown arguments and allowed values
        foreach (var kvp in args)
        {
            if (!byName.TryGetValue(kvp.Key, out var metadata))
            {
                errors.Add($"Unknown parameter: '{kvp.Key}'");
                continue;
            }

            if (metadata.AllowedValues is { Length: > 0 }
                && kvp.Value is string strValue
                && !metadata.AllowedValues.Contains(strValue, StringComparer.OrdinalIgnoreCase))
            {
                errors.Add($"Parameter '{kvp.Key}' value '{strValue}' is not allowed. Allowed values: {string.Join(", ", metadata.AllowedValues)}");
            }
        }

        // Check for missing required arguments
        foreach (var kvp in byName)
        {
            if (kvp.Value.IsRequired && !args.ContainsKey(kvp.Key))
            {
                errors.Add($"Required parameter '--{kvp.Value.InvocationName}' is missing.");
            }
        }

        return errors;
    }
}