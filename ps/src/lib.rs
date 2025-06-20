mod param;

use std::collections::HashMap;
use std::ffi::{CStr, CString};
use std::os::raw::{c_char, c_double, c_int};
use std::str::FromStr;

use az_core::VERSION;
use az_core::{
    argparse::{self, ArgValueAssignOp},
    error::AzError,
    uri::CrsUri,
    AzCore,
};
use serde_json::Value;

// Re-export parameter related types to maintain API compatibility
pub use param::{CArrayValue, CObjectValue, CParamData, CParamType, CParamValue};
use tracing::info;

// Asynchronous command execution function
async fn execute_command_async(
    uri: &str,
    parameters: HashMap<String, Value>,
) -> Result<Option<Value>, AzError> {
    let core = AzCore::instance().await;
    let uri = CrsUri::from_str(uri).unwrap();
    let invocation = core.resolve_invocation(uri).await?;

    let mut cmd_value_ops: Vec<ArgValueAssignOp> = Vec::new();

    for (arg_name, value_ops) in parameters {
        let arg_path = arg_name.split('.').collect::<Vec<&str>>();
        let key_parts: Vec<_> = arg_path
            .iter()
            .map(|s| argparse::SHPartialKeyPart::Name(s.to_string()))
            .collect();
        let sh_partial_keys = argparse::SHPartialKeys::from_parts(key_parts.iter());
        cmd_value_ops.push(ArgValueAssignOp {
            keys: sh_partial_keys,
            value: value_ops.clone(),
            is_append: false,
            from_blank: false,
        });
    }

    core.execute_command(&invocation, cmd_value_ops).await
}

/// Execute command using structured parameters
#[no_mangle]
pub extern "C" fn cirrus_run_command(
    uri: *const c_char,
    param_keys: *const *const c_char,
    param_values: *const CParamValue,
    param_count: usize,
) -> *mut c_char {
    if uri.is_null() {
        return std::ptr::null_mut();
    }

    // Parse URI
    let uri_str = unsafe {
        match CStr::from_ptr(uri).to_str() {
            Ok(s) => s,
            Err(_) => return std::ptr::null_mut(),
        }
    };

    // Convert parameters
    let mut parameters = HashMap::new();

    if !param_keys.is_null() && !param_values.is_null() && param_count > 0 {
        unsafe {
            for i in 0..param_count {
                let key_ptr = *param_keys.add(i);
                if key_ptr.is_null() {
                    continue;
                }

                let key_c_str = CStr::from_ptr(key_ptr);
                let key = match key_c_str.to_str() {
                    Ok(s) => s,
                    Err(_) => continue,
                };

                let param_value = &*param_values.add(i);
                match param::c_param_to_value(param_value) {
                    Ok(value) => {
                        parameters.insert(key.to_string(), value);
                    }
                    Err(e) => {
                        tracing::error!("Failed to convert parameter {}: {}", key, e);
                        continue;
                    }
                }
            }
        }
    }

    // Execute command
    let rt = tokio::runtime::Runtime::new().unwrap();
    let result = rt.block_on(async { execute_command_async(uri_str, parameters).await });

    match result {
        Ok(Some(value)) => {
            let json = serde_json::to_string(&value).unwrap();
            match CString::new(json) {
                Ok(c_string) => c_string.into_raw(),
                Err(_) => std::ptr::null_mut(),
            }
        }
        Ok(None) => match CString::new("") {
            Ok(c_string) => c_string.into_raw(),
            Err(_) => std::ptr::null_mut(),
        },
        Err(e) => {
            let err_json = format!("{{\"error\": \"{}\"}}", e);
            match CString::new(err_json) {
                Ok(c_string) => c_string.into_raw(),
                Err(_) => std::ptr::null_mut(),
            }
        }
    }
}

/// Initialize the Cirrus core library
#[no_mangle]
pub extern "C" fn cirrus_initialize() -> i32 {
    // Initialize logging
    env_logger::init();
    info!("Cirrus library initialized");
    0 // Success
}

/// Get the version of the Cirrus library
#[no_mangle]
pub extern "C" fn cirrus_get_version() -> *mut c_char {
    let version = VERSION;
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
