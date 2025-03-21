use crate::error::AzResult;
use crate::pb;

/// the trait of a service
pub trait AzService {
    /// get the metadata of the service
    fn metadata<T: AzServiceCtx>(
        &self,
        ctx: &T,
        request: pb::service::MetadataRequest,
    ) -> AzResult<pb::service::MetadataResponse>;

    /// check a cmd uri is available or not
    fn check<T: AzServiceCtx>(
        &self,
        ctx: &T,
        request: pb::service::CheckRequest,
    ) -> AzResult<pb::service::CheckResponse>;

    /// get the interface of a cmd uri
    fn interface<T: AzServiceCtx>(
        &self,
        ctx: &T,
        request: pb::service::InterfaceRequest,
    ) -> AzResult<pb::service::InterfaceResponse>;

    /// run a executable cmd uri
    fn execute<T: AzServiceCtx>(
        &self,
        ctx: &T,
        request: pb::service::ExecuteRequest,
    ) -> AzResult<pb::service::ExecuteResponse>;
}

// the trait of the context
pub trait AzServiceCtx {
    /// get the core info
    fn get_core_info(
        &self,
        request: pb::service::GetCoreInfoRequest,
    ) -> pb::service::GetCoreInfoResponse;
}
