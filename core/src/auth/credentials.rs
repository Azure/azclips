use azure_identity::interactive_credential::interactive_browser_credential::InteractiveBrowserCredential;
use crate::error::AuthenticationError;
use oauth2::TokenResponse;
use crate::auth::util::format_token_expires_on;

pub const AZURE_CLI_CLIENT_ID: &str = "04b07795-8ddb-461a-bbee-02f9e1bf7b46";

#[derive(Debug, Clone)]
pub struct UserCredential {
    username: Option<String>,
}

#[derive(Debug, Clone)]
pub struct Token {
    // Todo: Adjust the fields with Azure SDK's TokenResponse struct
    pub access_token: String,
    pub expires_on: String,
    pub expires_on_readable: String,
    pub refresh_token: Option<String>,
    // token_type: String, // Token type (e.g., Bearer)
    // tenant_id: String,
    // subscription_id: Option<String>,
}

impl UserCredential {
    pub fn new(username: Option<String>) -> Self {
        UserCredential {
            username,
        }
    }

    pub async fn acquire_token(&self,
        scopes: Vec<String>,
        tenant_id: String,
    ) -> Result<Token, AuthenticationError> {
        
        let interactive_credentials = InteractiveBrowserCredential::new(
            Some(oauth2::ClientId::new(AZURE_CLI_CLIENT_ID.to_string())),
            Some(tenant_id), 
            None // Use default redirect URL
        ).map_err(|e| {
            AuthenticationError::AuthenticationFailed(format!("Failed to create interactive browser credential: {}", e))
        })?;

        let scopes_as_str: Vec<&str> = scopes.iter().map(|s| s.as_str()).collect();
        let token_response = interactive_credentials
            .get_token(
                Some(&scopes_as_str),
                Some("select_account"),
                self.username.as_deref()
            )
            .await
            .map_err(|e| {
                AuthenticationError::AuthenticationFailed(format!("Failed to acquire token: {}", e))
            })?;

        let access_token = token_response.access_token().secret().to_string();
        
        let refresh_token = token_response.refresh_token()
            .map(|token| token.secret().to_string());

        let expires_in = token_response.expires_in().unwrap_or(std::time::Duration::from_secs(0));
        let (expires_on, expires_on_readable) = format_token_expires_on(expires_in);
        
        Ok(Token {
            access_token,
            expires_on,
            expires_on_readable,
            refresh_token,
        })
    }
}