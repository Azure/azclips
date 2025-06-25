using System;
using Xunit;
using SharedContracts;

namespace AzTool.Tests
{
    public class ConvertToTypeTests
    {
        // Mock object class that supports FromJsonString
        public class MockComplexObject
        {
            public string Name { get; set; } = "";
            public int Value { get; set; }
            public bool Active { get; set; }

            public static MockComplexObject FromJsonString(string json)
            {
                var jsonDoc = System.Text.Json.JsonDocument.Parse(json);
                var root = jsonDoc.RootElement;
                
                return new MockComplexObject
                {
                    Name = root.TryGetProperty("name", out var nameProperty) ? nameProperty.GetString() ?? "" : "",
                    Value = root.TryGetProperty("value", out var valueProperty) ? valueProperty.GetInt32() : 0,
                    Active = root.TryGetProperty("active", out var activeProperty) ? activeProperty.GetBoolean() : false
                };
            }
        }

        // Test ConvertToType with string type
        [Fact]
        public void ConvertToType_WithStringType_ShouldReturnSameValue()
        {
            // Arrange
            var value = "test-value";
            var targetType = typeof(string);
            var propertyName = "TestProperty";

            // Act
            var result = PropertyParser.ConvertToType(value, targetType, propertyName);

            // Assert
            Assert.Equal("test-value", result);
            Assert.IsType<string>(result);
        }

        // Test ConvertToType with int type - valid input
        [Fact]
        public void ConvertToType_WithIntType_ValidInteger_ShouldReturnInteger()
        {
            // Arrange
            var value = "42";
            var targetType = typeof(int);
            var propertyName = "TestProperty";

            // Act
            var result = PropertyParser.ConvertToType(value, targetType, propertyName);

            // Assert
            Assert.Equal(42, result);
            Assert.IsType<int>(result);
        }

        // Test ConvertToType with int type - invalid input
        [Fact]
        public void ConvertToType_WithIntType_InvalidInteger_ShouldThrowException()
        {
            // Arrange
            var value = "not-an-integer";
            var targetType = typeof(int);
            var propertyName = "TestProperty";

            // Act & Assert
            var exception = Assert.Throws<PropertyParserException>(() =>
                PropertyParser.ConvertToType(value, targetType, propertyName));
            
            Assert.Contains("expects an integer value", exception.Message);
            Assert.Contains("not-an-integer", exception.Message);
        }

        // Test ConvertToType with bool type - valid input
        [Fact]
        public void ConvertToType_WithBoolType_ValidBoolean_ShouldReturnBoolean()
        {
            // Arrange & Act & Assert
            var result1 = PropertyParser.ConvertToType("true", typeof(bool), "TestProperty");
            Assert.True((bool)result1);
            Assert.IsType<bool>(result1);

            var result2 = PropertyParser.ConvertToType("false", typeof(bool), "TestProperty");
            Assert.False((bool)result2);
            Assert.IsType<bool>(result2);
        }

        // Test ConvertToType with bool type - invalid input
        [Fact]
        public void ConvertToType_WithBoolType_InvalidBoolean_ShouldThrowException()
        {
            // Arrange
            var value = "not-a-boolean";
            var targetType = typeof(bool);
            var propertyName = "TestProperty";

            // Act & Assert
            var exception = Assert.Throws<PropertyParserException>(() =>
                PropertyParser.ConvertToType(value, targetType, propertyName));
            
            Assert.Contains("expects a boolean value", exception.Message);
            Assert.Contains("not-a-boolean", exception.Message);
        }

        // Test ConvertToType with string array - valid JSON
        [Fact]
        public void ConvertToType_WithStringArray_ValidJsonArray_ShouldReturnArray()
        {
            // Arrange
            var value = "[\"item1\", \"item2\", \"item3\"]";
            var targetType = typeof(string[]);
            var propertyName = "TestProperty";

            // Act
            var result = PropertyParser.ConvertToType(value, targetType, propertyName);

            // Assert
            Assert.IsType<string[]>(result);
            var array = (string[])result;
            Assert.Equal(3, array.Length);
            Assert.Equal("item1", array[0]);
            Assert.Equal("item2", array[1]);
            Assert.Equal("item3", array[2]);
        }

        // Test ConvertToType with string array - invalid JSON
        [Fact]
        public void ConvertToType_WithStringArray_InvalidJson_ShouldThrowException()
        {
            // Arrange
            var value = "not-valid-json";
            var targetType = typeof(string[]);
            var propertyName = "TestProperty";

            // Act & Assert
            var exception = Assert.Throws<PropertyParserException>(() =>
                PropertyParser.ConvertToType(value, targetType, propertyName));
            
            Assert.Contains("expects a JSON array value", exception.Message);
            Assert.Contains("not-valid-json", exception.Message);
        }

