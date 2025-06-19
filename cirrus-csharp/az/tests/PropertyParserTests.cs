using System;
using System.Reflection;
using Xunit;
using SharedContracts;

namespace AzTool.Tests
{
    public class PropertyParserTests
    {
        // Test class to use for property parsing tests
        public class TestClass
        {
            [ArgumentInfo(Required = true)]
            public string RequiredStringProperty { get; set; } = "";

            [ArgumentInfo(Required = false)]
            public string OptionalStringProperty { get; set; } = "";

            [ArgumentInfo(Required = true)]
            public int RequiredIntProperty { get; set; }

            [ArgumentInfo(Required = false)]
            public bool OptionalBoolProperty { get; set; }

            [ArgumentInfo(Required = false)]
            public string[] OptionalArrayProperty { get; set; } = Array.Empty<string>();

            [ArgumentInfo(Required = false)]
            public int[] OptionalIntArrayProperty { get; set; } = Array.Empty<int>();

            // Property without ArgumentInfo attribute
            public string PropertyWithoutAttribute { get; set; } = "";

            // Read-only property
            [ArgumentInfo(Required = false)]
            public string ReadOnlyProperty { get; } = "readonly";
        }

        [Fact]
        public void ParseProperty_WithRequiredStringProperty_ShouldSetValue()
        {
            // Arrange
            var testInstance = new TestClass();
            var propertyInfo = typeof(TestClass).GetProperty(nameof(TestClass.RequiredStringProperty));
            var input = new string[] { "test-value" };

            // Act
            PropertyParser.ParseProperty(input, testInstance, propertyInfo);

            // Assert
            Assert.Equal("test-value", testInstance.RequiredStringProperty);
        }

        [Fact]
        public void ParseProperty_WithRequiredStringProperty_EmptyInput_ShouldThrowException()
        {
            // Arrange
            var testInstance = new TestClass();
            var propertyInfo = typeof(TestClass).GetProperty(nameof(TestClass.RequiredStringProperty));
            var input = Array.Empty<string>();

            // Act & Assert
            var exception = Assert.Throws<PropertyParserException>(() =>
                PropertyParser.ParseProperty(input, testInstance, propertyInfo));
            
            Assert.Contains("is required but no value was provided", exception.Message);
        }

        [Fact]
        public void ParseProperty_WithOptionalStringProperty_EmptyInput_ShouldNotSetValue()
        {
            // Arrange
            var testInstance = new TestClass();
            var propertyInfo = typeof(TestClass).GetProperty(nameof(TestClass.OptionalStringProperty));
            var input = Array.Empty<string>();

            // Act
            PropertyParser.ParseProperty(input, testInstance, propertyInfo);

            // Assert
            Assert.Equal("", testInstance.OptionalStringProperty); // Should remain default value
        }

        [Fact]
        public void ParseProperty_WithIntProperty_ValidInteger_ShouldSetValue()
        {
            // Arrange
            var testInstance = new TestClass();
            var propertyInfo = typeof(TestClass).GetProperty(nameof(TestClass.RequiredIntProperty));
            var input = new string[] { "42" };

            // Act
            PropertyParser.ParseProperty(input, testInstance, propertyInfo);

            // Assert
            Assert.Equal(42, testInstance.RequiredIntProperty);
        }

        [Fact]
        public void ParseProperty_WithIntProperty_InvalidInteger_ShouldThrowException()
        {
            // Arrange
            var testInstance = new TestClass();
            var propertyInfo = typeof(TestClass).GetProperty(nameof(TestClass.RequiredIntProperty));
            var input = new string[] { "not-an-integer" };

            // Act & Assert
            var exception = Assert.Throws<PropertyParserException>(() =>
                PropertyParser.ParseProperty(input, testInstance, propertyInfo));
            
            Assert.Contains("expects an integer value", exception.Message);
        }

        [Fact]
        public void ParseProperty_WithBoolProperty_ValidBoolean_ShouldSetValue()
        {
            // Arrange
            var testInstance = new TestClass();
            var propertyInfo = typeof(TestClass).GetProperty(nameof(TestClass.OptionalBoolProperty));
            var input = new string[] { "true" };

            // Act
            PropertyParser.ParseProperty(input, testInstance, propertyInfo);

            // Assert
            Assert.True(testInstance.OptionalBoolProperty);
        }

