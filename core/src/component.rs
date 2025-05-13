pub mod built_in;
pub mod center;
pub mod cmd;
pub mod ctx;

use std::path::PathBuf;

use async_trait::async_trait;

use crate::error::AzError;
use crate::pb::component as pb;
use crate::{config, AzCfg};

#[async_trait]
pub trait AzComponentCtx: Send + Sync + 'static {
    /// get the core info
    async fn get_core_info(
        &self,
        request: pb::GetCoreInfoRequest,
    ) -> Result<pb::GetCoreInfoResponse, AzError>;

    /// get a clone of the core config
    ///
    /// # Example get the active cloud and its endpoint and resource id for authentication
    /// ```rust
    /// let cfg = ctx.get_cfg();
    /// let cloud = cfg.cloud.get_active();
    /// let endpoint = cloud.get_endpoint(endpoint::AUTHENTICATION).await;
    /// let resource_id = cloud.get_resource_id(resource_id::AUTHENTICATION).await;
    /// ```
    async fn get_cfg(&self) -> AzCfg;
}

/// the trait of a component
#[async_trait]
pub trait AzComponent<T: AzComponentCtx>: Send + Sync + 'static {
    /// get the metadata of the component
    async fn metadata(
        &self,
        ctx: &T,
        request: pb::MetadataRequest,
    ) -> Result<pb::MetadataResponse, AzError>;

    /// check a cmd uri is available or not
    /// if a cmd uri has positional arguments as segments part,it will be converted to params in the query part in the returned cmd uri
    async fn check(&self, ctx: &T, request: pb::CheckRequest)
        -> Result<pb::CheckResponse, AzError>;

    /// get the interface of a cmd uri
    async fn interface(
        &self,
        ctx: &T,
        request: pb::InterfaceRequest,
    ) -> Result<pb::InterfaceResponse, AzError>;

    /// run a executable cmd uri
    async fn execute(
        &self,
        ctx: &T,
        request: pb::ExecuteRequest,
    ) -> Result<pb::ExecuteResponse, AzError>;
}

pub fn get_component_dir() -> PathBuf {
    let config_dir = config::get_config_dir();
    config_dir.join("components")
}
