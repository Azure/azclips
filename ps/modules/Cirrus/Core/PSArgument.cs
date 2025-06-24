using System;
using System.Collections.Generic;
using System.Management.Automation;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Collections.Concurrent;

namespace Cirrus.Core
{
    /// <summary>
    /// Dynamic type builder for creating types at runtime with support for circular references
    /// Uses two-phase construction pattern to handle complex type dependencies
    /// </summary>
    public class DynamicTypeBuilder
    {
        private const string DEFAULT_ASSEMBLY_NAME = "Cirrus.Dynamic";
        private const string DEFAULT_MODULE_NAME = "Custom";

        // Global unique assembly - shared across all instances
        private static readonly Lazy<AssemblyBuilder> _assemblyBuilder = new(() =>
            AssemblyBuilder.DefineDynamicAssembly(
                new AssemblyName(DEFAULT_ASSEMBLY_NAME),
                AssemblyBuilderAccess.Run));

        // Cache of DynamicTypeBuilder instances per module name
        private static readonly ConcurrentDictionary<string, DynamicTypeBuilder> _builders = new();

        // Instance members
        private readonly string _moduleName;
        private readonly ModuleBuilder _moduleBuilder;
        private readonly ConcurrentDictionary<string, TypeBuilder> _pendingTypes = new();
        private readonly ConcurrentDictionary<string, Type> _completedTypes = new();
        private readonly object _creationLock = new object();

        /// <summary>
        /// Private constructor - use GetOrCreateBuilder to create instances
        /// </summary>
        private DynamicTypeBuilder(string moduleName)
        {
            _moduleName = string.IsNullOrEmpty(moduleName) ? DEFAULT_MODULE_NAME : moduleName;
            _moduleBuilder = _assemblyBuilder.Value.DefineDynamicModule($"{_moduleName}Module");
        }

        /// <summary>
        /// Generate a fully qualified type name from module name and class name
        /// </summary>
        /// <param name="moduleName">The module name</param>
        /// <param name="className">The class name</param>
        /// <returns>The fully qualified type name</returns>
        public static string GetTypeName(string moduleName, string className)
        {
            if (string.IsNullOrEmpty(moduleName))
            {
                moduleName = DEFAULT_MODULE_NAME;
            }

            if (string.IsNullOrEmpty(className))
            {
                throw new ArgumentException("Class name cannot be null or empty", nameof(className));
            }

            var namespaceName = $"{DEFAULT_ASSEMBLY_NAME}.{moduleName}";
            return $"{namespaceName}.{className}";
        }

        /// <summary>
        /// Get or create a DynamicTypeBuilder instance for the specified module
        /// </summary>
        /// <param name="moduleName">The module name</param>
        /// <returns>DynamicTypeBuilder instance for the specified module</returns>
        public static DynamicTypeBuilder GetOrCreateBuilder(string moduleName)
        {
            if (string.IsNullOrEmpty(moduleName))
            {
                moduleName = DEFAULT_MODULE_NAME;
            }

            return _builders.GetOrAdd(moduleName, name => new DynamicTypeBuilder(name));
        }

        /// <summary>
        /// Get all registered module names
        /// </summary>
        public static IReadOnlyCollection<string> GetRegisteredModuleNames()
        {
            return _builders.Keys.ToList().AsReadOnly();
        }

        /// <summary>
        /// Check if a module has been registered
        /// </summary>
        public static bool IsModuleRegistered(string moduleName)
        {
            if (string.IsNullOrEmpty(moduleName))
            {
                moduleName = DEFAULT_MODULE_NAME;
            }
            return _builders.ContainsKey(moduleName);
        }

        /// <summary>
        /// Get the ModuleBuilder for this instance
        /// </summary>
        public ModuleBuilder ModuleBuilder => _moduleBuilder;

        /// <summary>
        /// Get the module name for this instance
        /// </summary>
        public string ModuleName => _moduleName;

