// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

use async_lock::RwLock;
use azure_core::{
    credentials::{AccessToken, Secret},
    http::new_http_client,
};
use futures::Future;
use std::collections::HashMap;
use tracing::{error, trace};
use time;

use crate::refresh_token;

/// A struct to hold both access token and refresh token
#[derive(Debug, Clone)]
struct TokenData {
    access_token: AccessToken,
    refresh_token: Option<Secret>,
    client_id: Option<String>,
    tenant_id: Option<String>,
}

/// Implements a token cache for storing and retrieving tokens
#[derive(Debug)]
pub(crate) struct TokenCache(RwLock<HashMap<Vec<String>, TokenData>>);

impl Clone for TokenCache {
    fn clone(&self) -> Self {
        // Create a new empty cache - this is a simplification
        // In a real implementation we might want to copy the contents
        Self::new()
    }
}

impl TokenCache {
    pub(crate) fn new() -> Self {
        Self(RwLock::new(HashMap::new()))
    }

    #[allow(dead_code)]
    pub(crate) async fn clear(&self) -> azure_core::Result<()> {
        let mut token_cache = self.0.write().await;
        token_cache.clear();
        Ok(())
    }

    pub(crate) async fn get_token(
        &self,
        scopes: &[&str],
        callback: impl Future<Output = azure_core::Result<AccessToken>>,
    ) -> azure_core::Result<AccessToken> {
        let scopes_key = scopes.iter().map(ToString::to_string).collect::<Vec<_>>();
        
        // First, check if we have a valid non-expired token
        {
            let token_cache = self.0.read().await;
            if let Some(token_data) = token_cache.get(&scopes_key) {
                if !token_data.access_token.is_expired(None) {
                    trace!("returning cached valid token");
                    return Ok(token_data.access_token.clone());
                }
            }
        }

        // At this point, we either don't have a token or it's expired
        // Check if we have an expired token with a refresh token
        let refresh_attempt_result = {
            let token_cache = self.0.read().await;
            
            if let Some(token_data) = token_cache.get(&scopes_key) {
                // Extract refresh details before dropping the lock
                let refresh_data = match (&token_data.refresh_token, &token_data.client_id, &token_data.tenant_id) {
                    (Some(refresh_token), Some(client_id), Some(tenant_id)) => {
                        Some((
                            refresh_token.clone(),
                            client_id.clone(),
                            tenant_id.clone()
                        ))
                    }
                    _ => None
                };
                
                // If we have all required data for refresh
                if let Some((refresh_token, client_id, tenant_id)) = refresh_data {
                    trace!("attempting to refresh token using refresh token");
                    // We must drop the read lock before doing any async operations
                    drop(token_cache);
                    
                    // Format scope string for the refresh request
                    let scope_string = scopes.join(" ");
                    let scope = if scope_string.is_empty() { None } else { Some(scope_string.as_str()) };
                    
                    // Try to refresh the token
                    match refresh_token::exchange(
                        new_http_client(),
                        &tenant_id,
                        &client_id,
                        None,
                        &refresh_token,
                        scope,
                    ).await {
                        Ok(response) => {
                            trace!("successfully refreshed token");
                            let expires_in = std::time::Duration::from_secs(response.expires_in());
                            let expires_on = time::OffsetDateTime::now_utc() + expires_in;
                            
                            let access_token = AccessToken::new(
                                response.access_token().clone(),
                                expires_on,
                            );
                            
                            // Update the cache with the new tokens
                            let mut token_cache = self.0.write().await;
                            token_cache.insert(
                                scopes_key.clone(),
                                TokenData {
                                    access_token: access_token.clone(),
                                    refresh_token: Some(response.refresh_token().clone()),
                                    client_id: Some(client_id),
                                    tenant_id: Some(tenant_id),
                                },
                            );
                            
                            // Return the newly refreshed token
                            return Ok(access_token);
                        }
                        Err(err) => {
                            // Log the error but continue to fallback path
                            error!("Failed to refresh token: {}", err);
                            Some(err)
                        }
                    }
                } else {
                    trace!("no refresh token available for token refresh");
                    None
                }
            } else {
                trace!("no token found in cache");
                None
            }
        };
                
        // Double-check if another thread has already updated the token
        {
            let token_cache = self.0.read().await;
            if let Some(token_data) = token_cache.get(&scopes_key) {
                if !token_data.access_token.is_expired(None) {
                    trace!("returning token that was updated by another thread");
                    return Ok(token_data.access_token.clone());
                }
            }
        }

        // Log refresh attempt result for debugging
        if let Some(err) = refresh_attempt_result {
            trace!("refresh token failed, falling back to callback: {}", err);
        }
            
        // If we get here, we need to get a completely new token via the callback
        trace!("getting new token via callback");
        let access_token = callback.await?;
            
        // Store the new token in the cache
        // Note: The refresh token will be stored separately via store_token_with_refresh if available
        {
            let mut token_cache = self.0.write().await;
            token_cache.insert(
                scopes_key, 
                TokenData {
                    access_token: access_token.clone(),
                    refresh_token: None,  // Refresh token is stored separately if available
                    client_id: None,
                    tenant_id: None,
                }
            );
        }
            
        Ok(access_token)
    }
    
