use super::internal_server::*;
use crate::authorization_code_flow;
use crate::cirrus_token_cache::{
    AccessTokenEntry, AccountEntry, FilePersistentTokenCache, IdTokenEntry, RefreshTokenEntry,
};
use crate::utils::decode_part_id_token;
use azure_core::{
    credentials::{AccessToken, Secret, TokenCredential},
    error::ErrorKind,
    http::new_http_client,
    http::Url,
    Error,
};
use oauth2::{AuthorizationCode, ClientId, TokenResponse};
use serde_json;
use std::{path::PathBuf, str::FromStr, time::SystemTime, time::UNIX_EPOCH};

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
#[derive(Debug)]
pub struct InteractiveBrowserCredential {
    /// Client ID of the application.
    pub(crate) client_id: ClientId,
    /// Tenant ID for the authentication request.
    pub(crate) tenant_id: String,
    /// Redirect URI where the authentication response is sent.
    pub(crate) redirect_url: Url,
    /// Home account ID for the user.
    pub home_account_id: Option<String>,
    /// Cloud environment for the authentication request.
    pub environment: Option<String>,
    /// User account name
    pub username: Option<String>,
    /// Persistent Token cache for storing and retrieving tokens.
    pub(crate) cache_path: Option<PathBuf>,
}

impl InteractiveBrowserCredential {
    /// Creates a new `InteractiveBrowserCredential` instance with optional parameters.
    pub fn new(
        client_id: Option<ClientId>,
        tenant_id: Option<String>,
        redirect_url: Option<Url>,
        home_account_id: Option<String>,
        environment: Option<String>,
        username: Option<String>,
        cache_path: Option<PathBuf>,
    ) -> azure_core::Result<Self> {
        let client_id = client_id
            .unwrap_or_else(|| ClientId::new(DEFAULT_DEVELOPER_SIGNON_CLIENT_ID.to_owned()));

        let tenant_id = tenant_id.unwrap_or_else(|| DEFAULT_ORGANIZATIONS_TENANT_ID.to_owned());

        let redirect_url = redirect_url.unwrap_or_else(|| {
            Url::from_str(&format!("http://localhost:{}", LOCAL_SERVER_PORT))
                .expect("Failed to parse redirect URL")
        });
        Ok(Self {
            client_id,
            tenant_id,
            redirect_url,
            home_account_id,
            environment,
            username,
            cache_path,
        })
    }

