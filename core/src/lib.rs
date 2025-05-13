//! Azure CLI Lite Core Library
//!
//! This library provides the core functionality for the Azure CLI Lite application.
pub mod argparse;
pub mod cmd_uri;
pub mod error;
pub mod pb;

pub(crate) mod account;
pub(crate) mod component;
pub(crate) mod config;
pub(crate) mod param;
pub(crate) mod plugin;
/// The module defines the top level commands
pub(crate) mod top;
pub(crate) mod utils;

use argparse::ArgValueAssignOp;
use cmd_uri::CmdUri;
use component::center::AzComponentCenter;
use component::center::AzComponentInfo;
use component::AzComponent;
use config::{AzCfg, AzCloud};
use error::AzInternalError;
use error::AzResult;
use error::AzUserUsageError;
use pb::command::cmd_interface;
use pb::component::ComponentMetadata;
use pb::component::MetadataRequest;
use serde_json::Value;
use std::collections::HashMap;
use std::str::FromStr;
use std::sync::Arc;
use tokio::sync::OnceCell;
use tokio::sync::RwLock;
use tracing::error;

/// The version of the core library
pub const VERSION: &str = env!("CARGO_PKG_VERSION");

/// The application name will be used the the host part of cmd uri
pub const APPLICATION_NAME: &str = env!("CARGO_APPLICATION_NAME");

pub struct AzCore {
    pub(crate) cfg: RwLock<AzCfg>,
    pub(crate) component_center: RwLock<AzComponentCenter<Self>>,
}

impl AzCore {
    /// Get the global instance of AzCore
    pub async fn instance() -> &'static Arc<AzCore> {
        static INSTANCE: OnceCell<Arc<AzCore>> = OnceCell::const_new();
        INSTANCE
            .get_or_init(|| async { Arc::new(AzCore::new().await) })
            .await
    }

    /// Create a new instance of AzCore
    async fn new() -> Self {
        let cfg = AzCfg::load().expect("failed to load config");
        let component_center = AzComponentCenter::<Self>::new(&cfg.clouds.get_active().name)
            .await
            .expect("failed to create component center");
        // Perform async initialization here
        Self {
            cfg: RwLock::new(cfg),
            // Initialize fields as needed
            component_center: RwLock::new(component_center),
        }
    }
}

impl AzCore {
    pub async fn check_cmd_uri(&self, cmd_uri: CmdUri) -> AzResult<CmdUri> {
        // TODO: add auto installation of the plugin based on the configuration
        let component = self.load_component(&cmd_uri).await?;

        let request = pb::component::CheckRequest {
            uri: cmd_uri.to_string(),
        };

        // TODO: add auto installation of the plugin based on the configuration
        let cmd_uri = component.check(self, request).await?.uri.ok_or(
            AzUserUsageError::CommandNotFoundError(format!("command not found {}", cmd_uri)),
        )?;
        let cmd_uri = CmdUri::from_str(&cmd_uri)?;
        Ok(cmd_uri)
    }

    pub async fn get_cmd_interface(&self, cmd_uri: &CmdUri) -> AzResult<pb::command::CmdInterface> {
        let component = self.load_component(cmd_uri).await?;
        let request = pb::component::InterfaceRequest {
            uri: cmd_uri.to_string(),
            depth: 1, // only the top level command group and command information
        };

        let cmd_interface = component.interface(self, request).await?.interface.ok_or(
            AzUserUsageError::CommandNotFoundError(format!("command not found {}", cmd_uri)),
        )?;

        Ok(cmd_interface)
    }

