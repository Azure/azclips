pub mod service;

use std::path::PathBuf;
use std::sync::Arc;

use serde::{Deserialize, Serialize};
use tokio::fs;

use crate::cmd_uri::CmdUri;
use crate::cmd_uri::{deserialize_cmd_uri, serialize_cmd_uri};
use crate::error::AzInternalError;
use crate::service::{get_service_dir, AzService, AzServiceCtx};
use crate::AzResult;

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct AzPlugin {
    pub(crate) name: String,
    #[serde(
        serialize_with = "serialize_cmd_uri",
        deserialize_with = "deserialize_cmd_uri"
    )]
    pub(crate) uri: CmdUri,
    pub(crate) version: String,
}

impl AzPlugin {
    /// load the login from file and construct the service
    pub(crate) async fn load_service<T: AzServiceCtx>(
        &self,
    ) -> Result<Arc<dyn AzService<T>>, AzInternalError> {
        todo!()
    }

    /// load the plugin info from the file path
    pub(crate) fn from_path(path: PathBuf) -> Result<Self, AzInternalError> {
        unimplemented!()
    }

    pub(crate) fn get_plugin_paths() -> Result<Vec<PathBuf>, AzInternalError> {
        let mut plugin_paths = Vec::new();
        let plugin_dir = get_plugin_dir();
        if !plugin_dir.exists() {
            return Ok(plugin_paths);
        }
        for entry in std::fs::read_dir(plugin_dir)? {
            let entry = entry?;
            let path = entry.path();
            if path.is_file() && path.extension() == Some(std::ffi::OsStr::new("plugin")) {
                plugin_paths.push(path);
            }
        }
        Ok(plugin_paths)
    }
}

fn get_plugin_dir() -> PathBuf {
    get_service_dir().join("plugins")
}
