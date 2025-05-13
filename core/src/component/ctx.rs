use async_trait::async_trait;

use crate::pb::component::{CoreInfo, GetCoreInfoRequest, GetCoreInfoResponse};
use crate::AzCfg;
use crate::{error::AzError, AzCore, VERSION};

#[async_trait]
pub trait AzComponentCtx: Send + Sync + 'static {
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

    // Will be used in the future
    // /// get the core info
    // async fn get_core_info(
    //     &self,
    //     request: GetCoreInfoRequest,
    // ) -> Result<GetCoreInfoResponse, AzError>;
}

#[async_trait]
impl AzComponentCtx for AzCore {
    async fn get_cfg(&self) -> AzCfg {
        self.cfg.read().await.clone()
    }

    // async fn get_core_info(&self, _: GetCoreInfoRequest) -> Result<GetCoreInfoResponse, AzError> {
    //     Ok(GetCoreInfoResponse {
    //         core_info: Some(CoreInfo {
    //             version: VERSION.to_string(),
    //         }),
    //     })
    // }
}
