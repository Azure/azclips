using System;
using Xunit;

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
}