    pub async fn execute_cmd(
        &self,
        cmd_uri: &CmdUri,
        cmd_arg_ops: Vec<ArgValueAssignOp>,
    ) -> AzResult<Option<Value>> {
        let component = self.load_component(cmd_uri).await?;
        let request = pb::component::InterfaceRequest {
            uri: cmd_uri.to_string(),
            depth: 1, // only the top level command group and command information
        };

        let cmd_interface = component.interface(self, request).await?.interface.ok_or(
            AzUserUsageError::CommandNotFoundError(format!("command not found {}", cmd_uri)),
        )?;

        let cmd = cmd_interface
            .level
            .and_then(|level| match level {
                cmd_interface::Level::Command(cmd) => Some(cmd),
                _ => None,
            })
            .ok_or(AzUserUsageError::CommandGroupNotExecutableError)?;

        let mut param_builder = param::CmdParamBuilder::default();
        for op in cmd_arg_ops {
            param_builder.apply(op)?;
        }
        let params = param_builder.format_and_build(&cmd)?;

        let output_format = pb::command::CmdOutputFormat {
            flatten_mode: pb::command::CmdOutputFlattenMode::None as i32,
        };
        let request = pb::component::ExecuteRequest {
            uri: cmd_uri.to_string(),
            params: Some(params),
            output_format: Some(output_format),
        };
        let response = component.execute(self, request).await?;
        match response.data.expect("execute result should not be none") {
            pb::component::execute_response::Data::None(_) => Ok(None),
            pb::component::execute_response::Data::Json(data) => {
                Ok(Some(serde_json::from_slice(&data).map_err(|e| {
                    error!("failed to deserialize the execute result: {}", e);
                    AzInternalError::Json(e)
                })?))
            }
        }
    }
}

/// CLI only
#[cfg(feature = "cli")]
impl AzCore {
    /// return the sub components of the given cmd uri
    ///
    /// This function is used for cli to display the full help of a given command group
    pub async fn get_metadata_of_sub_components(
        &self,
        uri: &CmdUri,
    ) -> AzResult<HashMap<String, pb::component::ComponentMetadata>> {
        let component_uris = self
            .component_center
            .read()
            .await
            .iter_component_uris()
            .filter(|component_uri| component_uri.is_child_of(uri, 1))
            .collect::<Vec<_>>();

        let mut sub_components = HashMap::new();
        for component_uri in component_uris {
            let info = self
                .component_center
                .read()
                .await
                .route(&component_uri)
                .unwrap();
            let metadata = match info {
                AzComponentInfo::BuiltIn(_) => {
                    let component = self
                        .component_center
                        .write()
                        .await
                        .get_or_load_component(&info)
                        .await?;
                    component
                        .metadata(self, MetadataRequest::default())
                        .await?
                        .metadata
                        .expect("built-in component should have metadata")
                }
                // construct the metadata from the plugin info to avoid loading the plugins for performance
                AzComponentInfo::Plugin(plugin) => ComponentMetadata {
                    name: plugin.name.clone(),
                    uri: plugin.uri.to_string(),
                    version: plugin.version.clone(),
                    help: Some(plugin.help),
                },
            };

            sub_components.insert(component_uri.last_segment().to_string(), metadata);
        }

        Ok(sub_components)
    }
}

/// PowerShell only
#[cfg(feature = "ps")]
impl AzCore {
    /// return the component interface of the given cmd uri
    ///
    /// This function is used for ps to dynamically generate the cmdlets for the given component
    pub async fn get_component_interface(
        &self,
        uri: &CmdUri,
    ) -> AzResult<pb::command::CmdInterface> {
        let component = self.load_component(uri).await?;
        let request = pb::component::InterfaceRequest {
            uri: uri.to_string(),
            depth: 0, // all the depth
        };
        let component_interface = component.interface(self, request).await?.interface.ok_or(
            AzUserUsageError::CommandNotFoundError(format!(
                "component interface not found {}",
                uri
            )),
        )?;
        Ok(component_interface)
    }
}

/// Ansible only
#[cfg(feature = "ansible")]
impl AzCore {
    // TODO: define the ansible specific interface
}

impl AzCore {
    async fn load_component(&self, cmd_uri: &CmdUri) -> AzResult<Arc<Box<dyn AzComponent<Self>>>> {
        // TODO: add auto installation of the plugin based on the configuration
        let route = self.component_center.read().await.route(&cmd_uri).ok_or(
            AzUserUsageError::CommandNotFoundError(format!(
                "No route available for cmd uri {}",
                cmd_uri
            )),
        )?;
        let component = self
            .component_center
            .write()
            .await
            .get_or_load_component(&route)
            .await?;
        Ok(component)
    }

    /// Switch the active cloud
    ///
    /// If a customized cloud is not registered before, it will raise an error
    /// The switch will also rebuild the component center route table for the cloud
    async fn switch_cloud<'a>(&'a self, cloud: &AzCloud) -> AzResult<()> {
        {
            let mut cfg = self.cfg.write().await;
            _ = cfg.clouds.set_active(cloud.clone())?;
            cfg.save()?;
        }
        self.component_center
            .write()
            .await
            .rebuild_route_table(cloud)
            .await?;
        Ok(())
    }
}
