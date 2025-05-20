pub(crate) mod component;

use std::path::PathBuf;
use std::sync::Arc;

use serde::ser::SerializeMap;
use serde::{Deserialize, Serialize};
use serde_json::Map;

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
    #[serde(
        serialize_with = "serialize_cmd_help",
        deserialize_with = "deserialize_cmd_help"
    )]
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

pub(crate) fn serialize_cmd_help<S>(help: &CrsHelp, serializer: S) -> Result<S::Ok, S::Error>
where
    S: serde::Serializer,
{
    let mut map = serializer.serialize_map(Some(2))?;
    map.serialize_entry("short", &help.short)?;
    if let Some(long) = &help.long {
        map.serialize_entry("long", &long)?;
    }
    map.end()
}

pub(crate) fn deserialize_cmd_help<'de, D>(deserializer: D) -> Result<CrsHelp, D::Error>
where
    D: serde::Deserializer<'de>,
{
    let s = Map::deserialize(deserializer)?;

    Ok(CrsHelp {
        short: s
            .get("short")
            .and_then(|v| v.as_str())
            .ok_or_else(|| serde::de::Error::missing_field("short"))?
            .to_string(),
        long: s.get("long").and_then(|v| v.as_str()).map(String::from),
    })
}
