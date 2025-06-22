use serde_json::Value;
use std::str::FromStr;
use strum::IntoEnumIterator;
use tracing::info;

pub use az_core::VERSION;
use az_core::{argparse::ArgValueAssignOp, error::AzError, uri::CrsUri, AzCore};
use az_core::{argparse::SHPartialKeys, component::AzBuiltIn};

use crate::cmdlet::{retrieve_cmdlets, CmdletInfo};

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

pub async fn builtin_cmdlets() -> Result<Vec<CmdletInfo>, AzError> {
    let core = AzCore::instance().await;

    let mut cmdlets = Vec::new();
    for component in AzBuiltIn::iter() {
        let uri = component.uri();
        let interface = core.get_component_interface(&uri).await?;
        for cmdlet in retrieve_cmdlets(&interface) {
            cmdlets.push(cmdlet);
        }
    }

    Ok(cmdlets)
}
