using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using SharedContracts;
using NewCLIRequestPipelineDelegate = global::System.Action<global::System.Action<global::System.Func<global::System.Net.Http.HttpRequestMessage, global::System.Threading.CancellationToken, global::System.Action, global::System.Func<string, global::System.Threading.CancellationToken, global::System.Func<global::System.EventArgs>, global::System.Threading.Tasks.Task>, global::System.Func<global::System.Net.Http.HttpRequestMessage, global::System.Threading.CancellationToken, global::System.Action, global::System.Func<string, global::System.Threading.CancellationToken, global::System.Func<global::System.EventArgs>, global::System.Threading.Tasks.Task>, global::System.Threading.Tasks.Task<global::System.Net.Http.HttpResponseMessage>>, global::System.Threading.Tasks.Task<global::System.Net.Http.HttpResponseMessage>>>, global::System.Action<global::System.Func<global::System.Net.Http.HttpRequestMessage, global::System.Threading.CancellationToken, global::System.Action, global::System.Func<string, global::System.Threading.CancellationToken, global::System.Func<global::System.EventArgs>, global::System.Threading.Tasks.Task>, global::System.Func<global::System.Net.Http.HttpRequestMessage, global::System.Threading.CancellationToken, global::System.Action, global::System.Func<string, global::System.Threading.CancellationToken, global::System.Func<global::System.EventArgs>, global::System.Threading.Tasks.Task>, global::System.Threading.Tasks.Task<global::System.Net.Http.HttpResponseMessage>>, global::System.Threading.Tasks.Task<global::System.Net.Http.HttpResponseMessage>>>>;


namespace AzTool
{
    public class DllLoader
    {
        /// <summary>
        /// Loads a DLL from file path into memory
        /// </summary>
        /// <param name="dllPath">Path to the DLL file</param>
        /// <returns>Loaded Assembly</returns>
        public static Assembly LoadDllIntoMemory(string dllPath)
        {
            try
            {
                // Method 1: Load from file path
                return Assembly.LoadFrom(dllPath);

                // Alternative Method 2: Load as bytes (completely in memory)
                // byte[] dllBytes = File.ReadAllBytes(dllPath);
                // return Assembly.Load(dllBytes);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Error loading DLL: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Finds all classes in the assembly that implement IProcessor and match the target class name
        /// </summary>
        /// <param name="assembly">The loaded assembly</param>
        /// <param name="targetClassName">The target class name to search for</param>
        /// <returns>List of Type objects that implement IProcessor and match the target class name</returns>
        public static List<Type> FindClassesInheritingFromClassA(Assembly assembly, string targetClassName = "")
        {
            var inheritingClasses = new List<Type>();

            try
            {
                // Get all types from the assembly
                Type[] types = assembly.GetTypes();

                // Find types that implement IProcessor and are not abstract
                foreach (Type type in types)
                {
                    // Check if the type implements IProcessor and is not abstract/interface
                    if (typeof(IProcessor).IsAssignableFrom(type) &&
                        !type.IsAbstract &&
                        !type.IsInterface)
                    {
                        // If targetClassName is specified, filter by class name
                        if (!string.IsNullOrEmpty(targetClassName))
                        {
                            if (type.Name.Equals(targetClassName, StringComparison.OrdinalIgnoreCase))
                            {
                                inheritingClasses.Add(type);
                                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Found target class: {type.Name}");
                            }
                        }
                        else
                        {
                            inheritingClasses.Add(type);
                            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Found class: {type.Name}");
                        }
                    }
                }
            }
            catch (ReflectionTypeLoadException ex)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Error loading types from assembly:"); foreach (Exception? loaderEx in ex.LoaderExceptions)
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}]   - {loaderEx?.Message ?? "Unknown error"}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Error finding inheriting classes: {ex.Message}");
            }

            return inheritingClasses;
        }

