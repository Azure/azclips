use azure_identity::msal_token_cache::MsalTokenCache;
use std::path::Path;
use std::fs::{self, File};
use std::io::{Read, Write};

#[tokio::main]
async fn main() -> Result<(), Box<dyn std::error::Error>> {
    // Set up paths
    let home_dir = dirs::home_dir().ok_or_else(|| {
        std::io::Error::new(std::io::ErrorKind::NotFound, "Could not determine home directory")
    })?;
    let token_dir = home_dir.join(".cirrus");
    let msal_path = token_dir.join("msal_token_cache.json");
    
    // Create directory if it doesn't exist
    fs::create_dir_all(&token_dir)?;
    
    // Check if we have a legacy token
    let legacy_path = token_dir.join("azure_token.json");
    if legacy_path.exists() {
        println!("Legacy token found at {}", legacy_path.display());
        
        // Read legacy token
        let mut file = File::open(&legacy_path)?;
        let mut contents = String::new();
        file.read_to_string(&mut contents)?;
        
        println!("Legacy token contents:\n{}", contents);
    } else {
        println!("No legacy token found at {}", legacy_path.display());
    }
    
    // Check if we have an MSAL token cache
    if msal_path.exists() {
        println!("\nMSAL token cache found at {}", msal_path.display());
        
        // Read MSAL token cache
        let mut file = File::open(&msal_path)?;
        let mut contents = String::new();
        file.read_to_string(&mut contents)?;
        
        // Load it into a TokenCache
        let token_cache = MsalTokenCache::new();
        token_cache.deserialize(&contents).await?;
        
        // Serialize it again to pretty-print
        let serialized = token_cache.serialize().await?;
        println!("MSAL token cache contents:\n{}", serialized);
        
        // Here you could retrieve a token with:
        // let token = token_cache.get_token(&["your_scope"], None, None, None, None).await;
    } else {
        println!("\nNo MSAL token cache found at {}", msal_path.display());
    }
    
    println!("\nNote: To migrate your legacy token to MSAL format,");
    println!("simply use Token::write_token_to_file() which now supports both formats.");
    
    Ok(())
}
