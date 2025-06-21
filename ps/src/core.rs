use serde_json::Value;
use std::str::FromStr;
use tracing::info;

use az_core::argparse::SHPartialKeys;
pub use az_core::VERSION;
use az_core::{argparse::ArgValueAssignOp, error::AzError, uri::CrsUri, AzCore};

pub async fn initialize() {
    env_logger::init();
    let _ = AzCore::instance().await;
    info!("Cirrus library initialized");
}

// Asynchronous command execution function
pub async fn execute_command(
    uri: &str,
    parameters: Vec<(String, Value)>,
) -> Result<Option<Value>, AzError> {
    let core = AzCore::instance().await;
    let uri = CrsUri::from_str(uri).unwrap();
    let invocation = core.resolve_invocation(uri).await?;

    let mut cmd_value_ops: Vec<ArgValueAssignOp> = Vec::new();

    for (key, value) in parameters {
        let mut key_str = key.as_str();
        let is_append = key_str.ends_with('+');
        if is_append {
            key_str = &key_str[..key_str.len() - 1];
        }
        let keys = SHPartialKeys::try_from(key_str)?;
        cmd_value_ops.push(ArgValueAssignOp {
            keys,
            value,
            is_append,
            from_blank: false,
        });
    }

    core.execute_command(&invocation, cmd_value_ops).await
}
