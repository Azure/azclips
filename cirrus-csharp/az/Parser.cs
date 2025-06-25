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

        Dictionary<string, List<List<string>>> PopValues(string[] aliases)
        {
            Dictionary<string, List<List<string>>> values = new();
            foreach (var alias in aliases)
            {
                if (Properties.ContainsKey(alias))
                {
                    values[alias] = Properties[alias];
                    Properties.Remove(alias);
                }
            }
            return values;
        }

        public T? PopProperty<T>(PropertyInfo propertyInfo)
        {
            return (T?)PopProperty(propertyInfo);
        }

        /// <summary>
        /// Pops a property from the command line arguments based on the provided PropertyInfo.
        /// It checks for the presence of aliases, validates the action, and converts the values to the appropriate type.
        /// This method is designed to handle various argument actions such as Store, StoreTrue, and Count.
        /// </summary>
        /// <param name="propertyInfo">
        /// The PropertyInfo of the property to be parsed.
        /// </param>
        /// <returns>
        /// Returns the value of the property after parsing the command line arguments.
        /// If the property is not found, returns null or the default value based on the action.
        /// If the property is required but not found, throws PropertyParserException.
        /// If the action is not supported, throws NotSupportedException.
        /// </returns>
        /// <exception cref="PropertyParserException"></exception>
        /// <exception cref="NotSupportedException"></exception>
        /// TODO: Add check for conflicted aliases or positional arguments definitions.
        public object? PopProperty(PropertyInfo propertyInfo)
        {
            string[] aliases;
            Dictionary<string, List<List<string>>> rawValues;
            string[] usedAliases;
            ArgumentInfoAttribute attribute = propertyInfo.GetCustomAttribute<ArgumentInfoAttribute>() ?? throw new PropertyParserException($"Property '{propertyInfo.Name}' does not have an ArgumentInfoAttribute and cannot be parsed.");
            if (!attribute.Positional)
            {
                aliases = NormalizeArgumentNames(attribute.Aliases.Length > 0 ? attribute.Aliases : new[] { PropertyParser.GetPropertyRawAlias(propertyInfo) });
                rawValues = PopValues(aliases);
                usedAliases = rawValues.Keys.ToHashSet().ToArray();
            }
            else
            {
                var alias = $"<{propertyInfo.Name.ToUpperInvariant()}>";
                aliases = NormalizeArgumentNames(new[] { alias });
                usedAliases = aliases;
                rawValues = new Dictionary<string, List<List<string>>>
                {
                    [alias] = new List<List<string>> { PositionalArgs }
                };
                PositionalArgs = new List<string>(); // Clear positional args after processing
            }
            switch (attribute.Action)
            {
                case ArgumentAction.Store:
                    if (rawValues.Count == 0 && attribute.Required)
                    {
                        throw new PropertyParserException($"Argument '{string.Join("/", aliases)}' is required but no value was provided.");
                    }
                    else if (rawValues.Count == 0)
                    {
                        // If the parameter is not used and is not required, return the default value
                        // TODO: Use a Specialized CONSTANT for default values
                        return null;
                    }
                    int? numArgs = attribute.NumArgs switch
                    {
                        "?" => null, // Optional, can be 0 or 1
                        "*" => null, // Variable length, can be 0 or more
                        "+" => null, // Variable length, must be at least 1
                        _ when int.TryParse(attribute.NumArgs, out int n) && n > 0 => n,
                        _ => throw new PropertyParserException($"Invalid NumArgs setting '{attribute.NumArgs}' for property '{propertyInfo.Name}'.")
                    };
                    if ((numArgs.HasValue && numArgs.Value > 1) || attribute.NumArgs == "+")
                    {
                        foreach (var rawValueList in rawValues)
                        {
                            if (rawValueList.Value.Count == 0)
                            {
                                // If the parameter key is provided but no value is given, throw an exception
                                throw new PropertyParserException($"Argument '{rawValueList.Key}' expects a value, but no value was provided.");
                            }
                        }
                    }
                    if (attribute.NumArgs == "?" && rawValues.Count > 1)
                    {
                        // If the parameter is optional but multiple values are provided, throw an exception
                        throw new PropertyParserException($"Argument '{string.Join("/", usedAliases)}' expects at most one value, but received multiple usage.");
                    }
                    string[] values = rawValues.SelectMany(v => v.Value.SelectMany(v => v)).ToArray();

                    foreach (var value in values)
                    {
                        if (attribute.Enums.Length > 0 && !attribute.Enums.Contains(value, StringComparer.OrdinalIgnoreCase))
                        {
                            throw new PropertyParserException($"Argument '{string.Join("/", usedAliases)}' expects one of the following values: {string.Join(", ", attribute.Enums)}, but received '{value}'.");
                        }
                    }
                    if (attribute.NumArgs == "?")
                    {
                        if (values.Length > 1)
                        {
                            // If the parameter is optional but multiple values are provided, throw an exception
                            throw new PropertyParserException($"Argument '{string.Join("/", usedAliases)}' expects at most one value, but received multiple values: {string.Join(", ", values)}.");
                        }
                        if (values.Length == 0)
                        {
                            // If the parameter is optional and no value is provided, return null
                            // TODO: Explicitly set the property to null. Should differentiate between "not set" and "set to null" in the future
                            return null;
                        }
                        return PropertyParser.ConvertToType(values[0], propertyInfo.PropertyType, propertyInfo.Name);
                    }
                    else if (numArgs.HasValue && numArgs.Value == 1)
                    {
                        if (values.Length == 0)
                        {
                            // If the parameter key is provided but no value is given, throw an exception
                            throw new PropertyParserException($"Argument '{string.Join("/", usedAliases)}' expects a value, but no value was provided.");
                        }
                        else if (values.Length > 1)
                        {
                            throw new PropertyParserException($"Argument '{string.Join("/", usedAliases)}' expects a single value, but received multiple values: {string.Join(", ", values)}.");
                        }
                        return PropertyParser.ConvertToType(values[0], propertyInfo.PropertyType, propertyInfo.Name);
                    }
                    else
                    {
                        if (numArgs.HasValue && values.Length != numArgs.Value)
                        {
                            throw new PropertyParserException($"Argument '{string.Join("/", usedAliases)}' expects {numArgs.Value} values, but received {values.Length} value(s).");
                        }
                        if (!propertyInfo.PropertyType.IsArray)
                        {
                            throw new PropertyParserException($"Argument '{string.Join("/", usedAliases)}' with NumArgs='{attribute.NumArgs}' expects multiple values and should be an array, but the property '{propertyInfo.Name}' is of type '{propertyInfo.PropertyType.Name}'.");
                        }
                        return values.Select(v => PropertyParser.ConvertToType(v, propertyInfo.PropertyType, propertyInfo.Name)).ToArray();
                    }
                case ArgumentAction.StoreTrue:
                    if (propertyInfo.PropertyType != typeof(bool))
                    {
                        throw new PropertyParserException($"Argument '{string.Join("/", aliases)}' with Action='StoreTrue' must be of type bool, but the property '{propertyInfo.Name}' is of type '{propertyInfo.PropertyType.Name}'.");
                    }
                    if (rawValues.Count == 0)
                    {
                        // If the parameter is not used, return false
                        return false;
                    }
                    else if (rawValues.Count > 1)
                    {
                        // If the parameter is used multiple times, throw an exception
                        throw new PropertyParserException($"Argument '{string.Join("/", aliases)}' expects at most one occurrence, but received multiple usage.");
                    }
                    else if (rawValues.Count == 1 && rawValues.First().Value.Count > 0)
                    {
                        // If the parameter is used with a value, throw an exception
                        throw new PropertyParserException($"Argument '{string.Join("/", aliases)}' does not expect a value, but received '{string.Join(", ", rawValues.First().Value.SelectMany(v => v))}'.");
                    }
                    // If the parameter is used, return true
                    return true;
                case ArgumentAction.Count:
                    if (propertyInfo.PropertyType != typeof(int))
                    {
                        throw new PropertyParserException($"Argument '{string.Join("/", aliases)}' with Action='Count' must be of type int, but the property '{propertyInfo.Name}' is of type '{propertyInfo.PropertyType.Name}'.");
                    }
                    if (rawValues.Count == 0)
                    {
                        // If the parameter is not used, return 0
                        return 0;
                    }
                    else
                    {
                        foreach (var rawValueOccurance in rawValues)
                        {
                            foreach (var rawValueList in rawValueOccurance.Value)
                            {
                                if (rawValueList.Count > 0)
                                {
                                    // If the parameter is used with a value, throw an exception
                                    throw new PropertyParserException($"Argument '{string.Join("/", rawValueOccurance.Key)}' does not expect a value, but received '{string.Join(", ", rawValueList)}'.");
                                }
                            }
                        }
                        return rawValues.Sum(r => r.Value.Count);
                    }
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
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Searching for method '{methodName}' in derived type '{derivedType.FullName}'");
                var method = derivedType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
                if (method != null)
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Found method '{methodName}' in type '{derivedType.FullName}'");
                    return method;
                }
            }
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Method '{methodName}' not found in type '{type.FullName}' or its derived types.");
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
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Found implementation '{implByName.FullName}' for base type '{baseType.FullName}' by name.");
                yield return implByName;
            }
            else
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] No implementation found for base type '{baseType.FullName}' by name.");
            }
            // Now find all types in the assembly that are assignable to the base type
            // In most cases, we will not step into this branch as we expect implementations to be named with a prefix of "I"
            foreach (var type in baseType.Assembly.GetTypes())
            {
                if (type.IsAssignableTo(baseType) && !type.IsAbstract && !type.IsInterface && type != implByName)
                {
                    yield return type;
                }
            }
        }

        static Type? FindImplementation(Type type)
        {
            if ((type.IsAbstract || type.IsInterface) && type.Name.StartsWith("I", StringComparison.Ordinal) && type.Name.Length > 1)
            {
                var ns = type.Namespace ?? throw new ArgumentException($"Type '{type.FullName}' is abstract or an interface, and does not have a namespace to search for implementations.");
                var assembly = type.Assembly;
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Searching for implementations of '{type.FullName}' by name in assembly '{assembly.FullName}' and namespace '{ns}'");
                return assembly.GetType(ns + "." + type.Name[1..], false, true);
            }
            return null;
        }
    }
}
