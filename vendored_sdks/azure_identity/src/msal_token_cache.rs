// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
// The comments illustrate what's the difference between the original msal token cache

use async_lock::RwLock;
use azure_core::{
    credentials::{AccessToken, Secret},
    http::new_http_client,
    Error, Result,
};
use futures::Future;
use serde::{Deserialize, Serialize};
use serde_json::{from_str, to_string_pretty};
use std::{
    collections::HashMap,
    time::{SystemTime, UNIX_EPOCH},
};
use time::OffsetDateTime;
use tracing::{debug, error, trace};

use crate::refresh_token;

/// Credential types supported by the token cache
#[derive(Debug, Clone, PartialEq, Eq, Hash, Serialize, Deserialize)]
pub enum CredentialType {
    AccessToken,
    RefreshToken,
    Account,
    IdToken,
    AppMetadata,
}

impl std::fmt::Display for CredentialType {
    fn fmt(&self, f: &mut std::fmt::Formatter) -> std::fmt::Result {
        match self {
            CredentialType::AccessToken => write!(f, "AccessToken"),
            CredentialType::RefreshToken => write!(f, "RefreshToken"),
            CredentialType::Account => write!(f, "Account"),
            CredentialType::IdToken => write!(f, "IdToken"),
            CredentialType::AppMetadata => write!(f, "AppMetadata"),
        }
    }
}

/// Authority types
#[derive(Debug, Clone, PartialEq, Eq, Hash, Serialize, Deserialize)]
pub enum AuthorityType {
    /// Active Directory Federation Services
    ADFS,
    /// Microsoft Secure Token Service (AAD v2 for both AAD & MSA)
    MSSTS,
}

impl std::fmt::Display for AuthorityType {
    fn fmt(&self, f: &mut std::fmt::Formatter) -> std::fmt::Result {
        match self {
            AuthorityType::ADFS => write!(f, "ADFS"),
            AuthorityType::MSSTS => write!(f, "MSSTS"),
        }
    }
}

/// A struct to hold access token data
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct AccessTokenEntry {
    pub credential_type: CredentialType,
    pub secret: Secret,
    pub home_account_id: Option<String>,
    pub environment: Option<String>,
    pub client_id: Option<String>,
    pub scopes: Option<String>,    // target
    pub tenant_id: Option<String>, // realm
    pub token_type: Option<String>,
    pub cached_at: String,
    pub expires_on: String,
    pub extended_expires_on: String,
    pub refresh_on: Option<String>,
    // pub key_id: Option<String>,
}

/// A struct to hold refresh token data
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct RefreshTokenEntry {
    pub credential_type: CredentialType,
    pub secret: Secret,
    pub home_account_id: Option<String>,
    pub environment: Option<String>,
    pub client_id: Option<String>,
    pub last_modification_time: Option<String>,
    // pub scopes: Option<String>,  // target
    // pub family_id: Option<String>,
}

/// A struct to hold ID token data
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct IdTokenEntry {
    pub credential_type: CredentialType,
    pub secret: Secret,
    pub home_account_id: Option<String>,
    pub environment: Option<String>,
    pub tenant_id: Option<String>,
    pub client_id: Option<String>,
}

/// A struct to hold account information
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct AccountEntry {
    pub home_account_id: Option<String>,
    pub environment: Option<String>,
    pub tenant_id: Option<String>,
    pub local_account_id: Option<String>,
    pub username: Option<String>,
    pub authority_type: Option<AuthorityType>,
    pub account_source: Option<String>,
}

/// AppMetadata seems not needed in the current implementation
// #[derive(Debug, Clone, Serialize, Deserialize)]
// pub struct AppMetadataEntry {
//     pub client_id: Option<String>,
//     pub environment: Option<String>,
//     pub family_id: Option<String>,
// }

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(untagged)]
pub enum TokenCacheEntry {
    AccessToken(AccessTokenEntry),
    RefreshToken(RefreshTokenEntry),
    IdToken(IdTokenEntry),
    Account(AccountEntry),
    // AppMetadata(AppMetadataEntry),
}

/// Implements a token cache for storing and retrieving tokens using MSAL schema
#[derive(Debug)]
pub struct MsalTokenCache {
    cache: RwLock<HashMap<CredentialType, HashMap<String, TokenCacheEntry>>>,
    has_state_changed: RwLock<bool>,
}

impl MsalTokenCache {
    pub fn new() -> Self {
        Self {
            cache: RwLock::new(HashMap::new()),
            has_state_changed: RwLock::new(false),
        }
    }