        /// <summary>
        /// Phase 1: Create or get Type/TypeBuilder for a class (without defining members yet)
        /// This allows for forward references and circular dependencies
        /// </summary>
        /// <param name="className">The class name</param>
        /// <param name="typeAttributes">Type attributes</param>
        /// <returns>Type (completed) or TypeBuilder (pending) for the class</returns>
        public Type GetOrCreateType(string className, TypeAttributes? typeAttributes = null)
        {
            if (string.IsNullOrEmpty(className))
                throw new ArgumentException("Class name cannot be null or empty", nameof(className));

            var typeName = GetTypeName(_moduleName, className);

            // First check if type is already completed
            if (_completedTypes.TryGetValue(typeName, out var completedType))
            {
                return completedType;
            }

            // Otherwise get or create pending TypeBuilder
            return _pendingTypes.GetOrAdd(typeName, _ =>
            {
                return _moduleBuilder.DefineType(
                    typeName,
                    typeAttributes ?? (TypeAttributes.Public | TypeAttributes.Class));
            });
        }

        /// <summary>
        /// Phase 2: Define properties and members for a TypeBuilder
        /// </summary>
        /// <param name="typeBuilder">The TypeBuilder to define members on</param>
        /// <param name="props">Properties to add to the type</param>
        public void DefineTypeMembers(TypeBuilder typeBuilder, List<CrsArg> props)
        {
            foreach (var prop in props)
            {
                if (string.IsNullOrEmpty(prop.Name))
                    continue;

                DefineProperty(typeBuilder, prop);
            }
        }

        /// <summary>
        /// Phase 3: Complete type creation and return the final Type
        /// </summary>
        /// <param name="className">The class name</param>
        /// <returns>The completed Type</returns>
        public Type CreateType(string className)
        {
            var typeName = GetTypeName(_moduleName, className);

            // Check if type is already completed
            if (_completedTypes.TryGetValue(typeName, out var existingType))
            {
                return existingType;
            }

            lock (_creationLock)
            {
                // Double-check pattern
                if (_completedTypes.TryGetValue(typeName, out existingType))
                {
                    return existingType;
                }

                if (!_pendingTypes.TryGetValue(typeName, out var typeBuilder))
                {
                    throw new InvalidOperationException($"TypeBuilder for '{typeName}' not found. Call GetOrCreateType first.");
                }

                var completedType = typeBuilder.CreateType();
                _completedTypes.TryAdd(typeName, completedType);

                // Remove from pending after successful creation
                _pendingTypes.TryRemove(typeName, out _);

                return completedType;
            }
        }

