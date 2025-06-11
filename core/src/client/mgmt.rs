use std::sync::Arc;

use async_trait::async_trait;
use azure_core::http::Context;

use super::http::{HttpClient, Pipeline, Request, Response};
use super::options::ClientOptions;
use super::policies::{BearerTokenCredentialPolicy, ClientRequestIdPolicy, TelemetryPolicy};
use super::{ClientCredential, ClientResult};
use crate::{
    component::AzComponentCtx,
    config::cloud::{endpoint, resource_id},
    error::AzInternalError,
};

#[derive(Debug)]
pub struct MgmtClient {
    endpoint: String,
    pipeline: Pipeline,
}

impl MgmtClient {
    pub async fn build(
        ctx: &dyn AzComponentCtx,
        credential: ClientCredential,
        options: &ClientOptions,
    ) -> Result<Self, AzInternalError> {
        let cfg = ctx.get_cfg().await.clouds.get_active().to_owned();
        let endpoint = cfg.get_endpoint(&endpoint::RESOURCE_MANAGER).await?;
        let scope = cfg
            .get_credential_scope(&resource_id::RESOURCE_MANAGER)
            .await?;
        let scopes = vec![scope];

        let request_id_policy = Arc::new(ClientRequestIdPolicy::default());
        let telemetry_policy = Arc::new(TelemetryPolicy::new(&options.telemetry));
        let auth_policy = Arc::new(BearerTokenCredentialPolicy::new(credential, scopes));

        let pipeline = Pipeline::new(
            Default::default(),
            vec![request_id_policy, telemetry_policy],
            vec![auth_policy],
        );

        Ok(Self { endpoint, pipeline })
    }

    pub async fn send(&self, request: Request) -> Result<Response, AzInternalError> {
        // TODO: Context should get from the component ctx
        let ctx = Context::new();
        let mut request = request;
        let response = self.pipeline.send(&ctx, &mut request).await?;
        Ok(response)
    }
}
