pub mod cmdlet;
mod core;
pub mod param;

use std::ffi::{CStr, CString};
use std::os::raw::c_char;

// C-compatible structure for direct array passing
#[repr(C)]
pub struct CCmdletInfo {
    pub name: *mut c_char,
    pub uri: *mut c_char,
    pub synopsis: *mut c_char,
    pub description: *mut c_char,
}

// C-compatible array structure
#[repr(C)]
pub struct CCmdletArray {
    pub items: *mut CCmdletInfo,
    pub count: usize,
}

/// Initialize the Cirrus core library
#[no_mangle]
pub extern "C" fn cirrus_initialize() -> i32 {
    let rt = tokio::runtime::Runtime::new().unwrap();
    rt.block_on(core::initialize());
    0 // Success
}

/// Get the builtin cmdlets as C struct array
#[no_mangle]
pub extern "C" fn cirrus_get_builtin_cmdlets() -> *mut CCmdletArray {
    let result = std::panic::catch_unwind(|| {
        let rt = tokio::runtime::Runtime::new().unwrap();
        rt.block_on(async {
            match core::builtin_cmdlets().await {
                Ok(cmdlets) => {
                    let mut c_cmdlets = Vec::with_capacity(cmdlets.len());

                    for cmdlet in cmdlets {
                        let name_cstring = match CString::new(cmdlet.ps_name()) {
                            Ok(s) => s,
                            Err(_) => continue,
                        };
                        let uri_cstring = match CString::new(cmdlet.uri.to_string()) {
                            Ok(s) => s,
                            Err(_) => continue,
                        };
                        let synopsis_cstring = match CString::new(cmdlet.synopsis) {
                            Ok(s) => s,
                            Err(_) => match CString::new("") {
                                Ok(s) => s,
                                Err(_) => continue,
                            },
                        };
                        let description_cstring =
                            match CString::new(cmdlet.description.unwrap_or_default()) {
                                Ok(s) => s,
                                Err(_) => match CString::new("") {
                                    Ok(s) => s,
                                    Err(_) => continue,
                                },
                            };

                        c_cmdlets.push(CCmdletInfo {
                            name: name_cstring.into_raw(),
                            uri: uri_cstring.into_raw(),
                            synopsis: synopsis_cstring.into_raw(),
                            description: description_cstring.into_raw(),
                        });
                    }

                    let count = c_cmdlets.len();
                    let items = c_cmdlets.into_boxed_slice();
                    let items_ptr = Box::into_raw(items) as *mut CCmdletInfo;

                    let array = Box::new(CCmdletArray {
                        items: items_ptr,
                        count,
                    });

                    Box::into_raw(array)
                }
                Err(e) => {
                    tracing::error!("Failed to get builtin cmdlets: {}", e);
                    std::ptr::null_mut()
                }
            }
        })
    });

    match result {
        Ok(array_ptr) => array_ptr,
        Err(_) => {
            tracing::error!("Panic occurred while getting builtin cmdlets array");
            std::ptr::null_mut()
        }
    }
}

/// Free the cmdlet array memory
#[no_mangle]
pub extern "C" fn cirrus_free_cmdlet_array(array: *mut CCmdletArray) {
    if array.is_null() {
        return;
    }

    unsafe {
        let array = Box::from_raw(array);

        if !array.items.is_null() && array.count > 0 {
            let items = Box::from_raw(std::slice::from_raw_parts_mut(array.items, array.count));

            for item in items.iter() {
                if !item.name.is_null() {
                    let _ = CString::from_raw(item.name);
                }
                if !item.uri.is_null() {
                    let _ = CString::from_raw(item.uri);
                }
                if !item.synopsis.is_null() {
                    let _ = CString::from_raw(item.synopsis);
                }
                if !item.description.is_null() {
                    let _ = CString::from_raw(item.description);
                }
            }
        }
    }
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
