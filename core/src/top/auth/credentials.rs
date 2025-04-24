use azure_identity::interactive_credential::interactive_browser_credential::InteractiveBrowserCredential;
use crate::error::AuthenticationError;
use oauth2::TokenResponse; // Import the trait to access refresh_token

pub const AZURE_CLI_CLIENT_ID: &str = "04b07795-8ddb-461a-bbee-02f9e1bf7b46";

#[derive(Debug, Clone)]
pub struct UserCredential {
    username: Option<String>,
}

#[derive(Debug, Clone)]
pub struct Token {
    pub access_token: String,
    pub expires_on: String,
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

    pub async fn acquire_token(&self) -> Result<Token, AuthenticationError> {
        // Initialize the Interactive Browser Credential with appropriate parameters
        let interactive_credentials = InteractiveBrowserCredential::new(
            Some(oauth2::ClientId::new(AZURE_CLI_CLIENT_ID.to_string())),
            Some("organizations".to_string()), // Using organizations tenant for multi-tenant support
            None // Use default redirect URL
        ).map_err(|e| {
            AuthenticationError::AuthenticationFailed(format!("Failed to create interactive browser credential: {}", e))
        })?;

        // Acquire the token with select_account prompt behavior to allow user to choose an account
        let token_response = interactive_credentials
            .get_token(
                Some(&["https://management.azure.com/.default"]),
                Some("select_account") // Allow user to select which account to use
            )
            .await
            .map_err(|e| {
                AuthenticationError::AuthenticationFailed(format!("Failed to acquire token: {}", e))
            })?;

        // Extract refresh token if available for future silent authentication
        let refresh_token = token_response.refresh_token()
            .map(|token| token.secret().to_string());

        // Get expiration information
        let expires_in = token_response.expires_in().unwrap_or(std::time::Duration::from_secs(0));
        let now = std::time::SystemTime::now();
        let expires_at = now.checked_add(expires_in).unwrap_or(now);

        // Convert to a formatted string (if human-readable format is needed)
        let expires_on = match expires_at.duration_since(std::time::UNIX_EPOCH) {
            Ok(duration) => duration.as_secs().to_string(),
            Err(_) => "unknown".to_string(),
        };

        // Return the token
        Ok(Token {
            access_token: token_response.access_token().secret().to_string(),
            expires_on,
            refresh_token,
        })
    }
}