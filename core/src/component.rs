mod built_in;
mod center;
mod cmd;
mod ctx;

use async_trait::async_trait;
use std::path::PathBuf;

use crate::error::AzError;
use crate::pb::{
    CheckRequest, CheckResponse, ExecuteRequest, ExecuteResponse, InterfaceRequest,
    InterfaceResponse, MetadataResponse,
};
use crate::{config, pb::MetadataRequest};

pub use center::{AzComponentCenter, AzComponentInfo};
pub use cmd::{AzCmd, AzCmdExecute, AzCmdGroup};
pub use ctx::AzComponentCtx;

/// the trait of a component
#[async_trait]
pub trait AzComponent<T: AzComponentCtx>: Send + Sync + 'static {
    /// get the metadata of the component
    async fn metadata(
        &self,
        ctx: &T,
        request: MetadataRequest,
    ) -> Result<MetadataResponse, AzError>;

    /// check a cmd uri is available or not
    /// if a cmd uri has positional arguments as segments part,it will be converted to params in the query part in the returned cmd uri
    async fn check(&self, ctx: &T, request: CheckRequest) -> Result<CheckResponse, AzError>;

    /// get the interface of a cmd uri
    async fn interface(
        &self,
        ctx: &T,
        request: InterfaceRequest,
    ) -> Result<InterfaceResponse, AzError>;

    /// run a executable cmd uri
    async fn execute(&self, ctx: &T, request: ExecuteRequest) -> Result<ExecuteResponse, AzError>;
}

pub fn get_component_dir() -> PathBuf {
    let config_dir = config::get_config_dir();
    config_dir.join("components")
}