    fn generate_access_token_key(
        &self,
        home_account_id: Option<&str>,
        environment: Option<&str>,
        client_id: Option<&str>,
        tenant_id: Option<&str>,
        scopes: Option<&str>,
    ) -> String {
        format!(
            "{}-{}-{}-{}-{}-{}",
            home_account_id.unwrap_or(""),
            environment.unwrap_or(""),
            CredentialType::AccessToken,
            client_id.unwrap_or(""),
            tenant_id.unwrap_or(""),
            scopes.unwrap_or(""),
        )
        .to_lowercase()
    }

    fn generate_refresh_token_key(
        &self,
        home_account_id: Option<&str>,
        environment: Option<&str>,
        client_id: Option<&str>,
    ) -> String {
        format!(
            "{}-{}-{}-{}",
            home_account_id.unwrap_or(""),
            environment.unwrap_or(""),
            CredentialType::RefreshToken,
            client_id.unwrap_or(""),
            // "", // Refresh token is cross-tenant in AAD, msal input empty tenant_id here, but no idea why it is required, remove it for now
            // scopes.unwrap_or(""), // why scopes are needed for a refresh token
        )
        .to_lowercase()
    }

    fn generate_id_token_key(
        &self,
        home_account_id: Option<&str>,
        environment: Option<&str>,
        client_id: Option<&str>,
        tenant_id: Option<&str>,
    ) -> String {
        format!(
            "{}-{}-{}-{}-{}",
            home_account_id.unwrap_or(""),
            environment.unwrap_or(""),
            CredentialType::IdToken,
            client_id.unwrap_or(""),
            tenant_id.unwrap_or(""),
            // "", // Refer msal: Albeit irrelevant, schema requires an empty scope here. No idea why it is required, remove it for now
        )
        .to_lowercase()
    }

    fn generate_account_key(
        &self,
        home_account_id: Option<&str>,
        environment: Option<&str>,
        tenant_id: Option<&str>,
    ) -> String {
        format!(
            "{}-{}-{}",
            home_account_id.unwrap_or(""),
            environment.unwrap_or(""),
            tenant_id.unwrap_or(""),
        )
        .to_lowercase()
    }

    // fn generate_app_metadata_key(
    //     &self,
    //     environment: Option<&str>,
    //     client_id: Option<&str>,
    // ) -> String {
    //     format!(
    //         "appmetadata-{}-{}",
    //         environment.unwrap_or(""),
    //         client_id.unwrap_or(""),
    //     ).to_lowercase()
    // }

    /// Get an access token from the cache
    pub async fn get_access_token(
        &self,
        home_account_id: Option<&str>,
        environment: Option<&str>,
        client_id: Option<&str>,
        tenant_id: Option<&str>,
        scopes: Option<&[&str]>,
    ) -> Option<AccessTokenEntry> {
        let scopes_str = scopes.map(|t| t.join(" "));
        let key = self.generate_access_token_key(
            home_account_id,
            environment,
            client_id,
            tenant_id,
            scopes_str.as_deref(),
        );

        let cache = self.cache.read().await;
        if let Some(access_tokens) = cache.get(&CredentialType::AccessToken) {
            if let Some(TokenCacheEntry::AccessToken(token)) = access_tokens.get(&key) {
                if let Ok(expires_on) = token.expires_on.parse::<u64>() {
                    let now = SystemTime::now()
                        .duration_since(UNIX_EPOCH)
                        .unwrap_or_default()
                        .as_secs();

                    if expires_on > now {
                        return Some(token.clone());
                    }
                }
            }
        }

        None
    }

    pub async fn get_refresh_token(
        &self,
        home_account_id: Option<&str>,
        environment: Option<&str>,
        client_id: Option<&str>,
    ) -> Option<RefreshTokenEntry> {
        let key = self.generate_refresh_token_key(home_account_id, environment, client_id);

        let cache = self.cache.read().await;
        if let Some(refresh_tokens) = cache.get(&CredentialType::RefreshToken) {
            if let Some(TokenCacheEntry::RefreshToken(token)) = refresh_tokens.get(&key) {
                return Some(token.clone());
            }
        }

        None
    }

