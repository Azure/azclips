pub(crate) mod service;

use std::path::PathBuf;
use std::sync::Arc;

use serde::ser::SerializeMap;
use serde::{Deserialize, Serialize};
use serde_json::Map;

use crate::cmd_uri::CmdUri;
use crate::cmd_uri::{deserialize_cmd_uri, serialize_cmd_uri};
use crate::error::AzInternalError;
use crate::pb::command::CmdHelp;
use crate::service::{get_service_dir, AzService, AzServiceCtx};

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct AzPlugin {
    pub(crate) name: String,
    #[serde(
        serialize_with = "serialize_cmd_uri",
        deserialize_with = "deserialize_cmd_uri"
    )]
    pub(crate) uri: CmdUri,
    pub(crate) version: String,
    #[serde(
        serialize_with = "serialize_cmd_help",
        deserialize_with = "deserialize_cmd_help"
    )]
    /// The short summary of the plugin
    pub(crate) help: CmdHelp,
}

impl AzPlugin {
    /// load the login from file and construct the service
    pub(crate) async fn load_service<T: AzServiceCtx>(
        &self,
    ) -> Result<Arc<dyn AzService<T>>, AzInternalError> {
        todo!()
    }

    /// load the plugin info from the file path
    pub(crate) async fn from_path(path: PathBuf) -> Result<Self, AzInternalError> {
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

pub(crate) fn serialize_cmd_help<S>(help: &CmdHelp, serializer: S) -> Result<S::Ok, S::Error>
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

pub(crate) fn deserialize_cmd_help<'de, D>(deserializer: D) -> Result<CmdHelp, D::Error>
where
    D: serde::Deserializer<'de>,
{
    let s = Map::deserialize(deserializer)?;

    Ok(CmdHelp {
        short: s
            .get("short")
            .and_then(|v| v.as_str())
            .ok_or_else(|| serde::de::Error::missing_field("short"))?
            .to_string(),
        long: s.get("long").and_then(|v| v.as_str()).map(String::from),
    })
}
