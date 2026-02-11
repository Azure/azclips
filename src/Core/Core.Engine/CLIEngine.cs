using System.Diagnostics;
using System.Reflection;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Abstractions.Runtime.Handler;
using Core.Implementation;
using Core.Runtime;
using Core.Runtime.Handler;
using Microsoft.Extensions.DependencyInjection;

namespace Core.Engine;

/// <summary>
/// CLI command engine — handles context creation and execution only.
/// Extension loading, command resolution, and parameter binding are handled
/// by the caller (CLI's CommandResolver) before calling Execute().
/// </summary>
public class CLIEngine(IServiceProvider serviceProvider) : IEngine
{
    private readonly HttpClient _httpClient = new();
    private bool _disposed = false;

    public void Execute(IProcessor command, CommandOutputDelegate output, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var loggingService = serviceProvider.GetService<ILoggingService>();
        loggingService?.Info($"Executing command '{command.GetType().Name}'.");

        ICommandContext context = CreateCommandContext(output);
        command.Init(context);
        ExecuteCommandAsync(command, cancellationToken).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Creates a new command context for this invocation.
    /// Each command execution gets its own isolated context.
    /// </summary>
    private ICommandContext CreateCommandContext(CommandOutputDelegate output)
    {
        AuthContext authContext = serviceProvider.GetRequiredService<IAuthContextService>().GetCurrentContextAsync().GetAwaiter().GetResult() ?? new AuthContext() { SubscriptionId = "", TenantId = "" };
        CommandAuthHandler commandAuthHandler = new CommandAuthHandler
        {
            AuthContext = new AuthContext
            {
                SubscriptionId = authContext.SubscriptionId,
                TenantId = authContext.TenantId
            },
            AcquireTokenAsyncDefault = serviceProvider.GetRequiredService<IAuthService>().AcquireTokenSilentlyAsync,
            ApplyOverride = false
        };

        CommandOutputHandler commandOutputHandler = new CommandOutputHandler
        {
            WriteOutput = async (value, ct) => await output(value, ct)
        };
        CommandTelemetryHandler commandTelemetryHandler = new CommandTelemetryHandler
        {
            StartActivity = serviceProvider.GetRequiredService<ITelemetryService>().StartActivity
        };

        IRuntimeHttpPipeline httpPipeline = new HttpPipeline(new HttpClientFactory(_httpClient));
        ICommandContext context = new CommandContext
        {
            CommandOutputHandler = commandOutputHandler,
            CommandAuthHandler = commandAuthHandler,
            CommandTelemetryHandler = commandTelemetryHandler,
            HttpPipeline = httpPipeline.Prepend(commandAuthHandler.SendAsync)
        };
        return context;
    }

    private async Task ExecuteCommandAsync(IProcessor command, CancellationToken cancellationToken = default)
    {
        await Task.Run(async () =>
        {
            await command.ProcessAsync();
        }, cancellationToken);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _httpClient.Dispose();
            }
            _disposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}

public class PropertyParserException : Exception
{
    public PropertyParserException(string message)
        : base(message)
    {
    }

    public PropertyParserException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

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
            // Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Searching for method '{methodName}' in derived type '{derivedType.FullName}'");
            var method = derivedType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
            if (method != null)
            {
                // Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Found method '{methodName}' in type '{derivedType.FullName}'");
                return method;
            }
        }
        // Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Method '{methodName}' not found in type '{type.FullName}' or its derived types.");
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
            // Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Found implementation '{implByName.FullName}' for base type '{baseType.FullName}' by name.");
            yield return implByName;
        }
        else
        {
            // Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] No implementation found for base type '{baseType.FullName}' by name.");
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
            // Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Searching for implementations of '{type.FullName}' by name in assembly '{assembly.FullName}' and namespace '{ns}'");
            return assembly.GetType(ns + "." + type.Name[1..], false, true);
        }
        return null;
    }
}