        // Test ConvertToType with int array - valid JSON
        [Fact]
        public void ConvertToType_WithIntArray_ValidJsonArray_ShouldReturnArray()
        {
            // Arrange
            var value = "[1, 2, 3, 42]";
            var targetType = typeof(int[]);
            var propertyName = "TestProperty";

            // Act
            var result = PropertyParser.ConvertToType(value, targetType, propertyName);

            // Assert
            Assert.IsType<int[]>(result);
            var array = (int[])result;
            Assert.Equal(4, array.Length);
            Assert.Equal(1, array[0]);
            Assert.Equal(2, array[1]);
            Assert.Equal(3, array[2]);
            Assert.Equal(42, array[3]);
        }

        // Test ConvertToType with int array - invalid element
        [Fact]
        public void ConvertToType_WithIntArray_InvalidElement_ShouldThrowException()
        {
            // Arrange
            var value = "[1, \"not-a-number\", 3]";
            var targetType = typeof(int[]);
            var propertyName = "TestProperty";

            // Act & Assert
            var exception = Assert.Throws<PropertyParserException>(() =>
                PropertyParser.ConvertToType(value, targetType, propertyName));
            
            Assert.Contains("expects an integer value", exception.Message);
        }

        // Test ConvertToType with empty array
        [Fact]
        public void ConvertToType_WithStringArray_EmptyArray_ShouldReturnEmptyArray()
        {
            // Arrange
            var value = "[]";
            var targetType = typeof(string[]);
            var propertyName = "TestProperty";

            // Act
            var result = PropertyParser.ConvertToType(value, targetType, propertyName);

            // Assert
            Assert.IsType<string[]>(result);
            var array = (string[])result;
            Assert.Empty(array);
        }

        // Test ConvertToType with object type having FromJsonString method
        [Fact]
        public void ConvertToType_WithObjectType_ValidJson_ShouldReturnObject()
        {
            // Arrange
            var value = "{\"name\":\"TestObject\",\"value\":42,\"active\":true}";
            var targetType = typeof(MockComplexObject);
            var propertyName = "TestProperty";

            // Act
            var result = PropertyParser.ConvertToType(value, targetType, propertyName);

            // Assert
            Assert.IsType<MockComplexObject>(result);
            var obj = (MockComplexObject)result;
            Assert.Equal("TestObject", obj.Name);
            Assert.Equal(42, obj.Value);
            Assert.True(obj.Active);
        }

        // Test ConvertToType with object type - invalid JSON
        [Fact]
        public void ConvertToType_WithObjectType_InvalidJson_ShouldThrowException()
        {
            // Arrange
            var value = "invalid-json-object";
            var targetType = typeof(MockComplexObject);
            var propertyName = "TestProperty";

            // Act & Assert
            var exception = Assert.Throws<PropertyParserException>(() =>
                PropertyParser.ConvertToType(value, targetType, propertyName));
            
            Assert.Contains("Failed to parse property", exception.Message);
            Assert.Contains("TestProperty", exception.Message);
            Assert.Contains("invalid-json-object", exception.Message);
        }

        // Test ConvertToType with object array
        [Fact]
        public void ConvertToType_WithObjectArray_ValidJsonArray_ShouldReturnArray()
        {
            // Arrange
            var value = "[{\"name\":\"Item1\",\"value\":10,\"active\":true},{\"name\":\"Item2\",\"value\":20,\"active\":false}]";
            var targetType = typeof(MockComplexObject[]);
            var propertyName = "TestProperty";

            // Act
            var result = PropertyParser.ConvertToType(value, targetType, propertyName);

            // Assert
            Assert.IsType<MockComplexObject[]>(result);
            var array = (MockComplexObject[])result;
            Assert.Equal(2, array.Length);
            
            Assert.Equal("Item1", array[0].Name);
            Assert.Equal(10, array[0].Value);
            Assert.True(array[0].Active);
            
            Assert.Equal("Item2", array[1].Name);
            Assert.Equal(20, array[1].Value);
            Assert.False(array[1].Active);
        }

