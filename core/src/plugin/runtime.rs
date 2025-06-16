use std::path::PathBuf;
use async_trait::async_trait;
use prost::Message;

use crate::component::{AzComponent, AzComponentCtx};
use crate::error::{AzError, AzInternalError};
use crate::pb::{ComponentMetadata, CrsPluginComponent, ExecuteRequest, ExecuteResponse, InterfaceRequest, InterfaceResponse, MetadataRequest, MetadataResponse, ValidateRequest, ValidateResponse};

pub struct PluginRuntime {
    inner: CrsPluginComponent,
}

impl PluginRuntime {
    pub(crate) async fn load_from_path(path: &PathBuf) -> Result<Self, AzInternalError> {
        let contents = tokio::fs::read(path).await?;
        let component = CrsPluginComponent::decode(&contents[..])
            .map_err(|e| AzInternalError::PluginLoadError(e))?;
        Ok(Self { inner: component })
    }

    pub(crate) fn get_metadata(&self) -> Result<ComponentMetadata, AzInternalError> {
        let metadata = self.inner.metadata.clone();
        metadata.ok_or(AzInternalError::InvalidPluginError(format!("Invalid plugin: missing metadata")))
    }
}

#[async_trait]
impl<T: AzComponentCtx> AzComponent<T> for PluginRuntime {
    async fn metadata(
        &self,
        _: &T,
        _: MetadataRequest,
    ) -> Result<MetadataResponse, AzError> {    
        let metadata = self.get_metadata()?;
        Ok(MetadataResponse { metadata: Some(metadata) })
    }

    async fn validate(
        &self,
        ctx: &T,
        request: ValidateRequest,
    ) -> Result<ValidateResponse, AzError> {
        todo!()
    }

    async fn interface(
        &self,
        ctx: &T,
        request: InterfaceRequest,
    ) -> Result<InterfaceResponse, AzError> {
        todo!()
    }

    async fn execute(&self, ctx: &T, request: ExecuteRequest) -> Result<ExecuteResponse, AzError> {
        todo!()
    }
}