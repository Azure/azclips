// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;
using System.Reflection;
using Core.Abstractions;

namespace Core.Configuration.Schema;

internal static class ConfigurationSchemaBuilder
{
    public static IReadOnlyList<ConfigurationKeyMetadata> BuildAll(Assembly assembly)
    {
        var roots = FindSchemaRoots(assembly);

        var metadata = roots.SelectMany(Build).ToList();
        var duplicate = metadata
            .GroupBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new InvalidOperationException($"Duplicate configuration key '{duplicate.Key}'.");
        }

        return metadata;
    }

    public static Dictionary<string, object?> BuildDefaultValues(Assembly assembly)
    {
        var defaults = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var rootType in FindSchemaRoots(assembly))
        {
            var rootPath = rootType.GetCustomAttribute<ConfigurationSectionAttribute>()!.Name;
            var root = Activator.CreateInstance(rootType)
                ?? throw new InvalidOperationException($"Could not create configuration options type '{rootType}'.");
            VisitDefaults(root, rootType, rootPath, defaults);
        }

        return defaults;
    }

    public static IReadOnlyList<ConfigurationKeyMetadata> Build(Type rootType)
    {
        var rootPath = rootType.GetCustomAttribute<ConfigurationSectionAttribute>()?.Name
            ?? throw new InvalidOperationException($"Configuration options type '{rootType}' has no section name.");
        var root = Activator.CreateInstance(rootType)
            ?? throw new InvalidOperationException($"Could not create configuration options type '{rootType}'.");
        var metadata = new List<ConfigurationKeyMetadata>();
        Visit(root, rootType, rootPath, metadata);
        return metadata;
    }

    private static void Visit(object instance, Type type, string path, ICollection<ConfigurationKeyMetadata> metadata)
    {
        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.GetMethod is null || property.GetIndexParameters().Length != 0)
                continue;

            var attribute = property.GetCustomAttribute<ConfigurationKeyAttribute>();
            if (attribute is not null)
            {
                var storageKey = Combine(path, attribute.Key);
                metadata.Add(new ConfigurationKeyMetadata
                {
                    Key = attribute.PublicKey ?? storageKey,
                    StorageKey = storageKey,
                    Description = attribute.Description,
                    DefaultValue = attribute.IncludeDefault ? FormatDefault(property.GetValue(instance)) : null,
                    ValueType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType,
                    AllowedValues = attribute.AllowedValues,
                    ValidationPattern = attribute.ValidationPattern,
                    IsUri = attribute.IsUri,
                    Minimum = attribute.Minimum == int.MinValue ? null : attribute.Minimum,
                    Maximum = attribute.Maximum == int.MaxValue ? null : attribute.Maximum
                });
                continue;
            }

            if (property.PropertyType.IsClass && property.PropertyType != typeof(string))
            {
                var sectionAttribute = property.GetCustomAttribute<ConfigurationSectionAttribute>();
                if (sectionAttribute is { IncludeInSchema: false })
                    continue;

                var child = property.GetValue(instance);
                if (child is not null)
                {
                    var section = sectionAttribute?.Name
                        ?? ToConfigName(property.Name);
                    Visit(child, property.PropertyType, Combine(path, section), metadata);
                }
            }
        }
    }

    private static IEnumerable<Type> FindSchemaRoots(Assembly assembly) =>
        assembly.GetTypes()
            .Where(type => type.IsClass && type.GetCustomAttribute<ConfigurationSectionAttribute>() is { IncludeInSchema: true })
            .OrderBy(type => type.FullName, StringComparer.Ordinal);

    private static void VisitDefaults(object instance, Type type, string path, IDictionary<string, object?> defaults)
    {
        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.GetMethod is null || property.GetIndexParameters().Length != 0)
                continue;

            if (property.GetCustomAttribute<ConfigurationKeyAttribute>() is { } attribute)
            {
                defaults.Add(attribute.PublicKey ?? Combine(path, attribute.Key), property.GetValue(instance));
                continue;
            }

            if (property.PropertyType.IsClass && property.PropertyType != typeof(string) && property.GetValue(instance) is { } child)
            {
                var sectionAttribute = property.GetCustomAttribute<ConfigurationSectionAttribute>();
                if (sectionAttribute is { IncludeInSchema: false })
                    continue;

                var section = sectionAttribute?.Name
                    ?? ToConfigName(property.Name);
                VisitDefaults(child, property.PropertyType, Combine(path, section), defaults);
            }
        }
    }

    private static string Combine(string prefix, string segment) =>
        string.IsNullOrWhiteSpace(prefix) ? segment : $"{prefix}.{segment}";

    private static string ToConfigName(string name) =>
        char.ToLowerInvariant(name[0]) + name[1..];

    private static string? FormatDefault(object? value) => value switch
    {
        null => null,
        bool boolean => boolean.ToString().ToLowerInvariant(),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString()
    };
}
