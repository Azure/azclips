using System.Reflection;
using Core.Abstractions;
using Core.Cli.Exceptions;

namespace Core.Cli.Parser;

public class ArgumentParser
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

    public static ArgumentParser Parse(string[] args)
    {
        if (args == null || args.Length == 0)
        {
            return new ArgumentParser();
        }

        var commandLineArgs = new ArgumentParser();
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

        return [.. argumentNames.Select(name =>
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Argument name cannot be null or whitespace.", nameof(argumentNames));
            }

            var normalizedName = name.ToLowerInvariant();
            return normalizedName.Length == 1
                ? $"-{normalizedName}"
                : $"--{normalizedName}";
        })];
    }

    Dictionary<string, List<List<string>>> PopValues(string[] aliases)
    {
        Dictionary<string, List<List<string>>> values = new();
        foreach (var alias in NormalizeArgumentNames(aliases))
        {
            if (Properties.TryGetValue(alias, out List<List<string>>? value))
            {
                values[alias] = value;
                Properties.Remove(alias);
            }
        }
        return values;
    }

    public T? PopProperty<T>(PropertyInfo propertyInfo, ArgumentInfoAttribute attribute)
    {
        return (T?)PopProperty(propertyInfo, attribute);
    }

    public object? PopProperty(PropertyInfo propertyInfo, ArgumentInfoAttribute attribute)
    {
        // TODO: Implement logic to handle Positional arguments
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
                    // TODO: Use a Specialized CONSTANT for default values
                    return null;
                }
                string[] values = rawValues.SelectMany(v => v.Value.SelectMany(v => v)).ToArray();
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
