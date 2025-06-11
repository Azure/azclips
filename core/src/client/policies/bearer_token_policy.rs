use std::sync::Arc;
use async_trait::async_trait;

use super::{Context, HeaderName, Policy, PolicyResult, Request};
use crate::client::ClientCredential;

const AUTHORIZATION: HeaderName = HeaderName::from_static("authorization");
const AUX_AUTHORIZATION: HeaderName = HeaderName::from_static("x-ms-authorization-auxiliary");

#[derive(Debug, Clone)]
pub struct BearerTokenCredentialPolicy {
    credential: ClientCredential,
    scopes: Vec<String>,
}

impl BearerTokenCredentialPolicy {
    pub fn new<A, B>(credential: ClientCredential, scopes: A) -> Self
    where
        A: IntoIterator<Item = B>,
        B: Into<String>,
    {
        Self {
            credential,
            scopes: scopes.into_iter().map(|s| s.into()).collect(),
        }
    }

    fn scopes(&self) -> Vec<&str> {
        self.scopes
            .iter()
            .map(String::as_str)
            .collect::<Vec<&str>>()
    }
}

#[async_trait]
impl Policy for BearerTokenCredentialPolicy {
    async fn send(
        &self,
        ctx: &Context,
        request: &mut Request,
        next: &[Arc<dyn Policy>],
    ) -> PolicyResult {
        let access_token = self.credential.get_token(&self.scopes()).await?;
        let aux_tokens = self.credential.get_aux_tokens(&self.scopes()).await?;

        request.insert_header(
            AUTHORIZATION,
            format!("Bearer {}", access_token.token.secret()),
        );
        if !aux_tokens.is_empty() {
            request.insert_header(
                AUX_AUTHORIZATION,
                aux_tokens
                    .iter()
                    .map(|t| format!("Bearer {}", t.token.secret()))
                    .collect::<Vec<_>>()
                    .join(", "),
            );
        }
        next[0].send(ctx, request, &next[1..]).await
    }
}