        /// <summary>
        /// Complete creation of all pending types in this module
        /// Useful for resolving circular dependencies
        /// </summary>
        public void CreateAllPendingTypes()
        {
            lock (_creationLock)
            {
                var pendingTypeNames = _pendingTypes.Keys.ToList();

                foreach (var typeName in pendingTypeNames)
                {
                    if (_pendingTypes.TryGetValue(typeName, out var typeBuilder) &&
                        !_completedTypes.ContainsKey(typeName))
                    {
                        try
                        {
                            var completedType = typeBuilder.CreateType();
                            _completedTypes.TryAdd(typeName, completedType);
                            _pendingTypes.TryRemove(typeName, out _);
                        }
                        catch (Exception ex)
                        {
                            // Log error but continue with other types
                            System.Diagnostics.Debug.WriteLine($"Failed to create type {typeName}: {ex.Message}");
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Helper method to define a property on a TypeBuilder
        /// </summary>
        private void DefineProperty(TypeBuilder typeBuilder, CrsArg prop)
        {
            var fieldName = $"_{prop.Name}";
            var propertyName = prop.GetParameterName();
            var propertyType = prop.GetOrCreateParameterType(this);

            // Define backing field
            var fieldBuilder = typeBuilder.DefineField(
                fieldName,
                propertyType,
                FieldAttributes.Private);

            // Define property
            var propertyBuilder = typeBuilder.DefineProperty(
                propertyName,
                PropertyAttributes.None,
                propertyType,
                null);

            // System.Text.Json JsonPropertyNameAttribute
            var jsonPropertyNameType = Type.GetType("System.Text.Json.Serialization.JsonPropertyNameAttribute, System.Text.Json");
            if (jsonPropertyNameType != null)
            {
                var jsonPropertyNameCtor = jsonPropertyNameType.GetConstructor(new[] { typeof(string) });
                if (jsonPropertyNameCtor != null)
                {
                    var jsonPropertyNameAttr = new CustomAttributeBuilder(jsonPropertyNameCtor, new object[] { prop.Name! });
                    propertyBuilder.SetCustomAttribute(jsonPropertyNameAttr);
                }
            }

            // Define getter
            var getterBuilder = typeBuilder.DefineMethod(
                $"get_{propertyName}",
                MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
                propertyType,
                Type.EmptyTypes);

            var getterIL = getterBuilder.GetILGenerator();
            getterIL.Emit(OpCodes.Ldarg_0);
            getterIL.Emit(OpCodes.Ldfld, fieldBuilder);
            getterIL.Emit(OpCodes.Ret);

            // Define setter
            var setterBuilder = typeBuilder.DefineMethod(
                $"set_{propertyName}",
                MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
                null,
                new[] { propertyType });

            var setterIL = setterBuilder.GetILGenerator();
            setterIL.Emit(OpCodes.Ldarg_0);
            setterIL.Emit(OpCodes.Ldarg_1);
            setterIL.Emit(OpCodes.Stfld, fieldBuilder);
            setterIL.Emit(OpCodes.Ret);

            // Assign getter and setter to property
            propertyBuilder.SetGetMethod(getterBuilder);
            propertyBuilder.SetSetMethod(setterBuilder);
        }
    }

    /// <summary>
    /// Main argument type that represents a CrsArg from the protobuf
    /// </summary>
    public abstract class CrsArg
    {
        public string? VarName { get; set; }
        public string? Name { get; set; }

        public int? Position { get; set; }

        public bool Internal { get; set; }
        public bool Required { get; set; }
        public bool Nullable { get; set; }
        public CrsArgDefault? Default { get; set; }
        public string? ShortDescription { get; set; }
        public string? LongDescription { get; set; }

        /// <summary>
        /// Abstract method to build a RuntimeDefinedParameter from this argument
        /// </summary>
        /// <returns>A RuntimeDefinedParameter that can be used in PowerShell dynamic parameters</returns>
        public abstract RuntimeDefinedParameter Build(DynamicTypeBuilder builder);

        /// <summary>
        /// Helper method to create basic parameter attributes
        /// </summary>
        protected ParameterAttribute CreateParameterAttribute()
        {
            var attribute = new ParameterAttribute
            {
                Mandatory = Required,
                HelpMessage = ShortDescription ?? LongDescription ?? string.Empty
            };

            if (Position.HasValue)
                attribute.Position = Position.Value;

            if (Internal)
                attribute.DontShow = true;

            return attribute;
        }

        /// <summary>
        /// Helper method to get the parameter name with first letter capitalized
        /// </summary>
        public string GetParameterName()
        {
            var name = Name;

            if (string.IsNullOrEmpty(name))
                return "UnknownParameter";

            // Capitalize first letter
            return char.ToUpper(name[0]) + name.Substring(1);
        }

        public abstract Type GetOrCreateParameterType(DynamicTypeBuilder builder);
    }

    /// <summary>
    /// Base class for string formats
    /// </summary>
    public abstract class CrsArgFormat
    {
        /// <summary>
        /// Gets the appropriate .NET type for this argument format
        /// </summary>
        /// <returns>The Type that should be used for PowerShell parameters</returns>
        public abstract Type GetParameterType(DynamicTypeBuilder builder);

        /// <summary>
        /// Extends the parameter attributes collection with format-specific validation
        /// </summary>
        /// <param name="attributes">The attributes collection to extend</param>
        public virtual void ExtendParameterAttributes(Collection<Attribute> attributes) { }
    }

    public class CrsArgDefault
    {
        public required object Value { get; set; }
    }

    /// <summary>
    /// Represents CrsArgEnum - enumeration configuration
    /// </summary>
    public class CrsArgEnum
    {
        public List<CrsArgEnumItem> Items { get; set; } = [];
    }

    /// <summary>
    /// Represents CrsArgEnumItem - individual enum item
    /// </summary>
    public class CrsArgEnumItem
    {
        public required string Name { get; set; }
        public required object Value { get; set; }
    }

    // Argument Type Implementations

    /// <summary>
    /// Class argument type
    /// </summary>
    public class CrsClsArg : CrsArg
    {
        public required string ClsType { get; set; }

        public override Type GetOrCreateParameterType(DynamicTypeBuilder builder)
        {
            var type = builder.GetOrCreateType(ClsType);
            return type;
        }

        public override RuntimeDefinedParameter Build(DynamicTypeBuilder builder)
        {
            var attributes = new Collection<Attribute> { CreateParameterAttribute() };

            // For class arguments, we'll use object type as the default
            return new RuntimeDefinedParameter(
                GetParameterName(),
                GetOrCreateParameterType(builder),
                attributes
            );
        }
    }

    /// <summary>
    /// String argument type
    /// </summary>
    public class CrsStringArg : CrsArg
    {
        public CrsArgEnum? Enum { get; set; }
        public CrsStringFormat Format { get; set; } = new CrsStringFormatImpl();

        public override Type GetOrCreateParameterType(DynamicTypeBuilder builder) => Format.GetParameterType(builder);

        public override RuntimeDefinedParameter Build(DynamicTypeBuilder builder)
        {

            var attributes = new Collection<Attribute> { CreateParameterAttribute() };

            // Add ValidateSet if enum is present
            if (Enum?.Items.Count > 0)
            {
                var validValues = Enum.Items.Select(item => item.Name).ToArray();
                attributes.Add(new ValidateSetAttribute(validValues));
            }

            // Add null validation if not nullable
            if (!Nullable)
            {
                attributes.Add(new ValidateNotNullAttribute());
            }

            // Let the format extend the parameter attributes with format-specific validation
            Format.ExtendParameterAttributes(attributes);

            return new RuntimeDefinedParameter(
                GetParameterName(),
                GetOrCreateParameterType(builder),
                attributes
            );
        }
    }

    /// <summary>
    /// Base class for string formats
    /// </summary>
    public abstract class CrsStringFormat : CrsArgFormat
    {
    }

    /// <summary>
    /// Binary format for strings
    /// </summary>
    public class CrsBinaryFormat : CrsStringFormat
    {
        public override Type GetParameterType(DynamicTypeBuilder builder) => typeof(FileInfo);

        public override void ExtendParameterAttributes(Collection<Attribute> attributes)
        {
            attributes.Add(new ValidateScriptAttribute(ScriptBlock.Create(@"
                $_.Exists -and -not $_.PSIsContainer
            ")));
        }
    }

    /// <summary>
    /// Byte format for strings (base64 encoded)
    /// </summary>
    public class CrsByteFormat : CrsStringFormat
    {
        public override Type GetParameterType(DynamicTypeBuilder builder) => typeof(string);

        public override void ExtendParameterAttributes(Collection<Attribute> attributes)
        {
            // Add base64 format validation using ValidatePattern
            attributes.Add(new ValidatePatternAttribute(@"^[A-Za-z0-9+/]*={0,2}$"));

            // Add custom validation script for more thorough base64 validation
            attributes.Add(new ValidateScriptAttribute(ScriptBlock.Create(@"
                try {
                    [System.Convert]::FromBase64String($_) | Out-Null
                    $true
                } catch {
                    throw 'The value must be a valid Base64 encoded string.'
                }
            ")));
        }
    }

    /// <summary>
    /// Regular string format
    /// </summary>
    public class CrsStringFormatImpl : CrsStringFormat
    {
        public string? Pattern { get; set; }
        public uint? MaxLength { get; set; }
        public uint? MinLength { get; set; }

        public override Type GetParameterType(DynamicTypeBuilder builder) => typeof(string);

        public override void ExtendParameterAttributes(Collection<Attribute> attributes)
        {
            // Add length validation if specified
            if (MinLength.HasValue || MaxLength.HasValue)
            {
                attributes.Add(new ValidateLengthAttribute(
                    (int)(MinLength ?? 0),
                    (int)(MaxLength ?? int.MaxValue)));
            }

            // Add pattern validation if specified
            if (!string.IsNullOrEmpty(Pattern))
            {
                attributes.Add(new ValidatePatternAttribute(Pattern));
            }
        }
    }

    /// <summary>
    /// Duration format
    /// </summary>
    public class CrsDurationFormat : CrsStringFormat
    {
        public enum Protocol
        {
            ISO8601 = 0
        }
        public Protocol ProtocolType { get; set; }

        public override Type GetParameterType(DynamicTypeBuilder builder) => typeof(TimeSpan);
    }

    /// <summary>
    /// Date format
    /// </summary>
    public class CrsDateFormat : CrsStringFormat
    {
        public enum Protocol
        {
            ISO8601 = 0
        }
        public Protocol ProtocolType { get; set; }

        public override Type GetParameterType(DynamicTypeBuilder builder) => typeof(DateTime);
    }

    /// <summary>
    /// Time format
    /// </summary>
    public class CrsTimeFormat : CrsStringFormat
    {
        public enum Protocol
        {
            ISO8601 = 0
        }
        public Protocol ProtocolType { get; set; }
        public uint? MsLength { get; set; }

        public override Type GetParameterType(DynamicTypeBuilder builder)
        {
            // PowerShell can automatically convert time strings to TimeSpan
            // Users can input formats like "14:30:05" and PowerShell will convert to TimeSpan
            return typeof(TimeOnly);  // TimeOnly is available in PowerShell 7.2 or later
        }
    }

    /// <summary>
    /// DateTime format
    /// </summary>
    public class CrsDateTimeFormat : CrsStringFormat
    {
        public enum Protocol
        {
            ISO8601 = 0,
            RFC1123 = 1
        }
        public Protocol ProtocolType { get; set; }

        public override Type GetParameterType(DynamicTypeBuilder builder) => typeof(DateTime);
    }

    /// <summary>
    /// UUID format
    /// </summary>
    public class CrsUuidFormat : CrsStringFormat
    {
        public override Type GetParameterType(DynamicTypeBuilder builder) => typeof(Guid);
    }

    /// <summary>
    /// Password format
    /// </summary>
    public class CrsPasswordFormat : CrsStringFormat
    {
        public override Type GetParameterType(DynamicTypeBuilder builder) => typeof(System.Security.SecureString);
    }

    /// <summary>
    /// Subscription ID format
    /// </summary>
    public class CrsSubscriptionIdFormat : CrsStringFormat
    {
        public override Type GetParameterType(DynamicTypeBuilder builder) => typeof(Guid);
    }

    /// <summary>
    /// Resource group name format
    /// </summary>
    public class CrsResourceGroupNameFormat : CrsStringFormat
    {
        public override Type GetParameterType(DynamicTypeBuilder builder) => typeof(string);

        public override void ExtendParameterAttributes(Collection<Attribute> attributes)
        {
            // Add Azure resource group name validation
            attributes.Add(new ValidatePatternAttribute(@"^[a-zA-Z0-9._-]+$"));
            attributes.Add(new ValidateLengthAttribute(1, 90));
        }
    }

    /// <summary>
    /// Resource ID format
    /// </summary>
    public class CrsResourceIdFormat : CrsStringFormat
    {
        public List<string> Templates { get; set; } = [];

        public override Type GetParameterType(DynamicTypeBuilder builder) => typeof(string);

        public override void ExtendParameterAttributes(Collection<Attribute> attributes)
        {
            // TODO: add validation from the resource ID templates
            // attributes.Add(new ValidatePatternAttribute(@"^/subscriptions/[^/]+/resourceGroups/[^/]+/.*"));
        }
    }

    /// <summary>
    /// Resource location format
    /// </summary>
    public class CrsResourceLocationFormat : CrsStringFormat
    {
        public override Type GetParameterType(DynamicTypeBuilder builder) => typeof(string);
    }


    /// <summary>
    /// Number argument type
    /// </summary>
    public class CrsNumberArg : CrsArg
    {
        public CrsArgEnum? Enum { get; set; }
        public CrsNumberFormat Format { get; set; } = new CrsInt64Format();

        public override Type GetOrCreateParameterType(DynamicTypeBuilder builder) => Format.GetParameterType(builder);

        public override RuntimeDefinedParameter Build(DynamicTypeBuilder builder)
        {
            var attributes = new Collection<Attribute> { CreateParameterAttribute() };

            // Add ValidateSet if enum is present
            if (Enum?.Items.Count > 0)
            {
                var validValues = Enum.Items.Select(item => item.Value.ToString()).ToArray();
                attributes.Add(new ValidateSetAttribute(validValues));
            }

            // Let the format extend the parameter attributes with format-specific validation
            Format.ExtendParameterAttributes(attributes);

            return new RuntimeDefinedParameter(
                GetParameterName(),
                GetOrCreateParameterType(builder),
                attributes
            );
        }
    }

    /// <summary>
    /// Base class for number formats
    /// </summary>
    public abstract class CrsNumberFormat : CrsArgFormat
    {
    }

    /// <summary>
    /// Int32 format
    /// </summary>
    public class CrsInt32Format : CrsNumberFormat
    {
        public int? Minimum { get; set; }
        public int? Maximum { get; set; }
        public int? MultipleOf { get; set; }

        public override Type GetParameterType(DynamicTypeBuilder builder) => typeof(int);

        public override void ExtendParameterAttributes(Collection<Attribute> attributes)
        {
            if (Minimum.HasValue || Maximum.HasValue)
            {
                attributes.Add(new ValidateRangeAttribute(
                    Minimum ?? int.MinValue,
                    Maximum ?? int.MaxValue));
            }
        }
    }

    /// <summary>
    /// Int64 format
    /// </summary>
    public class CrsInt64Format : CrsNumberFormat
    {
        public long? Minimum { get; set; }
        public long? Maximum { get; set; }
        public long? MultipleOf { get; set; }

        public override Type GetParameterType(DynamicTypeBuilder builder) => typeof(long);

        public override void ExtendParameterAttributes(Collection<Attribute> attributes)
        {
            if (Minimum.HasValue || Maximum.HasValue)
            {
                attributes.Add(new ValidateRangeAttribute(
                    Minimum ?? long.MinValue,
                    Maximum ?? long.MaxValue));
            }
        }
    }

    /// <summary>
    /// UInt32 format
    /// </summary>
    public class CrsUint32Format : CrsNumberFormat
    {
        public uint? Minimum { get; set; }
        public uint? Maximum { get; set; }
        public uint? MultipleOf { get; set; }

        public override Type GetParameterType(DynamicTypeBuilder builder) => typeof(uint);

        public override void ExtendParameterAttributes(Collection<Attribute> attributes)
        {
            if (Minimum.HasValue || Maximum.HasValue)
            {
                attributes.Add(new ValidateRangeAttribute(
                    Minimum ?? uint.MinValue,
                    Maximum ?? uint.MaxValue));
            }
        }
    }

    /// <summary>
    /// UInt64 format
    /// </summary>
    public class CrsUint64Format : CrsNumberFormat
    {
        public ulong? Minimum { get; set; }
        public ulong? Maximum { get; set; }
        public ulong? MultipleOf { get; set; }

        public override Type GetParameterType(DynamicTypeBuilder builder) => typeof(ulong);

        public override void ExtendParameterAttributes(Collection<Attribute> attributes)
        {
            if (Minimum.HasValue || Maximum.HasValue)
            {
                attributes.Add(new ValidateRangeAttribute(
                    Minimum ?? ulong.MinValue,
                    Maximum ?? ulong.MaxValue));
            }
        }
    }

    /// <summary>
    /// Float format
    /// </summary>
    public class CrsFloatFormat : CrsNumberFormat
    {
        public float? Minimum { get; set; }
        public float? Maximum { get; set; }
        public bool ExclusiveMinimum { get; set; }
        public bool ExclusiveMaximum { get; set; }
        public float? MultipleOf { get; set; }

        public override Type GetParameterType(DynamicTypeBuilder builder) => typeof(float);

        public override void ExtendParameterAttributes(Collection<Attribute> attributes)
        {
            if (Minimum.HasValue || Maximum.HasValue)
            {
                var min = Minimum ?? float.MinValue;
                var max = Maximum ?? float.MaxValue;

                // Adjust for exclusive ranges
                if (ExclusiveMinimum && Minimum.HasValue)
                    min = Minimum.Value + float.Epsilon;
                if (ExclusiveMaximum && Maximum.HasValue)
                    max = Maximum.Value - float.Epsilon;

                attributes.Add(new ValidateRangeAttribute(min, max));
            }
        }
    }

    /// <summary>
    /// Double format
    /// </summary>
    public class CrsDoubleFormat : CrsNumberFormat
    {
        public double? Minimum { get; set; }
        public double? Maximum { get; set; }
        public bool ExclusiveMinimum { get; set; }
        public bool ExclusiveMaximum { get; set; }
        public double? MultipleOf { get; set; }

        public override Type GetParameterType(DynamicTypeBuilder builder) => typeof(double);

        public override void ExtendParameterAttributes(Collection<Attribute> attributes)
        {
            if (Minimum.HasValue || Maximum.HasValue)
            {
                var min = Minimum ?? double.MinValue;
                var max = Maximum ?? double.MaxValue;

                // Adjust for exclusive ranges
                if (ExclusiveMinimum && Minimum.HasValue)
                    min = Minimum.Value + double.Epsilon;
                if (ExclusiveMaximum && Maximum.HasValue)
                    max = Maximum.Value - double.Epsilon;

                attributes.Add(new ValidateRangeAttribute(min, max));
            }
        }
    }

    /// <summary>
    /// Boolean argument type
    /// 
    /// </summary>
    public class CrsBooleanArg : CrsArg
    {
        public override Type GetOrCreateParameterType(DynamicTypeBuilder builder)
        {
            // Two modes:
            // 1. Switch Parameter (IsSwitch = true, default): 
            //    Usage: MyCommand -Enable (sets Enable to true)
            //           MyCommand (Enable is false/not set)
            // 2. Boolean Parameter (IsSwitch = false):
            //    Usage: MyCommand -Enable $true
            //           MyCommand -Enable $false
            // Type parameterType = Nullable ? typeof(bool) : typeof(SwitchParameter);  // use switch parameter when it not nullable
            return typeof(bool);
        }

        public override RuntimeDefinedParameter Build(DynamicTypeBuilder builder)
        {
            var attributes = new Collection<Attribute> { CreateParameterAttribute() };
            return new RuntimeDefinedParameter(
                GetParameterName(),
                GetOrCreateParameterType(builder),
                attributes
            );
        }
    }


    /// <summary>
    /// Any type argument (can accept any data type)
    /// </summary>
    public class CrsAnyTypeArg : CrsArg
    {
        public override Type GetOrCreateParameterType(DynamicTypeBuilder builder) => typeof(object);

        public override RuntimeDefinedParameter Build(DynamicTypeBuilder builder)
        {
            var attributes = new Collection<Attribute> { CreateParameterAttribute() };

            return new RuntimeDefinedParameter(
                GetParameterName(),
                GetOrCreateParameterType(builder),
                attributes
            );
        }
    }


    /// <summary>
    /// Object argument type
    /// </summary>
    public class CrsObjectArg : CrsArg
    {

        public required string ClsType { get; set; }
        public List<CrsArg> Props { get; set; } = [];
        public CrsObjectArgAdditionalProperties? AdditionalProps { get; set; }

        public override Type GetOrCreateParameterType(DynamicTypeBuilder builder)
        {
            if (Props.Count == 0 && AdditionalProps != null)
            {
                return AdditionalProps.GetParameterType(builder);
            }
            else if (Props.Count > 0)
            {
                var type = builder.GetOrCreateType(ClsType);
                if (type is TypeBuilder tb)
                {
                    builder.DefineTypeMembers(tb, Props);
                }
                return type;
            }
            else
            {
                return typeof(object);
            }
        }

        public override RuntimeDefinedParameter Build(DynamicTypeBuilder builder)
        {
            var attributes = new Collection<Attribute> { CreateParameterAttribute() };
            return new RuntimeDefinedParameter(
                GetParameterName(),
                GetOrCreateParameterType(builder),
                attributes
            );
        }
    }


    /// <summary>
    /// Additional properties for object arguments
    /// </summary>
    public class CrsObjectArgAdditionalProperties
    {
        public CrsArg? Item { get; set; }

        public Type GetParameterType(DynamicTypeBuilder builder)
        {
            if (Item != null)
            {
                return typeof(Dictionary<,>).MakeGenericType(typeof(string), Item.GetOrCreateParameterType(builder));
            }
            else
            {
                return typeof(Dictionary<string, object>);
            }
        }
    }


    /// <summary>
    /// Array argument type
    /// </summary>
    public class CrsArrayArg : CrsArg
    {
        public required CrsArg Item { get; set; }

        public override Type GetOrCreateParameterType(DynamicTypeBuilder builder) => Item.GetOrCreateParameterType(builder).MakeArrayType();

        public override RuntimeDefinedParameter Build(DynamicTypeBuilder builder)
        {
            var attributes = new Collection<Attribute> { CreateParameterAttribute() };

            // For array arguments, we'll use object array type
            return new RuntimeDefinedParameter(
                GetParameterName(),
                GetOrCreateParameterType(builder),
                attributes
            );
        }
    }
}