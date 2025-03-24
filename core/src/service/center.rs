use std::collections::HashMap;
use std::path::PathBuf;
use std::sync::Arc;

use crate::error::AzInternalError;
use crate::plugin::AzPlugin;

use serde::{Deserialize, Serialize};
use strum::IntoEnumIterator;
use tokio::fs;
use tracing::error;

use super::built_in::AzBuildInService;
use super::built_in::{deserialize_az_build_in_service, serialize_az_build_in_service};
use super::{get_service_dir, AzService, AzServiceCtx};

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(tag = "type", content = "info")]
pub enum AzServiceInfo {
    #[serde(
        serialize_with = "serialize_az_build_in_service",
        deserialize_with = "deserialize_az_build_in_service"
    )]
    BuiltIn(AzBuildInService),
    Plugin(AzPlugin),
}

pub struct AzServiceCenter<T: AzServiceCtx> {
    route_table: HashMap<String, AzServiceInfo>,
    loaded_plugins: HashMap<String, Arc<dyn AzService<T>>>,
}

impl<T: AzServiceCtx> AzServiceCenter<T> {
    /// Create a new instance of AzServiceCenter
    /// TODO: we can add `profile` content here to support the switch between different profiles for different clouds
    pub(crate) async fn new() -> Result<Self, AzInternalError> {
        let route_table = match Self::load_route_table().await {
            Some(route_table) => route_table,
            None => Self::rebuild_route_table().await?,
        };
        Ok(Self {
            route_table,
            loaded_plugins: HashMap::new(),
        })
    }
}

impl<T: AzServiceCtx> AzServiceCenter<T> {
    async fn load_route_table() -> Option<HashMap<String, AzServiceInfo>> {
        fs::read_to_string(get_route_table_path())
            .await
            .ok()
            .and_then(|s| serde_json::from_str(&s).ok())
    }

    async fn rebuild_route_table() -> Result<HashMap<String, AzServiceInfo>, AzInternalError> {
        let mut route_table = HashMap::new();
        AzBuildInService::iter().for_each(|service| {
            route_table.insert(
                service.uri().segments_str(),
                AzServiceInfo::BuiltIn(service),
            );
        });

        let plugin_paths = AzPlugin::get_plugin_paths()?;
        for path in plugin_paths {
            let plugin = AzPlugin::from_path(path)?;
            route_table.insert(plugin.uri.segments_str(), AzServiceInfo::Plugin(plugin));
        }

        if Self::save_route_table(&route_table).await.is_err() {
            error!("Failed to save route table");
        }
        Ok(route_table)
    }

    async fn save_route_table(
        route_table: &HashMap<String, AzServiceInfo>,
    ) -> Result<(), AzInternalError> {
        fs::write(get_route_table_path(), serde_json::to_string(route_table)?).await?;
        Ok(())
    }
}

fn get_route_table_path() -> PathBuf {
    get_service_dir().join("route.json")
}
