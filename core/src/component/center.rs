use std::collections::HashMap;
use std::fmt;
use std::path::PathBuf;
use std::str::FromStr;
use std::sync::Arc;

use crate::cmd_uri::CmdUri;
use crate::error::AzInternalError;
use crate::plugin::AzPlugin;
use crate::top::component::TopComponent;

use futures::future::try_join_all;
use serde::{Deserialize, Serialize};
use strum::IntoEnumIterator;
use tokio::fs;
use tracing::error;

use super::built_in::AzBuildIn;
use super::built_in::{deserialize_az_build_in_component, serialize_az_build_in_component};
use super::{get_component_dir, AzComponent, AzComponentCtx};

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(tag = "type", content = "info")]
pub enum AzComponentInfo {
    #[serde(
        serialize_with = "serialize_az_build_in_component",
        deserialize_with = "deserialize_az_build_in_component"
    )]
    BuiltIn(AzBuildIn),
    Plugin(AzPlugin),
}

/// The center to manage the components
pub struct AzComponentCenter<T: AzComponentCtx> {
    /// the route table using the component uri segments as the key, it is used to find the component based on the cmd_uri
    pub(super) route_table: HashMap<String, AzComponentInfo>,
    /// the built-in components that are loaded
    pub(super) built_ins: HashMap<AzBuildIn, Arc<Box<dyn AzComponent<T>>>>,
    /// the plugins that are loaded, the key is the plugin name
    pub(super) plugins: HashMap<String, Arc<Box<dyn AzComponent<T>>>>,
}

impl<T: AzComponentCtx> AzComponentCenter<T> {
    /// Create a new instance of AzComponentCenter
    /// TODO: we can add `profile` content here to support the switch between different profiles for different clouds
    pub(crate) async fn new() -> Result<Self, AzInternalError> {
        let mut center = if let Some(route_table) = Self::load_route_table().await {
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
        center.load_built_in_components();
        Ok(center)
    }
}

impl<T: AzComponentCtx> AzComponentCenter<T> {
    pub(crate) async fn rebuild_route_table(&mut self) -> Result<(), AzInternalError> {
        self.route_table.clear();
        AzBuildIn::iter().for_each(|component| {
            // using the first reversed node to ignore the params in the uri
            let node = component.uri().ascending_nodes().next().unwrap();
            self.route_table
                .insert(node.to_string(), AzComponentInfo::BuiltIn(component));
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
                .insert(node.to_string(), AzComponentInfo::Plugin(plugin));
        }

        if Self::save_route_table(&self.route_table).await.is_err() {
            error!("Failed to save route table");
        }
        Ok(())
    }

    async fn load_route_table() -> Option<HashMap<String, AzComponentInfo>> {
        fs::read_to_string(get_route_table_path())
            .await
            .ok()
            .and_then(|s| serde_json::from_str(&s).ok())
    }

    async fn save_route_table(
        route_table: &HashMap<String, AzComponentInfo>,
    ) -> Result<(), AzInternalError> {
        fs::write(get_route_table_path(), serde_json::to_string(route_table)?).await?;
        Ok(())
    }
}

impl<T: AzComponentCtx> AzComponentCenter<T> {
    pub(crate) fn route(&self, cmd_uri: &CmdUri) -> Option<AzComponentInfo> {
        for node in cmd_uri.ascending_nodes() {
            if let Some(info) = self.route_table.get(&node.to_string()) {
                return Some(info.to_owned());
            }
        }
        None
    }

    pub(crate) async fn get_or_load_component(
        &mut self,
        info: &AzComponentInfo,
    ) -> Result<Arc<Box<dyn AzComponent<T>>>, AzInternalError> {
        let component = match info {
            AzComponentInfo::BuiltIn(component) => self
                .built_ins
                .get(component)
                // the build in components should be loaded when the component center is created
                .expect("The built_in component is not loaded")
                .to_owned(),
            AzComponentInfo::Plugin(plugin) => match self.plugins.get(&plugin.name) {
                Some(component) => component.to_owned(),
                None => {
                    let component = plugin.load::<T>().await?;
                    self.plugins
                        .insert(plugin.name.to_owned(), component.to_owned());
                    component
                }
            },
        };
        Ok(component)
    }

    pub(crate) fn iter_component_uris(&self) -> impl Iterator<Item = CmdUri> + '_ {
        self.route_table
            .keys()
            .map(|s| CmdUri::from_str(s).unwrap())
    }
}

impl fmt::Display for AzComponentInfo {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            AzComponentInfo::BuiltIn(component) => write!(f, "BuiltIn({})", component),
            AzComponentInfo::Plugin(plugin) => write!(f, "Plugin({})", plugin.name),
        }
    }
}

fn get_route_table_path() -> PathBuf {
    get_component_dir().join("route.json")
}
