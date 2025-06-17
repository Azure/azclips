using System.Reflection;
using System.Collections;

namespace Accounts;

public static class ObjectFlattener {
    public static Dictionary<string, object> ToFlatDictionary(object obj) {
        var result = new Dictionary<string, object>();
        
        if (obj == null) {
            return result;
        }
        
        FlattenRecursively(obj, "", result);
        return result;
    }
    
    private static void FlattenRecursively(object obj, string keyPrefix, Dictionary<string, object> result) {
        if (obj == null) {
            if (!string.IsNullOrEmpty(keyPrefix)) {
                result[keyPrefix] = null;
            }
            return;
        }
        
        Type objectType = obj.GetType();
        
        if (IsPrimitiveType(objectType)) {
            if (!string.IsNullOrEmpty(keyPrefix)) {
                result[keyPrefix] = obj;
            }
            return;
        }
        
        if (IsEnumerableType(obj)) {
            ProcessEnumerable((IEnumerable)obj, keyPrefix, result);
            return;
        }
        
        ProcessComplexObject(obj, objectType, keyPrefix, result);
    }
    
    private static void ProcessEnumerable(IEnumerable enumerable, string keyPrefix, Dictionary<string, object> result) {
        int index = 0;
        foreach (var item in enumerable) {
            string itemKey = string.IsNullOrEmpty(keyPrefix) ? $"[{index}]" : $"{keyPrefix}[{index}]";
            FlattenRecursively(item, itemKey, result);
            index++;
        }
    }
    
    private static void ProcessComplexObject(object obj, Type objectType, string keyPrefix, Dictionary<string, object> result) {
        PropertyInfo[] properties = objectType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        
        foreach (PropertyInfo property in properties) {
            if (!property.CanRead) continue;
            
            try {
                object propertyValue = property.GetValue(obj);
                string propertyKey = BuildPropertyKey(keyPrefix, property.Name);
                
                if (propertyValue == null) {
                    result[propertyKey] = null;
                }
                else if (IsPrimitiveType(propertyValue.GetType())) {
                    result[propertyKey] = propertyValue;
                }
                else {
                    FlattenRecursively(propertyValue, propertyKey, result);
                }
            }
            catch (Exception) {
                continue;
            }
        }
    }
    
    private static string BuildPropertyKey(string prefix, string propertyName) {
        return string.IsNullOrEmpty(prefix) ? propertyName : $"{prefix}.{propertyName}";
    }
    
    private static bool IsEnumerableType(object obj) {
        return obj is IEnumerable && !(obj is string);
    }
    
    private static bool IsPrimitiveType(Type type) {
        return type.IsPrimitive || 
               type.IsEnum || 
               type == typeof(string) || 
               type == typeof(decimal) || 
               type == typeof(DateTime) || 
               type == typeof(DateTimeOffset) || 
               type == typeof(TimeSpan) || 
               type == typeof(Guid) ||
               IsNullablePrimitiveType(type);
    }
    
    private static bool IsNullablePrimitiveType(Type type) {
        return type.IsGenericType && 
               type.GetGenericTypeDefinition() == typeof(Nullable<>) && 
               IsPrimitiveType(Nullable.GetUnderlyingType(type));
    }
} 