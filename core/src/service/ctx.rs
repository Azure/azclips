use crate::pb::service::{CoreInfo, GetCoreInfoRequest, GetCoreInfoResponse};
use crate::{error::AzError, AzCore, VERSION};
use std::future::Future;
use std::pin::Pin;
use std::sync::Weak;

use super::AzServiceCtx;

/// the context provided to the service
impl AzServiceCtx for Weak<AzCore> {
    fn get_core_info<'a>(
        &'a self,
        _: GetCoreInfoRequest,
    ) -> Pin<Box<dyn Future<Output = Result<GetCoreInfoResponse, AzError>> + Send + 'a>> {
        Box::pin(async move {
            Ok(GetCoreInfoResponse {
                core_info: Some(CoreInfo {
                    version: VERSION.to_string(),
                }),
            })
        })
    }
}
