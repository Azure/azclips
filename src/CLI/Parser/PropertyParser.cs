using System.Reflection;
using Core.Cli.Exceptions;

namespace Core.Cli.Parser;

public class PropertyParser
{
    public static string GetPropertyRawAlias(PropertyInfo propertyInfo)
    {
        var name = propertyInfo.Name;
        // Convert PascalCase to kebab-case
        var kebabCase = System.Text.RegularExpressions.Regex.Replace(
        name,
        "([a-z])([A-Z])",
        "$1-$2"
        ).ToLower();
        if (kebabCase.Length == 0)
        {
            throw new PropertyParserException($"Property '{propertyInfo.Name}' has an empty name after conversion to kebab-case.");
        }
        return kebabCase;
    }

    public static object ConvertToType(string value, Type targetType, string propertyName)
    {
        switch (targetType)
        {
            case var t when t == typeof(string):
                return value;
            case var t when t == typeof(int):
                try
                {
                    return int.Parse(value);
                }
                catch (FormatException)
                {
                    throw new PropertyParserException($"Property '{propertyName}' expects an integer value, but received '{value}'.");
                }
            case var t when t == typeof(bool):
                try
                {
                    return bool.Parse(value);
                }
                catch (FormatException)
                {
                    throw new PropertyParserException($"Property '{propertyName}' expects a boolean value, but received '{value}'.");
                }
            case var t when t.IsArray:
                var elementType = t.GetElementType() ?? throw new PropertyParserException($"Property '{propertyName}' is an array but its element type could not be determined.");
                try
                {
                    var jsonReader = new System.Text.Json.Utf8JsonReader(System.Text.Encoding.UTF8.GetBytes(value));

                    var jsonElement = System.Text.Json.JsonDocument.ParseValue(ref jsonReader).RootElement;
                    var items = jsonElement.EnumerateArray()
                        .Select(item =>
                        {
                            var itemString = item.ValueKind == System.Text.Json.JsonValueKind.String
                                ? item.GetString()!
                                : item.GetRawText();
                            return ConvertToType(itemString, elementType, propertyName + "[]");
                        })
                        .ToList();
                    var array = System.Array.CreateInstance(elementType, items.Count);
                    for (int i = 0; i < items.Count; i++)
                    {
                        array.SetValue(items[i], i);
                    }
                    return array;
                }
                catch (System.Text.Json.JsonException ex)
                {
                    throw new PropertyParserException($"Property '{propertyName}' expects a JSON array value, but received '{value}'.", ex);
                }
                catch (System.InvalidOperationException ex)
                {
                    throw new PropertyParserException($"Property '{propertyName}' is an array of type '{elementType?.Name}' but could not be parsed from the provided value '{value}'.", ex);
                }
            default:
                var fromJsonMethod = GetMethod(targetType, "FromJsonString");
                if (fromJsonMethod != null)
                {
                    try
                    {
                        return fromJsonMethod.Invoke(null, new object[] { value })!;
                    }
                    catch (TargetInvocationException ex)
                    {
                        throw new PropertyParserException($"Failed to parse property '{propertyName}' with value '{value}': {ex.InnerException?.Message}", ex.InnerException ?? ex);
                    }
                }
                else
                {
                    throw new PropertyParserException($"Property '{propertyName}' is of type '{targetType.Name}' which cannot be parsed from a string.");
                }
        }
    }

    static MethodInfo? GetMethod(Type type, string methodName)
    {
        if (string.IsNullOrWhiteSpace(methodName)) throw new ArgumentException("Method name cannot be null or empty.", nameof(methodName));
        foreach (var derivedType in FindAllDerived(type))
        {
            var method = derivedType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
            if (method != null)
            {
                return method;
            }
        }
        return null;
    }

    static IEnumerable<Type> FindAllDerived(Type baseType)
    {
        // Return the base type itself if it is not abstract or an interface
        if (!baseType.IsAbstract && !baseType.IsInterface)
        {
            yield return baseType;
        }
        // Find implementations by name first
        // This is useful for interfaces and abstract classes where implementations are often named with a prefix of "I"
        var implByName = FindImplementation(baseType);
        if (implByName != null)
        {
            yield return implByName;
        }
        // Now find all types in the assembly that are assignable to the base type
        // In most cases, we will not step into this branch as we expect implementations to be named with a prefix of "I"
        foreach (var type in baseType.Assembly.GetTypes()
            .Where(t => t.IsAssignableTo(baseType) && !t.IsAbstract && !t.IsInterface && t != implByName))
        {
            yield return type;
        }
    }

    static Type? FindImplementation(Type type)
    {
        if ((type.IsAbstract || type.IsInterface) && type.Name.StartsWith("I", StringComparison.Ordinal) && type.Name.Length > 1)
        {
            var ns = type.Namespace ?? throw new ArgumentException($"Type '{type.FullName}' is abstract or an interface, and does not have a namespace to search for implementations.");
            var assembly = type.Assembly;
            return assembly.GetType(ns + "." + type.Name[1..], false, true);
        }
        return null;
    }
}
