use serde_json::Value;
use std::collections::HashMap;
#[cfg(feature = "ps")]
use std::path::PathBuf;
use std::str::FromStr;
use std::sync::Arc;
use tokio::sync::OnceCell;
use tokio::sync::RwLock;
use tracing::error;

use crate::argparse::ArgValueAssignOp;
use crate::component::AzComponentCtx;
use crate::component::{AzComponent, AzComponentCenter, AzComponentInfo};
use crate::config::{cloud::AzCloud, AzCfg};
use crate::error::{AzInternalError, AzResult, AzUserUsageError};
use crate::param::CrsParamBuilder;
use crate::pb::{
    crs_interface, execute_response, ComponentMetadata, CrsInterface, CrsOutputFlattenMode,
    CrsOutputFormat, ExecuteRequest, InterfaceRequest, MetadataRequest, ValidateRequest,
};
use crate::uri::CrsUri;

pub struct AzCore {
    pub(crate) cfg: RwLock<AzCfg>,
    pub(crate) component_center: RwLock<AzComponentCenter<Self>>,
}

pub struct AzInvocation<T: AzComponentCtx> {
    pub uri: CrsUri,
    component: Arc<dyn AzComponent<T>>,
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
    pub async fn resolve_invocation(&self, uri: CrsUri) -> AzResult<AzInvocation<Self>> {
        // TODO: add auto installation of the plugin based on the configuration
        let component = self.load_component(&uri).await?;

        let request = ValidateRequest {
            uri: uri.to_string(),
        };

        // TODO: add auto installation of the plugin based on the configuration
        let uri = component.validate(self, request).await?.uri.ok_or(
            AzUserUsageError::CommandNotFoundError(format!("command not found {}", uri)),
        )?;
        let uri = CrsUri::from_str(&uri)?;
        Ok(AzInvocation { uri, component })
    }

    pub async fn get_interface(&self, invocation: &AzInvocation<Self>) -> AzResult<CrsInterface> {
        let uri = &invocation.uri;
        let component = &invocation.component;
        let request = InterfaceRequest {
            uri: uri.to_string(),
            depth: 1, // only the top level command group and command information
        };

        let crs_interface = component.interface(self, request).await?.interface.ok_or(
            AzUserUsageError::CommandNotFoundError(format!("command not found {}", uri)),
        )?;

        Ok(crs_interface)
    }

    pub async fn execute_command(
        &self,
        invocation: &AzInvocation<Self>,
        arg_ops: Vec<ArgValueAssignOp>,
    ) -> AzResult<Option<Value>> {
        let uri = &invocation.uri;
        let component = &invocation.component;
        let request = InterfaceRequest {
            uri: uri.to_string(),
            depth: 1, // only the top level command group and command information
        };

        let crs_interface = component.interface(self, request).await?.interface.ok_or(
            AzUserUsageError::CommandNotFoundError(format!("command not found {}", uri)),
        )?;

        let command = crs_interface
            .level
            .and_then(|level| match level {
                crs_interface::Level::Command(command) => Some(command),
                _ => None,
            })
            .ok_or(AzUserUsageError::CommandGroupNotExecutableError)?;

        let mut param_builder = CrsParamBuilder::new(&command);
        for op in arg_ops {
            param_builder.apply(op)?;
        }
        let params = param_builder.format_and_build()?;

        let output_format = CrsOutputFormat {
            flatten_mode: CrsOutputFlattenMode::None as i32,
        };
        let request = ExecuteRequest {
            uri: uri.to_string(),
            params: Some(params),
            output_format: Some(output_format),
        };
        let response = component.execute(self, request).await?;
        match response.data.expect("execute result should not be none") {
            execute_response::Data::None(_) => Ok(None),
            execute_response::Data::Json(data) => {
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
    /// return the sub components of the given uri
    ///
    /// This function is used for cli to display the full help of a given command group
    pub async fn get_metadata_of_sub_components(
        &self,
        uri: &CrsUri,
    ) -> AzResult<HashMap<String, ComponentMetadata>> {
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
    /// return the component interface of the given uri
    ///
    /// This function is used for ps to dynamically generate the cmdlets for the given component
    pub async fn get_component_interface(&self, uri: &CrsUri) -> AzResult<CrsInterface> {
        let component = self.load_component(uri).await?;
        let request = InterfaceRequest {
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

    pub async fn load_plugin_component(&self, path: PathBuf) -> AzResult<(CrsUri, String, String)> {
        let plugin = self
            .component_center
            .write()
            .await
            .load_plugin_component(path)
            .await?;
        Ok((plugin.uri, plugin.name, plugin.version))
    }
}

/// Ansible only
#[cfg(feature = "ansible")]
impl AzCore {
    // TODO: define the ansible specific interface
}

impl AzCore {
    async fn load_component(&self, uri: &CrsUri) -> AzResult<Arc<dyn AzComponent<Self>>> {
        // TODO: add auto installation of the plugin based on the configuration
        let route = self.component_center.read().await.route(uri).ok_or(
            AzUserUsageError::CommandNotFoundError(format!("No route available for uri {}", uri)),
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
    pub(crate) async fn switch_cloud(&self, cloud: &AzCloud) -> AzResult<()> {
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
