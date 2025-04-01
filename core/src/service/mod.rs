pub mod built_in;
pub mod center;
pub mod ctx;

use std::path::PathBuf;

use crate::config;
use crate::error::AzResult;
use crate::pb::service as pb;

pub trait AzServiceCtx: Send + Sync + 'static {
    /// get the core info
    fn get_core_info<'a>(
        &'a self,
        request: pb::GetCoreInfoRequest,
    ) -> std::pin::Pin<
        Box<dyn std::future::Future<Output = AzResult<pb::GetCoreInfoResponse>> + Send + 'a>,
    >;
}

/// the trait of a service
pub trait AzService<T: AzServiceCtx>: Send + Sync + 'static {
    /// get the metadata of the service
    fn metadata<'a>(
        &'a self,
        ctx: &'a T,
        request: pb::MetadataRequest,
    ) -> std::pin::Pin<
        Box<dyn std::future::Future<Output = AzResult<pb::MetadataResponse>> + Send + 'a>,
    >;

    /// check a cmd uri is available or not
    /// if a cmd uri has positional arguments as segments part,it will be converted to params in the query part in the returned cmd uri
    fn check<'a>(
        &'a self,
        ctx: &'a T,
        request: pb::CheckRequest,
    ) -> std::pin::Pin<Box<dyn std::future::Future<Output = AzResult<pb::CheckResponse>> + Send + 'a>>;

    /// get the interface of a cmd uri
    fn interface<'a>(
        &'a self,
        ctx: &'a T,
        request: pb::InterfaceRequest,
    ) -> std::pin::Pin<
        Box<dyn std::future::Future<Output = AzResult<pb::InterfaceResponse>> + Send + 'a>,
    >;

    /// run a executable cmd uri
    fn execute<'a>(
        &'a self,
        ctx: &'a T,
        request: pb::ExecuteRequest,
    ) -> std::pin::Pin<
        Box<dyn std::future::Future<Output = AzResult<pb::ExecuteResponse>> + Send + 'a>,
    >;
}

pub fn get_service_dir() -> PathBuf {
    let config_dir = config::get_config_dir();
    config_dir.join("services")
}