    /// Store a complete token including refresh token from external sources
    pub(crate) async fn store_token_with_refresh(
        &self,
        scopes: &[&str],
        access_token: AccessToken,
        refresh_token: Secret,
        client_id: String,
        tenant_id: String,
    ) -> azure_core::Result<()> {
        let scopes_key = scopes.iter().map(ToString::to_string).collect::<Vec<_>>();
        let mut token_cache = self.0.write().await;
        
        trace!("storing token with refresh token for scopes: {:?}", scopes);
        token_cache.insert(
            scopes_key,
            TokenData {
                access_token,
                refresh_token: Some(refresh_token),
                client_id: Some(client_id),
                tenant_id: Some(tenant_id),
            },
        );
        
        Ok(())
    }
}

impl Default for TokenCache {
    fn default() -> Self {
        TokenCache::new()
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use azure_core::credentials::Secret;
    use std::{sync::Mutex, time::Duration};
    use time::OffsetDateTime;

    #[derive(Debug)]
    struct MockCredential {
        token: AccessToken,
        get_token_call_count: Mutex<usize>,
    }

    impl MockCredential {
        fn new(token: AccessToken) -> Self {
            Self {
                token,
                get_token_call_count: Mutex::new(0),
            }
        }

        async fn get_token(&self, scopes: &[&str]) -> azure_core::Result<AccessToken> {
            // Include an incrementing counter in the token to track how many times the token has been refreshed
            let mut call_count = self.get_token_call_count.lock().unwrap();
            *call_count += 1;
            Ok(AccessToken {
                token: Secret::new(format!(
                    "{}-{}:{}",
                    scopes.join(" "),
                    self.token.token.secret(),
                    *call_count
                )),
                expires_on: self.token.expires_on,
            })
        }
    }

    const STORAGE_TOKEN_SCOPE: &str = "https://storage.azure.com/";
    const IOTHUB_TOKEN_SCOPE: &str = "https://iothubs.azure.net";

    #[tokio::test]
    async fn test_get_token_different_resources() -> azure_core::Result<()> {
        let resource1 = &[STORAGE_TOKEN_SCOPE];
        let resource2 = &[IOTHUB_TOKEN_SCOPE];
        let secret_string = "test-token";
        let expires_on = OffsetDateTime::now_utc() + Duration::from_secs(300);
        let access_token = AccessToken::new(Secret::new(secret_string), expires_on);

        let mock_credential = MockCredential::new(access_token);

        let cache = TokenCache::new();

        // Test that querying a token for the same resource twice returns the same (cached) token on the second call
        let token1 = cache
            .get_token(resource1, mock_credential.get_token(resource1))
            .await?;
        let token2 = cache
            .get_token(resource1, mock_credential.get_token(resource1))
            .await?;

        let expected_token = format!("{}-{}:1", resource1.join(" "), secret_string);
        assert_eq!(token1.token.secret(), expected_token);
        assert_eq!(token2.token.secret(), expected_token);

        // Test that querying a token for a second resource returns a different token, as the cache is per-resource.
        // Also test that the same token is the returned (cached) on a second call.
        let token3 = cache
            .get_token(resource2, mock_credential.get_token(resource2))
            .await?;
        let token4 = cache
            .get_token(resource2, mock_credential.get_token(resource2))
            .await?;
        let expected_token = format!("{}-{}:2", resource2.join(" "), secret_string);
        assert_eq!(token3.token.secret(), expected_token);
        assert_eq!(token4.token.secret(), expected_token);

        Ok(())
    }

    #[tokio::test]
    async fn test_refresh_expired_token() -> azure_core::Result<()> {
        let resource = &[STORAGE_TOKEN_SCOPE];
        let access_token = "test-token";
        let expires_on = OffsetDateTime::now_utc();
        let token_response = AccessToken::new(Secret::new(access_token), expires_on);

        let mock_credential = MockCredential::new(token_response);

        let cache = TokenCache::new();

        // Test that querying an expired token returns a new token
        for i in 1..5 {
            let token = cache
                .get_token(resource, mock_credential.get_token(resource))
                .await?;
            assert_eq!(
                token.token.secret(),
                format!("{}-{}:{}", resource.join(" "), access_token, i)
            );
        }

        Ok(())
    }
}
