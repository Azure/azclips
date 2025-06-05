use crate::account::credentials::Token;
use crate::error::{AzError, AzInternalError};
use azure_core::credentials::{AccessToken, Secret};
use azure_identity::msal_token_cache::{
    AccessTokenEntry, CredentialType, MsalTokenCache, RefreshTokenEntry,
};
use std::fs::{self, File};
use std::io::{Read, Write};
use std::path::Path;
use std::time::{Duration, SystemTime, UNIX_EPOCH};
use time::OffsetDateTime;

/// Default location for the MSAL token cache file
pub fn get_default_msal_cache_path() -> Result<std::path::PathBuf, AzInternalError> {
    let home_dir = dirs::home_dir().ok_or_else(|| {
        AzInternalError::InvalidPathError("Could not determine home directory".to_string())
    })?;

    let token_dir = home_dir.join(".cirrus");
    Ok(token_dir.join("msal_token_cache.json"))
}

/// Convert a Token to AccessTokenEntry and RefreshTokenEntry
pub fn token_to_msal_entries(token: &Token) -> (AccessTokenEntry, Option<RefreshTokenEntry>) {
    let now = SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .unwrap_or_default();

    let now_secs = now.as_secs();
    let expires_on = token.expires_on;
    let expires_on_str = expires_on.to_string();
    let extended_expires_on = expires_on + 3600; // add an hour as extended expiry by default

    // Create the AccessTokenEntry
    let access_token_entry = AccessTokenEntry {
        credential_type: CredentialType::AccessToken,
        secret: Secret::new(token.access_token.clone()),
        home_account_id: token.user_object_id.clone(),
        environment: Some("login.microsoftonline.com".to_string()), // Default environment
        client_id: None, // You may want to add client_id to your Token struct
        scopes: token.scopes.as_ref().map(|s| s.join(" ")),
        tenant_id: token.tenant_id.clone(),
        token_type: Some("Bearer".to_string()),
        cached_at: now_secs.to_string(),
        expires_on: expires_on_str,
        extended_expires_on: extended_expires_on.to_string(),
        refresh_on: None,
    };

    // Create the RefreshTokenEntry if refresh_token exists
    let refresh_token_entry = token.refresh_token.as_ref().map(|refresh_token| {
        RefreshTokenEntry {
            credential_type: CredentialType::RefreshToken,
            secret: Secret::new(refresh_token.clone()),
            home_account_id: token.user_object_id.clone(),
            environment: Some("login.microsoftonline.com".to_string()),
            client_id: None, // Add client_id to your Token struct
            last_modification_time: Some(now_secs.to_string()),
        }
    });

    (access_token_entry, refresh_token_entry)
}

/// Convert an AccessTokenEntry to Token
pub fn access_token_entry_to_token(entry: &AccessTokenEntry) -> Token {
    let expires_on = entry.expires_on.parse::<u64>().unwrap_or_default();

    Token {
        access_token: entry.secret.secret().to_string(),
        expires_on,
        refresh_token: None, // Will be added later if a refresh token is available
        id_token: None,      // MSAL doesn't store ID tokens in the same way
        tenant_id: entry.tenant_id.clone(),
        user_object_id: entry.home_account_id.clone(),
        user_preferred_username: None, // Not stored in MSAL tokens
        scopes: entry
            .scopes
            .as_ref()
            .map(|s| s.split(' ').map(String::from).collect()),
    }
}

/// Save a Token to the MSAL token cache
pub async fn save_token_to_msal_cache(
    token: &Token,
    cache_path: Option<&Path>,
) -> Result<(), AzInternalError> {
    let cache = MsalTokenCache::new();

    // Load existing cache if it exists
    if let Some(path) = cache_path {
        if path.exists() {
            let mut file = File::open(path)?;
            let mut content = String::new();
            file.read_to_string(&mut content)?;
            cache.deserialize(&content).await?;
        }
    }

    // Convert Token to MSAL entries
    let (access_token_entry, refresh_token_entry) = token_to_msal_entries(token);

    // Add entries to cache
    cache.add_access_token(access_token_entry).await?;
    if let Some(refresh_entry) = refresh_token_entry {
        cache.add_refresh_token(refresh_entry).await?;
    }

    // Serialize and save cache
    let serialized_cache = cache.serialize().await?;

    if let Some(path) = cache_path {
        if let Some(parent) = path.parent() {
            fs::create_dir_all(parent)?;
        }
        let mut file = File::create(path)?;
        file.write_all(serialized_cache.as_bytes())?;
    }

    Ok(())
}

/// Load a Token from the MSAL token cache
pub async fn load_token_from_msal_cache(
    scopes: &[&str],
    client_id: Option<&str>,
    tenant_id: Option<&str>,
    cache_path: Option<&Path>,
) -> Result<Token, AzError> {
    let cache = MsalTokenCache::new();

    // Load cache from file if it exists
    if let Some(path) = cache_path {
        if path.exists() {
            let mut file = File::open(path).map_err(AzInternalError::Io)?;
            let mut content = String::new();
            file.read_to_string(&mut content)
                .map_err(AzInternalError::Io)?;
            cache
                .deserialize(&content)
                .await
                .map_err(|e| AzInternalError::Other(e.to_string()))?;
        } else {
            return Err(AzInternalError::InvalidPathError(format!(
                "No cached token found at {}",
                path.display()
            ))
            .into());
        }
    }

    // Try to get the token from the cache
    let result = cache
        .get_token(scopes, None, None, client_id, tenant_id)
        .await;

    match result {
        Ok(access_token) => {
            // Convert access token to Token
            let now = SystemTime::now()
                .duration_since(UNIX_EPOCH)
                .map_err(|_| AzError::GeneralError("Failed to get current time".to_string()))?
                .as_secs();

            let expires_on = access_token
                .expires_on
                .unix_timestamp()
                .try_into()
                .map_err(|_| AzError::GeneralError("Failed to convert expires time".to_string()))?;

            // Check if token is expired
            if expires_on <= now {
                return Err(AzError::Authentication(
                    crate::error::AuthenticationError::TokenExpired,
                ));
            }

            Ok(Token {
                access_token: access_token.token.secret().to_string(),
                expires_on,
                refresh_token: None, // We don't have access to the refresh token directly
                id_token: None,      // We don't have access to the ID token directly
                tenant_id: tenant_id.map(String::from),
                user_object_id: None, // We don't have this information directly
                user_preferred_username: None, // We don't have this information directly
                scopes: Some(scopes.iter().map(|&s| s.to_string()).collect()),
            })
        }
        Err(err) => Err(AzError::Authentication(
            crate::error::AuthenticationError::AuthenticationFailed(format!(
                "Failed to retrieve token from MSAL cache: {}",
                err
            )),
        )),
    }
}