        [Fact]
        public void ParseProperty_WithBoolProperty_InvalidBoolean_ShouldThrowException()
        {
            // Arrange
            var testInstance = new TestClass();
            var propertyInfo = typeof(TestClass).GetProperty(nameof(TestClass.OptionalBoolProperty));
            var input = new string[] { "not-a-boolean" };

            // Act & Assert
            var exception = Assert.Throws<PropertyParserException>(() =>
                PropertyParser.ParseProperty(input, testInstance, propertyInfo));
            
            Assert.Contains("expects a boolean value", exception.Message);
        }

        [Fact]
        public void ParseProperty_WithArrayProperty_ValidJsonArray_ShouldSetValue()
        {
            // Arrange
            var testInstance = new TestClass();
            var propertyInfo = typeof(TestClass).GetProperty(nameof(TestClass.OptionalArrayProperty));
            var input = new string[] { "[\"item1\", \"item2\", \"item3\"]" };

            // Act
            PropertyParser.ParseProperty(input, testInstance, propertyInfo);

            // Assert
            Assert.Equal(3, testInstance.OptionalArrayProperty.Length);
            Assert.Equal("item1", testInstance.OptionalArrayProperty[0]);
            Assert.Equal("item2", testInstance.OptionalArrayProperty[1]);
            Assert.Equal("item3", testInstance.OptionalArrayProperty[2]);
        }

        [Fact]
        public void ParseProperty_WithArrayProperty_InvalidJson_ShouldThrowException()
        {
            // Arrange
            var testInstance = new TestClass();
            var propertyInfo = typeof(TestClass).GetProperty(nameof(TestClass.OptionalArrayProperty));
            var input = new string[] { "not-valid-json" };

            // Act & Assert
            var exception = Assert.Throws<PropertyParserException>(() =>
                PropertyParser.ParseProperty(input, testInstance, propertyInfo));
            
            Assert.Contains("expects a JSON array value", exception.Message);
        }

        [Fact]
        public void ParseProperty_WithIntArrayProperty_ValidJsonArray_ShouldSetValue()
        {
            // Arrange
            var testInstance = new TestClass();
            var propertyInfo = typeof(TestClass).GetProperty(nameof(TestClass.OptionalIntArrayProperty));
            var input = new string[] { "[1, 2, 3, 42]" };

            // Act
            PropertyParser.ParseProperty(input, testInstance, propertyInfo);

            // Assert
            Assert.Equal(4, testInstance.OptionalIntArrayProperty.Length);
            Assert.Equal(1, testInstance.OptionalIntArrayProperty[0]);
            Assert.Equal(2, testInstance.OptionalIntArrayProperty[1]);
            Assert.Equal(3, testInstance.OptionalIntArrayProperty[2]);
            Assert.Equal(42, testInstance.OptionalIntArrayProperty[3]);
        }

        [Fact]
        public void ParseProperty_WithMultipleInputValues_ShouldThrowException()
        {
            // Arrange
            var testInstance = new TestClass();
            var propertyInfo = typeof(TestClass).GetProperty(nameof(TestClass.RequiredStringProperty));
            var input = new string[] { "value1", "value2" };

            // Act & Assert
            var exception = Assert.Throws<PropertyParserException>(() =>
                PropertyParser.ParseProperty(input, testInstance, propertyInfo));
            
            Assert.Contains("expects a single string value, but received multiple values", exception.Message);
        }

        [Fact]
        public void ParseProperty_WithPropertyWithoutAttribute_ShouldThrowException()
        {
            // Arrange
            var testInstance = new TestClass();
            var propertyInfo = typeof(TestClass).GetProperty(nameof(TestClass.PropertyWithoutAttribute));
            var input = new string[] { "test-value" };

            // Act & Assert
            var exception = Assert.Throws<PropertyParserException>(() =>
                PropertyParser.ParseProperty(input, testInstance, propertyInfo));
            
            Assert.Contains("does not have an ArgumentInfoAttribute", exception.Message);
        }

        [Fact]
        public void ParseProperty_WithReadOnlyProperty_ShouldThrowException()
        {
            // Arrange
            var testInstance = new TestClass();
            var propertyInfo = typeof(TestClass).GetProperty(nameof(TestClass.ReadOnlyProperty));
            var input = new string[] { "test-value" };

            // Act & Assert
            var exception = Assert.Throws<PropertyParserException>(() =>
                PropertyParser.ParseProperty(input, testInstance, propertyInfo));
            
            Assert.Contains("is read-only and cannot be set", exception.Message);
        }