    /// Add or update an access token in the cache
    pub async fn add_access_token(&self, token: AccessTokenEntry) -> Result<()> {
        let key = self.generate_access_token_key(
            token.home_account_id.as_deref(),
            token.environment.as_deref(),
            token.client_id.as_deref(),
            token.tenant_id.as_deref(),
            token.scopes.as_deref(),
        );

        let mut cache = self.cache.write().await;
        let access_tokens = cache
            .entry(CredentialType::AccessToken)
            .or_insert_with(HashMap::new);
        access_tokens.insert(key, TokenCacheEntry::AccessToken(token));

        // Mark the cache as changed
        let mut has_state_changed = self.has_state_changed.write().await;
        *has_state_changed = true;

        Ok(())
    }

    /// Add or update a refresh token in the cache
    pub async fn add_refresh_token(&self, token: RefreshTokenEntry) -> Result<()> {
        let key = self.generate_refresh_token_key(
            token.home_account_id.as_deref(),
            token.environment.as_deref(),
            token.client_id.as_deref(),
        );

        let mut cache = self.cache.write().await;
        let refresh_tokens = cache
            .entry(CredentialType::RefreshToken)
            .or_insert_with(HashMap::new);
        refresh_tokens.insert(key, TokenCacheEntry::RefreshToken(token));

        // Mark the cache as changed
        let mut has_state_changed = self.has_state_changed.write().await;
        *has_state_changed = true;

        Ok(())
    }

    /// Add or update an ID token in the cache
    pub async fn add_id_token(&self, token: IdTokenEntry) -> Result<()> {
        let key = self.generate_id_token_key(
            token.home_account_id.as_deref(),
            token.environment.as_deref(),
            token.client_id.as_deref(),
            token.tenant_id.as_deref(),
        );

        let mut cache = self.cache.write().await;
        let id_tokens = cache
            .entry(CredentialType::IdToken)
            .or_insert_with(HashMap::new);
        id_tokens.insert(key, TokenCacheEntry::IdToken(token));

        // Mark the cache as changed
        let mut has_state_changed = self.has_state_changed.write().await;
        *has_state_changed = true;

        Ok(())
    }

    /// Add or update an account in the cache
    pub async fn add_account(&self, account: AccountEntry) -> Result<()> {
        let key = self.generate_account_key(
            account.home_account_id.as_deref(),
            account.environment.as_deref(),
            account.tenant_id.as_deref(),
        );

        let mut cache = self.cache.write().await;
        let accounts = cache
            .entry(CredentialType::Account)
            .or_insert_with(HashMap::new);
        accounts.insert(key, TokenCacheEntry::Account(account));

        // Mark the cache as changed
        let mut has_state_changed = self.has_state_changed.write().await;
        *has_state_changed = true;

        Ok(())
    }

    // /// Add or update app metadata in the cache
    // pub async fn add_app_metadata(&self, metadata: AppMetadataEntry) -> Result<()> {
    //     let key = self.generate_app_metadata_key(
    //         metadata.environment.as_deref(),
    //         metadata.client_id.as_deref(),
    //     );

    //     let mut cache = self.cache.write().await;
    //     let app_metadata = cache.entry(CredentialType::AppMetadata).or_insert_with(HashMap::new);
    //     app_metadata.insert(key, TokenCacheEntry::AppMetadata(metadata));

    //     // Mark the cache as changed
    //     let mut has_state_changed = self.has_state_changed.write().await;
    //     *has_state_changed = true;

    //     Ok(())
    // }

    /// Remove an access token from the cache
    pub async fn remove_access_token(
        &self,
        home_account_id: Option<&str>,
        environment: Option<&str>,
        client_id: Option<&str>,
        tenant_id: Option<&str>,
        scopes: Option<&[&str]>,
    ) -> Result<()> {
        let scopes_str = scopes.map(|t| t.join(" "));
        let key = self.generate_access_token_key(
            home_account_id,
            environment,
            client_id,
            tenant_id,
            scopes_str.as_deref(),
        );

        let mut cache = self.cache.write().await;
        if let Some(access_tokens) = cache.get_mut(&CredentialType::AccessToken) {
            if access_tokens.remove(&key).is_some() {
                let mut has_state_changed = self.has_state_changed.write().await;
                *has_state_changed = true;
            }
        }

        Ok(())
    }