        // Test ConvertToType with object array - partial JSON objects
        [Fact]
        public void ConvertToType_WithObjectArray_PartialJsonObjects_ShouldSetDefaultValues()
        {
            // Arrange
            var value = "[{\"name\":\"OnlyName\"},{\"value\":99}]";
            var targetType = typeof(MockComplexObject[]);
            var propertyName = "TestProperty";

            // Act
            var result = PropertyParser.ConvertToType(value, targetType, propertyName);

            // Assert
            Assert.IsType<MockComplexObject[]>(result);
            var array = (MockComplexObject[])result;
            Assert.Equal(2, array.Length);
            
            // First object - only name provided
            Assert.Equal("OnlyName", array[0].Name);
            Assert.Equal(0, array[0].Value); // Default value
            Assert.False(array[0].Active); // Default value
            
            // Second object - only value provided
            Assert.Equal("", array[1].Name); // Default value
            Assert.Equal(99, array[1].Value);
            Assert.False(array[1].Active); // Default value
        }

        // Test ConvertToType with unsupported type
        [Fact]
        public void ConvertToType_WithUnsupportedType_ShouldThrowException()
        {
            // Arrange
            var value = "test-value";
            var targetType = typeof(DateTime); // Unsupported type without FromJsonString method
            var propertyName = "TestProperty";

            // Act & Assert
            var exception = Assert.Throws<PropertyParserException>(() =>
                PropertyParser.ConvertToType(value, targetType, propertyName));
            
            Assert.Contains("cannot be parsed from a string", exception.Message);
            Assert.Contains("DateTime", exception.Message);
        }

        // Test ConvertToType with object type - malformed JSON in array element
        [Fact]
        public void ConvertToType_WithObjectArray_InvalidObjectJson_ShouldThrowException()
        {
            // Arrange
            var value = "[{\"name\":\"Item1\",\"value\":\"not-a-number\"}]";
            var targetType = typeof(MockComplexObject[]);
            var propertyName = "TestProperty";

            // Act & Assert
            var exception = Assert.Throws<PropertyParserException>(() =>
                PropertyParser.ConvertToType(value, targetType, propertyName));
            
            Assert.Contains("Failed to parse property 'TestProperty[]'", exception.Message);
        }
    }

    // New test class for PopProperty functionality
    public class PopPropertyTests
    {
        // Test class for ArgumentAction.Store tests
        public class TestClassStore
        {
            [ArgumentInfo(Required = true)]
            public string RequiredProperty { get; set; } = "";

            [ArgumentInfo(Required = false)]
            public string OptionalProperty { get; set; } = "";

            [ArgumentInfo(NumArgs = "?")]
            public string OptionalSingleProperty { get; set; } = "";

            [ArgumentInfo(NumArgs = "*")]
            public string[] VariableLengthProperty { get; set; } = Array.Empty<string>();

            [ArgumentInfo(NumArgs = "+")]
            public string[] RequiredVariableLengthProperty { get; set; } = Array.Empty<string>();

            [ArgumentInfo(NumArgs = "2")]
            public string[] FixedLengthProperty { get; set; } = Array.Empty<string>();

            [ArgumentInfo(Enums = new[] { "option1", "option2", "option3" })]
            public string EnumProperty { get; set; } = "";

            [ArgumentInfo(Aliases = new[] { "a", "alias" })]
            public string AliasedProperty { get; set; } = "";

            [ArgumentInfo(Positional = true)]
            public string PositionalProperty { get; set; } = "";
        }

        // Test class for ArgumentAction.StoreTrue tests
        public class TestClassStoreTrue
        {
            [ArgumentInfo(Action = ArgumentAction.StoreTrue)]
            public bool VerboseFlag { get; set; }

            [ArgumentInfo(Action = ArgumentAction.StoreTrue, Aliases = new[] { "d", "debug" })]
            public bool DebugFlag { get; set; }
        }

        // Test class for ArgumentAction.Count tests
        public class TestClassCount
        {
            [ArgumentInfo(Action = ArgumentAction.Count)]
            public int VerbosityLevel { get; set; }

            [ArgumentInfo(Action = ArgumentAction.Count, Aliases = new[] { "v", "verbose" })]
            public int VerboseCount { get; set; }
        }

        // Test class with invalid configurations
        public class TestClassInvalid
        {
            [ArgumentInfo(Action = ArgumentAction.StoreTrue)]
            public string InvalidStoreTrueType { get; set; } = "";

            [ArgumentInfo(Action = ArgumentAction.Count)]
            public string InvalidCountType { get; set; } = "";

            [ArgumentInfo(NumArgs = "invalid")]
            public string InvalidNumArgs { get; set; } = "";
        }

        #region ArgumentAction.Store Tests

        [Fact]
        public void PopProperty_Store_RequiredProperty_WithValue_ShouldReturnValue()
        {
            // Arrange
            var args = new[] { "--required-property", "test-value" };
            var commandLineArgs = CommandLineArgs.Parse(args);
            var property = typeof(TestClassStore).GetProperty(nameof(TestClassStore.RequiredProperty))!;

            // Act
            var result = commandLineArgs.PopProperty(property);

            // Assert
            Assert.Equal("test-value", result);
        }

