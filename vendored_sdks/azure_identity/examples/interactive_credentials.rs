use azure_core::{
    credentials::TokenCredential,
};
use azure_identity::{
    interactive_credential::interactive_browser_credential::InteractiveBrowserCredential,
};
use oauth2::ClientId;
use oauth2::TokenResponse;
use std::error::Error;
use std::fs;
use std::sync::Arc;
use tracing::{info, warn, error, Level};
use tracing_subscriber;
use azure_security_keyvault_secrets::{
    models::{Secret, SetSecretParameters},
    ResourceExt, SecretClient,
};

pub const AZURE_CLI_CLIENT_ID: &str = "04b07795-8ddb-461a-bbee-02f9e1bf7b46";


#[tokio::main]
async fn main() -> Result<(), Box<dyn Error>> {
    // Initialize tracing subscriber for logging
    tracing_subscriber::fmt()
        .with_max_level(Level::DEBUG)
        .init();
    
    info!("Starting interactive credentials example");
    
    let tenant_id = "organizations";
    // Define the path for the token cache file in $HOME/.cirrus/
    let home_dir = dirs::home_dir().ok_or_else(|| {
        std::io::Error::new(std::io::ErrorKind::NotFound, "Could not find home directory")
    })?;
    let cache_dir = home_dir.join(".cirrus");
      // Create the .cirrus directory if it doesn't exist
    if !cache_dir.exists() {
        info!("Creating directory: {}", cache_dir.display());
        fs::create_dir_all(&cache_dir)?;
    }
    // Define the token cache file path
    let cache_file_path = cache_dir.join("msal_test_token.json");
    info!("Using token cache file: {}", cache_file_path.display());      // Check if the token cache file already exists
    if cache_file_path.exists() {
        info!("Found existing token cache file: {}", cache_file_path.display());
        if let Ok(contents) = fs::read_to_string(&cache_file_path) {
            info!("Token cache file contains {} bytes of data.", contents.len());
        } else {
            warn!("Found token cache file but couldn't read its contents");
        }
    } else {
        warn!("Token cache file doesn't exist yet. Will be created after authentication.");
    }
    
    // Create the interactive browser credential with the file path
    let mut interactive_credentials = InteractiveBrowserCredential::new(
        Some(ClientId::new(AZURE_CLI_CLIENT_ID.to_string())),
        Some(tenant_id.to_string()),
        None,   // redirect URL
        Some(cache_file_path.clone()), // Pass the file path directly
    )?;
      // Use get_token_interactively to get a full token response with refresh token
    info!("Calling get_token_interactively to acquire a token interactively");
    let token_response = interactive_credentials
        .get_token_interactively(
            Some(&["https://management.core.windows.net/.default"]),
            Some("select_account"),  // prompt
            None,                    // login_hint
            None,                    // success_template
            None,                    // error_template
        )
        .await?;
    
    println!("\n--- Original token from get_token_interactively ---\n");
    println!("Access token: {}", token_response.access_token().secret());
    
    // Display the home_account_id and environment that were set during authentication
    info!("After authentication:");
    info!("  Home Account ID: {:?}", interactive_credentials.home_account_id);
    info!("  Environment: {:?}", interactive_credentials.environment);// Check if the token cache file was updated after authentication
    println!("\n--- Token cache file status after authentication ---\n");
    if cache_file_path.exists() {
        let cache_contents = fs::read_to_string(&cache_file_path)?;
        info!("Token cache file contains {} bytes of data.", cache_contents.len());
        info!("Token cache file was successfully created/updated");
        print!("Token cache contents:\n{}", cache_contents);
    } else {
        error!("Token cache file wasn't created after authentication!");
        println!("Warning: Token cache file wasn't created after authentication!");
    }
    
    // Test TokenCredential.get_token implementation
    println!("\n--- Testing TokenCredential.get_token implementation ---\n");
    
    // Define the scopes
    let scopes = &["https://vault.azure.net/.default"];
    
    // Call get_token method from the TokenCredential trait implementation
    let token_credential_response = interactive_credentials.get_token(scopes).await?;
    
    println!("TokenCredential.get_token succeeded!");
    println!("Access token: {}", token_credential_response.token.secret());
    println!("Expires at: {:?}", token_credential_response.expires_on);    // Try calling sdk
    let client = SecretClient::new(
        "https://cirrus-test-kv.vault.azure.net/",
        Arc::new(interactive_credentials) as _,
        None,
    )?;

    // Create a new secret using the secret client.
    let secret_set_parameters = SetSecretParameters {
        value: Some("secret-value".into()),
        ..Default::default()
    };

    let secret: Secret = client
        .set_secret("secret-name", secret_set_parameters.try_into()?, None)
        .await?
        .into_body()
        .await?;

    // Get version of created secret.
    let version = secret.resource_id()?.version.unwrap_or_default();

    // Retrieve a secret using the secret client.
    let secret: Secret = client
        .get_secret("secret-name", version.as_ref(), None)
        .await?
        .into_body()
        .await?;
    println!("{:?}", secret.value);
    
    Ok(())
}
