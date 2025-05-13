use async_trait::async_trait;

use crate::config::AzCfg;
use crate::pb::{CoreInfo, GetCoreInfoRequest, GetCoreInfoResponse};
use crate::{error::AzError, AzCore, VERSION};

#[async_trait]
pub trait AzComponentCtx: Send + Sync + 'static {
    /// get a clone of the core config
    ///
    /// # Example get the active cloud and its endpoint and resource id for authentication
    /// ```no_run
    /// use async_trait::async_trait;
    /// use crate::error::AzError;
    /// use crate::component::{AzCmdExecute, AzComponentCtx};
    /// use crate::config::cloud::metadata::endpoint;
    /// use crate::config::cloud::metadata::resource_id;
    ///
    /// struct MyCmd {
    /// }
    ///
    /// #[async_trait]
    /// impl AzCmdExecute for MyCmd {
    ///     type Output = ();
    ///
    ///     async fn run(&self, ctx: &dyn AzComponentCtx) -> Result<(), AzError> {
    ///         let cfg = ctx.get_cfg().await;
    ///         let cloud_metadata = cfg.clouds.get_active();
    ///         let endpoint = cloud_metadata.get_endpoint(endpoint::AUTHENTICATION).await;
    ///         let resource_id = cloud_metadata.get_resource_id(resource_id::AUTHENTICATION).await;
    ///         println!("endpoint: {}", endpoint);
    ///         println!("resource_id: {}", resource_id);
    ///         Ok(())
    ///     }
    /// }
    /// ```
    async fn get_cfg(&self) -> AzCfg;

    /// get the core info
    async fn get_core_info(
        &self,
        request: GetCoreInfoRequest,
    ) -> Result<GetCoreInfoResponse, AzError>;
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