        /// <summary>
        /// Finds and calls Init() method on objects in abc.m.d namespace
        /// </summary>
        /// <param name="assembly">The loaded assembly</param>
        public static void CallInitOnAbcMdNamespace(Assembly assembly)
        {
            try
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Searching for objects in abc.m.d namespace...");

                // Get all types from the assembly
                Type[] types = assembly.GetTypes();

                // Find types in abc.m.d namespace
                foreach (Type type in types)
                {
                    if (type.Namespace == "abc.m.d" && !type.IsAbstract && !type.IsInterface)
                    {
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Found class in abc.m.d namespace: {type.Name}");

                        try
                        {
                            // Create instance
                            object? instance = Activator.CreateInstance(type); if (instance != null)
                            {
                                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Created instance of {type.Name}");

                                // Look for Init method
                                MethodInfo? initMethod = type.GetMethod("Init", BindingFlags.Public | BindingFlags.Instance); if (initMethod != null)
                                {
                                    initMethod.Invoke(instance, null);
                                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Successfully called Init() on {type.Name}");
                                }
                                else
                                {
                                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] No Init() method found on {type.Name}");
                                }
                            }
                            else
                            {
                                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Failed to create instance of {type.Name}");
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Error working with {type.Name}: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Error processing abc.m.d namespace: {ex.Message}");
            }
        }/// <summary>
         /// Finds and initializes the Module singleton class that implements IModule interface
         /// </summary>
         /// <param name="assembly">The loaded assembly</param>
        public static void InitializeModuleSingleton(Assembly assembly)
        {
            try
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Searching for Module singleton class that implements IModule...");

                // Get all types from the assembly
                Type[] types = assembly.GetTypes();

                // Find Module class that implements IModule
                Type? moduleType = null;
                foreach (Type type in types)
                {
                    if (type.Name == "Module" &&
                        typeof(IModule).IsAssignableFrom(type) &&
                        !type.IsAbstract &&
                        !type.IsInterface)
                    {
                        moduleType = type;
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Found Module class: {type.FullName}");
                        break;
                    }
                }
                if (moduleType == null)
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] No Module class found that implements IModule interface");
                    return;
                }

                // Try to get singleton instance
                object? moduleInstance = GetModuleSingletonInstance(moduleType);
                if (moduleInstance == null)
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Failed to get Module singleton instance");
                    return;
                }

                // Cast to IModule to access interface methods
                if (moduleInstance is IModule module)
                {
                    // Set up OnNewCLIRequest delegate
                    SetupOnNewCLIRequest(module);
                    // Call Init() method
                    module.Init();
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Successfully called Init() on Module singleton");
                }
                else
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Module instance does not implement IModule interface");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Error initializing Module singleton: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets the singleton instance of the Module class using reflection
        /// </summary>
        /// <param name="moduleType">The Module class type</param>
        /// <returns>The singleton instance or null if not found</returns>
        private static object? GetModuleSingletonInstance(Type moduleType)
        {
            try
            {
                // Method 1: Check for static Instance property
                PropertyInfo? instanceProperty = moduleType.GetProperty("Instance",
                    BindingFlags.Public | BindingFlags.Static);
                if (instanceProperty != null && instanceProperty.PropertyType == moduleType)
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Found static Instance property, getting singleton instance");
                    return instanceProperty.GetValue(null);
                }

                // Method 2: Check for static GetInstance() method
                MethodInfo? getInstanceMethod = moduleType.GetMethod("GetInstance",
                    BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null);
                if (getInstanceMethod != null && getInstanceMethod.ReturnType == moduleType)
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Found static GetInstance() method, calling to get singleton instance");
                    return getInstanceMethod.Invoke(null, null);
                }

