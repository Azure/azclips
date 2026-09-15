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
        var roots = assembly.GetTypes()
            .Where(type => type.IsClass && type.GetCustomAttribute<ConfigurationSectionAttribute>() is { IncludeInSchema: true })
            .OrderBy(type => type.FullName, StringComparer.Ordinal);

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
                var key = Combine(path, attribute.Key);
                metadata.Add(new ConfigurationKeyMetadata
                {
                    Key = key,
                    Description = attribute.Description,
                    DefaultValue = FormatDefault(property.GetValue(instance)),
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
                var child = property.GetValue(instance);
                if (child is not null)
                {
                    var section = property.GetCustomAttribute<ConfigurationSectionAttribute>()?.Name
                        ?? ToConfigName(property.Name);
                    Visit(child, property.PropertyType, Combine(path, section), metadata);
                }
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
