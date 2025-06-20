using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Cirrus.Core;

public static class PSWrapper
{
    private const string LibraryName = "az_ps";
    private static bool _isInitialized = false;
    private static readonly object _lock = new object();

    static PSWrapper()
    {
        // Load the native library when the class is first used
        LoadNativeLibrary();
    }

    // Define C-style parameter type enumeration
    public enum CParamType
    {
        String = 0,
        Integer = 1,
        Float = 2,
        Boolean = 3,
        Array = 4,
        Object = 5,
        Null = 6,
    }

    // Define C-style parameter value structure
    [StructLayout(LayoutKind.Sequential)]
    public struct CParamValue
    {
        public CParamType ParamType;
        public CParamData Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct CParamData
    {
        [FieldOffset(0)]
        public IntPtr StringVal;

        [FieldOffset(0)]
        public int IntVal;

        [FieldOffset(0)]
        public double FloatVal;

        [FieldOffset(0)]
        public bool BoolVal;

        [FieldOffset(0)]
        public CArrayValue ArrayVal;

        [FieldOffset(0)]
        public CObjectValue ObjectVal;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct CArrayValue
    {
        public IntPtr Items;
        public UIntPtr Count;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct CObjectValue
    {
        public IntPtr Keys;
        public IntPtr Values;
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
    private static extern int cirrus_health_check();

    /// <summary>
    /// Helper class for managing native memory allocations
    /// </summary>
    private class NativeMemoryManager : IDisposable
    {
        private readonly List<IntPtr> _allocatedPointers = new List<IntPtr>();

        public IntPtr AllocateString(string str)
        {
            if (string.IsNullOrEmpty(str))
                return IntPtr.Zero;

            IntPtr ptr = Marshal.StringToHGlobalAnsi(str);
            _allocatedPointers.Add(ptr);
            return ptr;
        }

        public IntPtr AllocateArray<T>(T[] array) where T : struct
        {
            if (array == null || array.Length == 0)
                return IntPtr.Zero;

            int size = Marshal.SizeOf<T>() * array.Length;
            IntPtr ptr = Marshal.AllocHGlobal(size);
            _allocatedPointers.Add(ptr);

            for (int i = 0; i < array.Length; i++)
            {
                IntPtr elementPtr = IntPtr.Add(ptr, i * Marshal.SizeOf<T>());
                Marshal.StructureToPtr(array[i], elementPtr, false);
            }

            return ptr;
        }

        public IntPtr AllocateStringArray(string[] strings)
        {
            if (strings == null || strings.Length == 0)
                return IntPtr.Zero;

            IntPtr[] stringPtrs = new IntPtr[strings.Length];
            for (int i = 0; i < strings.Length; i++)
            {
                stringPtrs[i] = AllocateString(strings[i]);
            }

            IntPtr arrayPtr = Marshal.AllocHGlobal(IntPtr.Size * strings.Length);
            _allocatedPointers.Add(arrayPtr);
            Marshal.Copy(stringPtrs, 0, arrayPtr, stringPtrs.Length);

            return arrayPtr;
        }

        public void Dispose()
        {
            foreach (IntPtr ptr in _allocatedPointers)
            {
                if (ptr != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(ptr);
                }
            }
            _allocatedPointers.Clear();
        }
    }

    /// <summary>
    /// Convert .NET object to CParamValue
    /// </summary>
    private static CParamValue ConvertToCParam(object value, NativeMemoryManager memoryManager)
    {
        if (value == null)
        {
            return new CParamValue
            {
                ParamType = CParamType.Null,
                Data = new CParamData()
            };
        }

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
                    ParamType = CParamType.Integer,
                    Data = new CParamData { IntVal = intValue }
                };

            case long longValue when longValue >= int.MinValue && longValue <= int.MaxValue:
                return new CParamValue
                {
                    ParamType = CParamType.Integer,
                    Data = new CParamData { IntVal = (int)longValue }
                };

            case double doubleValue:
                return new CParamValue
                {
                    ParamType = CParamType.Float,
                    Data = new CParamData { FloatVal = doubleValue }
                };

            case float floatValue:
                return new CParamValue
                {
                    ParamType = CParamType.Float,
                    Data = new CParamData { FloatVal = floatValue }
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
                // Try to convert other types to string
                return new CParamValue
                {
                    ParamType = CParamType.String,
                    Data = new CParamData { StringVal = memoryManager.AllocateString(value.ToString()) }
                };
        }
    }

    private static CParamValue ConvertArrayToCParam(IList list, NativeMemoryManager memoryManager)
    {
        if (list == null || list.Count == 0)
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

        CParamValue[] items = new CParamValue[list.Count];
        for (int i = 0; i < list.Count; i++)
        {
            items[i] = ConvertToCParam(list[i], memoryManager);
        }

        IntPtr itemsPtr = memoryManager.AllocateArray(items);

        return new CParamValue
        {
            ParamType = CParamType.Array,
            Data = new CParamData
            {
                ArrayVal = new CArrayValue
                {
                    Items = itemsPtr,
                    Count = new UIntPtr((uint)list.Count)
                }
            }
        };
    }

    private static CParamValue ConvertObjectToCParam(IDictionary dict, NativeMemoryManager memoryManager)
    {
        if (dict == null || dict.Count == 0)
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

        string[] keys = new string[dict.Count];
        CParamValue[] values = new CParamValue[dict.Count];

        int index = 0;
        foreach (DictionaryEntry entry in dict)
        {
            keys[index] = entry.Key?.ToString() ?? "";
            values[index] = ConvertToCParam(entry.Value, memoryManager);
            index++;
        }

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
                    Count = new UIntPtr((uint)dict.Count)
                }
            }
        };
    }

    /// <summary>
    /// Execute a Cirrus command with parameters
    /// </summary>
    /// <param name="uri">Command URI</param>
    /// <param name="parameters">Parameters as key-value pairs</param>
    /// <returns>Result in JSON format</returns>
    public static string RunCommand(string uri, IDictionary<string, object> parameters = null)
    {
        EnsureInitialized();

        if (string.IsNullOrEmpty(uri))
            throw new ArgumentException("URI cannot be null or empty", nameof(uri));

        if (parameters == null || parameters.Count == 0)
        {
            // If no parameters, use empty arrays
            IntPtr ptr = cirrus_run_command(uri, IntPtr.Zero, IntPtr.Zero, UIntPtr.Zero);
            if (ptr == IntPtr.Zero)
                return "{}";

            try
            {
                return Marshal.PtrToStringAnsi(ptr) ?? "{}";
            }
            finally
            {
                cirrus_free_string(ptr);
            }
        }

        using var memoryManager = new NativeMemoryManager();

        try
        {
            // Convert parameters to C-style structures
            string[] keys = new string[parameters.Count];
            CParamValue[] values = new CParamValue[parameters.Count];

            int index = 0;
            foreach (var kvp in parameters)
            {
                keys[index] = kvp.Key;
                values[index] = ConvertToCParam(kvp.Value, memoryManager);
                index++;
            }

            IntPtr keysPtr = memoryManager.AllocateStringArray(keys);
            IntPtr valuesPtr = memoryManager.AllocateArray(values);

            IntPtr ptr = cirrus_run_command(
                uri,
                keysPtr,
                valuesPtr,
                new UIntPtr((uint)parameters.Count));

            if (ptr == IntPtr.Zero)
                return "{}";

            try
            {
                return Marshal.PtrToStringAnsi(ptr) ?? "{}";
            }
            finally
            {
                cirrus_free_string(ptr);
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to execute command: {ex.Message}", ex);
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
                Console.WriteLine("Cirrus library initialized successfully");
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