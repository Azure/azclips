use azure_core::{credentials::Secret, http::new_http_client};
use azure_identity::{
    interactive_credential::interactive_browser_credential::InteractiveBrowserCredential,
    refresh_token,
};
use oauth2::TokenResponse;
use reqwest::Client;
use std::error::Error;
use std::fmt;
use url::Url;
use std::time::{Duration, SystemTime, UNIX_EPOCH};

pub const AZURE_CLI_CLIENT_ID: &str = "04b07795-8ddb-461a-bbee-02f9e1bf7b46";

// Define a custom error type for missing refresh token
#[derive(Debug)]
struct NoRefreshTokenError;

impl fmt::Display for NoRefreshTokenError {
    fn fmt(&self, f: &mut fmt::Formatter) -> fmt::Result {
        write!(f, "No refresh token was provided, cannot refresh access token")
    }
}

impl Error for NoRefreshTokenError {}

#[tokio::main]
async fn main() -> Result<(), Box<dyn Error>> {
    // let test_tenant_id = "72f988bf-86f1-41af-91ab-2d7cd011db47".to_string(); // Microsoft
    // let test_tenant_id = "b5ee6c06-c2c2-4e3c-8606-5f170cee077a".to_string(); // Azure Client Tools (MFA enabled)
    // let test_tenant_id = "0ab32e5f-a697-43a7-862f-f980ad4cdbde".to_string();
    let test_tenant_id = "consumers".to_string();

    let _ = run_app_inter(test_tenant_id).await?;
    Ok(())
}

async fn run_app_inter(tenant_id: String) -> Result<(), Box<dyn Error>> {
    let interactive_credentials =
        InteractiveBrowserCredential::new(None, Some(tenant_id.clone()), None)?;

    let token_response = interactive_credentials
        .get_token(Some(&["https://management.core.windows.net/.default"]), Some("select_account"))
        .await?;
    
    let original_expires_in = token_response.expires_in().unwrap_or(Duration::from_secs(0));
    let original_expiration = SystemTime::now()
        .checked_add(original_expires_in)
        .unwrap_or(UNIX_EPOCH);
    
    println!("Original token expires in: {} seconds (at {:?})", 
             original_expires_in.as_secs(), 
             original_expiration);

    let refresh_token = token_response.refresh_token()
        .ok_or_else(|| NoRefreshTokenError)?; // Return an error if refresh token is missing

    let refresh_token_secret = Secret::new(refresh_token.secret().to_string());

    println!("\n--- Refreshing the token using refresh_token ---\n");

    // Use the refresh token to get a new access token silently (without browser prompt)
    let new_token_response = refresh_token::exchange(
        new_http_client(),
        &tenant_id,
        AZURE_CLI_CLIENT_ID,
        None,
        &refresh_token_secret,
    )
    .await?;

    let new_expires_in = Duration::from_secs(new_token_response.expires_in());
    let new_expiration = SystemTime::now()
        .checked_add(new_expires_in)
        .unwrap_or(UNIX_EPOCH);
    
    println!("New token expires in: {} seconds (at {:?})", 
             new_expires_in.as_secs(), 
             new_expiration);
    
    // Check if the new token has a more recent expiration time
    if new_expiration > original_expiration {
        println!("✅ New token has a more recent expiration time");
    } else {
        println!("❌ New token does NOT have a more recent expiration time. It is accepted, because the token duration is random.");
    }
    

    let access_token_secret = new_token_response.access_token().secret();

    let url = Url::parse("https://management.azure.com/subscriptions?api-version=2022-12-01")?;

    let resp = Client::new()
        .get(url)
        .header("Authorization", format!("Bearer {}", access_token_secret))
        .send()
        .await?
        .text()
        .await?;

    println!("Response: {}", resp);

    Ok(())
}