    /// Remove a refresh token from the cache
    pub async fn remove_refresh_token(
        &self,
        home_account_id: Option<&str>,
        environment: Option<&str>,
        client_id: Option<&str>,
    ) -> Result<()> {
        let key = self.generate_refresh_token_key(home_account_id, environment, client_id);

        let mut cache = self.cache.write().await;
        if let Some(refresh_tokens) = cache.get_mut(&CredentialType::RefreshToken) {
            if refresh_tokens.remove(&key).is_some() {
                let mut has_state_changed = self.has_state_changed.write().await;
                *has_state_changed = true;
            }
        }

        Ok(())
    }    
    
    
    /// Implement a general method to get an access token for a given scope from the cache
    /// Azure cli can only read token cache from a local file, it's unreasonable to request a new one using a callback function, the credential required for the callback function is not available in the context. So remove the callback function from the input.
    pub async fn get_access_token_and_possibly_refresh_it(
        &self,
        scopes: &[&str],
        home_account_id: Option<&str>,
        environment: Option<&str>,
        client_id: Option<&str>,
        tenant_id: Option<&str>,
    ) -> Result<azure_core::credentials::AccessToken> {
        // Try to get a valid token from the cache
        if let Some(token_entry) = self
            .get_access_token(
                home_account_id,
                environment,
                client_id,
                tenant_id,
                Some(scopes),
            )
            .await
        {
            trace!("Returning cached valid token");
            let expires_on = token_entry.expires_on.parse::<u64>()
                .map(|time| OffsetDateTime::from_unix_timestamp(time as i64))
                .unwrap_or_else(|_| Ok(OffsetDateTime::now_utc()))
                .unwrap_or_else(|_| OffsetDateTime::now_utc());
            
            return Ok(azure_core::credentials::AccessToken {
                token: token_entry.secret,
                expires_on,
            });
        }

        // Try to refresh the token using a refresh token if available
        if let Some(refresh_token) = self
            .get_refresh_token(home_account_id, environment, client_id)
            .await
        {
            trace!("Attempting to refresh token using refresh token");

            let scope_string = scopes.join(" ");
            let scope = if scope_string.is_empty() {
                None
            } else {
                Some(scope_string.as_str())
            };

            match refresh_token::exchange(
                new_http_client(),
                tenant_id.unwrap_or("common"),
                client_id.unwrap_or(""),
                None,
                &refresh_token.secret,
                scope,
            )
            .await
            {
                Ok(response) => {
                    trace!("Successfully refreshed token");
                    let expires_in = std::time::Duration::from_secs(response.expires_in());
                    let now = SystemTime::now()
                        .duration_since(UNIX_EPOCH)
                        .unwrap_or_default();
                    let now_secs = now.as_secs();
                    let expires_on = OffsetDateTime::now_utc() + expires_in;

                    // Create the AccessToken
                    let access_token = azure_core::credentials::AccessToken::new(
                        response.access_token().clone(),
                        expires_on,
                    );

                    //Update the cache with the new tokens
                    let home_account_id_str = home_account_id.map(String::from);
                    let environment_str = environment.map(String::from);
                    let client_id_str = client_id.map(String::from);
                    let tenant_id_str = tenant_id.map(String::from);
                    let scopes_str = Some(scopes.join(" "));

                    let access_token_entry = AccessTokenEntry {
                        credential_type: CredentialType::AccessToken,
                        secret: response.access_token().clone(),
                        home_account_id: home_account_id_str.clone(),
                        environment: environment_str.clone(),
                        client_id: client_id_str.clone(),
                        scopes: scopes_str.clone(),
                        tenant_id: tenant_id_str.clone(),
                        token_type: Some("Bearer".to_string()),
                        cached_at: now_secs.to_string(),
                        expires_on: (now_secs + response.expires_in()).to_string(),
                        extended_expires_on: (now_secs + response.ext_expires_in()).to_string(),
                        refresh_on: None,
                    };

                    let refresh_token_entry = RefreshTokenEntry {
                        credential_type: CredentialType::RefreshToken,
                        secret: response.refresh_token().clone(),
                        home_account_id: home_account_id_str,
                        environment: environment_str,
                        client_id: client_id_str,
                        last_modification_time: Some(now_secs.to_string()),
                    };

                    if let Err(e) = self.add_access_token(access_token_entry).await {
                        error!("Failed to add access token to cache: {}", e);
                    }

                    if let Err(e) = self.add_refresh_token(refresh_token_entry).await {
                        error!("Failed to add refresh token to cache: {}", e);
                    }                    return Ok(access_token);
                },
                Err(err) => {
                    error!("Failed to refresh token: {}", err);
                    return Err(Error::new(
                        azure_core::error::ErrorKind::Credential,
                        "No valid token found in cache and token refresh failed. Please log in again."
                    ));
                }
            }
        }

        Err(Error::new(
            azure_core::error::ErrorKind::Credential,
            "No valid token found in cache and no refresh token available. Please log in again."
        ))
    }

