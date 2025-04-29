// This module defines the Profile structures for managing authentication

use crate::error::AuthenticationError;
use crate::auth::util::can_launch_browser;
use crate::auth::credentials::UserCredential;
use crate::auth::credentials::Token;

// Todo: Define Azure cloud host endpoints, not support by azure_identity yet
// pub const AZURE_US_GOVERNMENT: &str = "login.microsoftonline.us";
// pub const AZURE_CHINA: &str = "login.chinacloudapi.cn";
// pub const AZURE_PUBLIC: &str = "login.microsoftonline.com";

// Default cloud endpoint
// Todo: Move this part to cloud.rs, and implement the cloud management logic
pub const WORLD_WIDE: &str = "login.microsoftonline.com";


pub struct Profile {
    authority_url: String, /* It should be of the format `https://login.microsoftonline.com/<tenant_id>`
    By default, we will use `https://login.microsoftonline.com/organizations` 
    Todo: Modify the API for azure identity to use different authority urls, will be finished when implementing soverign cloud support */
    arm_scope: String, //Todo: For the `az cloud register` param: endpoint-active-directory-resource-id: The resource ID to obtain AD token for. Users can use this to specify a different custom ARM endpoint for the cloud. e.g. For the public Azure cloud, it might be https://management.core.windows.net/; for Azure China cloud, it would be:https://management.core.chinacloudapi.cn/
}

impl Profile {
    pub fn new(authority_url: Option<String>, arm_scope: Option<String>) -> Self {
        Profile {
            authority_url: authority_url.unwrap_or_else(|| WORLD_WIDE.to_string()),
            arm_scope: arm_scope.unwrap_or_else(|| "https://management.core.windows.net/.default".to_string()),
        }
    }

    pub async fn login(&mut self,
        interactive: bool,
        use_device_code: Option<bool>,
        tenant_id: Option<String>,
        scopes: Option<Vec<String>>,
    ) -> Result<(), AuthenticationError> {
        let mut use_device_code = use_device_code.unwrap_or(false);
        
        let scopes_vec: Vec<String> = match &scopes {
            Some(scope_strings) => scope_strings.iter().map(|s| s.clone()).collect(),
            None => vec![self.arm_scope.clone()],
        };
        
        let tenant_id = tenant_id.unwrap_or_else(|| "organizations".to_string());
        
        if interactive {
            if !use_device_code && !can_launch_browser() {
                println!("No web browser is available. Falling back to device code.");
                use_device_code = true;
            }
            if use_device_code {
                self.login_with_device_code()?;
            } else {
                self.login_with_auth_code(scopes_vec, tenant_id).await?;
            }
        } else {
            // Todo: Implement non-interactive login logic
            println!("Logging in non-interactively...");
        }
        
        Ok(())
    }

    async fn login_with_auth_code(&mut self, 
        scopes: Vec<String>,
        tenant_id: String,
    ) -> Result<(), AuthenticationError> {
        println!("Logging in interactively...");
        
        let user_credential = UserCredential::new(None);
        
        let token_result = user_credential.acquire_token(scopes, tenant_id).await;
        
        match &token_result {
            Ok(token) => {
                println!("Token acquired successfully. Token expires on: {}\n", token.expires_on_readable);
                
                if let Err(e) = self.save_token_to_file(token.clone()) {
                    eprintln!("Warning: Failed to save token to file: {}", e);
                }
                
                Ok(())
            },
            Err(e) => {
                //Todo: Improve the error handling
                eprintln!("Authentication failed: {}", e);
                Err(AuthenticationError::AuthenticationFailed(format!(
                    "Authentication failed with Auth Code: {}", e
                )))
            }
        }
    }

    // Todo: Store token cache instead of plaintext
    fn save_token_to_file(&self, token: Token) -> Result<(), String> {
        use std::fs::File;
        use std::io::Write;
        use serde_json::{json, to_string_pretty};
        
        let token_data = json!({
            "access_token": token.access_token,
            "expires_on": token.expires_on,
            "expires_on_readable": token.expires_on_readable,
            "refresh_token": token.refresh_token,
        });
        
        let json_string = to_string_pretty(&token_data)
            .map_err(|e| format!("Failed to serialize token data: {}", e))?;
        
        let home_dir = dirs::home_dir()
            .ok_or_else(|| "Could not determine home directory".to_string())?;
        
        let token_dir = home_dir.join(".cirrus");
        std::fs::create_dir_all(&token_dir)
            .map_err(|e| format!("Failed to create token directory: {}", e))?;
        let file_path = token_dir.join("azure_token.json");
        

        let mut file = File::create(&file_path)
            .map_err(|e| format!("Failed to create token file: {}", e))?;
        
        file.write_all(json_string.as_bytes())
            .map_err(|e| format!("Failed to write token data to file: {}", e))?;
        
        println!("Token saved to {}", file_path.display());
        
        Ok(())
    }

