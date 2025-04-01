use crate::pb::component::{CoreInfo, GetCoreInfoRequest, GetCoreInfoResponse};
use crate::{error::AzError, AzCore, VERSION};
use std::future::Future;
use std::pin::Pin;

use super::AzComponentCtx;

impl AzComponentCtx for AzCore {
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
