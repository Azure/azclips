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

/// <summary>
/// Represents a plugin component information
/// </summary>
public class ComponentInfo
{
    public string Uri { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
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
    private enum CParamType : int
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

    // C-compatible structure for component info (internal use only)
    [StructLayout(LayoutKind.Sequential)]
    private struct CComponentInfo
    {
        public IntPtr Uri;
        public IntPtr Name;
        public IntPtr Version;
    }

    // C-compatible argument structures for GetCommandArguments
    [StructLayout(LayoutKind.Sequential)]
    private struct CArgumentArray
    {
        public IntPtr Items;
        public UIntPtr Count;
    }

    private enum CArgType : int
    {
        String = 0,
        Number = 1,
        Boolean = 2,
        Object = 3,
        Array = 4,
        AnyType = 5,
        Class = 6,
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CArgumentInfo
    {
        public IntPtr VarName;
        public IntPtr Name;
        public IntPtr Position;
        public IntPtr ShortDescription;
        public IntPtr LongDescription;
        public IntPtr DefaultValue;
        public IntPtr BlankValue;

        public CArgType ArgType;
        public CArgTypeDetails ArgTypeDetails;

        [MarshalAs(UnmanagedType.I1)]
        public bool Internal;
        [MarshalAs(UnmanagedType.I1)]
        public bool Required;
        [MarshalAs(UnmanagedType.I1)]
        public bool Nullable;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CArgEnumItem
    {
        public IntPtr Name;
        public IntPtr Value;
        [MarshalAs(UnmanagedType.I1)]
        public bool Internal;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CArgEnum
    {
        public IntPtr Items;
        public UIntPtr ItemCount;
        [MarshalAs(UnmanagedType.I1)]
        public bool SupportExtension;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CStringArg
    {
        public CStringFormat Format;
        public IntPtr Enum;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CNumberArg
    {
        public CNumberFormat Format;
        public IntPtr Enum;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CBooleanArg
    {
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CAnyTypeArg
    {
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CObjectArgAdditionalProperties
    {
        public CArgumentInfo Item;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CObjectArg
    {
        public IntPtr ClsType;
        public IntPtr AdditionalProps;
        public IntPtr Props;
        public UIntPtr PropsCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CArrayArg
    {
        public IntPtr Item;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CClassArg
    {
        public IntPtr ClsType;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct CArgTypeDetails
    {
        [FieldOffset(0)]
        public CStringArg StringArg;
        [FieldOffset(0)]
        public CNumberArg NumberArg;
        [FieldOffset(0)]
        public CBooleanArg BooleanArg;
        [FieldOffset(0)]
        public CObjectArg ObjectArg;
        [FieldOffset(0)]
        public CArrayArg ArrayArg;
        [FieldOffset(0)]
        public CAnyTypeArg AnyTypeArg;
        [FieldOffset(0)]
        public CClassArg ClassArg;
    }


    private enum CStringFormatType : int
    {
        String = 0,
        Binary = 1,
        Byte = 2,
        Duration = 3,
        Date = 4,
        Time = 5,
        DateTime = 6,
        Uuid = 7,
        Password = 8,
        SubscriptionId = 9,
        ResourceGroupName = 10,
        ResourceId = 11,
        ResourceLocation = 12,
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CStringFormat
    {
        public CStringFormatData FormatData;
        public CStringFormatType FormatType;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct CStringFormatData
    {
        [FieldOffset(0)]
        public CStringFormatImpl StringImpl;
        [FieldOffset(0)]
        public CBinaryFormatImpl BinaryImpl;
        [FieldOffset(0)]
        public CByteFormatImpl ByteImpl;
        [FieldOffset(0)]
        public CDurationFormatImpl DurationImpl;
        [FieldOffset(0)]
        public CDateFormatImpl DateImpl;
        [FieldOffset(0)]
        public CTimeFormatImpl TimeImpl;
        [FieldOffset(0)]
        public CDateTimeFormatImpl DateTimeImpl;
        [FieldOffset(0)]
        public CUuidFormatImpl UuidImpl;
        [FieldOffset(0)]
        public CPasswordFormatImpl PasswordImpl;
        [FieldOffset(0)]
        public CSubscriptionIdFormatImpl SubscriptionIdImpl;
        [FieldOffset(0)]
        public CResourceGroupNameFormatImpl ResourceGroupNameImpl;
        [FieldOffset(0)]
        public CResourceIdFormatImpl ResourceIdImpl;
        [FieldOffset(0)]
        public CResourceLocationFormatImpl ResourceLocationImpl;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CStringFormatImpl
    {
        public IntPtr Pattern;
        public IntPtr MinLength;
        public IntPtr MaxLength;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CBinaryFormatImpl
    {
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CByteFormatImpl
    {
    }

    private enum CDurationProtocol : int
    {
        ISO8601 = 0,
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CDurationFormatImpl
    {
        public CDurationProtocol Protocol;
    }

    private enum CDateProtocol : int
    {
        ISO8601 = 0,
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CDateFormatImpl
    {
        public CDateProtocol Protocol;
    }

    private enum CTimeProtocol : int
    {
        ISO8601 = 0,
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CTimeFormatImpl
    {
        public CTimeProtocol Protocol;
        public IntPtr MsLength;
    }

    private enum CDateTimeProtocol : int
    {
        ISO8601 = 0,
        RFC1123 = 1,
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CDateTimeFormatImpl
    {
        public CDateTimeProtocol Protocol;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CUuidFormatImpl
    {
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CPasswordFormatImpl
    {
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CSubscriptionIdFormatImpl
    {
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CResourceGroupNameFormatImpl
    {
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CResourceIdFormatImpl
    {
        public IntPtr Templates;
        public UIntPtr TemplateCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CResourceLocationFormatImpl
    {
    }

    private enum CNumberFormatType : int
    {
        Int32 = 0,
        Int64 = 1,
        UInt32 = 2,
        UInt64 = 3,
        Float = 4,
        Double = 5,
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CNumberFormat
    {
        public CNumberFormatData FormatData;
        public CNumberFormatType FormatType;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct CNumberFormatData
    {
        [FieldOffset(0)]
        public CInt32FormatImpl Int32Impl;
        [FieldOffset(0)]
        public CInt64FormatImpl Int64Impl;
        [FieldOffset(0)]
        public CUInt32FormatImpl UInt32Impl;
        [FieldOffset(0)]
        public CUInt64FormatImpl UInt64Impl;
        [FieldOffset(0)]
        public CFloatFormatImpl FloatImpl;
        [FieldOffset(0)]
        public CDoubleFormatImpl DoubleImpl;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CInt32FormatImpl
    {
        public IntPtr MinValue;
        public IntPtr MaxValue;
        public IntPtr MultipleOf;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CInt64FormatImpl
    {
        public IntPtr MinValue;
        public IntPtr MaxValue;
        public IntPtr MultipleOf;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CUInt32FormatImpl
    {
        public IntPtr MinValue;
        public IntPtr MaxValue;
        public IntPtr MultipleOf;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CUInt64FormatImpl
    {
        public IntPtr MinValue;
        public IntPtr MaxValue;
        public IntPtr MultipleOf;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CFloatFormatImpl
    {
        public IntPtr MinValue;
        public IntPtr MaxValue;
        public IntPtr MultipleOf;
        [MarshalAs(UnmanagedType.I1)]
        public bool ExclusiveMinimum;
        [MarshalAs(UnmanagedType.I1)]
        public bool ExclusiveMaximum;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CDoubleFormatImpl
    {
        public IntPtr MinValue;
        public IntPtr MaxValue;
        public IntPtr MultipleOf;
        [MarshalAs(UnmanagedType.I1)]
        public bool ExclusiveMinimum;
        [MarshalAs(UnmanagedType.I1)]
        public bool ExclusiveMaximum;
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
    private static extern IntPtr cirrus_get_component_cmdlets(string uri);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    private static extern void cirrus_free_cmdlet_array(IntPtr array);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int cirrus_health_check();

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr cirrus_get_command_arguments(string uri);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    private static extern void cirrus_free_argument_array(IntPtr array);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    private static extern UIntPtr cirrus_get_argument_info_size();

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    private static extern UIntPtr cirrus_get_arg_type_details_size();

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr cirrus_import_plugin_component(string path);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    private static extern void cirrus_free_component_info(IntPtr info);

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
    public static string RunCommand(string uri, KeyValuePair<string, object?>[]? parameters = null)
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
    public static string GetCirrusVersion()
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
            return ConvertCmdletArray(arrayPtr);
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
    /// Get cmdlets for a specific component
    /// </summary>
    /// <param name="uri">Component URI</param>
    /// <returns>Array of cmdlet information</returns>
    public static CmdletInfo[] GetComponentCmdlets(string uri)
    {
        EnsureInitialized();

        if (string.IsNullOrEmpty(uri))
            throw new ArgumentException("URI cannot be null or empty", nameof(uri));

        IntPtr arrayPtr = cirrus_get_component_cmdlets(uri);
        if (arrayPtr == IntPtr.Zero)
            return [];

        try
        {
            return ConvertCmdletArray(arrayPtr);
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
    /// Import a plugin component from the specified path
    /// </summary>
    /// <param name="path">Path to the plugin component file</param>
    /// <returns>Component information</returns>
    public static ComponentInfo? ImportPluginComponent(string path)
    {
        EnsureInitialized();

        if (string.IsNullOrEmpty(path))
            throw new ArgumentException("Path cannot be null or empty", nameof(path));

        IntPtr componentPtr = cirrus_import_plugin_component(path);
        if (componentPtr == IntPtr.Zero)
            return null;

        try
        {
            // Read the component structure
            CComponentInfo component = Marshal.PtrToStructure<CComponentInfo>(componentPtr);

            string uri = component.Uri != IntPtr.Zero ? Marshal.PtrToStringAnsi(component.Uri) ?? "" : "";
            string name = component.Name != IntPtr.Zero ? Marshal.PtrToStringAnsi(component.Name) ?? "" : "";
            string version = component.Version != IntPtr.Zero ? Marshal.PtrToStringAnsi(component.Version) ?? "" : "";

            return new ComponentInfo
            {
                Uri = uri,
                Name = name,
                Version = version
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to read component info: {ex.Message}");
            return null;
        }
        finally
        {
            if (componentPtr != IntPtr.Zero)
            {
                cirrus_free_component_info(componentPtr);
            }
        }
    }

    /// <summary>
    /// Helper method to convert CCmdletArray to CmdletInfo[]
    /// </summary>
    private static CmdletInfo[] ConvertCmdletArray(IntPtr arrayPtr)
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

    /// <summary>
    /// Get command arguments for the specified URI
    /// </summary>
    /// <param name="uri">Command URI</param>
    /// <returns>Array of command arguments</returns>
    public static CrsArg[] GetCommandArguments(string uri)
    {
        EnsureInitialized();

        if (string.IsNullOrEmpty(uri))
            throw new ArgumentException("URI cannot be null or empty", nameof(uri));

        IntPtr arrayPtr = cirrus_get_command_arguments(uri);
        if (arrayPtr == IntPtr.Zero)
            return [];

        try
        {
            // Read the array structure
            CArgumentArray array = Marshal.PtrToStructure<CArgumentArray>(arrayPtr);

            if (array.Items == IntPtr.Zero || array.Count == UIntPtr.Zero)
                return [];

            int count = (int)array.Count;
            CrsArg[] result = new CrsArg[count];

            int structSize = Marshal.SizeOf<CArgumentInfo>();

            for (int i = 0; i < count; i++)
            {
                try
                {
                    IntPtr itemPtr = IntPtr.Add(array.Items, i * structSize);
                    CArgumentInfo item = Marshal.PtrToStructure<CArgumentInfo>(itemPtr);

                    // Validate the item before processing
                    if (!IsValidCArgumentInfo(item))
                    {
                        Console.WriteLine($"Item {i} appears to be corrupted, skipping");
                        result[i] = new CrsAnyTypeArg(); // Use fallback
                        continue;
                    }

                    result[i] = ConvertCArgumentInfoToCrsArg(item);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing item {i}: {ex.Message}");
                    Console.WriteLine($"Exception type: {ex.GetType().Name}");
                    // Use fallback instead of throwing
                    result[i] = new CrsAnyTypeArg();
                    Console.WriteLine($"Using fallback for item {i}");
                }
            }
            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to read argument array: {ex.Message}");
            return [];
        }
        finally
        {
            if (arrayPtr != IntPtr.Zero)
            {
                cirrus_free_argument_array(arrayPtr);
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

    /// <summary>
    /// Validate if CArgumentInfo appears to be valid (not corrupted)
    /// </summary>
    private static bool IsValidCArgumentInfo(CArgumentInfo item)
    {
        // Check if ArgType is within valid range
        if (!Enum.IsDefined(typeof(CArgType), item.ArgType))
        {
            Console.WriteLine($"Invalid ArgType: {item.ArgType}");
            return false;
        }

        // Check for obviously invalid pointer values (too small to be valid pointers)
        // But allow zero (null) pointers
        var pointers = new[] { item.VarName, item.Name, item.ShortDescription, item.LongDescription, item.DefaultValue, item.BlankValue };
        foreach (var ptr in pointers)
        {
            if (ptr != IntPtr.Zero && ptr.ToInt64() > 0 && ptr.ToInt64() < 65536)
            {
                Console.WriteLine($"Suspicious pointer value: {ptr}");
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Convert CArgumentInfo to CrsArg
    /// </summary>
    private static CrsArg ConvertCArgumentInfoToCrsArg(CArgumentInfo item)
    {
        // Helper function to safely read string from IntPtr
        string ReadString(IntPtr ptr) => ptr != IntPtr.Zero ? Marshal.PtrToStringAnsi(ptr) ?? "" : "";

        // Helper function to read int from IntPtr
        int? ReadInt(IntPtr ptr)
        {
            try
            {
                if (ptr == IntPtr.Zero)
                {
                    return null;
                }
                var result = Marshal.ReadInt32(ptr);
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ReadInt: Error reading from ptr {ptr}: {ex.Message}");
                return null;
            }
        }

        // Helper function to parse JSON default value
        CrsArgDefault? ParseDefault(IntPtr ptr)
        {
            var jsonStr = ReadString(ptr);
            if (string.IsNullOrEmpty(jsonStr))
                return null;

            try
            {
                using var doc = JsonDocument.Parse(jsonStr);
                var value = ConvertJsonElementToObject(doc.RootElement);
                return new CrsArgDefault { Value = value };
            }
            catch
            {
                return null;
            }
        }

        // Helper function to parse JSON default value
        CrsArgBlank? ParseBlank(IntPtr ptr)
        {
            var jsonStr = ReadString(ptr);
            if (string.IsNullOrEmpty(jsonStr))
                return null;

            try
            {
                using var doc = JsonDocument.Parse(jsonStr);
                var value = ConvertJsonElementToObject(doc.RootElement);
                return new CrsArgBlank { Value = value };
            }
            catch
            {
                return null;
            }
        }

        // Create the specific argument type first
        CrsArg result = item.ArgType switch
        {
            CArgType.String => ConvertCStringArg(item.ArgTypeDetails.StringArg),
            CArgType.Number => ConvertCNumberArg(item.ArgTypeDetails.NumberArg),
            CArgType.Boolean => new CrsBooleanArg(),
            CArgType.AnyType => new CrsAnyTypeArg(),
            CArgType.Class => new CrsClsArg
            {
                ClsType = ReadString(item.ArgTypeDetails.ClassArg.ClsType)
            },
            CArgType.Array => ConvertCArrayArg(item.ArgTypeDetails.ArrayArg),
            CArgType.Object => ConvertCObjectArg(item.ArgTypeDetails.ObjectArg),
            _ => throw new NotSupportedException($"Unsupported argument type: {item.ArgType}")
        };

        // Apply common properties to all argument types
        result.VarName = ReadString(item.VarName);
        result.Name = ReadString(item.Name);
        result.Position = ReadInt(item.Position);
        result.Internal = item.Internal;
        result.Required = item.Required;
        result.Nullable = item.Nullable;
        result.ShortDescription = ReadString(item.ShortDescription);
        result.LongDescription = ReadString(item.LongDescription);
        result.Blank = ParseBlank(item.BlankValue);
        result.Default = ParseDefault(item.DefaultValue);

        return result;
    }

    private static object ConvertJsonElementToObject(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString()!,
            JsonValueKind.Number when element.TryGetInt32(out var intVal) => intVal,
            JsonValueKind.Number when element.TryGetInt64(out var longVal) => longVal,
            JsonValueKind.Number when element.TryGetDouble(out var doubleVal) => doubleVal,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null!,
            _ => element.ToString()
        };
    }

    private static CrsStringArg ConvertCStringArg(CStringArg cStringArg)
    {
        try
        {
            var format = ConvertCStringFormat(cStringArg.Format);
            var enumVal = ConvertCArgEnum(cStringArg.Enum);
            var result = new CrsStringArg
            {
                Format = format,
                Enum = enumVal
            };
            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ConvertCStringArg: Error during conversion: {ex.Message}");
            Console.WriteLine($"ConvertCStringArg: Stack trace: {ex.StackTrace}");
            return new CrsStringArg(); // Return default on error
        }
    }

    private static CrsStringFormat ConvertCStringFormat(CStringFormat format)
    {
        try
        {
            CrsStringFormat result = format.FormatType switch
            {
                CStringFormatType.String => ConvertCStringFormatImpl(format.FormatData.StringImpl),
                CStringFormatType.Binary => new CrsBinaryFormat(),
                CStringFormatType.Byte => new CrsByteFormat(),
                CStringFormatType.Duration => ConvertCDurationFormat(format.FormatData.DurationImpl),
                CStringFormatType.Date => ConvertCDateFormat(format.FormatData.DateImpl),
                CStringFormatType.Time => ConvertCTimeFormat(format.FormatData.TimeImpl),
                CStringFormatType.DateTime => ConvertCDateTimeFormat(format.FormatData.DateTimeImpl),
                CStringFormatType.Uuid => new CrsUuidFormat(),
                CStringFormatType.Password => new CrsPasswordFormat(),
                CStringFormatType.SubscriptionId => new CrsSubscriptionIdFormat(),
                CStringFormatType.ResourceGroupName => new CrsResourceGroupNameFormat(),
                CStringFormatType.ResourceId => ConvertCResourceIdFormat(format.FormatData.ResourceIdImpl),
                CStringFormatType.ResourceLocation => new CrsResourceLocationFormat(),
                _ => new CrsStringFormatImpl()
            };
            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ConvertCStringFormat: Error during conversion: {ex.Message}");
            Console.WriteLine($"ConvertCStringFormat: Stack trace: {ex.StackTrace}");
            return new CrsStringFormatImpl(); // Return default on error
        }
    }

    private static CrsStringFormatImpl ConvertCStringFormatImpl(CStringFormatImpl impl)
    {
        string ReadString(IntPtr ptr) => ptr != IntPtr.Zero ? Marshal.PtrToStringAnsi(ptr) ?? "" : "";
        uint? ReadUInt(IntPtr ptr) => ptr != IntPtr.Zero ? (uint?)Marshal.ReadInt32(ptr) : null;

        return new CrsStringFormatImpl
        {
            Pattern = ReadString(impl.Pattern),
            MinLength = ReadUInt(impl.MinLength),
            MaxLength = ReadUInt(impl.MaxLength)
        };
    }

    private static CrsDurationFormat ConvertCDurationFormat(CDurationFormatImpl impl)
    {
        return new CrsDurationFormat
        {
            ProtocolType = impl.Protocol switch
            {
                CDurationProtocol.ISO8601 => CrsDurationFormat.Protocol.ISO8601,
                _ => CrsDurationFormat.Protocol.ISO8601
            }
        };
    }

    private static CrsDateFormat ConvertCDateFormat(CDateFormatImpl impl)
    {
        return new CrsDateFormat
        {
            ProtocolType = impl.Protocol switch
            {
                CDateProtocol.ISO8601 => CrsDateFormat.Protocol.ISO8601,
                _ => CrsDateFormat.Protocol.ISO8601
            }
        };
    }

    private static CrsTimeFormat ConvertCTimeFormat(CTimeFormatImpl impl)
    {
        uint? ReadUInt(IntPtr ptr) => ptr != IntPtr.Zero ? (uint?)Marshal.ReadInt32(ptr) : null;

        return new CrsTimeFormat
        {
            ProtocolType = impl.Protocol switch
            {
                CTimeProtocol.ISO8601 => CrsTimeFormat.Protocol.ISO8601,
                _ => CrsTimeFormat.Protocol.ISO8601
            },
            MsLength = ReadUInt(impl.MsLength)
        };
    }

    private static CrsDateTimeFormat ConvertCDateTimeFormat(CDateTimeFormatImpl impl)
    {
        return new CrsDateTimeFormat
        {
            ProtocolType = impl.Protocol switch
            {
                CDateTimeProtocol.ISO8601 => CrsDateTimeFormat.Protocol.ISO8601,
                CDateTimeProtocol.RFC1123 => CrsDateTimeFormat.Protocol.RFC1123,
                _ => CrsDateTimeFormat.Protocol.ISO8601
            }
        };
    }

    private static CrsResourceIdFormat ConvertCResourceIdFormat(CResourceIdFormatImpl impl)
    {
        var templates = new List<string>();

        if (impl.Templates != IntPtr.Zero && impl.TemplateCount != UIntPtr.Zero)
        {
            int count = (int)impl.TemplateCount;
            for (int i = 0; i < count; i++)
            {
                IntPtr templatePtr = Marshal.ReadIntPtr(impl.Templates, i * IntPtr.Size);
                if (templatePtr != IntPtr.Zero)
                {
                    string template = Marshal.PtrToStringAnsi(templatePtr) ?? "";
                    if (!string.IsNullOrEmpty(template))
                    {
                        templates.Add(template);
                    }
                }
            }
        }

        return new CrsResourceIdFormat
        {
            Templates = templates
        };
    }

    private static CrsNumberArg ConvertCNumberArg(CNumberArg cNumberArg)
    {
        return new CrsNumberArg
        {
            Format = ConvertCNumberFormat(cNumberArg.Format),
            Enum = ConvertCArgEnum(cNumberArg.Enum)
        };
    }

    private static CrsNumberFormat ConvertCNumberFormat(CNumberFormat format)
    {
        return format.FormatType switch
        {
            CNumberFormatType.Int32 => ConvertCInt32Format(format.FormatData.Int32Impl),
            CNumberFormatType.Int64 => ConvertCInt64Format(format.FormatData.Int64Impl),
            CNumberFormatType.UInt32 => ConvertCUInt32Format(format.FormatData.UInt32Impl),
            CNumberFormatType.UInt64 => ConvertCUInt64Format(format.FormatData.UInt64Impl),
            CNumberFormatType.Float => ConvertCFloatFormat(format.FormatData.FloatImpl),
            CNumberFormatType.Double => ConvertCDoubleFormat(format.FormatData.DoubleImpl),
            _ => new CrsInt64Format()
        };
    }

    private static CrsInt32Format ConvertCInt32Format(CInt32FormatImpl impl)
    {
        int? ReadInt(IntPtr ptr) => ptr != IntPtr.Zero ? (int?)Marshal.ReadInt32(ptr) : null;

        return new CrsInt32Format
        {
            Minimum = ReadInt(impl.MinValue),
            Maximum = ReadInt(impl.MaxValue),
            MultipleOf = ReadInt(impl.MultipleOf)
        };
    }

    private static CrsInt64Format ConvertCInt64Format(CInt64FormatImpl impl)
    {
        long? ReadLong(IntPtr ptr) => ptr != IntPtr.Zero ? (long?)Marshal.ReadInt64(ptr) : null;

        return new CrsInt64Format
        {
            Minimum = ReadLong(impl.MinValue),
            Maximum = ReadLong(impl.MaxValue),
            MultipleOf = ReadLong(impl.MultipleOf)
        };
    }

    private static CrsUint32Format ConvertCUInt32Format(CUInt32FormatImpl impl)
    {
        uint? ReadUInt(IntPtr ptr) => ptr != IntPtr.Zero ? (uint?)Marshal.ReadInt32(ptr) : null;

        return new CrsUint32Format
        {
            Minimum = ReadUInt(impl.MinValue),
            Maximum = ReadUInt(impl.MaxValue),
            MultipleOf = ReadUInt(impl.MultipleOf)
        };
    }

    private static CrsUint64Format ConvertCUInt64Format(CUInt64FormatImpl impl)
    {
        ulong? ReadULong(IntPtr ptr) => ptr != IntPtr.Zero ? (ulong?)Marshal.ReadInt64(ptr) : null;

        return new CrsUint64Format
        {
            Minimum = ReadULong(impl.MinValue),
            Maximum = ReadULong(impl.MaxValue),
            MultipleOf = ReadULong(impl.MultipleOf)
        };
    }

    private static CrsFloatFormat ConvertCFloatFormat(CFloatFormatImpl impl)
    {
        float? ReadFloat(IntPtr ptr)
        {
            if (ptr == IntPtr.Zero) return null;
            byte[] bytes = new byte[4];
            Marshal.Copy(ptr, bytes, 0, 4);
            return BitConverter.ToSingle(bytes, 0);
        }

        return new CrsFloatFormat
        {
            Minimum = ReadFloat(impl.MinValue),
            Maximum = ReadFloat(impl.MaxValue),
            ExclusiveMinimum = impl.ExclusiveMinimum,
            ExclusiveMaximum = impl.ExclusiveMaximum,
            MultipleOf = ReadFloat(impl.MultipleOf)
        };
    }

    private static CrsDoubleFormat ConvertCDoubleFormat(CDoubleFormatImpl impl)
    {
        double? ReadDouble(IntPtr ptr)
        {
            if (ptr == IntPtr.Zero) return null;
            byte[] bytes = new byte[8];
            Marshal.Copy(ptr, bytes, 0, 8);
            return BitConverter.ToDouble(bytes, 0);
        }

        return new CrsDoubleFormat
        {
            Minimum = ReadDouble(impl.MinValue),
            Maximum = ReadDouble(impl.MaxValue),
            ExclusiveMinimum = impl.ExclusiveMinimum,
            ExclusiveMaximum = impl.ExclusiveMaximum,
            MultipleOf = ReadDouble(impl.MultipleOf)
        };
    }

    private static CrsArrayArg ConvertCArrayArg(CArrayArg cArrayArg)
    {
        CrsArg? item = null;
        if (cArrayArg.Item != IntPtr.Zero)
        {
            var itemInfo = Marshal.PtrToStructure<CArgumentInfo>(cArrayArg.Item);
            item = ConvertCArgumentInfoToCrsArg(itemInfo);
        }

        return new CrsArrayArg
        {
            Item = item ?? new CrsAnyTypeArg { }
        };
    }

    private static CrsObjectArg ConvertCObjectArg(CObjectArg cObjectArg)
    {
        string ReadString(IntPtr ptr) => ptr != IntPtr.Zero ? Marshal.PtrToStringAnsi(ptr) ?? "" : "";

        var props = new List<CrsArg>();
        if (cObjectArg.Props != IntPtr.Zero && cObjectArg.PropsCount != UIntPtr.Zero)
        {
            int count = (int)cObjectArg.PropsCount;
            int structSize = Marshal.SizeOf<CArgumentInfo>();

            for (int i = 0; i < count; i++)
            {
                IntPtr propPtr = IntPtr.Add(cObjectArg.Props, i * structSize);
                var propInfo = Marshal.PtrToStructure<CArgumentInfo>(propPtr);
                props.Add(ConvertCArgumentInfoToCrsArg(propInfo));
            }
        }

        CrsObjectArgAdditionalProperties? additionalProps = null;
        if (cObjectArg.AdditionalProps != IntPtr.Zero)
        {
            try
            {
                // Read the additional properties structure properly
                var cAdditionalProps = Marshal.PtrToStructure<CObjectArgAdditionalProperties>(cObjectArg.AdditionalProps);
                var itemArg = ConvertCArgumentInfoToCrsArg(cAdditionalProps.Item);

                additionalProps = new CrsObjectArgAdditionalProperties
                {
                    Item = itemArg ?? new CrsAnyTypeArg { }
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Failed to convert additional properties: {ex.Message}");
                // Fallback to any type for dictionary values
                additionalProps = new CrsObjectArgAdditionalProperties
                {
                    Item = new CrsAnyTypeArg { }
                };
            }
        }

        return new CrsObjectArg
        {
            ClsType = ReadString(cObjectArg.ClsType),
            Props = props,
            AdditionalProps = additionalProps
        };
    }

    private static CrsArgEnum? ConvertCArgEnum(IntPtr enumPtr)
    {
        if (enumPtr == IntPtr.Zero)
            return null;

        var cEnum = Marshal.PtrToStructure<CArgEnum>(enumPtr);
        if (cEnum.Items == IntPtr.Zero || cEnum.ItemCount == UIntPtr.Zero)
            return null;

        string ReadString(IntPtr ptr) => ptr != IntPtr.Zero ? Marshal.PtrToStringAnsi(ptr) ?? "" : "";

        var items = new List<CrsArgEnumItem>();
        int count = (int)cEnum.ItemCount;
        int structSize = Marshal.SizeOf<CArgEnumItem>();

        for (int i = 0; i < count; i++)
        {
            IntPtr itemPtr = IntPtr.Add(cEnum.Items, i * structSize);
            var cItem = Marshal.PtrToStructure<CArgEnumItem>(itemPtr);

            var valueStr = ReadString(cItem.Value);
            object value = valueStr;

            // Try to parse the JSON value
            try
            {
                using var doc = JsonDocument.Parse(valueStr);
                value = ConvertJsonElementToObject(doc.RootElement);
            }
            catch
            {
                // If parsing fails, use the string value
            }

            items.Add(new CrsArgEnumItem
            {
                Name = ReadString(cItem.Name),
                Value = value
            });
        }

        return new CrsArgEnum
        {
            Items = items,
            SupportExtension = cEnum.SupportExtension
        };
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

            // Initialize PSWrapper first
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