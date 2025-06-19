use bytes::Bytes;
use std::sync::Arc;

use super::http::{headers::Headers, Context, Method, Pipeline, Request, Url};
use super::options::ClientOptions;
use super::policies::{BearerTokenCredentialPolicy, ClientRequestIdPolicy, TelemetryPolicy};
use super::poller::Poller;
use super::{ClientCredential, ClientMethodOptions};
use crate::client::Response;
use crate::{
    component::AzComponentCtx,
    config::cloud::{endpoint, resource_id},
    error::AzInternalError,
};

#[derive(Debug)]
pub struct MgmtClient {
    endpoint: Url,
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
        let endpoint =
            Url::parse(&endpoint).map_err(|e| AzInternalError::UrlParseError(e.to_string()))?;
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

    pub async fn run(
        &self,
        method: Method,
        path: &str,
        query_pairs: Option<Vec<(String, String)>>,
        headers: Option<Headers>,
        body: Option<Bytes>,
        options: Option<ClientMethodOptions<'_>>,
    ) -> Result<Response, AzInternalError> {
        let options = options.unwrap_or_default();
        let mut url = self.endpoint.clone();
        url = url
            .join(path)
            .map_err(|e| AzInternalError::UrlParseError(e.to_string()))?;
        if let Some(query_pairs) = query_pairs {
            url.query_pairs_mut().extend_pairs(query_pairs);
        }
        let mut request = Request::new(url, method);
        if let Some(headers) = headers {
            headers.into_iter().for_each(|(k, v)| {
                request.insert_header(k, v);
            });
        }
        if let Some(body) = body {
            request.set_body(body);
        }

        let ctx = Context::with_context(&options.context);
        let raw_resp = self.pipeline.send(&ctx, &mut request).await?;
        let mut resp = Response::from_raw_response(raw_resp).await?;

        // For PUT, POST, PATCH, DELETE operations that can be a LRO, try to
        if [Method::Put, Method::Post, Method::Delete, Method::Patch]
            .iter()
            .any(|m| *m == method)
        {
            if let Ok(mut poller) = Poller::new(self.pipeline.clone(), &request, &resp, None).await
            {
                resp = poller.poll_until_done(&ctx, None).await?;
            }
        }

        Ok(resp)
    }
}
