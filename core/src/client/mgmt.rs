use std::sync::Arc;

use async_trait::async_trait;

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
        let endpoint = cfg.get_endpoint(&endpoint::RESOURCE_MANAGER).await.ok_or(
            AzInternalError::EndpointUnregisteredError(
                endpoint::RESOURCE_MANAGER.as_str().to_string(),
            ),
        )?;
        let scope = cfg
            .get_credential_scope(&resource_id::RESOURCE_MANAGER)
            .await
            .ok_or(AzInternalError::ResourceIdUnregisteredError(
                resource_id::RESOURCE_MANAGER.as_str().to_string(),
            ))?;
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
}

#[async_trait]
impl HttpClient for MgmtClient {
    async fn execute_request(&self, request: &Request) -> ClientResult<Response> {
        todo!()
    }
}