                // Method 3: Try to create instance using constructor (fallback)
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] No singleton pattern found, trying to create instance using constructor");
                return Activator.CreateInstance(moduleType);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Error getting Module singleton instance: {ex.Message}");
                return null;
            }
        }
        /// <summary>
        /// Sets up the OnNewCLIRequest delegate on the Module instance
        /// </summary>
        /// <param name="module">The Module instance that implements IModule</param>
        private static void SetupOnNewCLIRequest(IModule module)
        {
            try
            {            // Create a delegate that handles CLI requests
                NewCLIRequestPipelineDelegate cliRequestDelegate = (prepend, append) =>
                {
                    append((httpRequest, cancellationToken, action, signal, next) =>
                    {
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] CLI request processing started");

                        // Add authorization header with bearer token
                        httpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "[Placeholder for token]");
                        //Console.WriteLine("Added Authorization header with bearer token 'abc'");

                        // Process the request and call next step
                        return next(httpRequest, cancellationToken, action, signal);
                    });
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] CLI request pipeline invoked");
                    // Here you can add custom logic for CLI request processing
                    // For example, adding authentication, logging, telemetry, etc.

                    // Example: Add a custom step to the pipeline
                    // prepend((httpRequest, cancellationToken, action, signal) => 
                    // {
                    //     Console.WriteLine("Custom prepend step executed");
                    //     // Process the request and call next step
                    //     return next(httpRequest, cancellationToken, action, signal);
                    // });
                };
                // Assign the delegate to OnNewCLIRequest
                module.OnNewCLIRequest = cliRequestDelegate;
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Successfully set OnNewCLIRequest delegate on Module");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Error setting OnNewCLIRequest delegate: {ex.Message}");
            }
        }
    }

    public abstract record Invocation;

    public record CommandInvocation : Invocation
    {
        public IProcessor Processor { get; }
        public CommandInvocation(IProcessor processor)
        {
            Processor = processor ?? throw new ArgumentNullException(nameof(processor), "Processor cannot be null");
        }
    }

    public record CommandHelpInvocation : Invocation
    {
        public CommandProxy Command { get; }
        public CommandHelpInvocation(CommandProxy command)
        {
            Command = command ?? throw new ArgumentNullException(nameof(command), "Command cannot be null");
        }
    }

    public record ModuleHelpInvocation : Invocation
    {
        public ModuleProxy Module { get; }
        public ModuleHelpInvocation(ModuleProxy module)
        {
            Module = module ?? throw new ArgumentNullException(nameof(module), "Module cannot be null");
        }
    }

    public record AppHelpInvocation : Invocation { }

    /// <summary>
    /// Represents a proxy for a module loaded from an assembly.
    /// This class provides functionality to load commands and create invocations.
    /// </summary>
    public class ModuleProxy
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ModuleProxy"/> class by loading an assembly.
        /// </summary>
        /// <param name="assembly">The assembly to be loaded.</param>
        /// <exception cref="ArgumentNullException">Thrown when the provided assembly is null.</exception>
        public ModuleProxy(Assembly assembly)
        {
            Assembly = assembly ?? throw new ArgumentNullException(nameof(assembly), "Assembly cannot be null");
            Name = assembly.GetName().Name ?? throw new ArgumentException("Assembly name cannot be null", nameof(assembly));
            DllLoader.InitializeModuleSingleton(Assembly);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ModuleProxy"/> class by loading a DLL from the specified path.
        /// </summary>
        /// <param name="dllPath">The path to the DLL file to be loaded.</param>
        /// <exception cref="ArgumentException">Thrown when the provided DLL path is null or empty.</exception>
        /// <exception cref="Exception">Thrown when there is an error loading the DLL.</exception>
        public ModuleProxy(string dllPath)
        {
            if (string.IsNullOrEmpty(dllPath))
            {
                throw new ArgumentException("DLL path cannot be null or empty", nameof(dllPath));
            }
            Assembly = DllLoader.LoadDllIntoMemory(dllPath);
            Name = Assembly.GetName().Name ?? throw new ArgumentException("Assembly name cannot be null", nameof(dllPath));
            DllLoader.InitializeModuleSingleton(Assembly);
        }

        public Assembly Assembly { get; }
        public string Name { get; }

        /// <summary>
        /// Loads a <c>ModuleProxy</c> instance based on command line arguments.
        /// </summary>
        /// <param name="args">
        /// Command line arguments where the first element is the path to the DLL to be loaded.
        /// The arguments should exclude the executable name.
        /// </param>
        public static (ModuleProxy, string[]) LoadFromArgs(string[] args)
        {
            if (args.Length == 0)
            {
                throw new ArgumentException("No DLL path provided in arguments");
            }

            string dllPath = $"{args[0]}.dll";
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Loading module from DLL: {dllPath}");
            return (new ModuleProxy(dllPath), args.Skip(1).ToArray());
        }

        /// <summary>
        /// Gets all commands in the module that implement <see cref="IProcessor"/> and have a <see cref="CommandInfoAttribute"/>.
        /// </summary>
        /// <returns>
        /// An enumerable collection of <see cref="CommandProxy"/> instances representing the commands.
        /// </returns>
        public IEnumerable<CommandProxy> GetCommands()
        {
            foreach (var type in Assembly.GetTypes())
            {
                if (typeof(IProcessor).IsAssignableFrom(type) && !type.IsAbstract && !type.IsInterface)
                {
                    CommandInfoAttribute? commandInfo = type.GetCustomAttribute<CommandInfoAttribute>();
                    if (commandInfo != null)
                    {
                        yield return new CommandProxy(type);
                    }
                    else
                    {
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Type {type.Name} does not have CommandInfoAttribute, skipping");
                    }
                }
            }
        }

        /// <summary>
        /// Parses the command from the provided arguments.
        /// </summary>
        /// <param name="args">Command line arguments to parse.</param>
        /// <returns>
        /// A tuple containing the <see cref="CommandProxy"/> and an array of remaining arguments.
        /// </returns>
        /// <exception cref="CommandParserException">Thrown when an unrecognized command is encountered.</exception>
        public (CommandProxy, string[]) ParseCommand(string[] args)
        {
            string commandString = string.Join(" ", args);
            foreach (var cmd in GetCommands())
            {
                var commandInfo = cmd.CommandInfo;
                // Assumes command names are unique and no command name is a prefix of another.
                // For example, "storage" and "storage account" would conflict in this implementation.
                if (commandString.StartsWith(commandInfo.Name, StringComparison.OrdinalIgnoreCase))
                {
                    // Extract arguments after the command name
                    string[] commandArgs = args.Skip(commandInfo.Name.Split(' ').Length).ToArray();
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Command found: {commandInfo.Name} with arguments: {string.Join(", ", commandArgs)}");
                    return (cmd, commandArgs);
                }
            }
            throw new CommandParserException($"Unrecognized command in Module {Name}: {commandString}");
        }

        /// <summary>
        /// Creates an invocation based on the provided command line arguments.
        /// </summary>
        /// <param name="args">Command line arguments to process.</param>
        /// <returns>
        /// An <see cref="Invocation"/> representing the command to be executed.
        /// </returns>
        /// <exception cref="ArgumentException">Thrown when no command is recognized in the provided arguments.</exception>
        public Invocation CreateInvocation(string[] args)
        {
            var helpFlags = new[] { "--help", "-h", "help", "?", "-?" };
            if (args.Length == 1 && helpFlags.Contains(args[0], StringComparer.OrdinalIgnoreCase))
            {
                return new ModuleHelpInvocation(this);
            }
            var (command, commandArgs) = ParseCommand(args);
            return command.CreateInvocation(commandArgs);
        }
    }

    /// <summary>
    /// Represents a proxy for a command that implements <see cref="IProcessor"/>.
    /// This class provides functionality to instantiate the command and manage its arguments.
    /// </summary>
    public class CommandProxy
    {
        public Type CommandType { get; }
        public CommandInfoAttribute CommandInfo { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="CommandProxy"/> class.
        /// </summary>
        /// <param name="commandType">The type of the command to be proxied.</param>
        /// <exception cref="ArgumentNullException">Thrown when the provided command type is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the command type does not implement <see cref="IProcessor"/> or lacks a <see cref="CommandInfoAttribute"/>.</exception>
        public CommandProxy(Type commandType)
        {
            CommandType = commandType ?? throw new ArgumentNullException(nameof(commandType), "Command type cannot be null");
            if (!typeof(IProcessor).IsAssignableFrom(commandType))
            {
                throw new ArgumentException($"Type {commandType.Name} does not implement IProcessor", nameof(commandType));
            }
            CommandInfo = commandType.GetCustomAttribute<CommandInfoAttribute>()
                ?? throw new ArgumentException($"Type {commandType.Name} does not have CommandInfoAttribute", nameof(commandType));
        }

        /// <summary>
        /// Gets all properties of the command type that are decorated with <see cref="ArgumentInfoAttribute"/>.
        /// </summary>
        /// <returns>
        /// An enumerable collection of <see cref="PropertyInfo"/> representing the command arguments.
        /// </returns>
        /// <remarks>
        /// This method retrieves all public instance properties of the command type that have the <see cref="ArgumentInfoAttribute"/> applied.
        /// It is used to identify the arguments that can be set from command line arguments.
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown when the command type does not implement <see cref="IProcessor"/>.</exception>
        /// <exception cref="ArgumentException">Thrown when the command type does not have any properties with <see cref="ArgumentInfoAttribute"/>.</exception>
        public IEnumerable<PropertyInfo> GetArguments()
        {
            foreach (var property in CommandType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var attr = property.GetCustomAttribute<ArgumentInfoAttribute>();
                if (attr != null)
                {
                    yield return property;
                }
            }
        }

        /// <summary>
        /// Instantiates the command type and sets its properties based on the provided command line arguments.
        /// </summary>
        /// <param name="cmdArgs">The command line arguments to parse and set on the command instance.</param>
        /// <returns>
        /// An instance of <see cref="IProcessor"/> representing the command.
        /// </returns>
        /// <exception cref="InvalidOperationException">Thrown when the command type does not implement <see cref="IProcessor"/>.</exception>
        public IProcessor InstantiateCommand(CommandLineArgs cmdArgs)
        {
            object? instance = Activator.CreateInstance(CommandType);
            if (instance is IProcessor processorInstance)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Created instance of {CommandType.Name}");

                foreach (var property in GetArguments())
                {
                    var value = cmdArgs.PopProperty(property);
                    if (value != null)
                    {
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Setting property {property.Name} to value: {value}");
                        property.SetValue(instance, value);
                    }
                }
                cmdArgs.ThrowRemainingProperties();
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Set properties for {CommandType.Name} from command line arguments");
                return processorInstance;
            }
            else
            {
                throw new InvalidOperationException($"Type {CommandType.Name} does not implement IProcessor");
            }
        }

        /// <summary>
        /// Creates an invocation for the command based on the provided command line arguments.
        /// </summary>
        /// <param name="args">Command line arguments to process.</param>
        /// <returns>
        /// An <see cref="Invocation"/> representing the command to be executed.
        /// </returns>
        /// <exception cref="ArgumentException">Thrown when the command type does not implement <see cref="IProcessor"/>.</exception>
        public Invocation CreateInvocation(string[] args)
        {
            var cmdArgs = CommandLineArgs.Parse(args);
            if (cmdArgs.ShowHelp)
            {
                return new CommandHelpInvocation(this);
            }
            else
            {
                var processor = InstantiateCommand(cmdArgs);
                return new CommandInvocation(processor);
            }
        }
    }

}