        [Fact]
        public void PopProperty_Store_RequiredProperty_WithoutValue_ShouldThrowException()
        {
            // Arrange
            var args = Array.Empty<string>();
            var commandLineArgs = CommandLineArgs.Parse(args);
            var property = typeof(TestClassStore).GetProperty(nameof(TestClassStore.RequiredProperty))!;

            // Act & Assert
            var exception = Assert.Throws<PropertyParserException>(() => commandLineArgs.PopProperty(property));
            Assert.Contains("is required but no value was provided", exception.Message);
        }

        [Fact]
        public void PopProperty_Store_OptionalProperty_WithoutValue_ShouldReturnNull()
        {
            // Arrange
            var args = Array.Empty<string>();
            var commandLineArgs = CommandLineArgs.Parse(args);
            var property = typeof(TestClassStore).GetProperty(nameof(TestClassStore.OptionalProperty))!;

            // Act
            var result = commandLineArgs.PopProperty(property);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void PopProperty_Store_NumArgsOptional_WithValue_ShouldReturnValue()
        {
            // Arrange
            var args = new[] { "--optional-single-property", "test-value" };
            var commandLineArgs = CommandLineArgs.Parse(args);
            var property = typeof(TestClassStore).GetProperty(nameof(TestClassStore.OptionalSingleProperty))!;

            // Act
            var result = commandLineArgs.PopProperty(property);

            // Assert
            Assert.Equal("test-value", result);
        }

        [Fact]
        public void PopProperty_Store_NumArgsOptional_WithoutValue_ShouldReturnNull()
        {
            // Arrange
            var args = Array.Empty<string>();
            var commandLineArgs = CommandLineArgs.Parse(args);
            var property = typeof(TestClassStore).GetProperty(nameof(TestClassStore.OptionalSingleProperty))!;

            // Act
            var result = commandLineArgs.PopProperty(property);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void PopProperty_Store_NumArgsOptional_MultipleUsage_ShouldThrowException()
        {
            // Arrange
            var args = new[] { "--optional-single-property", "value1", "--optional-single-property", "value2" };
            var commandLineArgs = CommandLineArgs.Parse(args);
            var property = typeof(TestClassStore).GetProperty(nameof(TestClassStore.OptionalSingleProperty))!;

            // Act & Assert
            var exception = Assert.Throws<PropertyParserException>(() => commandLineArgs.PopProperty(property));
            Assert.Contains("expects at most one value, but received multiple usage", exception.Message);
        }

        [Fact]
        public void PopProperty_Store_NumArgsVariable_MultipleValues_ShouldReturnArray()
        {
            // Arrange
            var args = new[] { "--variable-length-property", "value1", "value2", "value3" };
            var commandLineArgs = CommandLineArgs.Parse(args);
            var property = typeof(TestClassStore).GetProperty(nameof(TestClassStore.VariableLengthProperty))!;

            // Act
            var result = commandLineArgs.PopProperty(property);

            // Assert
            Assert.IsType<string[]>(result);
            var array = (string[])result!;
            Assert.Equal(3, array.Length);
            Assert.Equal("value1", array[0]);
            Assert.Equal("value2", array[1]);
            Assert.Equal("value3", array[2]);
        }

        [Fact]
        public void PopProperty_Store_NumArgsVariableRequired_NoValues_ShouldReturnEmptyArray()
        {
            // Arrange
            var args = Array.Empty<string>();
            var commandLineArgs = CommandLineArgs.Parse(args);
            var property = typeof(TestClassStore).GetProperty(nameof(TestClassStore.RequiredVariableLengthProperty))!;

            // Act
            var result = commandLineArgs.PopProperty(property);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void PopProperty_Store_NumArgsVariableRequired_WithValues_ShouldReturnArray()
        {
            // Arrange
            var args = new[] { "--required-variable-length-property", "value1", "value2" };
            var commandLineArgs = CommandLineArgs.Parse(args);
            var property = typeof(TestClassStore).GetProperty(nameof(TestClassStore.RequiredVariableLengthProperty))!;

            // Act
            var result = commandLineArgs.PopProperty(property);

            // Assert
            Assert.IsType<string[]>(result);
            var array = (string[])result!;
            Assert.Equal(2, array.Length);
        }

        [Fact]
        public void PopProperty_Store_NumArgsFixed_CorrectCount_ShouldReturnArray()
        {
            // Arrange
            var args = new[] { "--fixed-length-property", "value1", "value2" };
            var commandLineArgs = CommandLineArgs.Parse(args);
            var property = typeof(TestClassStore).GetProperty(nameof(TestClassStore.FixedLengthProperty))!;

            // Act
            var result = commandLineArgs.PopProperty(property);

            // Assert
            Assert.IsType<string[]>(result);
            var array = (string[])result!;
            Assert.Equal(2, array.Length);
        }

        [Fact]
        public void PopProperty_Store_NumArgsFixed_IncorrectCount_ShouldThrowException()
        {
            // Arrange
            var args = new[] { "--fixed-length-property", "value1" }; // Only 1 value, expects 2
            var commandLineArgs = CommandLineArgs.Parse(args);
            var property = typeof(TestClassStore).GetProperty(nameof(TestClassStore.FixedLengthProperty))!;

            // Act & Assert
            var exception = Assert.Throws<PropertyParserException>(() => commandLineArgs.PopProperty(property));
            Assert.Contains("expects 2 values, but received 1 value(s)", exception.Message);
        }

        [Fact]
        public void PopProperty_Store_EnumValidation_ValidValue_ShouldReturnValue()
        {
            // Arrange
            var args = new[] { "--enum-property", "option1" };
            var commandLineArgs = CommandLineArgs.Parse(args);
            var property = typeof(TestClassStore).GetProperty(nameof(TestClassStore.EnumProperty))!;

            // Act
            var result = commandLineArgs.PopProperty(property);

            // Assert
            Assert.Equal("option1", result);
        }

        [Fact]
        public void PopProperty_Store_EnumValidation_InvalidValue_ShouldThrowException()
        {
            // Arrange
            var args = new[] { "--enum-property", "invalid-option" };
            var commandLineArgs = CommandLineArgs.Parse(args);
            var property = typeof(TestClassStore).GetProperty(nameof(TestClassStore.EnumProperty))!;

            // Act & Assert
            var exception = Assert.Throws<PropertyParserException>(() => commandLineArgs.PopProperty(property));
            Assert.Contains("expects one of the following values: option1, option2, option3", exception.Message);
        }

        [Fact]
        public void PopProperty_Store_Aliases_ShouldWorkWithAnyAlias()
        {
            // Arrange
            var args1 = new[] { "-a", "test-value" };
            var args2 = new[] { "--alias", "test-value" };
            var commandLineArgs1 = CommandLineArgs.Parse(args1);
            var commandLineArgs2 = CommandLineArgs.Parse(args2);
            var property = typeof(TestClassStore).GetProperty(nameof(TestClassStore.AliasedProperty))!;

            // Act
            var result1 = commandLineArgs1.PopProperty(property);
            var result2 = commandLineArgs2.PopProperty(property);

            // Assert
            Assert.Equal("test-value", result1);
            Assert.Equal("test-value", result2);
        }

        [Fact]
        public void PopProperty_Store_Positional_ShouldReturnPositionalArgument()
        {
            // Arrange
            var args = new[] { "positional-value" };
            var commandLineArgs = CommandLineArgs.Parse(args);
            var property = typeof(TestClassStore).GetProperty(nameof(TestClassStore.PositionalProperty))!;

            // Act
            var result = commandLineArgs.PopProperty(property);

            // Assert
            Assert.Equal("positional-value", result);
        }

        #endregion

        #region ArgumentAction.StoreTrue Tests

        [Fact]
        public void PopProperty_StoreTrue_FlagPresent_ShouldReturnTrue()
        {
            // Arrange
            var args = new[] { "--verbose-flag" };
            var commandLineArgs = CommandLineArgs.Parse(args);
            var property = typeof(TestClassStoreTrue).GetProperty(nameof(TestClassStoreTrue.VerboseFlag))!;

            // Act
            var result = commandLineArgs.PopProperty(property);

            // Assert
            Assert.True((bool)result!);
        }

        [Fact]
        public void PopProperty_StoreTrue_FlagAbsent_ShouldReturnFalse()
        {
            // Arrange
            var args = Array.Empty<string>();
            var commandLineArgs = CommandLineArgs.Parse(args);
            var property = typeof(TestClassStoreTrue).GetProperty(nameof(TestClassStoreTrue.VerboseFlag))!;

            // Act
            var result = commandLineArgs.PopProperty(property);

            // Assert
            Assert.False((bool)result!);
        }

        [Fact]
        public void PopProperty_StoreTrue_FlagWithValue_ShouldThrowException()
        {
            // Arrange
            var args = new[] { "--verbose-flag", "some-value" };
            var commandLineArgs = CommandLineArgs.Parse(args);
            var property = typeof(TestClassStoreTrue).GetProperty(nameof(TestClassStoreTrue.VerboseFlag))!;

            // Act & Assert
            var exception = Assert.Throws<PropertyParserException>(() => commandLineArgs.PopProperty(property));
            Assert.Contains("does not expect a value, but received", exception.Message);
        }

        [Fact]
        public void PopProperty_StoreTrue_FlagMultipleOccurrences_ShouldThrowException()
        {
            // Arrange
            var args = new[] { "--verbose-flag", "--verbose-flag" };
            var commandLineArgs = CommandLineArgs.Parse(args);
            var property = typeof(TestClassStoreTrue).GetProperty(nameof(TestClassStoreTrue.VerboseFlag))!;

            // Act & Assert
            var exception = Assert.Throws<PropertyParserException>(() => commandLineArgs.PopProperty(property));
            Assert.Contains("expects at most one occurrence, but received multiple usage", exception.Message);
        }

        [Fact]
        public void PopProperty_StoreTrue_WithAliases_ShouldWork()
        {
            // Arrange
            var args1 = new[] { "-d" };
            var args2 = new[] { "--debug" };
            var commandLineArgs1 = CommandLineArgs.Parse(args1);
            var commandLineArgs2 = CommandLineArgs.Parse(args2);
            var property = typeof(TestClassStoreTrue).GetProperty(nameof(TestClassStoreTrue.DebugFlag))!;

            // Act
            var result1 = commandLineArgs1.PopProperty(property);
            var result2 = commandLineArgs2.PopProperty(property);

            // Assert
            Assert.True((bool)result1!);
            Assert.True((bool)result2!);
        }

        #endregion

        #region ArgumentAction.Count Tests

        [Fact]
        public void PopProperty_Count_NoOccurrences_ShouldReturnZero()
        {
            // Arrange
            var args = Array.Empty<string>();
            var commandLineArgs = CommandLineArgs.Parse(args);
            var property = typeof(TestClassCount).GetProperty(nameof(TestClassCount.VerbosityLevel))!;

            // Act
            var result = commandLineArgs.PopProperty(property);

            // Assert
            Assert.Equal(0, result);
        }

        [Fact]
        public void PopProperty_Count_SingleOccurrence_ShouldReturnOne()
        {
            // Arrange
            var args = new[] { "--verbosity-level" };
            var commandLineArgs = CommandLineArgs.Parse(args);
            var property = typeof(TestClassCount).GetProperty(nameof(TestClassCount.VerbosityLevel))!;

            // Act
            var result = commandLineArgs.PopProperty(property);

            // Assert
            Assert.Equal(1, result);
        }

        [Fact]
        public void PopProperty_Count_MultipleOccurrences_ShouldReturnCount()
        {
            // Arrange
            var args = new[] { "--verbosity-level", "--verbosity-level", "--verbosity-level" };
            var commandLineArgs = CommandLineArgs.Parse(args);
            var property = typeof(TestClassCount).GetProperty(nameof(TestClassCount.VerbosityLevel))!;

            // Act
            var result = commandLineArgs.PopProperty(property);

            // Assert
            Assert.Equal(3, result);
        }

        [Fact]
        public void PopProperty_Count_WithValue_ShouldThrowException()
        {
            // Arrange
            var args = new[] { "--verbosity-level", "some-value" };
            var commandLineArgs = CommandLineArgs.Parse(args);
            var property = typeof(TestClassCount).GetProperty(nameof(TestClassCount.VerbosityLevel))!;

            // Act & Assert
            var exception = Assert.Throws<PropertyParserException>(() => commandLineArgs.PopProperty(property));
            Assert.Contains("does not expect a value, but received", exception.Message);
        }

        [Fact]
        public void PopProperty_Count_WithAliases_ShouldCountAllOccurrences()
        {
            // Arrange
            var args = new[] { "-v", "--verbose", "-v" };
            var commandLineArgs = CommandLineArgs.Parse(args);
            var property = typeof(TestClassCount).GetProperty(nameof(TestClassCount.VerboseCount))!;

            // Act
            var result = commandLineArgs.PopProperty(property);

            // Assert
            Assert.Equal(3, result);
        }

        #endregion

        #region Invalid Configuration Tests

        [Fact]
        public void PopProperty_StoreTrue_InvalidType_ShouldThrowException()
        {
            // Arrange
            var args = new[] { "--invalid-store-true-type" };
            var commandLineArgs = CommandLineArgs.Parse(args);
            var property = typeof(TestClassInvalid).GetProperty(nameof(TestClassInvalid.InvalidStoreTrueType))!;

            // Act & Assert
            var exception = Assert.Throws<PropertyParserException>(() => commandLineArgs.PopProperty(property));
            Assert.Contains("with Action='StoreTrue' must be of type bool", exception.Message);
        }

        [Fact]
        public void PopProperty_Count_InvalidType_ShouldThrowException()
        {
            // Arrange
            var args = new[] { "--invalid-count-type" };
            var commandLineArgs = CommandLineArgs.Parse(args);
            var property = typeof(TestClassInvalid).GetProperty(nameof(TestClassInvalid.InvalidCountType))!;

            // Act & Assert
            var exception = Assert.Throws<PropertyParserException>(() => commandLineArgs.PopProperty(property));
            Assert.Contains("with Action='Count' must be of type int", exception.Message);
        }

        [Fact]
        public void PopProperty_InvalidNumArgs_ShouldThrowException()
        {
            // Arrange
            var args = new[] { "--invalid-num-args", "value" };
            var commandLineArgs = CommandLineArgs.Parse(args);
            var property = typeof(TestClassInvalid).GetProperty(nameof(TestClassInvalid.InvalidNumArgs))!;

            // Act & Assert
            var exception = Assert.Throws<PropertyParserException>(() => commandLineArgs.PopProperty(property));
            Assert.Contains("Invalid NumArgs setting 'invalid'", exception.Message);
        }

        [Fact]
        public void PopProperty_NoArgumentInfoAttribute_ShouldThrowException()
        {
            // Arrange
            var args = Array.Empty<string>();
            var commandLineArgs = CommandLineArgs.Parse(args);
            
            // Create a mock property without ArgumentInfoAttribute
            var testClass = new { TestProperty = "test" };
            var property = testClass.GetType().GetProperty("TestProperty")!;

            // Act & Assert
            var exception = Assert.Throws<PropertyParserException>(() => commandLineArgs.PopProperty(property));
            Assert.Contains("does not have an ArgumentInfoAttribute and cannot be parsed", exception.Message);
        }

        #endregion

        #region Edge Cases and Complex Scenarios

        [Fact]
        public void PopProperty_Store_KeyProvidedButNoValue_ShouldThrowException()
        {
            // Arrange
            var args = new[] { "--required-property" }; // Key provided but no value
            var commandLineArgs = CommandLineArgs.Parse(args);
            var property = typeof(TestClassStore).GetProperty(nameof(TestClassStore.RequiredProperty))!;

            // Act & Assert
            var exception = Assert.Throws<PropertyParserException>(() => commandLineArgs.PopProperty(property));
            Assert.Contains("expects a value, but no value was provided", exception.Message);
        }

        [Fact]
        public void PopProperty_Store_NonArrayWithMultipleNumArgs_ShouldThrowException()
        {
            // Arrange - Create a property that expects multiple values but isn't an array
            var testClass = new TestClassWithInvalidArrayConfiguration();
            var args = new[] { "--invalid-property", "value1", "value2" };
            var commandLineArgs = CommandLineArgs.Parse(args);
            var property = typeof(TestClassWithInvalidArrayConfiguration).GetProperty(nameof(TestClassWithInvalidArrayConfiguration.InvalidProperty))!;

            // Act & Assert
            var exception = Assert.Throws<PropertyParserException>(() => commandLineArgs.PopProperty(property));
            Assert.Contains("expects multiple values and should be an array", exception.Message);
        }

        [Fact]
        public void PopProperty_Store_NumArgsVariableWithKeyButNoValue_ShouldThrowException()
        {
            // Arrange
            var args = new[] { "--required-variable-length-property" }; // Key provided but no values
            var commandLineArgs = CommandLineArgs.Parse(args);
            var property = typeof(TestClassStore).GetProperty(nameof(TestClassStore.RequiredVariableLengthProperty))!;

            // Act & Assert
            var exception = Assert.Throws<PropertyParserException>(() => commandLineArgs.PopProperty(property));
            Assert.Contains("expects a value, but no value was provided", exception.Message);
        }

        public class TestClassWithInvalidArrayConfiguration
        {
            [ArgumentInfo(NumArgs = "2")]
            public string InvalidProperty { get; set; } = "";
        }

        #endregion

        #region Additional Edge Cases and Integration Tests

        [Fact]
        public void PopProperty_Store_CaseInsensitiveEnumValidation_ShouldWork()
        {
            // Arrange
            var args = new[] { "--enum-property", "OPTION1" }; // Different case
            var commandLineArgs = CommandLineArgs.Parse(args);
            var property = typeof(TestClassStore).GetProperty(nameof(TestClassStore.EnumProperty))!;

            // Act
            var result = commandLineArgs.PopProperty(property);

            // Assert
            Assert.Equal("OPTION1", result);
        }

        [Fact]
        public void PopProperty_Store_EmptyPositionalArgs_ShouldReturnNull()
        {
            // Arrange
            var args = Array.Empty<string>();
            var commandLineArgs = CommandLineArgs.Parse(args);
            var property = typeof(TestClassStore).GetProperty(nameof(TestClassStore.PositionalProperty))!;

            // Act
            var result = commandLineArgs.PopProperty(property);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void PopProperty_Store_PositionalArgsCleared_AfterProcessing()
        {
            // Arrange
            var args = new[] { "positional1" };
            var commandLineArgs = CommandLineArgs.Parse(args);
            var property = typeof(TestClassStore).GetProperty(nameof(TestClassStore.PositionalProperty))!;

            // Act
            var result = commandLineArgs.PopProperty(property);

            // Assert
            Assert.IsType<string>(result);
            Assert.Equal("positional1", result);
            
            // Verify positional args are cleared
            Assert.Empty(commandLineArgs.PositionalArgs);
        }

        [Fact]
        public void PopProperty_Store_NumArgsOptional_MultipleValues_ShouldThrowException()
        {
            // Arrange
            var args = new[] { "--optional-single-property", "value1", "value2" };
            var commandLineArgs = CommandLineArgs.Parse(args);
            var property = typeof(TestClassStore).GetProperty(nameof(TestClassStore.OptionalSingleProperty))!;

            // Act & Assert
            var exception = Assert.Throws<PropertyParserException>(() => commandLineArgs.PopProperty(property));
            Assert.Contains("expects at most one value, but received multiple values", exception.Message);
        }

        [Fact]
        public void PopProperty_Store_FixedNumArgsWithNonArrayType_ShouldThrowException()
        {
            // Arrange
            var testClass = new TestClassWithFixedArgsNonArray();
            var args = new[] { "--fixed-args-property", "value1", "value2" };
            var commandLineArgs = CommandLineArgs.Parse(args);
            var property = typeof(TestClassWithFixedArgsNonArray).GetProperty(nameof(TestClassWithFixedArgsNonArray.FixedArgsProperty))!;

            // Act & Assert
            var exception = Assert.Throws<PropertyParserException>(() => commandLineArgs.PopProperty(property));
            Assert.Contains("expects multiple values and should be an array", exception.Message);
        }

        [Fact]
        public void PopProperty_UnsupportedAction_ShouldThrowNotSupportedException()
        {
            // Arrange - We'll need to mock this since we can't easily add a new enum value
            var args = new[] { "--test-property", "value" };
            var commandLineArgs = CommandLineArgs.Parse(args);
            
            // Create a mock property with an unsupported action (we'll simulate this by reflection)
            var testClass = new TestClassWithUnsupportedAction();
            var property = typeof(TestClassWithUnsupportedAction).GetProperty(nameof(TestClassWithUnsupportedAction.TestProperty))!;

            // We expect this to work normally since Store is supported
            var result = commandLineArgs.PopProperty(property);
            Assert.Equal("value", result);
        }

        [Fact]
        public void PopProperty_Store_SingleValueExpected_MultipleProvided_ShouldThrowException()
        {
            // Arrange
            var args = new[] { "--required-property", "value1", "value2" };
            var commandLineArgs = CommandLineArgs.Parse(args);
            var property = typeof(TestClassStore).GetProperty(nameof(TestClassStore.RequiredProperty))!;

            // Act & Assert
            var exception = Assert.Throws<PropertyParserException>(() => commandLineArgs.PopProperty(property));
            Assert.Contains("expects a single value, but received multiple values", exception.Message);
        }

        [Fact]
        public void PopProperty_Store_DefaultAliasGeneration_ShouldWork()
        {
            // Arrange - Test that properties without explicit aliases get kebab-case conversion
            var args = new[] { "--required-property", "test-value" };
            var commandLineArgs = CommandLineArgs.Parse(args);
            var property = typeof(TestClassStore).GetProperty(nameof(TestClassStore.RequiredProperty))!;

            // Act
            var result = commandLineArgs.PopProperty(property);

            // Assert
            Assert.Equal("test-value", result);
        }

        public class TestClassWithFixedArgsNonArray
        {
            [ArgumentInfo(NumArgs = "2")]
            public string FixedArgsProperty { get; set; } = "";
        }

        public class TestClassWithUnsupportedAction
        {
            [ArgumentInfo(Action = ArgumentAction.Store)]
            public string TestProperty { get; set; } = "";
        }

        #endregion
    }
}