    fn login_with_device_code(&mut self) -> Result<(), AuthenticationError> {
        println!("Logging in with device code...");
        Ok(())
    }

    pub fn logout(&mut self) -> Result<(), AuthenticationError> {
        // Todo: Implement logout logic
        println!("Logging out...");
        Ok(())
    }

    async fn fake_get_subscriptions(&self, access_token: String) -> Result<Vec<String>, &'static str> {
        use reqwest::{Client, Url};
        use serde_json::Value;

        let url = match Url::parse("https://management.azure.com/subscriptions?api-version=2022-12-01") {
            Ok(url) => url,
            Err(_) => return Err("Failed to parse URL"),
        };

        let response = match Client::new()
            .get(url)
            .header("Authorization", format!("Bearer {}", access_token))
            .send()
            .await {
                Ok(response) => response,
                Err(_) => return Err("Failed to send request to Azure API"),
            };

        if !response.status().is_success() {
            return Err("API request failed with non-success status code");
        }

        let json_text = match response.text().await {
            Ok(text) => text,
            Err(_) => return Err("Failed to get response text"),
        };

        let json: Value = match serde_json::from_str(&json_text) {
            Ok(json) => json,
            Err(_) => return Err("Failed to parse API response"),
        };

        let subscriptions = match json.get("value") {
            Some(value) => {
                if let Some(array) = value.as_array() {
                    let mut subscription_ids = Vec::new();
                    for item in array {
                        if let Some(subscription_id) = item.get("subscriptionId").and_then(|id| id.as_str()) {
                            subscription_ids.push(subscription_id.to_string());
                        }
                    }
                    subscription_ids
                } else {
                    return Err("API response format invalid: 'value' is not an array");
                }
            },
            None => return Err("API response format invalid: missing 'value' field"),
        };

        println!("Found {} subscriptions", subscriptions.len());
        Ok(subscriptions)
    }

    pub async fn fake_list_subscriptions(&self, access_token: String) -> Result<(), &'static str> {
        let subscriptions = self.fake_get_subscriptions(access_token).await?;
        
        if subscriptions.is_empty() {
            println!("No subscriptions found.");
        } else {
            println!("Available subscriptions:");
            for (i, subscription_id) in subscriptions.iter().enumerate() {
                println!("  {}. {}", i+1, subscription_id);
            }
        }
        
        Ok(())
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::fs::File;
    use std::io::Read;
    use serde_json::Value;

    #[tokio::test]
    async fn test_login_logout() {
        // Don't run this test in CI, as it requires a browser to be available
        let mut profile = Profile::new(
            None,
            None
        );
        
        assert!(profile.login(true, None, None, None).await.is_ok());
        assert!(profile.logout().is_ok());
        assert!(profile.login(false, None, None, None).await.is_ok());
        assert!(profile.logout().is_ok());
    }

    #[tokio::test]
    async fn test_list_subscriptions_from_cached_token() {
        let home_dir = match dirs::home_dir() {
            Some(dir) => dir,
            None => {
                println!("Could not determine home directory, skipping test");
                return;
            }
        };
        
        let token_path = home_dir.join(".cirrus").join("azure_token.json");
        if !token_path.exists() {
            println!("No cached token found at {}, skipping test", token_path.display());
            return;
        }
        
        let mut file = match File::open(&token_path) {
            Ok(file) => file,
            Err(e) => {
                println!("Could not open token file: {}, skipping test", e);
                return;
            }
        };
        
        let mut contents = String::new();
        if let Err(e) = file.read_to_string(&mut contents) {
            println!("Could not read token file: {}, skipping test", e);
            return;
        }
        
        let token_data: Value = match serde_json::from_str(&contents) {
            Ok(data) => data,
            Err(e) => {
                println!("Could not parse token file: {}, skipping test", e);
                return;
            }
        };
        
        let access_token = match token_data.get("access_token").and_then(|t| t.as_str()) {
            Some(token) => token.to_string(),
            None => {
                println!("No access token found in token file, skipping test");
                return;
            }
        };
        
        if let Some(expires_on) = token_data.get("expires_on").and_then(|e| e.as_u64()) {
            let now = std::time::SystemTime::now()
                .duration_since(std::time::UNIX_EPOCH)
                .unwrap_or_default()
                .as_secs();
                
            if expires_on <= now {
                println!("Token is expired, skipping test");
                return;
            }
        }
        
        let profile = Profile::new(None, None);
        
        println!("Attempting to list subscriptions with cached token...");
        match profile.fake_list_subscriptions(access_token).await {
            Ok(_) => println!("Successfully listed subscriptions"),
            Err(e) => println!("Failed to list subscriptions: {}", e),
        }
    }
}