        [Fact]
        public void ParseProperty_WithNullPropertyInfo_ShouldReturn()
        {
            // Arrange
            var testInstance = new TestClass();
            PropertyInfo? propertyInfo = null;
            var input = new string[] { "test-value" };

            // Act & Assert (should not throw)
            PropertyParser.ParseProperty(input, testInstance, propertyInfo);
        }
    }

    // Test class for object arrays with FromJsonString functionality
    public class ObjectArrayWithFromJsonStringTests
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

        // Test class with object array properties
        public class TestClassWithObjectArrays
        {
            [ArgumentInfo(Required = true)]
            public MockComplexObject[] RequiredObjectArray { get; set; } = Array.Empty<MockComplexObject>();

            [ArgumentInfo(Required = false)]
            public MockComplexObject[] OptionalObjectArray { get; set; } = Array.Empty<MockComplexObject>();

            [ArgumentInfo(Required = false)]
            public MockComplexObject SingleObject { get; set; } = new MockComplexObject();

            // Property without ArgumentInfo attribute
            public MockComplexObject[] PropertyWithoutAttribute { get; set; } = Array.Empty<MockComplexObject>();
        }

        [Fact]
        public void ParseProperty_WithObjectArray_ValidJsonArray_ShouldSetValue()
        {
            // Arrange
            var testInstance = new TestClassWithObjectArrays();
            var propertyInfo = typeof(TestClassWithObjectArrays).GetProperty(nameof(TestClassWithObjectArrays.RequiredObjectArray));
            var input = new string[] { 
                "[{\"name\":\"Item1\",\"value\":10,\"active\":true},{\"name\":\"Item2\",\"value\":20,\"active\":false}]" 
            };

            // Act
            PropertyParser.ParseProperty(input, testInstance, propertyInfo);

            // Assert
            Assert.Equal(2, testInstance.RequiredObjectArray.Length);
            Assert.Equal("Item1", testInstance.RequiredObjectArray[0].Name);
            Assert.Equal(10, testInstance.RequiredObjectArray[0].Value);
            Assert.True(testInstance.RequiredObjectArray[0].Active);
            Assert.Equal("Item2", testInstance.RequiredObjectArray[1].Name);
            Assert.Equal(20, testInstance.RequiredObjectArray[1].Value);
            Assert.False(testInstance.RequiredObjectArray[1].Active);
        }

        [Fact]
        public void ParseProperty_WithObjectArray_EmptyJsonArray_ShouldSetEmptyArray()
        {
            // Arrange
            var testInstance = new TestClassWithObjectArrays();
            var propertyInfo = typeof(TestClassWithObjectArrays).GetProperty(nameof(TestClassWithObjectArrays.OptionalObjectArray));
            var input = new string[] { "[]" };

            // Act
            PropertyParser.ParseProperty(input, testInstance, propertyInfo);

            // Assert
            Assert.Empty(testInstance.OptionalObjectArray);
        }

        [Fact]
        public void ParseProperty_WithObjectArray_InvalidJson_ShouldThrowException()
        {
            // Arrange
            var testInstance = new TestClassWithObjectArrays();
            var propertyInfo = typeof(TestClassWithObjectArrays).GetProperty(nameof(TestClassWithObjectArrays.RequiredObjectArray));
            var input = new string[] { "invalid-json-array" };

            // Act & Assert
            var exception = Assert.Throws<PropertyParserException>(() =>
                PropertyParser.ParseProperty(input, testInstance, propertyInfo));
            
            Assert.Contains("expects a JSON array value", exception.Message);
        }

        [Fact]
        public void ParseProperty_WithObjectArray_InvalidObjectJson_ShouldThrowException()
        {
            // Arrange
            var testInstance = new TestClassWithObjectArrays();
            var propertyInfo = typeof(TestClassWithObjectArrays).GetProperty(nameof(TestClassWithObjectArrays.RequiredObjectArray));
            var input = new string[] { "[{\"name\":\"Item1\",\"value\":\"not-a-number\"}]" };

            // Act & Assert
            var exception = Assert.Throws<PropertyParserException>(() =>
                PropertyParser.ParseProperty(input, testInstance, propertyInfo));
            
            Assert.Contains("Failed to parse property 'RequiredObjectArray[]' with value ", exception.Message);
        }

