use std::collections::HashMap;
use std::fmt;
use std::path::PathBuf;
use std::str::FromStr;
use std::sync::Arc;

use crate::cmd_uri::CmdUri;
use crate::error::AzInternalError;
use crate::plugin::AzPlugin;

use futures::future::try_join_all;
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
    /// the route table using the service uri segments as the key, it is used to find the service based on the cmd_uri
    route_table: HashMap<String, AzServiceInfo>,
    /// the built-in services that are loaded
    built_ins: HashMap<AzBuildInService, Arc<dyn AzService<T>>>,
    /// the plugins that are loaded, the key is the plugin name
    plugins: HashMap<String, Arc<dyn AzService<T>>>,
}

impl<T: AzServiceCtx> AzServiceCenter<T> {
    /// Create a new instance of AzServiceCenter
    /// TODO: we can add `profile` content here to support the switch between different profiles for different clouds
    pub(crate) async fn new() -> Result<Self, AzInternalError> {
        let center = if let Some(route_table) = Self::load_route_table().await {
            Self {
                route_table,
                built_ins: HashMap::new(),
                plugins: HashMap::new(),
            }
        } else {
            let mut center = Self {
                route_table: HashMap::new(),
                built_ins: HashMap::new(),
                plugins: HashMap::new(),
            };
            center.rebuild_route_table().await?;
            center
        };
        // TODO: here we should init the built-in services
        Ok(center)
    }
}

impl<T: AzServiceCtx> AzServiceCenter<T> {
    pub(crate) async fn rebuild_route_table(&mut self) -> Result<(), AzInternalError> {
        self.route_table.clear();
        AzBuildInService::iter().for_each(|service| {
            // using the first reversed node to ignore the params in the uri
            let node = service.uri().ascending_nodes().next().unwrap();
            self.route_table
                .insert(node.to_string(), AzServiceInfo::BuiltIn(service));
        });

        let plugin_paths = AzPlugin::get_plugin_paths()?;

        let plugin_futures: Vec<_> = plugin_paths
            .into_iter()
            .map(|path| AzPlugin::from_path(path))
            .collect();

        let plugins = try_join_all(plugin_futures).await?;

        for plugin in plugins {
            // using the first reversed node to ignore the params in the uri
            let node = plugin.uri.ascending_nodes().next().unwrap();
            self.route_table
                .insert(node.to_string(), AzServiceInfo::Plugin(plugin));
        }

        if Self::save_route_table(&self.route_table).await.is_err() {
            error!("Failed to save route table");
        }
        Ok(())
    }

    async fn load_route_table() -> Option<HashMap<String, AzServiceInfo>> {
        fs::read_to_string(get_route_table_path())
            .await
            .ok()
            .and_then(|s| serde_json::from_str(&s).ok())
    }

    async fn save_route_table(
        route_table: &HashMap<String, AzServiceInfo>,
    ) -> Result<(), AzInternalError> {
        fs::write(get_route_table_path(), serde_json::to_string(route_table)?).await?;
        Ok(())
    }
}

impl<T: AzServiceCtx> AzServiceCenter<T> {
    pub(crate) fn route(&self, cmd_uri: &CmdUri) -> Option<AzServiceInfo> {
        for node in cmd_uri.ascending_nodes() {
            if let Some(service_info) = self.route_table.get(&node.to_string()) {
                return Some(service_info.to_owned());
            }
        }
        None
    }

    pub(crate) async fn get_or_load_service(
        &mut self,
        service_info: &AzServiceInfo,
    ) -> Result<Arc<dyn AzService<T>>, AzInternalError> {
        let service = match service_info {
            AzServiceInfo::BuiltIn(service) => self
                .built_ins
                .get(service)
                // the build in services should be loaded when the service center is created
                .expect("The built_in service is not loaded")
                .to_owned(),
            AzServiceInfo::Plugin(plugin) => match self.plugins.get(&plugin.name) {
                Some(service) => service.to_owned(),
                None => {
                    let service = plugin.load_service::<T>().await?;
                    self.plugins
                        .insert(plugin.name.to_owned(), service.to_owned());
                    service
                }
            },
        };
        Ok(service)
    }

    pub(crate) fn iter_service_uris(&self) -> impl Iterator<Item = CmdUri> + '_ {
        self.route_table
            .keys()
            .map(|s| CmdUri::from_str(s).unwrap())
    }
}

impl fmt::Display for AzServiceInfo {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            AzServiceInfo::BuiltIn(service) => write!(f, "BuiltIn({})", service),
            AzServiceInfo::Plugin(plugin) => write!(f, "Plugin({})", plugin.name),
        }
    }
}

fn get_route_table_path() -> PathBuf {
    get_service_dir().join("route.json")
}
