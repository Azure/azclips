use super::utils::{decode_part_id_token, format_token_expires_on, load_landing_page_templates};
use crate::error::{AuthenticationError, AzError, AzInternalError};
use azure_identity::interactive_credential::interactive_browser_credential::InteractiveBrowserCredential;
use oauth2::TokenResponse;
use serde::{Deserialize, Serialize};
use serde_json::to_string_pretty;
use std::fs::File;
use std::io::{Read, Write};

pub const AZURE_CLI_CLIENT_ID: &str = "04b07795-8ddb-461a-bbee-02f9e1bf7b46";

#[derive(Debug, Clone)]
pub struct UserCredential {
    username: Option<String>,
}

impl UserCredential {
    pub fn new(username: Option<String>) -> Self {
        UserCredential { username }
    }

    pub async fn acquire_token(
        &self,
        scopes: &[&str],
        tenant_id: &str,
    ) -> Result<Token, AuthenticationError> {
        let interactive_credentials = InteractiveBrowserCredential::new(
            Some(oauth2::ClientId::new(AZURE_CLI_CLIENT_ID.to_string())),
            Some(tenant_id.to_string()),
            None, // Use default redirect URL
        )
        .map_err(|e| {
            AuthenticationError::AuthenticationFailed(format!(
                "Failed to create interactive browser credential: {}",
                e
            ))
        })?;

        let (success_template, error_template) = load_landing_page_templates();

        if success_template.is_empty() || error_template.is_empty() {
            return Err(AuthenticationError::AuthenticationFailed(
                "Failed to load landing page templates".to_string(),
            ));
        }

        let token_response = interactive_credentials
            .get_token(
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

        let access_token = token_response.access_token().secret().to_string();

        let refresh_token = token_response
            .refresh_token()
            .map(|token| token.secret().to_string());

        let expires_in = token_response
            .expires_in()
            .unwrap_or(std::time::Duration::from_secs(0));
        let expires_on = format_token_expires_on(expires_in);

        let id_token = token_response.extra_fields().id_token.clone();

        let mut username = None;
        let mut tenant_id = None;
        let mut object_id = None;

        if let Some(token) = &id_token {
            let parts: Vec<&str> = token.split('.').collect();
            if parts.len() == 3 {
                // Decode the payload part (middle part) of the JWT
                match decode_part_id_token(parts[1], Some("utf-8")) {
                    Ok(payload_json) => {
                        match serde_json::from_str::<serde_json::Value>(&payload_json) {
                            Ok(payload_value) => {
                                username = payload_value
                                    .get("preferred_username")
                                    .and_then(|v| v.as_str())
                                    .map(|s| s.to_string());

                                tenant_id = payload_value
                                    .get("tid")
                                    .and_then(|v| v.as_str())
                                    .map(|s| s.to_string());

                                object_id = payload_value
                                    .get("oid")
                                    .and_then(|v| v.as_str())
                                    .map(|s| s.to_string());
                            }
                            Err(_) => {
                                return Err(AuthenticationError::InvalidIdToken(
                                    "Failed to parse ID token payload".to_string(),
                                ));
                            }
                        }
                    }
                    Err(_) => {
                        return Err(AuthenticationError::InvalidIdToken(
                            "Failed to decode ID token payload".to_string(),
                        ));
                    }
                }
            }
        }

        let token = Token {
            access_token,
            expires_on,
            refresh_token,
            id_token: id_token,
            tenant_id: tenant_id,
            user_object_id: object_id,
            user_preferred_username: username,
            scopes: Some(scopes.iter().map(|s| s.to_string()).collect()),
        };

        // Save the token to a file
        token.write_token_to_file().map_err(|e| {
            AuthenticationError::AuthenticationFailed(format!(
                "Failed to write token to file after token acquired for user credential: {}",
                e
            ))
        })?;

        Ok(token)
    }
}

#[derive(Debug, Clone)]
pub struct ServicePrincipalCredential {
    client_id: Option<String>,
    client_credential: Option<String>,
}

#[derive(Debug, Clone)]
pub struct ManagedIdentityCredential {
    client_id: Option<String>,
    resource_id: Option<String>,
    object_id: Option<String>,
}

#[derive(Debug, Clone, Default, Serialize, Deserialize)]
pub struct Token {
    // Todo: Adjust the fields with Azure SDK's TokenResponse struct
    pub access_token: String,
    pub expires_on: u64,
    pub refresh_token: Option<String>,
    pub id_token: Option<String>,
    pub tenant_id: Option<String>,
    pub user_object_id: Option<String>,
    pub user_preferred_username: Option<String>,
    pub scopes: Option<Vec<String>>,
}

impl Token {
    pub fn write_token_to_file(&self) -> Result<(), AzInternalError> {
        let json_string = to_string_pretty(&self)?;

        let home_dir = dirs::home_dir().ok_or_else(|| {
            AzInternalError::InvalidPathError("Could not determine home directory".to_string())
        })?;

        let token_dir = home_dir.join(".cirrus");
        std::fs::create_dir_all(&token_dir)?;

        let file_path = token_dir.join("azure_token.json");

        let mut file = File::create(&file_path)?;

        file.write_all(json_string.as_bytes())?;

        println!("Token saved to {}", file_path.display());

        Ok(())
    }

    pub fn read_token_from_file(&self) -> Result<Token, AzError> {
        let home_dir = dirs::home_dir().ok_or_else(|| {
            AzError::Internal(AzInternalError::InvalidPathError(
                "Could not determine home directory".to_string(),
            ))
        })?;

        let token_path = home_dir.join(".cirrus").join("azure_token.json");
        if !token_path.exists() {
            return Err(AzInternalError::InvalidPathError(format!(
                "No cached token found at {}",
                token_path.display()
            ))
            .into());
        }

        let mut file = File::open(&token_path).map_err(AzInternalError::Io)?;

        let mut contents = String::new();
        file.read_to_string(&mut contents)
            .map_err(AzInternalError::Io)?;

        let token: Token = serde_json::from_str(&contents).map_err(|e| AzInternalError::Json(e))?;

        // Check if token is expired
        let now = std::time::SystemTime::now()
            .duration_since(std::time::UNIX_EPOCH)
            .map_err(|_| AzError::GeneralError("Failed to get current time".to_string()))?
            .as_secs();

        if token.expires_on <= now {
            return Err(AzError::Authentication(AuthenticationError::TokenExpired));
        }
        if token.access_token.is_empty() {
            return Err(AzError::Authentication(
                AuthenticationError::InvalidAccessToken(
                    "No access token found in token file".to_string(),
                ),
            ));
        }

        Ok(token)
    }
}
