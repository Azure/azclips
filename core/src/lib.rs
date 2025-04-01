//! Azure CLI Lite Core Library
//!
//! This library provides the core functionality for the Azure CLI Lite application.
pub mod argparse;
pub mod cmd_uri;
pub mod error;
pub mod pb;

pub(crate) mod account;
pub(crate) mod cloud;
pub(crate) mod config;
pub(crate) mod param;
pub(crate) mod plugin;
pub(crate) mod service;
/// The module defines the top level commands
pub(crate) mod top;
pub(crate) mod utils;

use argparse::ArgValueAssignOp;
use cmd_uri::CmdUri;
use error::AzInternalError;
use error::AzResult;
use error::AzUserUsageError;
use pb::command::cmd_interface;
use pb::service::MetadataRequest;
use pb::service::ServiceMetadata;
use serde_json::Value;
use service::center::AzServiceCenter;
use service::center::AzServiceInfo;
use service::AzService;
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
    // Add fields as needed
    service_center: RwLock<AzServiceCenter<Self>>,
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
        let service_center = AzServiceCenter::<Self>::new()
            .await
            .expect("failed to create service center");
        // Perform async initialization here
        Self {
            // Initialize fields as needed
            service_center: RwLock::new(service_center),
        }
    }
}

impl AzCore {
    pub async fn check_cmd_uri(&self, cmd_uri: CmdUri) -> AzResult<CmdUri> {
        // TODO: add auto installation of the plugin based on the configuration
        let service = self.load_service(&cmd_uri).await?;

        let request = pb::service::CheckRequest {
            uri: cmd_uri.to_string(),
        };

        // TODO: add auto installation of the plugin based on the configuration
        let cmd_uri = service.check(self, request).await?.uri.ok_or(
            AzUserUsageError::CommandNotFoundError(format!("command not found {}", cmd_uri)),
        )?;
        let cmd_uri = CmdUri::from_str(&cmd_uri)?;
        Ok(cmd_uri)
    }

    pub async fn get_cmd_interface(&self, cmd_uri: &CmdUri) -> AzResult<pb::command::CmdInterface> {
        let service = self.load_service(cmd_uri).await?;
        let request = pb::service::InterfaceRequest {
            uri: cmd_uri.to_string(),
            depth: 1, // only the top level command group and command information
        };

        let cmd_interface = service.interface(self, request).await?.interface.ok_or(
            AzUserUsageError::CommandNotFoundError(format!("command not found {}", cmd_uri)),
        )?;

        Ok(cmd_interface)
    }

    pub async fn execute_cmd(
        &self,
        cmd_uri: &CmdUri,
        cmd_arg_ops: Vec<ArgValueAssignOp>,
    ) -> AzResult<Option<Value>> {
        let service = self.load_service(cmd_uri).await?;
        let request = pb::service::InterfaceRequest {
            uri: cmd_uri.to_string(),
            depth: 1, // only the top level command group and command information
        };

        let cmd_interface = service.interface(self, request).await?.interface.ok_or(
            AzUserUsageError::CommandNotFoundError(format!("command not found {}", cmd_uri)),
        )?;

        let cmd = cmd_interface
            .level
            .and_then(|level| match level {
                cmd_interface::Level::Command(cmd) => Some(cmd),
                _ => None,
            })
            .ok_or(AzUserUsageError::CommandGroupNotExecutableError)?;

        let params = cmd_arg_ops
            .into_iter()
            .fold(param::CmdParamBuilder::default(), |builder, op| {
                builder.apply(op)
            })
            .format_and_build(&cmd)?;

        let output_format = pb::command::CmdOutputFormat {
            flatten_mode: pb::command::CmdOutputFlattenMode::None as i32,
        };
        let request = pb::service::ExecuteRequest {
            uri: cmd_uri.to_string(),
            params: Some(params),
            output_format: Some(output_format),
        };
        let response = service.execute(self, request).await?;
        match response.data.expect("execute result should not be none") {
            pb::service::execute_response::Data::None(_) => Ok(None),
            pb::service::execute_response::Data::Json(data) => {
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
    /// return the sub services of the given cmd uri
    ///
    /// This function is used for cli to display the full help of a given command group
    pub async fn get_metadata_of_sub_services(
        &self,
        uri: &CmdUri,
    ) -> AzResult<HashMap<String, pb::service::ServiceMetadata>> {
        let service_uris = self
            .service_center
            .read()
            .await
            .iter_service_uris()
            .filter(|service_uri| service_uri.is_child_of(uri, 1))
            .collect::<Vec<_>>();

        let mut sub_services = HashMap::new();
        for service_uri in service_uris {
            let service_info = self
                .service_center
                .read()
                .await
                .route(&service_uri)
                .unwrap();
            let metadata = match service_info {
                AzServiceInfo::BuiltIn(_) => {
                    let service = self
                        .service_center
                        .write()
                        .await
                        .get_or_load_service(&service_info)
                        .await?;
                    service
                        .metadata(self, MetadataRequest::default())
                        .await?
                        .metadata
                        .expect("built-in service should have metadata")
                }
                // construct the metadata from the plugin info to avoid loading the plugins for performance
                AzServiceInfo::Plugin(plugin) => ServiceMetadata {
                    name: plugin.name.clone(),
                    uri: plugin.uri.to_string(),
                    version: plugin.version.clone(),
                    help: Some(plugin.help),
                },
            };

            sub_services.insert(service_uri.last_segment().to_string(), metadata);
        }

        Ok(sub_services)
    }
}

/// PowerShell only
#[cfg(feature = "ps")]
impl AzCore {
    /// return the service interface of the given cmd uri
    ///
    /// This function is used for ps to dynamically generate the cmdlets for the given service
    pub async fn get_service_interface(&self, uri: &CmdUri) -> AzResult<pb::command::CmdInterface> {
        let service = self.load_service(uri).await?;
        let request = pb::service::InterfaceRequest {
            uri: uri.to_string(),
            depth: 0, // all the depth
        };
        let service_interface = service.interface(self, request).await?.interface.ok_or(
            AzUserUsageError::CommandNotFoundError(format!("service interface not found {}", uri)),
        )?;
        Ok(service_interface)
    }
}

impl AzCore {
    async fn load_service(&self, cmd_uri: &CmdUri) -> AzResult<Arc<dyn AzService<Self>>> {
        // TODO: add auto installation of the plugin based on the configuration
        let route = self.service_center.read().await.route(&cmd_uri).ok_or(
            AzUserUsageError::CommandNotFoundError(format!(
                "No route available for cmd uri {}",
                cmd_uri
            )),
        )?;
        let service = self
            .service_center
            .write()
            .await
            .get_or_load_service(&route)
            .await?;
        Ok(service)
    }
}
