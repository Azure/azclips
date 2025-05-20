pub(crate) mod component;

use std::path::PathBuf;
use std::sync::Arc;

use serde::{Deserialize, Serialize};

use crate::component::{get_component_dir, AzComponent, AzComponentCtx};
use crate::config::cloud::AzCloud;
use crate::error::AzInternalError;
use crate::pb::CrsHelp;
use crate::uri::CrsUri;

const PLUGIN_FILE_EXTENSION: &str = "plugin";
const PLUGIN_DIR_NAME: &str = "plugins";

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct AzPlugin {
    pub(crate) name: String,
    pub(crate) uri: CrsUri,
    pub(crate) version: String,
    /// The short summary of the plugin
    pub(crate) help: CrsHelp,
}

impl AzPlugin {
    /// load the plugin from file and construct the component
    pub(crate) async fn load<T: AzComponentCtx>(
        &self,
    ) -> Result<Arc<dyn AzComponent<T>>, AzInternalError> {
        todo!()
    }

    /// load the plugin info from the file path
    pub(crate) async fn from_path(_: PathBuf) -> Result<Self, AzInternalError> {
        todo!()
    }

    /// If the cloud is specified, it will iterate over the sub directory with cloud specified in the plugin directory,
    /// otherwise, it will iterate over the root plugin directory.
    pub(crate) fn get_plugin_paths(
        cloud: Option<&AzCloud>,
    ) -> Result<Vec<PathBuf>, AzInternalError> {
        let mut plugin_paths = Vec::new();
        let plugin_dir = get_plugin_dir(cloud);
        if !plugin_dir.exists() {
            return Ok(plugin_paths);
        }
        for entry in std::fs::read_dir(plugin_dir)? {
            let entry = entry?;
            let path = entry.path();
            if path.is_file()
                && path.extension() == Some(std::ffi::OsStr::new(PLUGIN_FILE_EXTENSION))
            {
                plugin_paths.push(path);
            }
        }
        Ok(plugin_paths)
    }
}

fn get_plugin_dir(cloud: Option<&AzCloud>) -> PathBuf {
    let plugin_dir = get_component_dir().join(PLUGIN_DIR_NAME);
    if let Some(cloud) = cloud {
        plugin_dir.join(cloud.to_string())
    } else {
        plugin_dir
    }
}
