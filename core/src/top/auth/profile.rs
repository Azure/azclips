// This module defines the Profile structures for managing authentication

use crate::top::auth::util::can_launch_browser;
use crate::top::auth::credentials::UserCredential;

// Todo: Define Azure cloud host endpoints, not support by azure_identity yet
// pub const AZURE_US_GOVERNMENT: &str = "login.microsoftonline.us";
// pub const AZURE_CHINA: &str = "login.chinacloudapi.cn";
// pub const AZURE_PUBLIC: &str = "login.microsoftonline.com";

// Default cloud endpoint
pub const WORLD_WIDE: &str = "login.microsoftonline.com";

pub struct Authority {
    authority_url: String, /* It should be of the format `https://login.microsoftonline.com/<tenant_id>`
    By default, we will use `https://login.microsoftonline.com/organizations` */
    // More fields will be added later
}

impl Authority {
    pub fn new(host: Option<String>, tenant_id: Option<String>) -> Self {
        let host = host.unwrap_or_else(|| WORLD_WIDE.to_string());
        let tenant = tenant_id.unwrap_or_else(|| "organizations".to_string());
        
        Authority {
            authority_url: format!("{}/{}", host, tenant),
        }
    }
}

pub struct Profile {
    authority: Authority, //Todo: Modify the API for azure identity to use different authority urls, will be finished when implementing soverign cloud support
    arm_scope: String, //Todo: For the `az cloud register` param: endpoint-active-directory-resource-id: The resource ID to obtain AD token for. Users can use this to specify a different custom ARM endpoint for the cloud. e.g. For the public Azure cloud, it might be https://management.core.windows.net/; for Azure China cloud, it would be:https://management.core.chinacloudapi.cn/
}

impl Profile {
    pub fn new(authority: Authority, arm_scope: Option<String>) -> Self {
        Profile {
            authority,
            arm_scope: arm_scope.unwrap_or_else(|| "https://management.core.windows.net/.default".to_string()),
        }
    }

    pub async fn login(&mut self,
        interactive: bool,
        use_device_code: Option<bool>,
        tenant_id: Option<String>,
        scopes: Option<Vec<String>>,
    ) -> Result<(), &'static str> {
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
    ) -> Result<(), &'static str> {
        println!("Logging in interactively...");
        
        let user_credential = UserCredential::new(None);
        
        let token_result = user_credential.acquire_token(scopes, tenant_id).await;

        
        
        match &token_result {
            Ok(token) => {
                println!("Token acquired successfully. Token expires on: {}\n", token.expires_on_readable);
                Ok(())
            },
            Err(e) => {
                eprintln!("Authentication failed: {}", e);
                Err("Failed to acquire authentication token")
            }
        }
    }

    fn login_with_device_code(&mut self) -> Result<(), &'static str> {
        println!("Logging in with device code...");
        Ok(())
    }

    pub fn logout(&mut self) -> Result<(), &'static str> {
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

    #[tokio::test]
    async fn test_login_logout() {
        // Don't run this test in CI, as it requires a browser to be available
        let authority = Authority::new(
            None,
            None
        );
        
        let mut profile = Profile::new(
            authority,
            None
        );
        
        assert!(profile.login(true, None, None, None).await.is_ok());
        assert!(profile.logout().is_ok());
        assert!(profile.login(false, None, None, None).await.is_ok());
        assert!(profile.logout().is_ok());
    }
    
    #[test]
    fn test_authority_new() {
        let default_authority = Authority::new(None, None);
        assert_eq!(default_authority.authority_url, format!("{}/{}", WORLD_WIDE, "organizations"));
        
        let custom_host = "custom.host.example.com".to_string();
        let custom_tenant = "custom-tenant".to_string();
        let custom_authority = Authority::new(
            Some(custom_host.clone()),
            Some(custom_tenant.clone())
        );
        assert_eq!(custom_authority.authority_url, format!("{}/{}", custom_host, custom_tenant));
    }
}