        [Fact]
        public void ParseProperty_WithSingleObject_ValidJson_ShouldSetValue()
        {
            // Arrange
            var testInstance = new TestClassWithObjectArrays();
            var propertyInfo = typeof(TestClassWithObjectArrays).GetProperty(nameof(TestClassWithObjectArrays.SingleObject));
            var input = new string[] { "{\"name\":\"TestObject\",\"value\":42,\"active\":true}" };

            // Act
            PropertyParser.ParseProperty(input, testInstance, propertyInfo);

            // Assert
            Assert.Equal("TestObject", testInstance.SingleObject.Name);
            Assert.Equal(42, testInstance.SingleObject.Value);
            Assert.True(testInstance.SingleObject.Active);
        }

        [Fact]
        public void ParseProperty_WithSingleObject_InvalidJson_ShouldThrowException()
        {
            // Arrange
            var testInstance = new TestClassWithObjectArrays();
            var propertyInfo = typeof(TestClassWithObjectArrays).GetProperty(nameof(TestClassWithObjectArrays.SingleObject));
            var input = new string[] { "invalid-json-object" };

            // Act & Assert
            var exception = Assert.Throws<PropertyParserException>(() =>
                PropertyParser.ParseProperty(input, testInstance, propertyInfo));
            
            Assert.Contains("Failed to parse property", exception.Message);
        }

        [Fact]
        public void ParseProperty_WithRequiredObjectArray_EmptyInput_ShouldThrowException()
        {
            // Arrange
            var testInstance = new TestClassWithObjectArrays();
            var propertyInfo = typeof(TestClassWithObjectArrays).GetProperty(nameof(TestClassWithObjectArrays.RequiredObjectArray));
            var input = Array.Empty<string>();

            // Act & Assert
            var exception = Assert.Throws<PropertyParserException>(() =>
                PropertyParser.ParseProperty(input, testInstance, propertyInfo));
            
            Assert.Contains("is required but no value was provided", exception.Message);
        }

        [Fact]
        public void ParseProperty_WithOptionalObjectArray_EmptyInput_ShouldNotSetValue()
        {
            // Arrange
            var testInstance = new TestClassWithObjectArrays();
            var originalArray = testInstance.OptionalObjectArray;
            var propertyInfo = typeof(TestClassWithObjectArrays).GetProperty(nameof(TestClassWithObjectArrays.OptionalObjectArray));
            var input = Array.Empty<string>();

            // Act
            PropertyParser.ParseProperty(input, testInstance, propertyInfo);

            // Assert
            Assert.Same(originalArray, testInstance.OptionalObjectArray); // Should remain unchanged
        }

        [Fact]
        public void ParseProperty_WithObjectArray_MultipleInputValues_ShouldThrowException()
        {
            // Arrange
            var testInstance = new TestClassWithObjectArrays();
            var propertyInfo = typeof(TestClassWithObjectArrays).GetProperty(nameof(TestClassWithObjectArrays.RequiredObjectArray));
            var input = new string[] { 
                "[{\"name\":\"Item1\"}]", 
                "[{\"name\":\"Item2\"}]" 
            };

            // Act & Assert
            var exception = Assert.Throws<PropertyParserException>(() =>
                PropertyParser.ParseProperty(input, testInstance, propertyInfo));
            
            Assert.Contains("expects a single string value, but received multiple values", exception.Message);
        }

        [Fact]
        public void ParseProperty_WithObjectArray_PartialJsonObjects_ShouldSetDefaultValues()
        {
            // Arrange
            var testInstance = new TestClassWithObjectArrays();
            var propertyInfo = typeof(TestClassWithObjectArrays).GetProperty(nameof(TestClassWithObjectArrays.OptionalObjectArray));
            var input = new string[] { "[{\"name\":\"OnlyName\"},{\"value\":99}]" };

            // Act
            PropertyParser.ParseProperty(input, testInstance, propertyInfo);

            // Assert
            Assert.Equal(2, testInstance.OptionalObjectArray.Length);
            
            // First object - only name provided
            Assert.Equal("OnlyName", testInstance.OptionalObjectArray[0].Name);
            Assert.Equal(0, testInstance.OptionalObjectArray[0].Value); // Default value
            Assert.False(testInstance.OptionalObjectArray[0].Active); // Default value
            
            // Second object - only value provided
            Assert.Equal("", testInstance.OptionalObjectArray[1].Name); // Default value
            Assert.Equal(99, testInstance.OptionalObjectArray[1].Value);
            Assert.False(testInstance.OptionalObjectArray[1].Active); // Default value
        }
    }
}
