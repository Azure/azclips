// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
// The comments illustrate what's the difference between the original msal token cache

use azure_core::{
    credentials::{AccessToken, Secret},
    http::new_http_client,
    Error, Result,
};

use serde::{Deserialize, Serialize};
use serde_json::{from_str, to_string_pretty};
use std::{
    collections::HashMap,
    fs,
    path::PathBuf,
    sync::RwLock,
    time::{SystemTime, UNIX_EPOCH},
};
use time::OffsetDateTime;
use tracing::{error, trace};

use crate::refresh_token;

/// Credential types supported by the token cache
#[derive(Debug, Clone, PartialEq, Eq, Hash, Serialize, Deserialize)]
pub enum CredentialType {
    AccessToken,
    RefreshToken,
    Account,
    IdToken,
    // AppMetadata, // Not used in the current implementation
}

impl std::fmt::Display for CredentialType {
    fn fmt(&self, f: &mut std::fmt::Formatter) -> std::fmt::Result {
        match self {
            CredentialType::AccessToken => write!(f, "AccessToken"),
            CredentialType::RefreshToken => write!(f, "RefreshToken"),
            CredentialType::Account => write!(f, "Account"),
            CredentialType::IdToken => write!(f, "IdToken"),
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

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(untagged)]
pub enum TokenCacheEntry {
    AccessToken(AccessTokenEntry),
    RefreshToken(RefreshTokenEntry),
    IdToken(IdTokenEntry),
    Account(AccountEntry),
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

    /// Get an access token directly from the cache
    pub fn get_access_token(
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

        let cache = self.cache.read().unwrap();
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

    pub fn get_refresh_token(
        &self,
        home_account_id: Option<&str>,
        environment: Option<&str>,
        client_id: Option<&str>,
    ) -> Option<RefreshTokenEntry> {
        let key = self.generate_refresh_token_key(home_account_id, environment, client_id);

        let cache = self.cache.read().unwrap();
        if let Some(refresh_tokens) = cache.get(&CredentialType::RefreshToken) {
            if let Some(TokenCacheEntry::RefreshToken(token)) = refresh_tokens.get(&key) {
                return Some(token.clone());
            }
        }

        None
    }

    pub fn get_id_token(
        &self,
        home_account_id: Option<&str>,
        environment: Option<&str>,
        client_id: Option<&str>,
        tenant_id: Option<&str>,
    ) -> Option<IdTokenEntry> {
        let key = self.generate_id_token_key(home_account_id, environment, client_id, tenant_id);

        let cache = self.cache.read().unwrap();
        if let Some(id_tokens) = cache.get(&CredentialType::IdToken) {
            if let Some(TokenCacheEntry::IdToken(token)) = id_tokens.get(&key) {
                return Some(token.clone());
            }
        }

        None
    }

    pub fn get_account(
        &self,
        home_account_id: Option<&str>,
        environment: Option<&str>,
        tenant_id: Option<&str>,
    ) -> Option<AccountEntry> {
        let key = self.generate_account_key(home_account_id, environment, tenant_id);

        let cache = self.cache.read().unwrap();
        if let Some(accounts) = cache.get(&CredentialType::Account) {
            if let Some(TokenCacheEntry::Account(account)) = accounts.get(&key) {
                return Some(account.clone());
            }
        }

        None
    }
    /// Add or update an access token in the cache
    pub fn add_access_token(&self, token: AccessTokenEntry) -> Result<()> {
        let key = self.generate_access_token_key(
            token.home_account_id.as_deref(),
            token.environment.as_deref(),
            token.client_id.as_deref(),
            token.tenant_id.as_deref(),
            token.scopes.as_deref(),
        );

        let mut cache = self.cache.write().unwrap();
        let access_tokens = cache
            .entry(CredentialType::AccessToken)
            .or_insert_with(HashMap::new);
        access_tokens.insert(key, TokenCacheEntry::AccessToken(token));

        let mut has_state_changed = self.has_state_changed.write().unwrap();
        *has_state_changed = true;

        Ok(())
    }

    /// Add or update a refresh token in the cache
    pub fn add_refresh_token(&self, token: RefreshTokenEntry) -> Result<()> {
        let key = self.generate_refresh_token_key(
            token.home_account_id.as_deref(),
            token.environment.as_deref(),
            token.client_id.as_deref(),
        );

        let mut cache = self.cache.write().unwrap();
        let refresh_tokens = cache
            .entry(CredentialType::RefreshToken)
            .or_insert_with(HashMap::new);
        refresh_tokens.insert(key, TokenCacheEntry::RefreshToken(token));

        let mut has_state_changed = self.has_state_changed.write().unwrap();
        *has_state_changed = true;

        Ok(())
    }

    /// Add or update an ID token in the cache
    pub fn add_id_token(&self, token: IdTokenEntry) -> Result<()> {
        let key = self.generate_id_token_key(
            token.home_account_id.as_deref(),
            token.environment.as_deref(),
            token.client_id.as_deref(),
            token.tenant_id.as_deref(),
        );

        let mut cache = self.cache.write().unwrap();
        let id_tokens = cache
            .entry(CredentialType::IdToken)
            .or_insert_with(HashMap::new);
        id_tokens.insert(key, TokenCacheEntry::IdToken(token));

        let mut has_state_changed = self.has_state_changed.write().unwrap();
        *has_state_changed = true;

        Ok(())
    }
    /// Add or update an account in the cache
    pub fn add_account(&self, account: AccountEntry) -> Result<()> {
        let key = self.generate_account_key(
            account.home_account_id.as_deref(),
            account.environment.as_deref(),
            account.tenant_id.as_deref(),
        );

        let mut cache = self.cache.write().unwrap();
        let accounts = cache
            .entry(CredentialType::Account)
            .or_insert_with(HashMap::new);
        accounts.insert(key, TokenCacheEntry::Account(account));

        let mut has_state_changed = self.has_state_changed.write().unwrap();
        *has_state_changed = true;

        Ok(())
    }
    /// Remove an access token from the cache
    pub fn remove_access_token(
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

        let mut cache = self.cache.write().unwrap();
        if let Some(access_tokens) = cache.get_mut(&CredentialType::AccessToken) {
            if access_tokens.remove(&key).is_some() {
                let mut has_state_changed = self.has_state_changed.write().unwrap();
                *has_state_changed = true;
            }
        }

        Ok(())
    }
    /// Remove a refresh token from the cache
    pub fn remove_refresh_token(
        &self,
        home_account_id: Option<&str>,
        environment: Option<&str>,
        client_id: Option<&str>,
    ) -> Result<()> {
        let key = self.generate_refresh_token_key(home_account_id, environment, client_id);

        let mut cache = self.cache.write().unwrap_or_else(|e| {
            error!("Failed to write cache: {}", e);
            panic!("Failed to write cache: {}", e);
        });
        if let Some(refresh_tokens) = cache.get_mut(&CredentialType::RefreshToken) {
            if refresh_tokens.remove(&key).is_some() {
                let mut has_state_changed = self.has_state_changed.write().unwrap();
                *has_state_changed = true;
            }
        }

        Ok(())
    }
    /// Remove an ID token from the cache
    pub fn remove_id_token(
        &self,
        home_account_id: Option<&str>,
        environment: Option<&str>,
        client_id: Option<&str>,
        tenant_id: Option<&str>,
    ) -> Result<()> {
        let key = self.generate_id_token_key(home_account_id, environment, client_id, tenant_id);

        let mut cache = self.cache.write().unwrap_or_else(|e| {
            error!("Failed to write cache: {}", e);
            panic!("Failed to write cache: {}", e);
        });
        if let Some(id_tokens) = cache.get_mut(&CredentialType::IdToken) {
            if id_tokens.remove(&key).is_some() {
                let mut has_state_changed = self.has_state_changed.write().unwrap();
                *has_state_changed = true;
            }
        }

        Ok(())
    }
    /// Remove an account from the cache
    pub fn remove_account(
        &self,
        home_account_id: Option<&str>,
        environment: Option<&str>,
        tenant_id: Option<&str>,
    ) -> Result<()> {
        let key = self.generate_account_key(home_account_id, environment, tenant_id);

        let mut cache = self.cache.write().unwrap_or_else(|e| {
            error!("Failed to write cache: {}", e);
            panic!("Failed to write cache: {}", e);
        });
        if let Some(accounts) = cache.get_mut(&CredentialType::Account) {
            if accounts.remove(&key).is_some() {
                let mut has_state_changed = self.has_state_changed.write().unwrap();
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
        if let Some(token_entry) = self.get_access_token(
            home_account_id,
            environment,
            client_id,
            tenant_id,
            Some(scopes),
        ) {
            trace!("Returning cached valid token");
            let expires_on = token_entry
                .expires_on
                .parse::<u64>()
                .map(|time| OffsetDateTime::from_unix_timestamp(time as i64))
                .unwrap_or_else(|_| Ok(OffsetDateTime::now_utc()))
                .unwrap_or_else(|_| OffsetDateTime::now_utc());

            return Ok(azure_core::credentials::AccessToken {
                token: token_entry.secret,
                expires_on,
            });
        } // Try to refresh the token using a refresh token if available
        if let Some(refresh_token) = self.get_refresh_token(home_account_id, environment, client_id)
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

                    // Add tokens to cache (synchronously now)
                    if let Err(e) = self.add_access_token(access_token_entry) {
                        error!("Failed to add access token to cache: {}", e);
                    }

                    if let Err(e) = self.add_refresh_token(refresh_token_entry) {
                        error!("Failed to add refresh token to cache: {}", e);
                    }
                    return Ok(access_token);
                }
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
            "No valid token found in cache and no refresh token available. Please log in again.",
        ))
    }
    /// Clear the token cache
    pub fn clear(&self) -> Result<()> {
        let mut cache = self.cache.write().unwrap_or_else(|e| {
            error!("Failed to write cache: {}", e);
            panic!("Failed to write cache: {}", e);
        });
        cache.clear();

        let mut has_state_changed = self.has_state_changed.write().unwrap();
        *has_state_changed = true;

        Ok(())
    }

    /// Check if the token cache has changed since last serialization/deserialization
    pub fn has_state_changed(&self) -> bool {
        let has_state_changed = self.has_state_changed.read().unwrap();
        *has_state_changed
    }

    /// Serialize the token cache to a string
    pub fn serialize(&self) -> Result<String> {
        let cache = self.cache.read().unwrap();
        let json = to_string_pretty(&*cache)?;

        // Reset the state changed flag
        let mut has_state_changed = self.has_state_changed.write().unwrap();
        *has_state_changed = false;

        Ok(json)
    }

    /// Deserialize the token cache from a string
    pub fn deserialize(&self, state: &str) -> Result<()> {
        let parsed_cache: HashMap<CredentialType, HashMap<String, TokenCacheEntry>> =
            from_str(state)?;

        let mut cache = self.cache.write().unwrap();
        *cache = parsed_cache;

        // Reset the state changed flag
        let mut has_state_changed = self.has_state_changed.write().unwrap();
        *has_state_changed = false;

        Ok(())
    }

    // Save before exit. This is used by FilePersistentTokenCache when it is dropped.
    pub fn save_on_exit(&self) -> Result<()> {
        // No-op for base MsalTokenCache since it doesn't persist to any storage
        // This method exists to satisfy the Drop implementation in FilePersistentTokenCache
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
        Self::new()
    }
}

/// A helper struct that manages persistence of an MsalTokenCache to a file
pub struct FilePersistentTokenCache {
    cache: MsalTokenCache,
    cache_path: PathBuf,
}

impl FilePersistentTokenCache {
    pub fn new<P: AsRef<std::path::Path>>(cache_path: P) -> Result<Self> {
        let cache_path = cache_path.as_ref().to_path_buf();
        let cache = MsalTokenCache::new();

        // Load existing cache if available
        if cache_path.exists() {
            let cached_data = fs::read_to_string(&cache_path)?;
            cache.deserialize(&cached_data)?;
        }

        Ok(Self { cache, cache_path })
    }
    /// Save the cache to file if it has changed
    pub fn save_if_changed(&self) -> Result<()> {
        if self.cache.has_state_changed() {
            let serialized = self.cache.serialize()?;

            // Make sure directory exists
            if let Some(parent) = self.cache_path.parent() {
                if !parent.exists() {
                    fs::create_dir_all(parent)?;
                }
            }

            fs::write(&self.cache_path, serialized)?;
        }
        Ok(())
    }

    /// Save before exit.
    pub fn save_on_exit(&self) -> Result<()> {
        self.save_if_changed()
    }

    /// Get the underlying cache
    pub fn cache(&self) -> &MsalTokenCache {
        &self.cache
    }
}

impl Drop for FilePersistentTokenCache {
    fn drop(&mut self) {
        // Save the cache to file when the struct is dropped, but only if changed
        if self.cache.has_state_changed() {
            match self.save_if_changed() {
                Ok(_) => trace!("Successfully saved token cache on exit"),
                Err(e) => error!("Failed to save token cache on exit: {}", e),
            }
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_access_token_cache() -> Result<()> {
        let cache = MsalTokenCache::new();

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

        cache.add_access_token(token.clone())?;

        // Check if it's retrievable
        let scopes = &["scope1", "scope2"];
        let retrieved = cache.get_access_token(
            Some(home_account_id),
            Some(environment),
            Some(client_id),
            Some(tenant_id),
            Some(scopes),
        );

        assert!(retrieved.is_some());
        let retrieved = retrieved.unwrap();
        assert_eq!(retrieved.secret.secret(), "token_value");

        // Test removal
        cache.remove_access_token(
            Some(home_account_id),
            Some(environment),
            Some(client_id),
            Some(tenant_id),
            Some(scopes),
        )?;

        let retrieved = cache.get_access_token(
            Some(home_account_id),
            Some(environment),
            Some(client_id),
            Some(tenant_id),
            Some(scopes),
        );

        assert!(retrieved.is_none());

        Ok(())
    }

    #[test]
    fn test_serialization() -> Result<()> {
        let cache = MsalTokenCache::new();

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

        cache.add_access_token(token.clone())?;

        assert!(cache.has_state_changed());

        // Serialize
        let serialized = cache.serialize()?;

        // Check that serialization resets the state changed flag
        assert!(!cache.has_state_changed());

        // Clear the cache
        cache.clear()?;

        // Check that the token is gone
        let scopes = &["scope1", "scope2"];
        let retrieved = cache.get_access_token(
            Some(home_account_id),
            Some(environment),
            Some(client_id),
            Some(tenant_id),
            Some(scopes),
        );
        assert!(retrieved.is_none());

        // Deserialize
        cache.deserialize(&serialized)?;

        // Check that the token is back
        let retrieved = cache.get_access_token(
            Some(home_account_id),
            Some(environment),
            Some(client_id),
            Some(tenant_id),
            Some(scopes),
        );

        assert!(retrieved.is_some());
        let retrieved = retrieved.unwrap();
        assert_eq!(retrieved.secret.secret(), "token_value");

        Ok(())
    }

    #[test]
    fn test_file_based_token_cache() -> Result<()> {
        use std::env;
        use std::fs;
        use std::path::PathBuf;

        // Set up home directory based cache path
        let home_dir = env::var("HOME")
            .or_else(|_| env::var("USERPROFILE"))
            .unwrap_or_else(|_| std::env::temp_dir().to_string_lossy().to_string());
        let cirrus_dir = PathBuf::from(&home_dir).join(".cirrus");
        if !cirrus_dir.exists() {
            fs::create_dir_all(&cirrus_dir)?;
        }
        let cache_path = cirrus_dir.join("msal_test_cache2.json");
        let cache_path_str = cache_path.to_string_lossy().to_string();


        // Add a token to the cache
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
            secret: Secret::new("file_cache_token"),
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
        }; // Create a FilePersistentTokenCache - this will load from the file if it exists
        let persistent_cache = FilePersistentTokenCache::new(&cache_path)?;

        // Add the token to the cache
        persistent_cache.cache().add_access_token(token.clone())?;

        // The cache state is now changed, and will be written when the FilePersistentTokenCache is dropped
        println!("Token cache will be written to: {}", cache_path_str); // Force the persistent_cache to be dropped, which should write to file due to the Drop implementation
        drop(persistent_cache);

        // Create a new FilePersistentTokenCache to test loading from file
        let new_persistent_cache = FilePersistentTokenCache::new(&cache_path)?;

        // Verify the token was loaded correctly from the file
        let scopes = &["scope1", "scope2"];
        let retrieved = new_persistent_cache.cache().get_access_token(
            Some(home_account_id),
            Some(environment),
            Some(client_id),
            Some(tenant_id),
            Some(scopes),
        );

        assert!(
            retrieved.is_some(),
            "Token should be retrieved from FilePersistentTokenCache"
        );
        let retrieved = retrieved.unwrap();
        assert_eq!(retrieved.secret.secret(), "file_cache_token");

        // Test save if changed
        let new_token = AccessTokenEntry {
            credential_type: CredentialType::AccessToken,
            secret: Secret::new("new_token_value"),
            home_account_id: Some(home_account_id.to_string()),
            environment: Some(environment.to_string()),
            client_id: Some(client_id.to_string()),
            scopes: Some("new_scope".to_string()),
            tenant_id: Some(tenant_id.to_string()),
            token_type: Some("Bearer".to_string()),
            cached_at: now_secs.to_string(),
            expires_on: expires_on.to_string(),
            extended_expires_on: expires_on.to_string(),
            refresh_on: None,
        };
        new_persistent_cache.cache().add_access_token(new_token)?;

        // Instead of explicitly calling save_if_changed, we'll rely on the Drop implementation
        // to save when the cache is dropped
        drop(new_persistent_cache);

        // Create a new cache to verify it was persisted
        let reloaded_cache = FilePersistentTokenCache::new(&cache_path)?;
        let new_retrieved = reloaded_cache.cache().get_access_token(
            Some(home_account_id),
            Some(environment),
            Some(client_id),
            Some(tenant_id),
            Some(&["new_scope"]),
        );

        assert!(
            new_retrieved.is_some(),
            "New token should be retrieved from reloaded FilePersistentTokenCache"
        );
        let new_retrieved = new_retrieved.unwrap();
        assert_eq!(new_retrieved.secret.secret(), "new_token_value"); // No need to explicitly call save_on_exit since the Drop implementation handles it
                                                                      // We'll modify the cache to verify the Drop implementation works
        let final_token = AccessTokenEntry {
            credential_type: CredentialType::AccessToken,
            secret: Secret::new("final_token_value"),
            home_account_id: Some(home_account_id.to_string()),
            environment: Some(environment.to_string()),
            client_id: Some(client_id.to_string()),
            scopes: Some("final_scope".to_string()),
            tenant_id: Some(tenant_id.to_string()),
            token_type: Some("Bearer".to_string()),
            cached_at: now_secs.to_string(),
            expires_on: expires_on.to_string(),
            extended_expires_on: expires_on.to_string(),
            refresh_on: None,
        };

        reloaded_cache.cache().add_access_token(final_token)?;
        // Let reloaded_cache go out of scope, which should trigger the Drop implementation

        // // Clean up the test file
        // if cache_path.exists() {
        //     fs::remove_file(&cache_path)?;
        // }

        Ok(())
    }
}
