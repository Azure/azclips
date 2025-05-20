use async_trait::async_trait;

use crate::config::AzCfg;
use crate::pb::{CoreInfo, GetCoreInfoRequest, GetCoreInfoResponse};
use crate::{error::AzError, AzCore, VERSION};

#[async_trait]
pub trait AzComponentCtx: Send + Sync + 'static {
    /// get the core info
    async fn get_core_info(
        &self,
        request: GetCoreInfoRequest,
    ) -> Result<GetCoreInfoResponse, AzError>;

    /// get a clone of the core config
    ///
    /// # Example get the active cloud and its endpoint and resource id for authentication
    /// ```
    /// use async_trait::async_trait;
    /// use az_core::error::AzError;
    /// use az_core::component::{AzCommandExecute, AzComponentCtx};
    /// use az_core::config::cloud::{endpoint, resource_id};
    ///
    /// struct MyCmd {}
    ///
    /// #[async_trait]
    /// impl AzCommandExecute for MyCmd {
    ///     type Output = ();
    ///
    ///     async fn run<T: AzComponentCtx>(&self, ctx: &T) -> Result<Option<Self::Output>, AzError> {
    ///         let cfg = ctx.get_cfg().await;
    ///         let cloud_metadata = cfg.clouds.get_active();
    ///         let endpoint = cloud_metadata.get_endpoint(&endpoint::AUTHENTICATION).await.expect("failed to get endpoint");
    ///         let resource_id = cloud_metadata.get_resource_id(&resource_id::AUTHENTICATION).await.expect("failed to get resource id");
    ///         println!("endpoint: {}", endpoint);
    ///         println!("resource_id: {}", resource_id);
    ///         Ok(None)
    ///     }
    /// }
    /// ```
    async fn get_cfg(&self) -> AzCfg;
}

#[async_trait]
impl AzComponentCtx for AzCore {
    async fn get_cfg(&self) -> AzCfg {
        self.cfg.read().await.clone()
    }

    async fn get_core_info(&self, _: GetCoreInfoRequest) -> Result<GetCoreInfoResponse, AzError> {
        Ok(GetCoreInfoResponse {
            core_info: Some(CoreInfo {
                version: VERSION.to_string(),
            }),
        })
    }
}
