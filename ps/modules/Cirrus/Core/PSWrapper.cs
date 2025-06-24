using System.Collections;
using System.Management.Automation;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace Cirrus.Core;

/// <summary>
/// Represents a PowerShell cmdlet information
/// </summary>
public class CmdletInfo
{
    public string Name { get; set; } = string.Empty;
    public string Uri { get; set; } = string.Empty;
    public string Synopsis { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}


public static class PSWrapper
{
    private const string LibraryName = "cirrus_ps";  // the name of the cargo crate
    private static bool _isInitialized = false;
    private static readonly object _lock = new object();

    static PSWrapper()
    {
        // Load the native library when the class is first used
        LoadNativeLibrary();
    }

    // Define C-style parameter type enumeration (internal use only)
    private enum CParamType
    {
        Null = 0,
        Int64 = 1,
        Uint64 = 2,
        Float64 = 3,
        Boolean = 4,
        String = 5,
        Array = 6,
        Object = 7,
    }

    // Define C-style parameter value structure (internal use only)
    [StructLayout(LayoutKind.Sequential)]
    private struct CParamValue
    {
        public CParamType ParamType;
        public CParamData Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct CParamData
    {
        [FieldOffset(0)]
        public long Int64Val;

        [FieldOffset(0)]
        public ulong Uint64Val;

        [FieldOffset(0)]
        public double Float64Val;

        [FieldOffset(0)]
        public bool BoolVal;

        [FieldOffset(0)]
        public IntPtr StringVal;

        [FieldOffset(0)]
        public CArrayValue ArrayVal;

        [FieldOffset(0)]
        public CObjectValue ObjectVal;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CArrayValue
    {
        public IntPtr Items;
        public UIntPtr Count;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CObjectValue
    {
        public IntPtr Keys;
        public IntPtr Values;
        public UIntPtr Count;
    }

    // C-compatible structures for direct cmdlet array passing (internal use only)
    [StructLayout(LayoutKind.Sequential)]
    private struct CCmdletInfo
    {
        public IntPtr Name;
        public IntPtr Uri;
        public IntPtr Synopsis;
        public IntPtr Description;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CCmdletArray
    {
        public IntPtr Items;
        public UIntPtr Count;
    }

    // P/Invoke declarations for Rust functions
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int cirrus_initialize();

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr cirrus_get_version();

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr cirrus_run_command(
        string uri,
        IntPtr paramKeys,
        IntPtr paramValues,
        UIntPtr paramCount);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    private static extern void cirrus_free_string(IntPtr ptr);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr cirrus_get_builtin_cmdlets();

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    private static extern void cirrus_free_cmdlet_array(IntPtr array);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int cirrus_health_check();

    /// <summary>
    /// Helper class for managing native memory allocations
    /// </summary>
    private class NativeMemoryManager : IDisposable
    {
        private readonly List<IntPtr> _allocatedPointers = new();
        private readonly object _lock = new object();
        private bool _disposed = false;

        public IntPtr AllocateString(string? str)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(NativeMemoryManager));

            if (string.IsNullOrEmpty(str))
                return IntPtr.Zero;

            IntPtr ptr = Marshal.StringToHGlobalAnsi(str);
            lock (_lock)
            {
                if (!_disposed)
                {
                    _allocatedPointers.Add(ptr);
                }
                else
                {
                    Marshal.FreeHGlobal(ptr);
                    throw new ObjectDisposedException(nameof(NativeMemoryManager));
                }
            }
            return ptr;
        }

        public IntPtr AllocateArray<T>(T[] array) where T : struct
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(NativeMemoryManager));

            if (array == null || array.Length == 0)
                return IntPtr.Zero;

            int size = Marshal.SizeOf<T>() * array.Length;
            IntPtr ptr = Marshal.AllocHGlobal(size);

            lock (_lock)
            {
                if (!_disposed)
                {
                    _allocatedPointers.Add(ptr);
                }
                else
                {
                    Marshal.FreeHGlobal(ptr);
                    throw new ObjectDisposedException(nameof(NativeMemoryManager));
                }
            }

            for (int i = 0; i < array.Length; i++)
            {
                IntPtr elementPtr = IntPtr.Add(ptr, i * Marshal.SizeOf<T>());
                Marshal.StructureToPtr(array[i], elementPtr, false);
            }

            return ptr;
        }

        public IntPtr AllocateStringArray(string[] strings)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(NativeMemoryManager));

            if (strings == null || strings.Length == 0)
                return IntPtr.Zero;

            IntPtr[] stringPtrs = new IntPtr[strings.Length];
            for (int i = 0; i < strings.Length; i++)
            {
                stringPtrs[i] = AllocateString(strings[i]);
            }

