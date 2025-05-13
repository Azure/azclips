use async_trait::async_trait;

use crate::pb::component::{CoreInfo, GetCoreInfoRequest, GetCoreInfoResponse};
use crate::AzCfg;
use crate::{error::AzError, AzCore, VERSION};

use super::AzComponentCtx;

#[async_trait]
impl AzComponentCtx for AzCore {
    async fn get_core_info(&self, _: GetCoreInfoRequest) -> Result<GetCoreInfoResponse, AzError> {
        Ok(GetCoreInfoResponse {
            core_info: Some(CoreInfo {
                version: VERSION.to_string(),
            }),
        })
    }

    async fn get_cfg(&self) -> AzCfg {
        self.cfg.read().await.clone()
    }
}
