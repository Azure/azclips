use az_core::error::AzUserUsageError;
use serde_json::Value;
use std::ffi::{c_char, CStr};
use std::os::raw::{c_double, c_longlong, c_ulonglong};

// Define C-style parameter type enumeration
#[repr(C)]
#[derive(Debug, Clone, Copy)]
pub enum CParamType {
    Null = 0,
    Int64 = 1,
    Uint64 = 2,
    Float64 = 3,
    Boolean = 4,
    String = 5,
    Array = 6,
    Object = 7,
}

// Define C-style parameter value structure
#[repr(C)]
#[derive(Clone, Copy)]
pub struct CParamValue {
    pub param_type: CParamType,
    pub data: CParamData,
}

// Union for storing different types of values
#[repr(C)]
#[derive(Clone, Copy)]
pub union CParamData {
    pub int64_val: c_longlong,
    pub uint64_val: c_ulonglong,
    pub float64_val: c_double,
    pub bool_val: bool,
    pub string_val: *const c_char,
    pub array_val: CArrayValue,
    pub object_val: CObjectValue,
}

#[repr(C)]
#[derive(Clone, Copy)]
pub struct CArrayValue {
    pub items: *const CParamValue,
    pub count: usize,
}

#[repr(C)]
#[derive(Clone, Copy)]
pub struct CObjectValue {
    pub keys: *const *const c_char,
    pub values: *const CParamValue,
    pub count: usize,
}

// Convert C-style parameters to serde_json::Value
pub unsafe fn c_param_to_value(param: &CParamValue) -> Result<Value, AzUserUsageError> {
    match param.param_type {
        CParamType::Null => Ok(Value::Null),
        CParamType::Int64 => Ok(Value::Number(serde_json::Number::from(
            param.data.int64_val,
        ))),
        CParamType::Uint64 => Ok(Value::Number(serde_json::Number::from(
            param.data.uint64_val,
        ))),
        CParamType::Float64 => {
            if let Some(num) = serde_json::Number::from_f64(param.data.float64_val) {
                Ok(Value::Number(num))
            } else {
                Err(AzUserUsageError::InvalidArgumentValueError(
                    "Invalid float value".to_string(),
                ))
            }
        }
        CParamType::Boolean => Ok(Value::Bool(param.data.bool_val)),
        CParamType::String => {
            if param.data.string_val.is_null() {
                Ok(Value::Null)
            } else {
                let c_str = CStr::from_ptr(param.data.string_val);
                let str_val = c_str.to_str().map_err(|_| {
                    AzUserUsageError::InvalidArgumentValueError("Invalid UTF-8 string".to_string())
                })?;
                Ok(Value::String(str_val.to_string()))
            }
        }
        CParamType::Array => {
            let array_val = param.data.array_val;
            if array_val.items.is_null() || array_val.count == 0 {
                return Ok(Value::Array(Vec::new()));
            }

            let mut vec = Vec::new();
            for i in 0..array_val.count {
                let item = &*array_val.items.add(i);
                vec.push(c_param_to_value(item)?);
            }
            Ok(Value::Array(vec))
        }
        CParamType::Object => {
            let object_val = param.data.object_val;
            if object_val.keys.is_null() || object_val.values.is_null() || object_val.count == 0 {
                return Ok(Value::Object(serde_json::Map::new()));
            }

            let mut map = serde_json::Map::new();
            for i in 0..object_val.count {
                let key_ptr = *object_val.keys.add(i);
                if key_ptr.is_null() {
                    continue;
                }
                let key_c_str = CStr::from_ptr(key_ptr);
                let key = key_c_str.to_str().map_err(|_| {
                    AzUserUsageError::InvalidArgumentValueError("Invalid UTF-8 key".to_string())
                })?;
                let value_param = &*object_val.values.add(i);
                let value = c_param_to_value(value_param)?;
                map.insert(key.to_string(), value);
            }
            Ok(Value::Object(map))
        }
    }
}
