// This module defines the Profile structures for managing authentication

use crate::top::auth::util::can_launch_browser;

// Define Azure cloud host endpoints, not support by azure_identity yet
// pub const AZURE_US_GOVERNMENT: &str = "login.microsoftonline.us";
// pub const AZURE_CHINA: &str = "login.chinacloudapi.cn";
// pub const AZURE_PUBLIC: &str = "login.microsoftonline.com";
// Default cloud endpoint
pub const WORLD_WIDE: &str = "login.microsoftonline.com";

pub struct Authority {
    authority_url: String, /* It should be of the format `https://login.microsoftonline.com/<tenant_id>`
    By default, we will use `https://login.microsoftonline.com/common` */
    // More fields will be added later
}

impl Authority {
    pub fn new(host: Option<String>, tenant_id: Option<String>) -> Self {
        let host = host.unwrap_or_else(|| WORLD_WIDE.to_string());
        let tenant = tenant_id.unwrap_or_else(|| "common".to_string());
        
        Authority {
            authority_url: format!("{}/{}", host, tenant),
        }
    }
}

pub struct Profile {
    authority: Authority, 
    arm_scope: String,
}

impl Profile {
    pub fn new(authority: Authority, arm_scope: Option<String>) -> Self {
        Profile {
            authority,
            arm_scope: arm_scope.unwrap_or_else(|| "https://management.azure.com/.default".to_string()),
        }
    }

    pub async fn login(&mut self,
        interactive: bool,
        use_device_code: Option<bool>,
    ) -> Result<(), &'static str> {
        let mut use_device_code = use_device_code.unwrap_or(false);
        
        if interactive {
            if !use_device_code && !can_launch_browser() {
                println!("No web browser is available. Falling back to device code.");
                use_device_code = true;
            }
            if use_device_code {
                self.login_with_device_code()?;
            } else {
                self.login_with_auth_code().await?;
            }
        } else {
            // Todo: Implement non-interactive login logic
            println!("Logging in non-interactively...");
            // Return error until non-interactive login is implemented
            return Err("Non-interactive login not implemented yet");
        }
        
        Ok(())
    }

    async fn login_with_auth_code(&mut self) -> Result<(), &'static str> {
        println!("Logging in interactively...");
        
        let username = std::env::var("USERNAME").unwrap_or_else(|_| "user".to_string());
        let user_credential = crate::top::auth::credentials::UserCredential::new(Some(username));
        
        // No block_on needed, just await
        let token_result = user_credential.acquire_token().await;
        
        match token_result {
            Ok(token) => {
                println!("Successfully authenticated. Token expires on: {}", token.expires_on);
                // Store the token for later use, possibly in self or some other storage
                // self.token = Some(token);  // You would need to add a token field to Profile
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
}

#[cfg(test)]
mod tests {
    use super::*;

    // Use tokio's async test framework
    #[tokio::test]
    async fn test_login_logout() {
        // Create Authority using the new method
        let authority = Authority::new(
            Some("https://authority.example.com".to_string()),
            Some("test-tenant".to_string())
        );
        
        // Create Profile using the new method with optional arm_scope
        let mut profile = Profile::new(
            authority,
            Some("https://management.azure.com/.default".to_string())
        );
        
        // Test login (both interactive and non-interactive)
        // Now we can await the async function
        assert!(profile.login(true, None).await.is_ok());
        
        // For thorough testing, we could reset and try non-interactive mode
        assert!(profile.logout().is_ok());
        assert!(profile.login(false, None).await.is_ok());
        
        // Test logout
        assert!(profile.logout().is_ok());
    }
    
    // This test doesn't involve async code, so it can remain as is
    #[test]
    fn test_authority_new() {
        // Test with default values
        let default_authority = Authority::new(None, None);
        assert_eq!(default_authority.authority_url, format!("{}/{}", WORLD_WIDE, "common"));
        
        // Test with custom values
        let custom_host = "custom.host.example.com".to_string();
        let custom_tenant = "custom-tenant".to_string();
        let custom_authority = Authority::new(
            Some(custom_host.clone()),
            Some(custom_tenant.clone())
        );
        assert_eq!(custom_authority.authority_url, format!("{}/{}", custom_host, custom_tenant));
    }
}