namespace AzTool
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using SharedContracts;
    using NewCLIRequestPipelineDelegate = global::System.Action<global::System.Action<global::System.Func<global::System.Net.Http.HttpRequestMessage, global::System.Threading.CancellationToken, global::System.Action, global::System.Func<string, global::System.Threading.CancellationToken, global::System.Func<global::System.EventArgs>, global::System.Threading.Tasks.Task>, global::System.Func<global::System.Net.Http.HttpRequestMessage, global::System.Threading.CancellationToken, global::System.Action, global::System.Func<string, global::System.Threading.CancellationToken, global::System.Func<global::System.EventArgs>, global::System.Threading.Tasks.Task>, global::System.Threading.Tasks.Task<global::System.Net.Http.HttpResponseMessage>>, global::System.Threading.Tasks.Task<global::System.Net.Http.HttpResponseMessage>>>, global::System.Action<global::System.Func<global::System.Net.Http.HttpRequestMessage, global::System.Threading.CancellationToken, global::System.Action, global::System.Func<string, global::System.Threading.CancellationToken, global::System.Func<global::System.EventArgs>, global::System.Threading.Tasks.Task>, global::System.Func<global::System.Net.Http.HttpRequestMessage, global::System.Threading.CancellationToken, global::System.Action, global::System.Func<string, global::System.Threading.CancellationToken, global::System.Func<global::System.EventArgs>, global::System.Threading.Tasks.Task>, global::System.Threading.Tasks.Task<global::System.Net.Http.HttpResponseMessage>>, global::System.Threading.Tasks.Task<global::System.Net.Http.HttpResponseMessage>>>>;

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
        /// Creates instances of the found classes and calls their Process() method
        /// </summary>
        /// <param name="classTypes">List of Type objects to instantiate</param>
        /// <param name="parameters">Properties to set on the instances</param>
        public static async Task CreateInstancesAndCallProcess(List<Type> classTypes, Dictionary<string, List<string>> parameters)
        {
            foreach (Type classType in classTypes)
            {
                try
                {
                    // Create instance of the class
                    object? instance = Activator.CreateInstance(classType);
                    if (instance is IProcessor processorInstance)
                    {
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Created instance of {classType.Name}");

                        foreach (var property in classType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                        {
                            // Check if the property has ArgumentInfoAttribute
                            var attr = property.GetCustomAttribute<ArgumentInfoAttribute>();
                            if (attr != null)
                            {
                                string[] aliases = attr.Aliases.Length > 0 ? attr.Aliases : new[] { PropertyParser.GetPropertyRawAlias(property) };
                                string[] input = aliases.Where(alias => parameters.ContainsKey(alias)).SelectMany(alias => parameters[alias]).ToArray();
                                PropertyParser.ParseProperty(input, instance, property);
                            }
                        }

                        // Call the Process() method
                        await processorInstance.Process();
                    }
                    else
                    {
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Failed to cast {classType.Name} to IProcessor");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Error creating instance of {classType.Name}: {ex.Message}");
                }
            }
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

        // Main execution method that ties everything together
        public static async Task ExecuteWorkflow(string dllPath, string targetClassName = "", Dictionary<string, List<string>>? parameters = null)
        {
            try
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Loading DLL: {dllPath}");
                // Step 1: Load DLL into memory
                Assembly assembly = LoadDllIntoMemory(dllPath);
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] DLL loaded successfully");

                // Step 1.5: Initialize Module singleton
                InitializeModuleSingleton(assembly);

                // Step 2: Find classes that inherit from ClassA
                List<Type> inheritingClasses = FindClassesInheritingFromClassA(assembly, targetClassName);
                if (inheritingClasses.Count == 0)
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] No classes found that implement IProcessor");
                    return;
                }            // Step 3: Create instances and call Process()
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Found {inheritingClasses.Count} classes. Creating instances and calling Process():");
                await CreateInstancesAndCallProcess(inheritingClasses, parameters ?? new Dictionary<string, List<string>>());

                // Step 4: Call Init() on objects in abc.m.d namespace
                // CallInitOnAbcMdNamespace(assembly);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Workflow execution failed: {ex.Message}");
            }
        }
    }

    // Command line argument parser for Azure CLI style commands
    public class CommandLineArgs
    {
        public string ExtensionName { get; set; } = string.Empty;
        public string ResourceName { get; set; } = string.Empty;
        public List<string> SubResources { get; set; } = new List<string>();
        public string Action { get; set; } = string.Empty;
        public Dictionary<string, List<string>> Properties { get; set; } = new Dictionary<string, List<string>>();
        public bool ShowHelp { get; set; }
        public string DllPath { get; set; } = string.Empty;
        public string ClassName { get; set; } = string.Empty;

        public static CommandLineArgs Parse(string[] args)
        {
            var result = new CommandLineArgs();

            if (args.Length == 0)
            {
                result.ShowHelp = true;
                return result;
            }

            try
            {
                // Check for help first
                if (args.Any(arg => arg.ToLower() == "-h" || arg.ToLower() == "-help" ||
                                  arg.ToLower() == "--help" || arg.ToLower() == "/h" || arg.ToLower() == "/?"))
                {
                    result.ShowHelp = true;
                    return result;
                }

                int currentIndex = 0;

                // Parse command structure: az [extension] [resource] [subresource...] [action]
                if (currentIndex < args.Length && args[currentIndex].ToLower() == "az")
                {
                    currentIndex++; // Skip "az"
                }

                // Parse extension name (e.g., "databricks")
                if (currentIndex < args.Length && !args[currentIndex].StartsWith("-"))
                {
                    result.ExtensionName = args[currentIndex];
                    currentIndex++;
                }

                // Parse resource name (e.g., "workspace")
                if (currentIndex < args.Length && !args[currentIndex].StartsWith("-"))
                {
                    result.ResourceName = args[currentIndex];
                    currentIndex++;
                }

                // Parse subresources and action
                var nonParameterArgs = new List<string>();
                while (currentIndex < args.Length && !args[currentIndex].StartsWith("-"))
                {
                    nonParameterArgs.Add(args[currentIndex]);
                    currentIndex++;
                }

                // Last non-parameter argument is the action, others are subresources
                if (nonParameterArgs.Count > 0)
                {
                    result.Action = nonParameterArgs.Last();
                    result.SubResources = nonParameterArgs.Take(nonParameterArgs.Count - 1).ToList();
                }

                // Parse parameters (e.g., --resource-name "value" --name "value")
                while (currentIndex < args.Length)
                {
                    string paramName = args[currentIndex];

                    if (paramName.StartsWith("-"))
                    {
                        if (paramName.StartsWith("--"))
                        {
                            // Double dash parameter
                            string cleanParamName = paramName[2..];
                            currentIndex++;
    
                            if (cleanParamName.Length <= 1)
                            {
                                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Error: Double dash parameter must have more than 1 character after '--'");
                                result.ShowHelp = true;
                                continue;
                            }
                            for (int i = currentIndex; i < args.Length; i++)
                            {
                                if (args[i].StartsWith("-"))
                                {
                                    // If next argument starts with '-', stop here
                                    break;
                                }
                                string paramValue = args[i];
                                if (!result.Properties.ContainsKey(cleanParamName))
                                {
                                    result.Properties[cleanParamName] = new List<string>();
                                }
                                result.Properties[cleanParamName].Add(paramValue);
                                currentIndex++;
                            }
                        }
                        else
                        {
                            // Single dash parameter
                            string cleanParamName = paramName[1..];
                            currentIndex++;
                            
                            if (cleanParamName.Length != 1)
                            {
                                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Error: Single dash parameter must have exactly 1 character after '-'");
                                result.ShowHelp = true;
                                continue;
                            }
                            for (int i = currentIndex; i < args.Length; i++)
                            {
                                if (args[i].StartsWith("-"))
                                {
                                    // If next argument starts with '-', stop here
                                    break;
                                }
                                string paramValue = args[i];
                                if (!result.Properties.ContainsKey(cleanParamName))
                                {
                                    result.Properties[cleanParamName] = new List<string>();
                                }
                                result.Properties[cleanParamName].Add(paramValue);
                                currentIndex++;
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Error: Unexpected argument {args[currentIndex]}. Parameters must start with '-' or '--'");
                        result.ShowHelp = true;
                        currentIndex++;
                    }
                }
                
                // Build DLL path and class name
                BuildDllPathAndClassName(result);            // Validate required fields
                if (string.IsNullOrEmpty(result.ExtensionName))
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Error: Extension name is required");
                    result.ShowHelp = true;
                }
                if (string.IsNullOrEmpty(result.ResourceName))
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Error: Resource name is required");
                    result.ShowHelp = true;
                }
                if (string.IsNullOrEmpty(result.Action))
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Error: Action is required");
                    result.ShowHelp = true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Error parsing command line arguments: {ex.Message}");
                result.ShowHelp = true;
            }
            return result;
        }

        /// <summary>
        /// Builds the DLL path and class name based on parsed command arguments
        /// </summary>
        /// <param name="result">The CommandLineArgs instance to update</param>
        private static void BuildDllPathAndClassName(CommandLineArgs result)
        {
            // Build DLL path: [ExtensionName].dll
            if (!string.IsNullOrEmpty(result.ExtensionName))
            {
                result.DllPath = $"{result.ExtensionName}.dll";
            }

            // Build class name: [ResourceName]_[SubResource1]_[SubResource2]..._[Action]
            if (!string.IsNullOrEmpty(result.ResourceName))
            {
                var classNameParts = new List<string> { result.ResourceName };
                classNameParts.AddRange(result.SubResources);

                // Add action at the end
                if (!string.IsNullOrEmpty(result.Action))
                {
                    classNameParts.Add(result.Action);
                }

                result.ClassName = string.Concat(classNameParts.Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
            }
        }
        public void DisplayParsedInfo()
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Parsed Command Information:");
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Extension Name: {ExtensionName}");
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Resource Name: {ResourceName}");
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Sub Resources: {string.Join(", ", SubResources)}");
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Action: {Action}");
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] DLL Path: {DllPath}");
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Target Class Name: {ClassName}");
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Properties:");
            foreach (var prop in Properties)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}]   {prop.Key}: {prop.Value}");
            }
            Console.WriteLine();
        }
    }

    // Main program class for the Az tool
    public class Program
    {
        public static async Task<int> Main(string[] args)
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Az Tool - Dynamic DLL Processor v1.0");
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] =====================================");

            // Parse command line arguments
            var cmdArgs = CommandLineArgs.Parse(args);

            // Show help if requested or if no arguments provided
            if (cmdArgs.ShowHelp || args.Length == 0)
            {
                ShowHelp();
                return 0; // Return success for help
            }

            // Display parsed command information for debugging
            cmdArgs.DisplayParsedInfo();        // Check if DLL file exists
            if (!File.Exists(cmdArgs.DllPath))
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Error: DLL file not found: {cmdArgs.DllPath}");
                return 1; // Return error code
            }        // Execute the main workflow
            try
            {
                Console.WriteLine();
                await DllLoader.ExecuteWorkflow(cmdArgs.DllPath, cmdArgs.ClassName, cmdArgs.Properties);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Fatal error: {ex.Message}");
                return 1; // Return error code
            }

            Console.WriteLine();
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Execution completed.");
            return 0; // Return success code
        }

        private static void ShowHelp()
        {
            Console.WriteLine("Usage:");
            Console.WriteLine("  az [extension] [resource] [subresource...] [action] [options]");
            Console.WriteLine();
            Console.WriteLine("Parameters:");
            Console.WriteLine("  extension      Name of the extension (determines DLL to load)");
            Console.WriteLine("  resource       Primary resource name");
            Console.WriteLine("  subresource    Optional sub-resource names");
            Console.WriteLine("  action         Action to perform (e.g., show, create, delete)");
            Console.WriteLine("  -help          Show this help message");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  -[property]    Set property value on the target class instance");
            Console.WriteLine();
            Console.WriteLine("Examples:");
            Console.WriteLine("  az databricks workspace show -resourceName \"myworkspace\" -Name \"test\"");
            Console.WriteLine("  az databricks workspace cluster create -Name \"mycluster\" -Size \"Standard_D3_v2\"");
            Console.WriteLine("  az storage account blob list -accountName \"mystorage\" -containerName \"data\"");
            Console.WriteLine(); Console.WriteLine("Description:");
            Console.WriteLine("  This tool loads a .NET DLL file based on the extension name, searches for classes");
            Console.WriteLine("  that implement the IProcessor interface and match the resource pattern, creates");
            Console.WriteLine("  instances of those classes, sets specified properties, and calls their Process() method.");
            Console.WriteLine();
            Console.WriteLine("Class Name Resolution:");
            Console.WriteLine("  The tool searches for classes named: [resource]_[subresource1]_[subresource2]..._[action]");
            Console.WriteLine("  Example: 'workspace show' becomes 'WorkspaceShow', 'workspace cluster create' becomes 'WorkspaceClusterCreate'");
        }
    }
}