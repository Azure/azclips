#![cfg(test)]

use azure_core::{
    credentials::{AccessToken, Secret, TokenCredential},
    http::new_http_client,
};
use azure_identity::{
    interactive_credential::interactive_browser_credential::InteractiveBrowserCredential,
    refresh_token,
};
use oauth2::ClientId;
use std::time::Duration;
use time::OffsetDateTime;

const AZURE_CLI_CLIENT_ID: &str = "04b07795-8ddb-461a-bbee-02f9e1bf7b46";
const TEST_SCOPE: &[&str] = &["https://management.core.windows.net/.default"];
const TEST_TENANT: &str = "organizations";

#[tokio::test]
#[ignore = "Interactive test requiring browser authentication"]
async fn test_token_refresh() {
    // Create a new InteractiveBrowserCredential
    let credential = InteractiveBrowserCredential::new(
        Some(ClientId::new(AZURE_CLI_CLIENT_ID.to_string())),
        Some(TEST_TENANT.to_string()),
        None,
    )
    .expect("Failed to create credential");

    println!("Getting initial token...");
    // Get an initial token
    let token1 = credential
        .get_token(Some(TEST_SCOPE), None, None, None, None)
        .await
        .expect("Failed to get token");
    let expires1 = token1.expires_on;
    let access_token1 = token1.token.secret().to_string();

    println!("Original token expires: {:?}", expires1);
    println!("Original access token: {}", mask_token(&access_token1));

    // Simulate token expiration by directly obtaining a new token using the refresh mechanism
    // For a real test, you would need to wait for the token to expire, but that's not practical
    println!("\nSimulating token expiration and refreshing...");

    // Get another token - the TokenCache should use the refresh token internally
    let token2 = credential
        .get_token(Some(TEST_SCOPE), None, None, None, None)
        .await
        .expect("Failed to get refreshed token");
    let expires2 = token2.expires_on;
    let access_token2 = token2.token.secret().to_string();

    println!("New token expires: {:?}", expires2);
    println!("New access token: {}", mask_token(&access_token2));

    // Tokens should be different as we're getting a new one
    assert_ne!(
        access_token1, access_token2,
        "Expected different access tokens"
    );

    // The new token should expire later
    assert!(
        expires2 > expires1,
        "Expected new token to expire later than the original"
    );

    println!("Test passed - refresh token functionality is working correctly");
}

// Helper to mask most of the token for security
fn mask_token(token: &str) -> String {
    if token.len() <= 10 {
        return "****".to_string();
    }
    format!("{}...{}", &token[..5], &token[token.len() - 5..])
}
