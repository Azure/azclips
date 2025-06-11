use super::utils::{get_default_token_cache_path, load_landing_page_templates};
use crate::error::{AuthenticationError};
use azure_identity::interactive_credential::interactive_browser_credential::InteractiveBrowserCredential;
use oauth2::{ClientId};
use std::path::PathBuf;
use azure_core::credentials::AccessToken;

pub const AZURE_CLI_CLIENT_ID: &str = "04b07795-8ddb-461a-bbee-02f9e1bf7b46";

#[derive(Debug, Clone)]
pub struct UserCredential {
    pub username: Option<String>,
    pub home_account_id: Option<String>,
    pub environment: Option<String>,
    pub tenant_id: Option<String>,
}

impl UserCredential {
    pub fn new(username: Option<String>, home_account_id: Option<String>, environment: Option<String>, tenant_id: Option<String>) -> Self {
        UserCredential { 
            username,
            home_account_id,
            environment,
            tenant_id,
        }
    }
    
    //Try to acquire a token silently from the cache without user interaction
    pub async fn acquire_token_silently(
        &self,
        scopes: &[&str],
        tenant_id: &str,
    ) -> Result<AccessToken, AuthenticationError> {
        let cache_path_str = get_default_token_cache_path().ok_or_else(|| {
            AuthenticationError::AuthenticationFailed("Failed to get token cache path".to_string())
        })?;
        
        let cache_path = PathBuf::from(cache_path_str);
        
        let credential = InteractiveBrowserCredential::new(
            Some(ClientId::new(AZURE_CLI_CLIENT_ID.to_string())),
            Some(tenant_id.to_string()),
            None, // Use default redirect URL
            self.home_account_id.clone(),
            self.environment.clone(),
            self.username.clone(),
            Some(cache_path),
        )
        .map_err(|e| {
            AuthenticationError::AuthenticationFailed(format!(
                "Failed to create interactive browser credential: {}",
                e
            ))
        })?;

        // Try to get token silently from the cache
        let token = credential
            .get_token_sliently(scopes)
            .await
            .map_err(|e| {
                AuthenticationError::AuthenticationFailed(format!(
                    "Failed to acquire token silently: {}",
                    e
                ))
            })?;
        
        Ok(token)
    }
    
    /// Attempt to acquire a token silently first, falling back to interactive authentication if necessary
    pub async fn acquire_token(
        &mut self,
        scopes: &[&str],
        tenant_id: &str,
    ) -> Result<AccessToken, AuthenticationError> {
        // Try silent authentication first
        match self.acquire_token_silently(scopes, tenant_id).await {
            Ok(token) => Ok(token),
            Err(_) => {
                // Fall back to interactive authentication if silent auth fails
                self.acquire_token_interactive(scopes, tenant_id).await
            }
        }
    }    
    
    pub async fn acquire_token_interactive(
        &mut self,
        scopes: &[&str],
        tenant_id: &str,
    ) -> Result<AccessToken, AuthenticationError> {
        let cache_path_str = get_default_token_cache_path().ok_or_else(|| {
            AuthenticationError::AuthenticationFailed("Failed to get token cache path".to_string())
        })?;
        
        let cache_path = PathBuf::from(cache_path_str);
        
        let mut interactive_credentials = InteractiveBrowserCredential::new(
            Some(ClientId::new(AZURE_CLI_CLIENT_ID.to_string())),
            Some(tenant_id.to_string()),
            None, // Use default redirect URL
            self.home_account_id.clone(),
            self.environment.clone(),
            self.username.clone(),
            Some(cache_path),
        )
        .map_err(|e| {
            AuthenticationError::AuthenticationFailed(format!(
                "Failed to create interactive browser credential: {}",
                e
            ))
        })?;

        let (success_template, error_template) = load_landing_page_templates();        if success_template.is_empty() || error_template.is_empty() {
            return Err(AuthenticationError::AuthenticationFailed(
                "Failed to load landing page templates".to_string(),
            ));
        }        
        
        let token_response = interactive_credentials
            .get_token_interactively(
                Some(scopes),
                Some("select_account"),
                self.username.as_deref(),
                Some(&success_template),
                Some(&error_template),
            )
            .await
            .map_err(|e| {
                AuthenticationError::AuthenticationFailed(format!(
                    "Failed to acquire token for user credential: {}",
                    e
                ))
            })?;
        
        //Copy the value of InteractiveBrowserCredential's home_account_id, username and environment to UserCredential
        self.home_account_id = interactive_credentials.home_account_id.clone();
        self.username = interactive_credentials.username.clone();
        self.environment = interactive_credentials.environment.clone();
        Ok(token_response)
    }
}

#[derive(Debug, Clone)]
pub struct ServicePrincipalCredential {
    client_id: Option<String>,
    client_credential: Option<String>,
}
