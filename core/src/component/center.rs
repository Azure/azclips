use std::collections::HashMap;
use std::fmt;
use std::path::PathBuf;
use std::str::FromStr;
use std::sync::Arc;

use crate::cmd_uri::CmdUri;
use crate::config::cloud::AzCloud;
use crate::error::AzInternalError;
use crate::plugin::AzPlugin;

use futures::future::try_join_all;
use serde::{Deserialize, Serialize};
use tokio::fs;
use tracing::error;

use super::built_in::AzBuildIn;
use super::{get_component_dir, AzComponent, AzComponentCtx};

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(tag = "type", content = "info")]
pub enum AzComponentInfo {
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
    /// We use the cloud name to control the plugin components loading
    pub(crate) async fn new(cloud: &AzCloud) -> Result<Self, AzInternalError> {
        let mut center = Self {
            route_table: HashMap::new(),
            built_ins: HashMap::new(),
            plugins: HashMap::new(),
        };
        center.load_built_in_components();
        center.rebuild_route_table(cloud).await?;
        Ok(center)
    }

    /// It will rebuild the route table according to the cloud
    /// The route table will combine the built-in components and the plugins components
    /// It will also clear the loaded plugins
    pub(crate) async fn rebuild_route_table(
        &mut self,
        cloud: &AzCloud,
    ) -> Result<(), AzInternalError> {
        self.route_table =
            if let Some(plugin_route_table) = Self::load_plugin_route_table(cloud).await {
                plugin_route_table
            } else {
                Self::build_plugin_route_table(cloud).await?
            };
        self.built_ins.keys().for_each(|component| {
            // using the first reversed node to ignore the params in the uri
            let node = component.uri().ascending_nodes().next().unwrap();
            self.route_table.insert(
                node.to_string(),
                AzComponentInfo::BuiltIn(component.clone()),
            );
        });
        self.plugins.clear();
        Ok(())
    }
}

impl<T: AzComponentCtx> AzComponentCenter<T> {
    /// Build the plugin components route table for the given cloud and save it to the file
    async fn build_plugin_route_table(
        cloud: &AzCloud,
    ) -> Result<HashMap<String, AzComponentInfo>, AzInternalError> {
        let mut route_table = HashMap::new();

        let common_plugin_paths = AzPlugin::get_plugin_paths(None)?;
        // load cloud plugin paths later so that it can override the common plugins when creating the route table
        let cloud_plugin_paths = AzPlugin::get_plugin_paths(Some(cloud))?;

        let plugin_futures: Vec<_> = common_plugin_paths
            .into_iter()
            .chain(cloud_plugin_paths.into_iter())
            .map(|path| AzPlugin::from_path(path))
            .collect();

        let plugins = try_join_all(plugin_futures).await?;

        for plugin in plugins {
            // using the first reversed node to ignore the params in the uri
            let node = plugin.uri.ascending_nodes().next().unwrap();
            route_table.insert(node.to_string(), AzComponentInfo::Plugin(plugin));
        }

        if Self::save_plugin_route_table(cloud, &route_table)
            .await
            .is_err()
        {
            error!("Failed to save route table for cloud: {}", cloud);
        }
        Ok(route_table)
    }

    /// Load the plugin components route table from the file
    /// The file is different for different clouds
    async fn load_plugin_route_table(cloud: &AzCloud) -> Option<HashMap<String, AzComponentInfo>> {
        fs::read_to_string(get_route_table_path(cloud))
            .await
            .ok()
            .and_then(|s| serde_json::from_str(&s).ok())
    }

    /// Save the plugin components route table to the file
    /// The file is different for different clouds
    async fn save_plugin_route_table(
        cloud: &AzCloud,
        route_table: &HashMap<String, AzComponentInfo>,
    ) -> Result<(), AzInternalError> {
        fs::write(
            get_route_table_path(cloud),
            serde_json::to_string(route_table)?,
        )
        .await?;
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

fn get_route_table_path(cloud: &AzCloud) -> PathBuf {
    get_component_dir().join(format!("{}.json", cloud))
}
