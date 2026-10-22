// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.Azclips.Cmdlets.Accounts;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Core.Abstractions;

public class PSArgumentParsingProvider() : IArgumentParsingProvider
{
    public bool ValidateArguments(IDictionary<string, (string, object)> args, IDictionary<string, ParameterMetadata> parameters, out IList<string>? errors)
    {
        errors = null;
        return true;
    }

    public string? ResolveParameterSet(IDictionary<string, (string, object)> parsedArgs, IDictionary<string, ParameterMetadata> parameters, IDictionary<string, ParameterSetMetadata>? parameterSets, Func<IDictionary<string, (string, object)>, IDictionary<string, ParameterMetadata>, IDictionary<string, ParameterSetMetadata>?, (string?, IList<string>?)> defaultResolve, out IList<string>? errors)
    {
        var (resolved, errorList) = defaultResolve(parsedArgs, parameters, parameterSets);
        errors = errorList;
        return resolved;
    }

    public IDictionary<string, (string, object)> ParseArguments(IDictionary<string, object> args, IDictionary<string, ParameterMetadata> parameters, IDictionary<string, string>? parameterAliases)
    {
        var parsedArguments = new Dictionary<string, (string,object)>(StringComparer.OrdinalIgnoreCase);

        foreach (var (invocationName, rawValue) in args)
        {
            // If we have no metadata for this parameter, passthrough.
            if (!parameters.TryGetValue(invocationName, out var metadata))
            {
                parsedArguments[invocationName] = (invocationName, rawValue);
                continue;
            }
            var targetName = metadata.Name;

            // 1. Arg is array/sequence AND parameter expects a collection -> convert.
            if (IsArrayLike(rawValue) && TryGetCollectionElementType(metadata.ParameterType, out var elementType))
            {
                parsedArguments[targetName] = (invocationName, ConvertToCollection(rawValue, metadata.ParameterType, elementType!));
                continue;
            }

            // 2. Otherwise passthrough.
            parsedArguments[targetName] = (invocationName, rawValue);
        }

        return parsedArguments;
    }

    private static bool IsArrayLike(object? value)
        => value is not null and not string && value is Array or IEnumerable;

    private static bool TryGetCollectionElementType(Type type, out Type? elementType)
    {
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

    private static object ConvertToCollection(object value, Type targetType, Type elementType)
    {
        // Recursively convert each element (handles nested arrays like string[][] -> List<List<string>>).
        var converted = new List<object?>();
        foreach (var item in (IEnumerable)value)
        {
            if (item is not null && IsArrayLike(item) && TryGetCollectionElementType(elementType, out var nestedElement))
            {
                converted.Add(ConvertToCollection(item, elementType, nestedElement!));
            }
            else
            {
                converted.Add(item is null || elementType.IsInstanceOfType(item)
                    ? item
                    : Convert.ChangeType(item, elementType));
            }
        }

        // Materialize into the requested target type.
        if (targetType.IsArray)
        {
            var array = Array.CreateInstance(elementType, converted.Count);
            for (int i = 0; i < converted.Count; i++)
            {
                array.SetValue(converted[i], i);
            }
            return array;
        }

        var listType = typeof(List<>).MakeGenericType(elementType);
        var list = (IList)Activator.CreateInstance(listType)!;
        foreach (var item in converted)
        {
            list.Add(item);
        }

        // If the target type is assignable from List<T> (e.g. IEnumerable<T>, IList<T>, ICollection<T>, List<T>), return the list directly.
        if (targetType.IsAssignableFrom(listType))
        {
            return list;
        }

        // Fallback: try Activator with the list as argument (e.g. Collection<T>(IList<T>)).
        try
        {
            return Activator.CreateInstance(targetType, list)!;
        }
        catch
        {
            return list;
        }
    }
}
