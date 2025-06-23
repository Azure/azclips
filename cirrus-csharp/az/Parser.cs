namespace AzTool
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using SharedContracts;

    public class CommandLineArgs
    {
        public List<string> PositionalArgs { get; set; } = new List<string>();
        /// <summary>
        /// Dictionary to hold properties and their values.
        /// </summary>
        /// <remarks>
        /// The key is the property name in kebab-case, and the value is a list of lists of strings.
        /// Each outer list element represents a separate occurrence of the property.
        /// Each inner list contains the values for that specific occurrence.
        /// </remarks>
        public Dictionary<string, List<List<string>>> Properties { get; set; } = new Dictionary<string, List<List<string>>>();

        public bool ShowHelp => Properties.ContainsKey("--help") || Properties.ContainsKey("-h") || Properties.ContainsKey("--?") || Properties.ContainsKey("-?") || PositionalArgs.Contains("help") || PositionalArgs.Contains("?");

        public bool ShowVersion => Properties.ContainsKey("--version");

        public static CommandLineArgs Parse(string[] args)
        {
            if (args == null || args.Length == 0)
            {
                return new CommandLineArgs();
            }

            var commandLineArgs = new CommandLineArgs();
            var currentProperty = string.Empty;
            var currentValues = new List<string>();

            foreach (var arg in args)
            {
                if (arg.StartsWith("-"))
                {
                    // If we were collecting values for a previous property, save it
                    if (!string.IsNullOrEmpty(currentProperty))
                    {
                        if (!commandLineArgs.Properties.ContainsKey(currentProperty))
                        {
                            commandLineArgs.Properties[currentProperty] = new List<List<string>>();
                        }
                        commandLineArgs.Properties[currentProperty].Add(currentValues);
                    }

                    // Start a new property
                    currentProperty = arg.ToLowerInvariant(); // Convert to kebab-case
                    currentValues = new List<string>();
                }
                else
                {
                    if (string.IsNullOrEmpty(currentProperty))
                    {
                        // If no property is set, treat this as a positional argument
                        commandLineArgs.PositionalArgs.Add(arg);
                    }
                    else
                    {
                        // Collect values for the current property
                        currentValues.Add(arg);
                    }
                }
            }

            // Save the last property collected
            if (!string.IsNullOrEmpty(currentProperty))
            {
                if (!commandLineArgs.Properties.ContainsKey(currentProperty))
                {
                    commandLineArgs.Properties[currentProperty] = new List<List<string>>();
                }
                commandLineArgs.Properties[currentProperty].Add(currentValues);
            }

            return commandLineArgs;
        }

        static string[] NormalizeArgumentNames(string[] argumentNames)
        {
            if (argumentNames == null || argumentNames.Length == 0)
            {
                throw new ArgumentException("Argument names cannot be null or empty.", nameof(argumentNames));
            }

            return argumentNames.Select(name =>
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    throw new ArgumentException("Argument name cannot be null or whitespace.", nameof(argumentNames));
                }

                var normalizedName = name.ToLowerInvariant();
                return normalizedName.Length == 1
                    ? $"-{normalizedName}"
                    : $"--{normalizedName}";
            }).ToArray();
        }

        List<List<string>> PopValues(string[] aliases)
        {
            List<List<string>> values = new();
            foreach (var alias in NormalizeArgumentNames(aliases))
            {
                if (Properties.ContainsKey(alias))
                {
                    values.AddRange(Properties[alias]);
                    Properties.Remove(alias);
                }
            }
            return values;
        }

        public T? PopProperty<T>(PropertyInfo propertyInfo)
        {
            return (T?)PopProperty(propertyInfo);
        }

        public object? PopProperty(PropertyInfo propertyInfo)
        {
            // TODO: Implement logic to handle Positional arguments
            ArgumentInfoAttribute attribute = propertyInfo.GetCustomAttribute<ArgumentInfoAttribute>() ?? throw new PropertyParserException($"Property '{propertyInfo.Name}' does not have an ArgumentInfoAttribute and cannot be parsed.");
            string[] aliases = attribute.Aliases.Length > 0 ? attribute.Aliases : new[] { PropertyParser.GetPropertyRawAlias(propertyInfo) };
            var rawValues = PopValues(aliases);
            switch (attribute.Action)
            {
                case ArgumentAction.Store:
                    if (rawValues.Count == 0 && attribute.Required)
                    {
                        throw new PropertyParserException($"Property '{propertyInfo.Name}' is required but no value was provided.");
                    }
                    else if (rawValues.Count == 0)
                    {
                        // If the parameter is not used and is not required, return the default value
                        return null;
                    }
                    string[] values = rawValues.SelectMany(v => v).ToArray();
                    if (values.Length == 0)
                    {
                        // If the parameter key is provided but no value is given, throw an exception
                        throw new PropertyParserException($"Property '{propertyInfo.Name}' expects a value, but no value was provided.");
                    }
                    else if (values.Length > 1)
                    {
                        // TODO: Support for multiple values
                        throw new PropertyParserException($"Property '{propertyInfo.Name}' expects a single value, but received multiple values: {string.Join(", ", values)}.");
                    }
                    return PropertyParser.ConvertToType(values[0], propertyInfo.PropertyType, propertyInfo.Name);
                default:
                    throw new NotSupportedException($"Action '{attribute.Action}' is not supported for property '{propertyInfo.Name}'.");
            }
        }

        public void ThrowRemainingProperties()
        {
            if (PositionalArgs.Count > 0)
            {
                var positionalArgs = string.Join(", ", PositionalArgs);
                throw new PropertyParserException($"Unrecognized positional arguments: {positionalArgs}.");
            }
            if (Properties.Count > 0)
            {
                var remainingProperties = string.Join(", ", Properties.Keys);
                throw new PropertyParserException($"Unrecognized arguments: {remainingProperties}. Please check the command line arguments.");
            }
        }
    }

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

        public static object ConvertToType(string value, System.Type targetType, string propertyName)
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
                    catch (System.FormatException)
                    {
                        throw new PropertyParserException($"Property '{propertyName}' expects an integer value, but received '{value}'.");
                    }
                case var t when t == typeof(bool):
                    try
                    {
                        return bool.Parse(value);
                    }
                    catch (System.FormatException)
                    {
                        throw new PropertyParserException($"Property '{propertyName}' expects a boolean value, but received '{value}'.");
                    }
                case var t when t.IsArray:
                    var elementType = t.GetElementType() ?? throw new PropertyParserException($"Property '{propertyName}' is an array but its element type could not be determined.");
                    try
                    {
                        var jsonReader = new System.Text.Json.Utf8JsonReader(System.Text.Encoding.UTF8.GetBytes(value));

                        var jsonElement = System.Text.Json.JsonDocument.ParseValue(ref jsonReader).RootElement;
                        var items = new System.Collections.Generic.List<object>();
                        foreach (var item in jsonElement.EnumerateArray())
                        {
                            var itemString = item.ValueKind == System.Text.Json.JsonValueKind.String
                                ? item.GetString()!
                                : item.GetRawText();
                            var convertedItem = ConvertToType(itemString, elementType, propertyName + "[]");
                            items.Add(convertedItem);
                        }
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
                    var fromJsonMethod = targetType.GetMethod("FromJsonString", BindingFlags.Public | BindingFlags.Static);
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
    }
}