    /// Clear the token cache
    pub async fn clear(&self) -> Result<()> {
        let mut cache = self.cache.write().await;
        cache.clear();

        let mut has_state_changed = self.has_state_changed.write().await;
        *has_state_changed = true;

        Ok(())
    }

    /// Check if the token cache has changed since last serialization/deserialization
    pub async fn has_state_changed(&self) -> bool {
        let has_state_changed = self.has_state_changed.read().await;
        *has_state_changed
    }

    /// Serialize the token cache to a string
    pub async fn serialize(&self) -> Result<String> {
        let cache = self.cache.read().await;
        let json = to_string_pretty(&*cache)?;

        // Reset the state changed flag
        let mut has_state_changed = self.has_state_changed.write().await;
        *has_state_changed = false;

        Ok(json)
    }

    /// Deserialize the token cache from a string
    pub async fn deserialize(&self, state: &str) -> Result<()> {
        let parsed_cache: HashMap<CredentialType, HashMap<String, TokenCacheEntry>> =
            from_str(state)?;

        let mut cache = self.cache.write().await;
        *cache = parsed_cache;

        // Reset the state changed flag
        let mut has_state_changed = self.has_state_changed.write().await;
        *has_state_changed = false;

        Ok(())
    }
}


impl Default for MsalTokenCache {
    fn default() -> Self {
        Self::new()
    }
}

