use futures::future::join_all;
use std::sync::Arc;

use super::ClientResult;
use azure_core::credentials::{AccessToken, TokenCredential};

#[derive(Debug, Clone)]
pub struct ClientCredential {
    credential: Arc<dyn TokenCredential>,
    /// MSAL credentials for cross-tenant authentication. Details about [cross-tenant authentication](https://learn.microsoft.com/en-us/azure/azure-resource-manager/management/authenticate-multi-tenant)
    aux_credentials: Vec<Arc<dyn TokenCredential>>,
}

impl ClientCredential {
    pub fn new(credential: Arc<dyn TokenCredential>) -> Self {
        Self {
            credential,
            aux_credentials: Default::default(),
        }
    }

    pub fn add_aux_credential(&mut self, credential: Arc<dyn TokenCredential>) -> &mut Self {
        self.aux_credentials.push(credential);
        self
    }
}

impl ClientCredential {
    pub async fn get_token(&self, scopes: &[&str]) -> ClientResult<AccessToken> {
        let token = self.credential.get_token(scopes).await?;
        Ok(token)
    }

    pub async fn get_aux_tokens(&self, scopes: &[&str]) -> ClientResult<Vec<AccessToken>> {
        let futures = self
            .aux_credentials
            .iter()
            .map(|cred| cred.get_token(scopes));
        let tokens = join_all(futures)
            .await
            .into_iter()
            .collect::<Result<Vec<_>, _>>()?;
        Ok(tokens)
    }
}
