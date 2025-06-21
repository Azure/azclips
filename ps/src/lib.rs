mod core;
pub mod param;

use std::ffi::{CStr, CString};
use std::os::raw::c_char;

/// Initialize the Cirrus core library
#[no_mangle]
pub extern "C" fn cirrus_initialize() -> i32 {
    let rt = tokio::runtime::Runtime::new().unwrap();
    rt.block_on(core::initialize());
    0 // Success
}

/// Execute command using structured parameters
#[no_mangle]
pub extern "C" fn cirrus_run_command(
    uri: *const c_char,
    param_keys: *const *const c_char,
    param_values: *const param::CParamValue,
    param_count: usize,
) -> *mut c_char {
    if uri.is_null() {
        tracing::error!("URI pointer is null");
        return std::ptr::null_mut();
    }

    if param_count > 0 && (param_keys.is_null() || param_values.is_null()) {
        tracing::error!("Parameter pointers are null but count > 0");
        return std::ptr::null_mut();
    }

    // Parse URI
    let uri_str = unsafe {
        match CStr::from_ptr(uri).to_str() {
            Ok(s) => s,
            Err(e) => {
                tracing::error!("Failed to parse URI: {}", e);
                return std::ptr::null_mut();
            }
        }
    };

    // Convert parameters with enhanced error handling
    let mut parameters = Vec::new();

    if param_count > 0 {
        unsafe {
            for i in 0..param_count {
                let key_ptr = match param_keys.add(i).as_ref() {
                    Some(ptr) => *ptr,
                    None => {
                        tracing::error!("Invalid key pointer at index {}", i);
                        continue;
                    }
                };

                if key_ptr.is_null() {
                    tracing::warn!("Null key pointer at index {}", i);
                    continue;
                }

                let key_c_str = match CStr::from_ptr(key_ptr).to_str() {
                    Ok(s) => s,
                    Err(e) => {
                        tracing::error!("Failed to parse key at index {}: {}", i, e);
                        continue;
                    }
                };

                let param_value = match param_values.add(i).as_ref() {
                    Some(val) => val,
                    None => {
                        tracing::error!("Invalid parameter value pointer at index {}", i);
                        continue;
                    }
                };

                match param::c_param_to_value(param_value) {
                    Ok(value) => {
                        parameters.push((key_c_str.to_string(), value));
                    }
                    Err(e) => {
                        tracing::error!("Failed to convert parameter {}: {}", key_c_str, e);
                        continue;
                    }
                }
            }
        }
    }

    // Execute command with better error handling
    let result = match std::panic::catch_unwind(|| {
        let rt = tokio::runtime::Runtime::new().unwrap();
        rt.block_on(async { core::execute_command(uri_str, parameters).await })
    }) {
        Ok(result) => result,
        Err(_) => {
            tracing::error!("Panic occurred during command execution");
            let err_json = r#"{"error": "Internal panic during command execution"}"#;
            return match CString::new(err_json) {
                Ok(c_string) => c_string.into_raw(),
                Err(_) => std::ptr::null_mut(),
            };
        }
    };

    match result {
        Ok(Some(value)) => match serde_json::to_string(&value) {
            Ok(json) => match CString::new(json) {
                Ok(c_string) => c_string.into_raw(),
                Err(e) => {
                    tracing::error!("Failed to create CString from JSON: {}", e);
                    std::ptr::null_mut()
                }
            },
            Err(e) => {
                tracing::error!("Failed to serialize result to JSON: {}", e);
                let err_json = format!(r#"{{"error": "Serialization failed: {}"}}"#, e);
                match CString::new(err_json) {
                    Ok(c_string) => c_string.into_raw(),
                    Err(_) => std::ptr::null_mut(),
                }
            }
        },
        Ok(None) => match CString::new("") {
            Ok(c_string) => c_string.into_raw(),
            Err(_) => std::ptr::null_mut(),
        },
        Err(e) => {
            tracing::error!("Command execution failed: {}", e);
            let err_json = format!(r#"{{"error": "{}"}}"#, e.to_string().replace('"', r#"\""#));
            match CString::new(err_json) {
                Ok(c_string) => c_string.into_raw(),
                Err(_) => std::ptr::null_mut(),
            }
        }
    }
}

/// Get the version of the Cirrus library
#[no_mangle]
pub extern "C" fn cirrus_get_version() -> *mut c_char {
    let version = core::VERSION;
    match CString::new(version) {
        Ok(c_string) => c_string.into_raw(),
        Err(_) => std::ptr::null_mut(),
    }
}

/// Free memory allocated by Rust functions
#[no_mangle]
pub extern "C" fn cirrus_free_string(ptr: *mut c_char) {
    if !ptr.is_null() {
        unsafe {
            let _ = CString::from_raw(ptr);
        }
    }
}

/// Check if the library is properly loaded
#[no_mangle]
pub extern "C" fn cirrus_health_check() -> i32 {
    1 // Return 1 for success
}