impl Clone for MsalTokenCache {
    fn clone(&self) -> Self {
        Self::new() // In a real implementation, might want to copy contents
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::time::Duration;
    use tokio::test;

    #[test]
    async fn test_access_token_cache() -> Result<()> {
        let cache = MsalTokenCache::new();

        // Create a token
        let home_account_id = "home_id";
        let environment = "environment";
        let client_id = "client_id";
        let tenant_id = "tenant_id";
        let scopes = "scope1 scope2";

        let now = SystemTime::now()
            .duration_since(UNIX_EPOCH)
            .unwrap_or_default();
        let now_secs = now.as_secs();
        let expires_on = now_secs + 3600;

        let token = AccessTokenEntry {
            credential_type: CredentialType::AccessToken,
            secret: Secret::new("token_value"),
            home_account_id: Some(home_account_id.to_string()),
            environment: Some(environment.to_string()),
            client_id: Some(client_id.to_string()),
            scopes: Some(scopes.to_string()),
            tenant_id: Some(tenant_id.to_string()),
            token_type: Some("Bearer".to_string()),
            cached_at: now_secs.to_string(),
            expires_on: expires_on.to_string(),
            extended_expires_on: expires_on.to_string(),
            refresh_on: None,
        };

        // Add to cache
        cache.add_access_token(token.clone()).await?;

        // Check if it's retrievable
        let scopes = &["scope1", "scope2"];
        let retrieved = cache
            .get_access_token(
                Some(home_account_id),
                Some(environment),
                Some(client_id),
                Some(tenant_id),
                Some(scopes),
            )
            .await;

        assert!(retrieved.is_some());
        let retrieved = retrieved.unwrap();
        assert_eq!(retrieved.secret.secret(), "token_value");

        // Test removal
        cache
            .remove_access_token(
                Some(home_account_id),
                Some(environment),
                Some(client_id),
                Some(tenant_id),
                Some(scopes),
            )
            .await?;

        let retrieved = cache
            .get_access_token(
                Some(home_account_id),
                Some(environment),
                Some(client_id),
                Some(tenant_id),
                Some(scopes),
            )
            .await;

        assert!(retrieved.is_none());

        Ok(())
    }

    #[test]
    async fn test_serialization() -> Result<()> {
        let cache = MsalTokenCache::new();

        // Create a token
        let home_account_id = "home_id";
        let environment = "environment";
        let client_id = "client_id";
        let tenant_id = "tenant_id";
        let scopes = "scope1 scope2";

        let now = SystemTime::now()
            .duration_since(UNIX_EPOCH)
            .unwrap_or_default();
        let now_secs = now.as_secs();
        let expires_on = now_secs + 3600;

        let token = AccessTokenEntry {
            credential_type: CredentialType::AccessToken,
            secret: Secret::new("token_value"),
            home_account_id: Some(home_account_id.to_string()),
            environment: Some(environment.to_string()),
            client_id: Some(client_id.to_string()),
            scopes: Some(scopes.to_string()),
            tenant_id: Some(tenant_id.to_string()),
            token_type: Some("Bearer".to_string()),
            cached_at: now_secs.to_string(),
            expires_on: expires_on.to_string(),
            extended_expires_on: expires_on.to_string(),
            refresh_on: None,
        };

        // Add to cache
        cache.add_access_token(token.clone()).await?;

        // Check if state changed
        assert!(cache.has_state_changed().await);

        // Serialize
        let serialized = cache.serialize().await?;

        // Check that serialization resets the state changed flag
        assert!(!cache.has_state_changed().await);

        // Clear the cache
        cache.clear().await?;

        // Check that the token is gone
        let scopes = &["scope1", "scope2"];
        let retrieved = cache
            .get_access_token(
                Some(home_account_id),
                Some(environment),
                Some(client_id),
                Some(tenant_id),
                Some(scopes),
            )
            .await;
        assert!(retrieved.is_none());

        // Deserialize
        cache.deserialize(&serialized).await?;

        // Check that the token is back
        let retrieved = cache
            .get_access_token(
                Some(home_account_id),
                Some(environment),
                Some(client_id),
                Some(tenant_id),
                Some(scopes),
            )
            .await;

        assert!(retrieved.is_some());
        let retrieved = retrieved.unwrap();
        assert_eq!(retrieved.secret.secret(), "token_value");

        Ok(())
    }    #[test]
    async fn test_get_token_from_cache() -> Result<()> {
        let cache = MsalTokenCache::new();

        let home_account_id = "home_id";
        let environment = "environment";
        let client_id = "client_id";
        let tenant_id = "tenant_id";
        let scopes = &["scope1", "scope2"];

        // Manually create and add an access token to the cache
        let expires_on = OffsetDateTime::now_utc() + Duration::from_secs(3600);
        let expires_on_secs = expires_on.unix_timestamp() as u64;
        let now = SystemTime::now()
            .duration_since(UNIX_EPOCH)
            .unwrap_or_default();
        let now_secs = now.as_secs();
        
        let access_token_entry = AccessTokenEntry {
            credential_type: CredentialType::AccessToken,
            secret: Secret::new("token_value"),
            home_account_id: Some(home_account_id.to_string()),
            environment: Some(environment.to_string()),
            client_id: Some(client_id.to_string()),
            scopes: Some(scopes.join(" ")),
            tenant_id: Some(tenant_id.to_string()),
            token_type: Some("Bearer".to_string()),
            cached_at: now_secs.to_string(),
            expires_on: expires_on_secs.to_string(),
            extended_expires_on: (expires_on_secs + 3600).to_string(),
            refresh_on: None,
        };
        
        // Add access token to cache
        cache.add_access_token(access_token_entry).await?;
        
        // Also add a refresh token to fully test the cache
        let refresh_token_entry = RefreshTokenEntry {
            credential_type: CredentialType::RefreshToken,
            secret: Secret::new("refresh_token_value"),
            home_account_id: Some(home_account_id.to_string()),
            environment: Some(environment.to_string()),
            client_id: Some(client_id.to_string()),
            last_modification_time: Some(now_secs.to_string()),
        };
        
        cache.add_refresh_token(refresh_token_entry).await?;

        // Get a token from the cache
        let token = cache
            .get_token(
                scopes,
                Some(home_account_id),
                Some(environment),
                Some(client_id),
                Some(tenant_id),
            )
            .await?;

        assert_eq!(token.token.secret(), "token_value");

        // Getting the token again should still use the cache
        let token2 = cache
            .get_token(
                scopes,
                Some(home_account_id),
                Some(environment),
                Some(client_id),
                Some(tenant_id),
            )
            .await?;        assert_eq!(token2.token.secret(), "token_value");

        // Test token refresh scenario by removing the access token and keeping only the refresh token
        cache
            .remove_access_token(
                Some(home_account_id),
                Some(environment),
                Some(client_id),
                Some(tenant_id),
                Some(scopes),
            )
            .await?;

        // Verify the access token is gone
        let retrieved = cache
            .get_access_token(
                Some(home_account_id),
                Some(environment),
                Some(client_id),
                Some(tenant_id),
                Some(scopes),
            )
            .await;
        assert!(retrieved.is_none());
        
        // Without actually being able to refresh the token since refresh_token::exchange
        // would need to be mocked, we'll just test that the refresh token exists
        let refresh_token = cache
            .get_refresh_token(
                Some(home_account_id),
                Some(environment), 
                Some(client_id)
            )
            .await;
        
        assert!(refresh_token.is_some());
        assert_eq!(refresh_token.unwrap().secret.secret(), "refresh_token_value");

        Ok(())
    }

}
