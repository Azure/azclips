use azure_identity::interactive_credential::interactive_browser_credential::InteractiveBrowserCredential;
use crate::error::{AuthenticationError, AzInternalError, AzError};
use oauth2::TokenResponse;
use crate::top::utils::{format_token_expires_on, load_landing_page_templates};
use serde_json::{json, Value, to_string_pretty};
use std::fs::File;
use std::io::{Read,Write};

pub const AZURE_CLI_CLIENT_ID: &str = "04b07795-8ddb-461a-bbee-02f9e1bf7b46";

#[derive(Debug, Clone)]
pub struct UserCredential {
    username: Option<String>,
}

impl UserCredential {
    pub fn new(username: Option<String>) -> Self {
        UserCredential {
            username,
        }
    }

    pub async fn acquire_token(&self,
        scopes: &[&str],
        tenant_id: &str,
    ) -> Result<Token, AuthenticationError> {
        
        let interactive_credentials = InteractiveBrowserCredential::new(
            Some(oauth2::ClientId::new(AZURE_CLI_CLIENT_ID.to_string())),
            Some(tenant_id.to_string()), 
            None // Use default redirect URL
        ).map_err(|e| {
            AuthenticationError::AuthenticationFailed(format!("Failed to create interactive browser credential: {}", e))
        })?;

        let (success_template, error_template) = load_landing_page_templates();

        if success_template.is_empty() || error_template.is_empty() {
            return Err(AuthenticationError::AuthenticationFailed("Failed to load landing page templates".to_string()));
        }
        
        let token_response = interactive_credentials
            .get_token(
                Some(&scopes),
                Some("select_account"),
                self.username.as_deref(),
                Some(&success_template),
                Some(&error_template),
            )
            .await
            .map_err(|e| {
                AuthenticationError::AuthenticationFailed(format!("Failed to acquire token for user credential: {}", e))
            })?;

        let access_token = token_response.access_token().secret().to_string();
        
        let refresh_token = token_response.refresh_token()
            .map(|token| token.secret().to_string());

        let expires_in = token_response.expires_in().unwrap_or(std::time::Duration::from_secs(0));
        let expires_on = format_token_expires_on(expires_in);
        
        let token = Token {
            access_token,
            expires_on,
            refresh_token,
        };

        // Save the token to a file
        token.write_token_to_file().map_err(|e| {
            AuthenticationError::AuthenticationFailed(format!("Failed to write token to file after token acquired for user credential: {}", e))
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

#[derive(Debug, Clone, Default)]
pub struct Token {
    // Todo: Adjust the fields with Azure SDK's TokenResponse struct
    pub access_token: String,
    pub expires_on: u64,
    pub refresh_token: Option<String>,
}

impl Token {
    pub fn write_token_to_file(&self) -> Result<(), AzInternalError> {
        //TODO: Store token in cache
        //TODO: Store access token for different scopes, tenants, and users 
        let token_data = json!({
            "access_token": self.access_token,
            "expires_on": self.expires_on,
            "refresh_token": self.refresh_token,
        });
        
        let json_string = to_string_pretty(&token_data)?;
        
        let home_dir = dirs::home_dir()
            .ok_or_else(|| AzInternalError::InvalidPathError("Could not determine home directory".to_string()))?;
        
        let token_dir = home_dir.join(".cirrus");
        std::fs::create_dir_all(&token_dir)?;
        
        let file_path = token_dir.join("azure_token.json");
        
        let mut file = File::create(&file_path)?;
        
        file.write_all(json_string.as_bytes())?;
        
        println!("Token saved to {}", file_path.display());
        
        Ok(())
    }

    pub fn read_token_from_file(&self) -> Result<Token, AzError> {
        let home_dir = dirs::home_dir()
            .ok_or_else(|| AzError::Internal(AzInternalError::InvalidPathError("Could not determine home directory".to_string())))?;
        
        let token_path = home_dir.join(".cirrus").join("azure_token.json");
        if !token_path.exists() {
            return Err(AzInternalError::InvalidPathError(
                format!("No cached token found at {}", token_path.display())
            ).into());
        }
        
        let mut file = File::open(&token_path)
            .map_err(|e| AzInternalError::Io(e))?;
        
        let mut contents = String::new();
        file.read_to_string(&mut contents)
            .map_err(|e| AzInternalError::Io(e))?;
        
        let token_data: Value = serde_json::from_str(&contents)
            .map_err(|e| AzInternalError::Json(e))?;
        
        let access_token = token_data.get("access_token")
            .and_then(|t| t.as_str())
            .ok_or_else(|| AuthenticationError::InvalidToken("No access token found in token file".to_string()))?
            .to_string();
        
        let expires_on = token_data.get("expires_on")
            .and_then(|e| e.as_u64())
            .ok_or_else(|| AuthenticationError::InvalidToken("No expiration time found in token file or invalid format".to_string()))?;
        
        let refresh_token = token_data.get("refresh_token")
            .and_then(|t| t.as_str())
            .map(|s| s.to_string());
        
        // Check if token is expired
        let now = std::time::SystemTime::now()
            .duration_since(std::time::UNIX_EPOCH)
            .map_err(|_| AzError::GeneralError("Failed to get current time".to_string()))?
            .as_secs();
        
            if expires_on <= now {
                return Err(AzError::Authentication(AuthenticationError::TokenExpired));
            }
        
        Ok(Token {
            access_token,
            refresh_token,
            expires_on,
        })
    }
}