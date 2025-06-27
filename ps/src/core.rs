use serde_json::Value;
use std::path::PathBuf;
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
    uri: CrsUri,
    parameters: Vec<(String, Value)>,
) -> Result<Option<Value>, AzError> {
    let core = AzCore::instance().await;
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

pub async fn import_plugin_component(path: PathBuf) -> Result<(CrsUri, String, String), AzError> {
    let core = AzCore::instance().await;
    let (uri, name, version) = core.load_plugin_component(path).await?;
    Ok((uri, name, version))
}

pub async fn component_cmdlets(uri: CrsUri) -> Result<Vec<CmdletInfo>, AzError> {
    let core = AzCore::instance().await;

    let mut cmdlets = Vec::new();
    let interface = core.get_component_interface(&uri).await?;
    for cmdlet in retrieve_cmdlets(&interface) {
        cmdlets.push(cmdlet);
    }

    Ok(cmdlets)
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
