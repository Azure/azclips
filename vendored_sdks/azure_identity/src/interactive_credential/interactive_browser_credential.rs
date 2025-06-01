use super::internal_server::*;
use crate::authorization_code_flow;
use crate::custom_token_fields::CustomTokenFields;
use azure_core::{
    credentials::{AccessToken, Secret, TokenCredential},
    error::ErrorKind, 
    http::new_http_client, 
    http::Url, 
    Error
};
use oauth2::{basic::BasicTokenType, AuthorizationCode, ClientId, TokenResponse};
use std::{str::FromStr, sync::Arc};
use time;

/// Default OAuth scopes used when none are provided.
#[allow(dead_code)]
const DEFAULT_SCOPE_ARR: [&str; 3] = ["openid", "offline_access", "profile"];
/// Default client ID for interactive browser authentication.
#[allow(dead_code)]
const DEFAULT_DEVELOPER_SIGNON_CLIENT_ID: &str = "04b07795-8ddb-461a-bbee-02f9e1bf7b46";
/// Default tenant ID used when none is specified.
#[allow(dead_code)]
const DEFAULT_ORGANIZATIONS_TENANT_ID: &str = "organizations";

/// Provides interactive browser-based authentication.
#[derive(Clone, Debug)]
pub struct InteractiveBrowserCredential {
    /// Client ID of the application.
    pub(crate) client_id: ClientId,
    /// Tenant ID for the authentication request.
    pub(crate) tenant_id: String,
    /// Redirect URI where the authentication response is sent.
    pub(crate) redirect_url: Url,
    /// Token cache for storing and retrieving tokens.
    pub(crate) cache: crate::token_cache::TokenCache,
}

impl InteractiveBrowserCredential {
    /// Creates a new `InteractiveBrowserCredential` instance with optional parameters.
    pub fn new(
        client_id: Option<ClientId>,
        tenant_id: Option<String>,
        redirect_url: Option<Url>,
    ) -> azure_core::Result<Arc<Self>> {
        let client_id = client_id
            .unwrap_or_else(|| ClientId::new(DEFAULT_DEVELOPER_SIGNON_CLIENT_ID.to_owned()));

        let tenant_id = tenant_id.unwrap_or_else(|| DEFAULT_ORGANIZATIONS_TENANT_ID.to_owned());

        let redirect_url = redirect_url.unwrap_or_else(|| {
            Url::from_str(&format!("http://localhost:{}", LOCAL_SERVER_PORT))
                .expect("Failed to parse redirect URL")
        });        Ok(Arc::new(Self {
            client_id,
            tenant_id,
            redirect_url,
            cache: crate::token_cache::TokenCache::new(),
        }))
    }
    /// Starts the interactive browser authentication flow and returns an access token.
    /// If no scopes are provided, default scopes will be used.
    ///
    /// Returns a token response that includes standard OAuth2 token information and additional ID token.
    #[allow(dead_code)]
    pub async fn get_token(
        &self,
        scopes: Option<&[&str]>,
        prompt: Option<&str>,
        login_hint: Option<&str>,
        success_template: Option<&str>,
        error_template: Option<&str>,
    ) -> azure_core::Result<oauth2::StandardTokenResponse<CustomTokenFields, BasicTokenType>> {
        let mut combined_scopes = DEFAULT_SCOPE_ARR.to_vec();

        if let Some(user_scopes) = scopes {
            for &scope in user_scopes {
                if !combined_scopes.contains(&scope) {
                    combined_scopes.push(scope);
                }
            }
        }

        println!("Initiating interactive login...");
        println!("A web browser will open for you to authenticate.");

        let authorization_code_flow = authorization_code_flow::authorize(
            self.client_id.clone(),
            None,
            &self.tenant_id,
            self.redirect_url.clone(),
            &combined_scopes,
            prompt,
            login_hint,
        );

        println!("Waiting for authentication in browser. If the browser doesn't open automatically, please go to: {}", 
                 authorization_code_flow.authorize_url.as_ref());

        match open_url(
            authorization_code_flow.authorize_url.as_ref(),
            success_template,
            error_template,
        )
        .await
        {
            Ok(code) => {
                authorization_code_flow
                    .exchange(new_http_client(), AuthorizationCode::new(code))
                    .await
            }
            Err(error) => Err(Error::new(
                ErrorKind::Credential,
                format!("Failed to retrieve authorization code. {}", error),
            )),
        }
    }
}

// Implement the TokenCredential trait to support the common pattern
#[cfg_attr(target_arch = "wasm32", async_trait::async_trait(?Send))]
#[cfg_attr(not(target_arch = "wasm32"), async_trait::async_trait)]
impl TokenCredential for InteractiveBrowserCredential {
    async fn get_token(&self, scopes: &[&str]) -> azure_core::Result<AccessToken> {
        // Use the cache to get or refresh the token
        self.cache
            .get_token(scopes, Box::pin(async move {
                // This closure is called when we need a new token
                let token_response = self
                    .get_token(Some(scopes), None, None, None, None)
                    .await?;
                
                let expires_in = token_response
                    .expires_in()
                    .unwrap_or(std::time::Duration::from_secs(0));
                let expires_on = time::OffsetDateTime::now_utc() + expires_in;
                
                // Create an AccessToken from the token response
                let oauth2_token = token_response.access_token();
                let access_token = AccessToken::new(
                    Secret::new(oauth2_token.secret().to_string()),
                    expires_on,
                );
                
                // If we have a refresh token, store it in the cache for future use
                if let Some(refresh_token) = token_response.refresh_token() {
                    // Store the complete token data with refresh token in the cache
                    self.cache
                        .store_token_with_refresh(
                            scopes,
                            access_token.clone(),
                            Secret::new(refresh_token.secret().to_string()),
                            self.client_id.to_string(),
                            self.tenant_id.clone(),
                        )
                        .await?;
                }
                
                Ok(access_token)
            }))
            .await
    }
}

/*
    vcpkg integrate install
    vcpkg install openssl
    vcpkg install openssl:x64-windows-static-md
*/
// Don't run it in the CI
#[cfg(test)]
mod tests {
    use super::*;
    use tracing::debug;
    use tracing::Level;
    use tracing_subscriber;
    static INIT: std::sync::Once = std::sync::Once::new();

    fn init_tracing() {
        INIT.call_once(|| {
            let _ = tracing_subscriber::fmt()
                .with_max_level(Level::DEBUG)
                .try_init();
        });
    }

    #[tokio::test]
    #[ignore = "Interactive test requiring browser authentication"]
    async fn interactive_auth_flow_should_return_token() {
        init_tracing();
        debug!("Starting interactive authentication test");

        let credential = InteractiveBrowserCredential::new(None, None, None)
            .expect("Failed to create credential");

        let token_response = credential.get_token(None, None, None, None, None).await;
        debug!("Authentication result: {:#?}", token_response);
        assert!(token_response.is_ok());
    }
}