            IntPtr arrayPtr = Marshal.AllocHGlobal(IntPtr.Size * strings.Length);
            lock (_lock)
            {
                if (!_disposed)
                {
                    _allocatedPointers.Add(arrayPtr);
                }
                else
                {
                    Marshal.FreeHGlobal(arrayPtr);
                    throw new ObjectDisposedException(nameof(NativeMemoryManager));
                }
            }
            Marshal.Copy(stringPtrs, 0, arrayPtr, stringPtrs.Length);

            return arrayPtr;
        }

        public void Dispose()
        {
            lock (_lock)
            {
                if (_disposed)
                    return;

                foreach (IntPtr ptr in _allocatedPointers)
                {
                    if (ptr != IntPtr.Zero)
                    {
                        try
                        {
                            Marshal.FreeHGlobal(ptr);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Warning: Failed to free memory at {ptr:X}: {ex.Message}");
                        }
                    }
                }
                _allocatedPointers.Clear();
                _disposed = true;
            }
        }
    }



    /// <summary>
    /// Convert .NET object to CParamValue
    /// Uses JSON intermediate representation for robust type conversion
    /// </summary>
    private static CParamValue ConvertToCParam(object? value, NativeMemoryManager memoryManager)
    {
        if (value == null)
        {
            return new CParamValue
            {
                ParamType = CParamType.Null,
                Data = new CParamData()
            };
        }

        // Handle basic types that we want to process directly for performance
        switch (value)
        {
            case string strValue:
                return new CParamValue
                {
                    ParamType = CParamType.String,
                    Data = new CParamData { StringVal = memoryManager.AllocateString(strValue) }
                };

            case int intValue:
                return new CParamValue
                {
                    ParamType = CParamType.Int64,
                    Data = new CParamData { Int64Val = intValue }
                };

            case long longValue:
                return new CParamValue
                {
                    ParamType = CParamType.Int64,
                    Data = new CParamData { Int64Val = longValue }
                };

            case uint uintValue:
                return new CParamValue
                {
                    ParamType = CParamType.Uint64,
                    Data = new CParamData { Uint64Val = uintValue }
                };

            case ulong ulongValue:
                return new CParamValue
                {
                    ParamType = CParamType.Uint64,
                    Data = new CParamData { Uint64Val = ulongValue }
                };

            case float floatValue:
                return new CParamValue
                {
                    ParamType = CParamType.Float64,
                    Data = new CParamData { Float64Val = floatValue }
                };

            case decimal decimalValue:
                return new CParamValue
                {
                    ParamType = CParamType.Float64,
                    Data = new CParamData { Float64Val = (double)decimalValue }
                };

            case double doubleValue:
                return new CParamValue
                {
                    ParamType = CParamType.Float64,
                    Data = new CParamData { Float64Val = doubleValue }
                };

            case bool boolValue:
                return new CParamValue
                {
                    ParamType = CParamType.Boolean,
                    Data = new CParamData { BoolVal = boolValue }
                };

            case IList listValue:
                return ConvertArrayToCParam(listValue, memoryManager);

            case IDictionary dictValue:
                return ConvertObjectToCParam(dictValue, memoryManager);

            default:
                // For all other types, use JSON intermediate representation
                // This handles: long, float, decimal, DateTime, Guid, enums, 
                // complex objects, etc. with proper serialization
                return ConvertViJsonToCParam(value, memoryManager);
        }
    }

    /// <summary>
    /// Convert IList to CParamValue
    /// </summary>
    private static CParamValue ConvertArrayToCParam(IList list, NativeMemoryManager memoryManager)
    {
        if (list == null || list.Count == 0)
        {
            return CreateEmptyArrayParam();
        }

        CParamValue[] items = new CParamValue[list.Count];
        for (int i = 0; i < list.Count; i++)
        {
            items[i] = ConvertToCParam(list[i], memoryManager);
        }

        return CreateArrayParam(items, memoryManager);
    }

    /// <summary>
    /// Helper method to create an empty array parameter
    /// </summary>
    private static CParamValue CreateEmptyArrayParam()
    {
        return new CParamValue
        {
            ParamType = CParamType.Array,
            Data = new CParamData
            {
                ArrayVal = new CArrayValue
                {
                    Items = IntPtr.Zero,
                    Count = UIntPtr.Zero
                }
            }
        };
    }

    /// <summary>
    /// Helper method to create an array parameter from items
    /// </summary>
    private static CParamValue CreateArrayParam(CParamValue[] items, NativeMemoryManager memoryManager)
    {
        IntPtr itemsPtr = memoryManager.AllocateArray(items);

        return new CParamValue
        {
            ParamType = CParamType.Array,
            Data = new CParamData
            {
                ArrayVal = new CArrayValue
                {
                    Items = itemsPtr,
                    Count = new UIntPtr((uint)items.Length)
                }
            }
        };
    }

    /// <summary>
    /// Convert IDictionary to CParamValue
    /// </summary>
    private static CParamValue ConvertObjectToCParam(IDictionary dict, NativeMemoryManager memoryManager)
    {
        if (dict == null || dict.Count == 0)
        {
            return CreateEmptyObjectParam();
        }

        string[] keys = new string[dict.Count];
        CParamValue[] values = new CParamValue[dict.Count];

        int index = 0;
        foreach (DictionaryEntry entry in dict)
        {
            keys[index] = entry.Key?.ToString() ?? "";
            values[index] = ConvertToCParam(entry.Value, memoryManager);
            index++;
        }

        return CreateObjectParam(keys, values, memoryManager);
    }

    /// <summary>
    /// Helper method to create an empty object parameter
    /// </summary>
    private static CParamValue CreateEmptyObjectParam()
    {
        return new CParamValue
        {
            ParamType = CParamType.Object,
            Data = new CParamData
            {
                ObjectVal = new CObjectValue
                {
                    Keys = IntPtr.Zero,
                    Values = IntPtr.Zero,
                    Count = UIntPtr.Zero
                }
            }
        };
    }

    /// <summary>
    /// Helper method to create an object parameter from keys and values
    /// </summary>
    private static CParamValue CreateObjectParam(string[] keys, CParamValue[] values, NativeMemoryManager memoryManager)
    {
        IntPtr keysPtr = memoryManager.AllocateStringArray(keys);
        IntPtr valuesPtr = memoryManager.AllocateArray(values);

        return new CParamValue
        {
            ParamType = CParamType.Object,
            Data = new CParamData
            {
                ObjectVal = new CObjectValue
                {
                    Keys = keysPtr,
                    Values = valuesPtr,
                    Count = new UIntPtr((uint)keys.Length)
                }
            }
        };
    }

    /// <summary>
    /// Convert complex objects using JSON intermediate representation
    /// This handles: enums, DateTime, Guid, complex objects, nested structures, etc.
    /// </summary>
    private static CParamValue ConvertViJsonToCParam(object? value, NativeMemoryManager memoryManager)
    {
        if (value == null)
        {
            return new CParamValue { ParamType = CParamType.Null, Data = new CParamData() };
        }

        try
        {
            // Use System.Text.Json to serialize to JsonElement - handles all complex cases
            string json = JsonSerializer.Serialize(value);
            using JsonDocument doc = JsonDocument.Parse(json);
            return ConvertJsonElementToCParam(doc.RootElement, memoryManager);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: JSON conversion failed for {value.GetType().Name}: {ex.Message}");
            // Fallback to string representation
            return new CParamValue
            {
                ParamType = CParamType.String,
                Data = new CParamData { StringVal = memoryManager.AllocateString(value.ToString() ?? "") }
            };
        }
    }

    /// <summary>
    /// Convert JsonElement to CParamValue
    /// </summary>
    private static CParamValue ConvertJsonElementToCParam(JsonElement element, NativeMemoryManager memoryManager)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
                return new CParamValue
                {
                    ParamType = CParamType.Null,
                    Data = new CParamData()
                };

            case JsonValueKind.String:
                return new CParamValue
                {
                    ParamType = CParamType.String,
                    Data = new CParamData { StringVal = memoryManager.AllocateString(element.GetString()) }
                };

            case JsonValueKind.Number:
                if (element.TryGetInt64(out long longVal))
                {
                    if (longVal >= 0)
                    {
                        return new CParamValue
                        {
                            ParamType = CParamType.Uint64,
                            Data = new CParamData { Uint64Val = (ulong)longVal }
                        };
                    }
                    else
                    {
                        return new CParamValue
                        {
                            ParamType = CParamType.Int64,
                            Data = new CParamData { Int64Val = longVal }
                        };
                    }
                }
                else if (element.TryGetUInt64(out ulong ulongVal))
                {
                    return new CParamValue
                    {
                        ParamType = CParamType.Uint64,
                        Data = new CParamData { Uint64Val = ulongVal }
                    };
                }
                else if (element.TryGetDouble(out double doubleVal))
                {
                    return new CParamValue
                    {
                        ParamType = CParamType.Float64,
                        Data = new CParamData { Float64Val = doubleVal }
                    };
                }
                // Fallback for numbers that can't be parsed
                return new CParamValue
                {
                    ParamType = CParamType.String,
                    Data = new CParamData { StringVal = memoryManager.AllocateString(element.ToString()) }
                };

            case JsonValueKind.True:
            case JsonValueKind.False:
                return new CParamValue
                {
                    ParamType = CParamType.Boolean,
                    Data = new CParamData { BoolVal = element.GetBoolean() }
                };

            case JsonValueKind.Array:
                var arrayLength = element.GetArrayLength();
                if (arrayLength == 0)
                {
                    return CreateEmptyArrayParam();
                }

                CParamValue[] items = new CParamValue[arrayLength];
                int index = 0;
                foreach (JsonElement item in element.EnumerateArray())
                {
                    items[index] = ConvertJsonElementToCParam(item, memoryManager);
                    index++;
                }

                return CreateArrayParam(items, memoryManager);

            case JsonValueKind.Object:
                var propertyCount = 0;
                foreach (var _ in element.EnumerateObject())
                {
                    propertyCount++;
                }

                if (propertyCount == 0)
                {
                    return CreateEmptyObjectParam();
                }

                string[] keys = new string[propertyCount];
                CParamValue[] values = new CParamValue[propertyCount];

                int objIndex = 0;
                foreach (JsonProperty property in element.EnumerateObject())
                {
                    keys[objIndex] = property.Name;
                    values[objIndex] = ConvertJsonElementToCParam(property.Value, memoryManager);
                    objIndex++;
                }

                return CreateObjectParam(keys, values, memoryManager);

            default:
                // Fallback to string representation
                return new CParamValue
                {
                    ParamType = CParamType.String,
                    Data = new CParamData { StringVal = memoryManager.AllocateString(element.ToString()) }
                };
        }
    }

    /// <summary>
    /// Execute a Cirrus command with parameters
    /// </summary>
    /// <param name="uri">Command URI</param>
    /// <param name="parameters">Parameters as key-value pairs array</param>
    /// <returns>Result in JSON format</returns>
    public static string RunCommand(string uri, KeyValuePair<string, object>[]? parameters = null)
    {
        EnsureInitialized();

        if (string.IsNullOrEmpty(uri))
            throw new ArgumentException("URI cannot be null or empty", nameof(uri));

        // Prepare parameters
        IntPtr keysPtr = IntPtr.Zero, valuesPtr = IntPtr.Zero;
        uint paramCount = 0;
        NativeMemoryManager? memoryManager = null;

        try
        {
            if (parameters?.Length > 0)
            {
                memoryManager = new NativeMemoryManager();

                var keys = new string[parameters.Length];
                var values = new CParamValue[parameters.Length];

                for (int i = 0; i < parameters.Length; i++)
                {
                    keys[i] = parameters[i].Key;
                    values[i] = ConvertToCParam(parameters[i].Value, memoryManager);
                }

                keysPtr = memoryManager.AllocateStringArray(keys);
                valuesPtr = memoryManager.AllocateArray(values);
                paramCount = (uint)parameters.Length;
            }

            IntPtr ptr = cirrus_run_command(uri, keysPtr, valuesPtr, new UIntPtr(paramCount));

            try
            {
                if (ptr == IntPtr.Zero)
                    return "{}";

                return Marshal.PtrToStringAnsi(ptr) ?? "{}";
            }
            finally
            {
                if (ptr != IntPtr.Zero)
                {
                    cirrus_free_string(ptr);
                }
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to execute command: {ex.Message}", ex);
        }
        finally
        {
            memoryManager?.Dispose();
        }
    }

    /// <summary>
    /// Initialize the Cirrus PowerShell wrapper
    /// </summary>
    public static void Initialize()
    {
        lock (_lock)
        {
            if (_isInitialized)
                return;

            try
            {
                // Test if the library is loaded
                int healthCheck = cirrus_health_check();
                if (healthCheck != 1)
                {
                    throw new InvalidOperationException("Cirrus native library health check failed");
                }

                // Initialize the Rust library
                int result = cirrus_initialize();
                if (result != 0)
                {
                    throw new InvalidOperationException($"Failed to initialize Cirrus library. Error code: {result}");
                }

                _isInitialized = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to initialize Cirrus library: {ex.Message}");
                throw;
            }
        }
    }

    /// <summary>
    /// Get the version of the Cirrus library
    /// </summary>
    /// <returns>Version string</returns>
    public static string GetVersion()
    {
        EnsureInitialized();

        IntPtr ptr = cirrus_get_version();
        if (ptr == IntPtr.Zero)
            return "Unknown";

        try
        {
            return Marshal.PtrToStringAnsi(ptr) ?? "Unknown";
        }
        finally
        {
            cirrus_free_string(ptr);
        }
    }

    /// <summary>
    /// Get all builtin cmdlets
    /// </summary>
    /// <returns>Array of cmdlet information</returns>
    public static CmdletInfo[] GetBuiltinCmdlets()
    {
        EnsureInitialized();

        IntPtr arrayPtr = cirrus_get_builtin_cmdlets();
        if (arrayPtr == IntPtr.Zero)
            return [];

        try
        {
            // Read the array structure
            CCmdletArray array = Marshal.PtrToStructure<CCmdletArray>(arrayPtr);

            if (array.Items == IntPtr.Zero || array.Count == UIntPtr.Zero)
                return [];

            int count = (int)array.Count;
            CmdletInfo[] result = new CmdletInfo[count];

            int structSize = Marshal.SizeOf<CCmdletInfo>();

            for (int i = 0; i < count; i++)
            {
                IntPtr itemPtr = IntPtr.Add(array.Items, i * structSize);
                CCmdletInfo item = Marshal.PtrToStructure<CCmdletInfo>(itemPtr);

                string name = item.Name != IntPtr.Zero ? Marshal.PtrToStringAnsi(item.Name) ?? "" : "";
                string uri = item.Uri != IntPtr.Zero ? Marshal.PtrToStringAnsi(item.Uri) ?? "" : "";
                string synopsis = item.Synopsis != IntPtr.Zero ? Marshal.PtrToStringAnsi(item.Synopsis) ?? "" : "";
                string description = item.Description != IntPtr.Zero ? Marshal.PtrToStringAnsi(item.Description) ?? "" : "";

                result[i] = new CmdletInfo
                {
                    Name = name,
                    Uri = uri,
                    Synopsis = synopsis,
                    Description = description
                };
            }

            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to read cmdlet array: {ex.Message}");
            return [];
        }
        finally
        {
            if (arrayPtr != IntPtr.Zero)
            {
                cirrus_free_cmdlet_array(arrayPtr);
            }
        }
    }

    /// <summary>
    /// Check if the library is initialized
    /// </summary>
    public static bool IsInitialized => _isInitialized;

    private static void EnsureInitialized()
    {
        if (!_isInitialized)
        {
            throw new InvalidOperationException("Cirrus library is not initialized. Call Initialize() first.");
        }
    }

    private static void LoadNativeLibrary()
    {
        try
        {
            // Get the directory where this assembly is located
            string assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";

            // Determine the library filename based on the platform
            string libraryFileName = GetNativeLibraryFileName();
            string libraryPath = Path.Combine(assemblyDir, libraryFileName);

            if (!File.Exists(libraryPath))
            {
                // Try in a subdirectory based on architecture
                string arch = Environment.Is64BitProcess ? "x64" : "x86";
                libraryPath = Path.Combine(assemblyDir, arch, libraryFileName);

                if (!File.Exists(libraryPath))
                {
                    // Try in the parent directory (for development)
                    string parentDir = Directory.GetParent(assemblyDir)?.FullName ?? "";
                    libraryPath = Path.Combine(parentDir, "target", "release", libraryFileName);
                }
            }

            if (File.Exists(libraryPath))
            {
                Console.WriteLine($"Loading native library from: {libraryPath}");
            }
            else
            {
                Console.WriteLine($"Warning: Native library not found. Expected at: {libraryPath}");
                Console.WriteLine("Make sure to build the Rust library first with: cargo build --release");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Failed to locate native library: {ex.Message}");
        }
    }

    private static string GetNativeLibraryFileName()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return $"{LibraryName}.dll";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return $"lib{LibraryName}.so";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return $"lib{LibraryName}.dylib";
        }
        else
        {
            throw new PlatformNotSupportedException("Unsupported platform");
        }
    }
}

/// <summary>
/// Module initializer that automatically calls PSWrapper.Initialize() when the module is loaded
/// </summary>
public class CirrusModuleInitializer : IModuleAssemblyInitializer
{
    /// <summary>
    /// Called when the module assembly is imported
    /// </summary>
    public void OnImport()
    {
        try
        {
            Console.WriteLine("Initializing Cirrus module...");
            PSWrapper.Initialize();
            Console.WriteLine("✓ Cirrus library initialized successfully");
        }
        catch (Exception ex)
        {
            // Log the error but don't throw to avoid breaking module import
            Console.WriteLine($"Warning: Failed to initialize Cirrus library: {ex.Message}");
            Console.WriteLine("Make sure the Rust library (az_ps.dll) is built and available");
        }
    }
}