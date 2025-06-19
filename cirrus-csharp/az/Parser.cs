namespace AzTool
{
    using System.Reflection;
    using SharedContracts;

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

        public static void ParseProperty(string[] input, object instance, PropertyInfo propertyInfo)
        {
            if (!propertyInfo.CanWrite)
            {
                throw new PropertyParserException($"Property '{propertyInfo.Name}' is read-only and cannot be set.");
            }
            ArgumentInfoAttribute attribute = propertyInfo.GetCustomAttribute<ArgumentInfoAttribute>() ?? throw new PropertyParserException($"Property '{propertyInfo.Name}' does not have an ArgumentInfoAttribute and cannot be parsed.");
            if (input.Length > 1)
            {
                throw new PropertyParserException($"Property '{propertyInfo.Name}' expects a single string value, but received multiple values.");
            }
            else if (input.Length == 0 && attribute.Required)
            {
                throw new PropertyParserException($"Property '{propertyInfo.Name}' is required but no value was provided.");
            }
            else if (input.Length == 0 && !attribute.Required)
            {
                // If the property is not required, we can skip setting it.
                return;
            }
            var argumentValue = input[0];

            try
            {
                var convertedValue = ConvertToType(argumentValue, propertyInfo.PropertyType, propertyInfo.Name);
                propertyInfo.SetValue(instance, convertedValue);
            }
            catch (PropertyParserException)
            {
                // Re-throw PropertyParserException as-is
                throw;
            }
        }

        static object ConvertToType(string value, System.Type targetType, string propertyName)
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
                    catch (System.FormatException)
                    {
                        throw new PropertyParserException($"Property '{propertyName}' expects an integer value, but received '{value}'.");
                    }
                case var t when t == typeof(bool):
                    try
                    {
                        return bool.Parse(value);
                    }
                    catch (System.FormatException)
                    {
                        throw new PropertyParserException($"Property '{propertyName}' expects a boolean value, but received '{value}'.");
                    }
                case var t when t.IsArray:
                    var elementType = t.GetElementType() ?? throw new PropertyParserException($"Property '{propertyName}' is an array but its element type could not be determined.");
                    try
                    {
                        var jsonReader = new System.Text.Json.Utf8JsonReader(System.Text.Encoding.UTF8.GetBytes(value));

                        var jsonElement = System.Text.Json.JsonDocument.ParseValue(ref jsonReader).RootElement;
                        var items = new System.Collections.Generic.List<object>();
                        foreach (var item in jsonElement.EnumerateArray())
                        {
                            var itemString = item.ValueKind == System.Text.Json.JsonValueKind.String 
                                ? item.GetString()! 
                                : item.GetRawText();
                            var convertedItem = ConvertToType(itemString, elementType, propertyName + "[]");
                            items.Add(convertedItem);
                        }
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
                    var fromJsonMethod = targetType.GetMethod("FromJsonString", BindingFlags.Public | BindingFlags.Static);
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
    }
}