    /// Starts the interactive browser authentication flow and returns an access token.
    /// If no scopes are provided, default scopes will be used.
    ///
    /// Returns a token response that includes standard OAuth2 token information and additional ID token.
    #[allow(dead_code)]
    pub async fn get_token_interactively(
        &mut self,
        scopes: Option<&[&str]>,
        prompt: Option<&str>,
        login_hint: Option<&str>,
        success_template: Option<&str>,
        error_template: Option<&str>,
    ) -> azure_core::Result<AccessToken> {
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

        let code = open_url(
            authorization_code_flow.authorize_url.as_ref(),
            success_template,
            error_template,
        )
        .await
        .map_err(|error| {
            Error::new(
                ErrorKind::Credential,
                format!("Failed to retrieve authorization code. {}", error),
            )
        })?;

        let token_response = authorization_code_flow
            .exchange(new_http_client(), AuthorizationCode::new(code))
            .await?;

        // Create a persistent cache if a cache path is provided
        let persistent_cache = if let Some(cache_path) = &self.cache_path {
            Some(FilePersistentTokenCache::new(cache_path.clone())?)
        } else {
            None
        };

        // Store the access token, id token and account information in the cache
        let now = SystemTime::now()
            .duration_since(UNIX_EPOCH)
            .unwrap_or_default();
        let now_secs = now.as_secs();
        let expires_in = token_response.expires_in().unwrap_or_default();
        let expires_on_secs = now_secs + expires_in.as_secs();

        // Extract information from ID token if available
        let mut home_account_id = None;
        let mut environment = None;
        let mut username = None;
        let mut object_id: Option<String> = None;

        // Only record the audience part for cached scope

        if let Some(id_token) = &token_response.extra_fields().id_token {
            // Parse the ID token to extract account information
            if let Some(payload) = id_token.split('.').nth(1) {
                // Use the decode_part_id_token utility function to decode the payload
                if let Ok(payload_json) = decode_part_id_token(payload, Some("utf-8")) {
                    if let Ok(payload_value) =
                        serde_json::from_str::<serde_json::Value>(&payload_json)
                    {
                        // Extract home_account_id from the ID token payload (combination of sub and tid claims)
                        let oid = payload_value.get("oid").and_then(|v| v.as_str());
                        let tid = payload_value.get("tid").and_then(|v| v.as_str());

                        object_id = oid.map(|s| s.to_string());

                        if let (Some(oid_value), Some(tid_value)) = (oid, tid) {
                            home_account_id = Some(format!("{}.{}", oid_value, tid_value));
                            // Set the home_account_id in the struct for future use
                            self.home_account_id = home_account_id.clone();
                            tracing::info!("Found home_account_id: {:?}", self.home_account_id);
                        }

                        // Extract the username from preferred_username claim
                        username = payload_value
                            .get("preferred_username")
                            .and_then(|v| v.as_str())
                            .map(String::from);
                        self.username = username.clone();
                        tracing::info!("Found username: {:?}", self.username);

                        // For Azure AD, environment is typically "login.microsoftonline.com"
                        environment = payload_value
                            .get("iss")
                            .and_then(|v| v.as_str())
                            .and_then(|s| s.split('/').nth(2))
                            .map(String::from);
                        self.environment = environment.clone();
                        tracing::info!("Found environment: {:?}", self.environment);
                    }
                }
            }
            let id_token_entry = IdTokenEntry {
                credential_type: crate::cirrus_token_cache::CredentialType::IdToken,
                secret: Secret::new(id_token.clone()),
                home_account_id: home_account_id.clone(),
                environment: environment.clone(),
                tenant_id: Some(self.tenant_id.clone()),
                client_id: Some(self.client_id.to_string()),
                last_modification_time: Some(now_secs.to_string()),
            };
            if persistent_cache.is_none() {
                tracing::warn!("No persistent cache provided, ID token will not be stored.");
            } else if let Some(persistent_cache) = &persistent_cache {
                // Store the ID token in the persistent cache
                if let Err(e) = persistent_cache.cache().add_id_token(id_token_entry) {
                    tracing::error!("Failed to add ID token to persistent cache: {}", e);
                }
            }
        }

        // Create access token entry
        let access_token_entry = AccessTokenEntry {
            credential_type: crate::cirrus_token_cache::CredentialType::AccessToken,
            secret: Secret::new(token_response.access_token().secret().to_string()),
            home_account_id: home_account_id.clone(),
            environment: environment.clone(),
            client_id: Some(self.client_id.to_string()),
            scopes: Some(combined_scopes.join(" ")),
            tenant_id: Some(self.tenant_id.clone()),
            token_type: Some("Bearer".to_string()),
            cached_at: now_secs.to_string(),
            expires_on: expires_on_secs.to_string(),
            extended_expires_on: expires_on_secs.to_string(),
        };

        // Store the access token in the cache
        if persistent_cache.is_none() {
            tracing::warn!("No persistent cache provided, access token will not be stored.");
        } else if let Some(persistent_cache) = &persistent_cache {
            // Store the access token in the persistent cache
            if let Err(e) = persistent_cache
                .cache()
                .add_access_token(access_token_entry.clone())
            {
                tracing::error!("Failed to add access token to persistent cache: {}", e);
            }
        }

        // Create and store account entry
        if let (Some(home_account_id_value), Some(environment_value)) =
            (home_account_id.clone(), environment.clone())
        {
            let account_entry = AccountEntry {
                home_account_id: Some(home_account_id_value),
                environment: Some(environment_value),
                tenant_id: Some(self.tenant_id.clone()),
                local_account_id: object_id, // Could extract from ID token if needed
                username,
                authority_type: Some(crate::cirrus_token_cache::AuthorityType::MSSTS),
                account_source: Some("authorization_code".to_string()),
                is_default: None, // Interactive method will only be called when login, set it as the default account for the user
            };

            if persistent_cache.is_none() {
                tracing::warn!("No persistent cache provided, account entry will not be stored.");
            } else if let Some(persistent_cache) = &persistent_cache {
                // Store the account entry in the persistent cache
                if let Err(e) = persistent_cache.cache().add_account(account_entry.clone()) {
                    tracing::error!("Failed to add account to persistent cache: {}", e);
                }
            }
        }

        // If we have a refresh token, store it in the cache for future use
        if let Some(refresh_token) = token_response.refresh_token() {
            let refresh_token_entry = RefreshTokenEntry {
                credential_type: crate::cirrus_token_cache::CredentialType::RefreshToken,
                secret: Secret::new(refresh_token.secret().to_string()),
                home_account_id: home_account_id,
                environment: environment,
                client_id: Some(self.client_id.to_string()),
                last_modification_time: Some(now_secs.to_string()),
            };

            if persistent_cache.is_none() {
                tracing::warn!("No persistent cache provided, refresh token will not be stored.");
            } else if let Some(persistent_cache) = &persistent_cache {
                // Store the refresh token in the persistent cache
                if let Err(e) = persistent_cache
                    .cache()
                    .add_refresh_token(refresh_token_entry.clone())
                {
                    tracing::error!("Failed to add refresh token to persistent cache: {}", e);
                }
            }
        }

        // Extract token and expiration information
        let access_token_str = token_response.access_token().secret().to_string();

        // Get current time and add the expires_in to determine the expiration time
        use time::OffsetDateTime;
        let expires_in = token_response.expires_in().unwrap_or_default();
        let expires_on =
            OffsetDateTime::now_utc() + std::time::Duration::from_secs(expires_in.as_secs());

        // Create the AccessToken
        let access_token = AccessToken::new(access_token_str, expires_on);

        Ok(access_token)
    }

