pub(crate) mod built_in;

mod center;
mod command;
mod command_group;
mod ctx;

use async_trait::async_trait;
use std::path::PathBuf;
use std::str::FromStr;

pub use built_in::AzBuiltIn;
pub use center::{AzComponentCenter, AzComponentInfo};
pub use command::{AzCommand, AzCommandExecute};
pub use command_group::AzCommandGroup;
pub use ctx::AzComponentCtx;

use crate::{
    config,
    error::AzError,
    pb::{
        ComponentMetadata, ExecuteRequest, ExecuteResponse, InterfaceRequest, InterfaceResponse,
        MetadataRequest, MetadataResponse, ValidateRequest, ValidateResponse,
    },
    uri::CrsUri,
    VERSION,
};

/// the trait of a component
#[async_trait]
pub trait AzComponent<T: AzComponentCtx>: Send + Sync + 'static {
    /// get the metadata of the component
    async fn metadata(
        &self,
        ctx: &T,
        request: MetadataRequest,
    ) -> Result<MetadataResponse, AzError>;

    /// validate a uri
    /// if a uri has positional arguments as segments part,it will be converted to params in the query part in the returned uri
    async fn validate(
        &self,
        ctx: &T,
        request: ValidateRequest,
    ) -> Result<ValidateResponse, AzError>;

    /// get the interface of a uri
    async fn interface(
        &self,
        ctx: &T,
        request: InterfaceRequest,
    ) -> Result<InterfaceResponse, AzError>;

    /// run a executable uri
    async fn execute(&self, ctx: &T, request: ExecuteRequest) -> Result<ExecuteResponse, AzError>;
}

/// The trait which implemented the AzComponent trait automatically when the type implements the AzCommandGroup trait
pub trait AzComponentImpl<T: AzComponentCtx>: AzCommandGroup + Send + Sync + 'static {
    const VERSION: &'static str;
}

#[async_trait]
impl<T: AzComponentCtx, C: AzComponentImpl<T>> AzComponent<T> for C {
    async fn metadata(&self, _: &T, _: MetadataRequest) -> Result<MetadataResponse, AzError> {
        let interface = Self::build_interface(Some(0));
        Ok(MetadataResponse {
            metadata: Some(ComponentMetadata {
                name: interface.name,
                version: Self::VERSION.to_owned(),
                uri: interface.uri,
                help: interface.help,
            }),
        })
    }

    async fn interface(
        &self,
        _: &T,
        request: InterfaceRequest,
    ) -> Result<InterfaceResponse, AzError> {
        let skip = Self::uri().segments().count();
        let uri = CrsUri::from_str(&request.uri).unwrap();
        let depth = match request.depth {
            0 => None, // None means all the depth
            _ => Some(request.depth),
        };
        let segments = uri.segments().skip(skip).map(|s| s.to_string());
        let interface = Self::find_interface(depth, segments);
        Ok(InterfaceResponse { interface })
    }

    async fn execute(&self, ctx: &T, request: ExecuteRequest) -> Result<ExecuteResponse, AzError> {
        let skip = Self::uri().segments().count();
        let uri = CrsUri::from_str(&request.uri).unwrap();
        let segments = uri.segments().skip(skip).map(|s| s.to_string());
        let result =
            Self::execute_group(ctx, segments, request.params, request.output_format).await?;
        Ok(ExecuteResponse { data: Some(result) })
    }

    async fn validate(&self, _: &T, request: ValidateRequest) -> Result<ValidateResponse, AzError> {
        let skip = Self::uri().segments().count();
        let uri = CrsUri::from_str(&request.uri).unwrap();
        let segments = uri.segments().skip(skip).map(|s| s.to_string());
        let new_uri = Self::resolve_uri(segments).map(|u| {
            // add version and params from the uri
            let mut new_uri = u.with_version(VERSION);
            for (key, value) in uri.params() {
                new_uri = new_uri.with_param_values(key, value.collect::<Vec<_>>());
            }
            new_uri.to_string()
        });
        Ok(ValidateResponse { uri: new_uri })
    }
}

pub fn get_component_dir() -> PathBuf {
    let config_dir = config::get_config_dir();
    config_dir.join("components")
}
