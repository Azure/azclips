pub mod argument;
pub mod cmdlet;
mod core;
pub mod param;

use std::ffi::{CStr, CString};
use std::os::raw::c_char;
use std::path::PathBuf;
use std::str::FromStr;

use argument::{
    convert_crs_arg_to_c_argument_info, free_c_argument_info, get_command_arguments_async,
    CArgumentArray, CArgumentInfo,
};
use az_core::uri::CrsUri;
use cmdlet::{convert_cmdlets_to_c_array, CCmdletArray};

// C-compatible structure for component info
#[repr(C)]
pub struct CComponentInfo {
    pub uri: *mut c_char,
    pub name: *mut c_char,
    pub version: *mut c_char,
}

/// Initialize the Cirrus core library
#[no_mangle]
pub extern "C" fn cirrus_initialize() -> i32 {
    let rt = tokio::runtime::Runtime::new().unwrap();
    rt.block_on(core::initialize());
    0 // Success
}

#[no_mangle]
pub extern "C" fn cirrus_import_plugin_component(path: *const c_char) -> *mut CComponentInfo {
    if path.is_null() {
        tracing::error!("Path pointer is null");
        return std::ptr::null_mut();
    }

    let path_str = unsafe {
        match CStr::from_ptr(path).to_str() {
            Ok(s) => s,
            Err(e) => {
                tracing::error!("Failed to parse path: {}", e);
                return std::ptr::null_mut();
            }
        }
    };

    let path = PathBuf::from(path_str);
    if !path.exists() || !path.is_file() {
        tracing::error!("Path file {} does not exist", path.display());
        return std::ptr::null_mut();
    }

    let result = std::panic::catch_unwind(|| {
        let rt = tokio::runtime::Runtime::new().unwrap();
        rt.block_on(async {
            match core::import_plugin_component(path).await {
                Ok((uri, name, version)) => {
                    let uri_cstring = match CString::new(uri.to_string()) {
                        Ok(s) => s,
                        Err(e) => {
                            tracing::error!("Failed to create CString for URI: {}", e);
                            return std::ptr::null_mut();
                        }
                    };
                    let name_cstring = match CString::new(name) {
                        Ok(s) => s,
                        Err(e) => {
                            tracing::error!("Failed to create CString for name: {}", e);
                            return std::ptr::null_mut();
                        }
                    };
                    let version_cstring = match CString::new(version) {
                        Ok(s) => s,
                        Err(e) => {
                            tracing::error!("Failed to create CString for version: {}", e);
                            return std::ptr::null_mut();
                        }
                    };

                    let component_info = CComponentInfo {
                        uri: uri_cstring.into_raw(),
                        name: name_cstring.into_raw(),
                        version: version_cstring.into_raw(),
                    };
                    Box::into_raw(Box::new(component_info))
                }
                Err(e) => {
                    tracing::error!("Failed to import plugin component: {}", e);
                    std::ptr::null_mut()
                }
            }
        })
    });

    match result {
        Ok(component_ptr) => component_ptr,
        Err(_) => {
            tracing::error!("Panic occurred while importing plugin component");
            std::ptr::null_mut()
        }
    }
}

/// Free the component info memory
#[no_mangle]
pub extern "C" fn cirrus_free_component_info(info: *mut CComponentInfo) {
    if info.is_null() {
        return;
    }

    unsafe {
        let info = Box::from_raw(info);

        if !info.uri.is_null() {
            let _ = CString::from_raw(info.uri);
        }
        if !info.name.is_null() {
            let _ = CString::from_raw(info.name);
        }
        if !info.version.is_null() {
            let _ = CString::from_raw(info.version);
        }
    }
}

/// Get the builtin cmdlets as C struct array
#[no_mangle]
pub extern "C" fn cirrus_get_builtin_cmdlets() -> *mut CCmdletArray {
    let result = std::panic::catch_unwind(|| {
        let rt = tokio::runtime::Runtime::new().unwrap();
        rt.block_on(async {
            match core::builtin_cmdlets().await {
                Ok(cmdlets) => convert_cmdlets_to_c_array(cmdlets),
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

/// Get the cmdlets of the component as C struct array
#[no_mangle]
pub extern "C" fn cirrus_get_component_cmdlets(uri: *const c_char) -> *mut CCmdletArray {
    if uri.is_null() {
        tracing::error!("URI pointer is null");
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

    let uri = match CrsUri::from_str(uri_str) {
        Ok(uri) => uri,
        Err(e) => {
            tracing::error!("Failed to parse URI: {}", e);
            return std::ptr::null_mut();
        }
    };

    let result = std::panic::catch_unwind(|| {
        let rt = tokio::runtime::Runtime::new().unwrap();
        rt.block_on(async {
            match core::component_cmdlets(uri).await {
                Ok(cmdlets) => convert_cmdlets_to_c_array(cmdlets),
                Err(e) => {
                    tracing::error!("Failed to get component cmdlets: {}", e);
                    std::ptr::null_mut()
                }
            }
        })
    });

    match result {
        Ok(array_ptr) => array_ptr,
        Err(_) => {
            tracing::error!("Panic occurred while getting component cmdlets array");
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

    let uri = match CrsUri::from_str(uri_str) {
        Ok(uri) => uri,
        Err(e) => {
            tracing::error!("Failed to parse URI: {}", e);
            return std::ptr::null_mut();
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
        rt.block_on(async { core::execute_command(uri, parameters).await })
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

/// Get command arguments for the specified URI
#[no_mangle]
pub extern "C" fn cirrus_get_command_arguments(uri: *const c_char) -> *mut CArgumentArray {
    if uri.is_null() {
        tracing::error!("URI pointer is null");
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

    let result = std::panic::catch_unwind(|| {
        let rt = tokio::runtime::Runtime::new().unwrap();
        rt.block_on(async {
            match get_command_arguments_async(uri_str).await {
                Ok(args) => {
                    if args.is_empty() {
                        return Box::into_raw(Box::new(CArgumentArray::new()));
                    }

                    let mut c_args = Vec::with_capacity(args.len());

                    for arg in args {
                        let c_arg = convert_crs_arg_to_c_argument_info(&arg);
                        c_args.push(c_arg);
                    }

                    let count = c_args.len();
                    let items = c_args.into_boxed_slice();
                    let items_ptr = Box::into_raw(items) as *mut CArgumentInfo;

                    let array = Box::new(CArgumentArray {
                        items: items_ptr,
                        count,
                    });

                    Box::into_raw(array)
                }
                Err(e) => {
                    tracing::error!("Failed to get command arguments: {}", e);
                    std::ptr::null_mut()
                }
            }
        })
    });

    match result {
        Ok(array_ptr) => array_ptr,
        Err(_) => {
            tracing::error!("Panic occurred while getting command arguments array");
            std::ptr::null_mut()
        }
    }
}

/// Free the argument array memory
#[no_mangle]
pub extern "C" fn cirrus_free_argument_array(array: *mut CArgumentArray) {
    if array.is_null() {
        return;
    }

    unsafe {
        let array = Box::from_raw(array);

        if !array.items.is_null() && array.count > 0 {
            let items = Box::from_raw(std::slice::from_raw_parts_mut(array.items, array.count));

            for item in items.iter() {
                free_c_argument_info(*item);
            }
        }
    }
}