    /// Get token sliently from the cache or refresh it if needed.
    pub async fn get_token_sliently(&self, scopes: &[&str]) -> azure_core::Result<AccessToken> {
        // Create a persistent cache if a cache path is provided
        let persistent_cache = if let Some(cache_path) = &self.cache_path {
            Some(FilePersistentTokenCache::new(cache_path.clone())?)
        } else {
            None
        };

        // Try to get token from cache or refresh if needed
        if persistent_cache.is_none() {
            tracing::warn!("No persistent cache provided, cannot retrieve token silently.");
            return Err(Error::new(
                ErrorKind::Credential,
                "No persistent cache provided for silent token retrieval.",
            ));
        }

        match persistent_cache
            .as_ref()
            .unwrap()
            .cache()
            .get_access_token_and_possibly_refresh_it(
                scopes,
                self.home_account_id.as_deref(),
                self.environment.as_deref(),
                Some(&self.client_id.to_string()),
                Some(&self.tenant_id),
            )
            .await
        {
            Ok(access_token) => {
                // Make sure we save any changes to the cache after refreshing
                if let Err(e) = persistent_cache.as_ref().unwrap().save_if_changed() {
                    tracing::warn!("Failed to save token cache after refreshing: {}", e);
                }
                Ok(access_token)
            }
            Err(error) => {
                // If get_access_token_and_possibly_refresh_it failed, propagate the error instead of
                // attempting an interactive authentication flow
                tracing::error!("Failed to get or refresh token from cache: {}", error);
                Err(error)
            }
        }
    }
}

// Implement the TokenCredential trait to support the common pattern
#[cfg_attr(target_arch = "wasm32", async_trait::async_trait(?Send))]
#[cfg_attr(not(target_arch = "wasm32"), async_trait::async_trait)]
impl TokenCredential for InteractiveBrowserCredential {
    async fn get_token(&self, scopes: &[&str]) -> azure_core::Result<AccessToken> {
        self.get_token_sliently(scopes).await
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
    use std::env;

    fn get_test_cache_path() -> PathBuf {
        let temp_dir = env::temp_dir();
        temp_dir.join("msal_test_token_cache.json")
    }

    #[tokio::test]
    #[ignore = "Interactive test requiring browser authentication"]
    async fn interactive_auth_flow_should_return_token() {
        println!("Starting interactive authentication test");

        // Create a credential with a temp file cache
        let test_path = get_test_cache_path();
        let mut credential = InteractiveBrowserCredential::new(
            None,
            None,
            None,
            None,
            None,
            None,
            Some(test_path.clone()),
        )
        .expect("Failed to create credential");

        let token_response = credential
            .get_token_interactively(None, None, None, None, None)
            .await;
        println!("Authentication result: {:#?}", token_response);

        // Check if the cache file was created
        if test_path.exists() {
            let cache_contents =
                std::fs::read_to_string(&test_path).expect("Failed to read cache file");
            println!("Token cache contents: {}", cache_contents);
        } else {
            println!("Warning: Cache file was not created!");
        }

        assert!(token_response.is_ok());
    }

    #[tokio::test]
    #[ignore = "Interactive test requiring browser authentication"]
    async fn test_token_credential_get_token() {
        println!("Starting TokenCredential implementation test");

        // Create a credential with a temp file cache
        let test_path = get_test_cache_path();
        let mut credential = InteractiveBrowserCredential::new(
            None,
            None,
            None,
            None,
            None,
            None,
            Some(test_path.clone()),
        )
        .expect("Failed to create credential");

        // Test the trait method (using it as a TokenCredential)
        // Note: This will fail if there's no valid token in the cache yet
        let scopes = &["https://management.azure.com/.default"];
        let token =
            <InteractiveBrowserCredential as TokenCredential>::get_token(&credential, scopes).await;
        println!("Token result: {:#?}", token);

        if token.is_err() {
            println!("Token retrieval failed silently - this is expected if no valid token exists in cache");
            println!("Try running the interactive_auth_flow_should_return_token test first");
        } else {
            assert!(token.is_ok());
        }

        // Try interactive auth if silent retrieval fails
        if token.is_err() {
            println!("Attempting interactive authentication...");
            let interactive_token = credential
                .get_token_interactively(Some(scopes), Some("select_account"), None, None, None)
                .await;

            println!("Interactive token result: {:#?}", interactive_token);
            assert!(interactive_token.is_ok());

            // Now try silent retrieval again
            let silent_token = credential.get_token_sliently(scopes).await;
            println!(
                "Silent token result after interactive auth: {:#?}",
                silent_token
            );
            assert!(silent_token.is_ok());
        }
    }
}
