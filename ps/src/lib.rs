use std::collections::HashMap;
use std::ffi::{CStr, CString};
use std::os::raw::c_char;
use std::str::FromStr;

use az_core::{
    argparse::{self, ArgValueAssignOp},
    error::AzError,
    uri::CrsUri,
    AzCore,
};
use serde_json::Value;

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

#[no_mangle]
pub extern "C" fn run(uri: *const c_char, arguments_json: *const c_char) -> *mut c_char {
    let c_str = unsafe { CStr::from_ptr(uri) };
    let uri: &str = c_str.to_str().unwrap();

    let args_json = unsafe { CStr::from_ptr(arguments_json) }.to_str().unwrap();

    let args_maps: HashMap<String, Value> = match serde_json::from_str(args_json) {
        Ok(result) => result,
        Err(e) => {
            println!("Failed to parse arguments: {}", e);
            HashMap::<String, Value>::new()
        }
    };

    let rt = tokio::runtime::Runtime::new().unwrap();
    let result = rt.block_on(async { execute_command_async(uri, args_maps).await });

    // TODO: handle the error
    match result {
        Ok(Some(value)) => {
            let json = serde_json::to_string(&value).unwrap();
            CString::new(json).unwrap().into_raw()
        }
        Ok(None) => CString::new("").unwrap().into_raw(),
        Err(e) => {
            let err_json = serde_json::to_string(&format!("Error: {}", e)).unwrap();
            CString::new(err_json).unwrap().into_raw()
        }
    }
}
