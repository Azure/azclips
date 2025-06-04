use azure_identity::msal_token_cache::MsalTokenCache;
use std::fs::File;
use std::io::Write;
use std::path::Path;

#[tokio::main]
async fn main() -> Result<(), Box<dyn std::error::Error>> {
    // Create or get your token cache instance
    let token_cache = MsalTokenCache::new();
    
    // Serialize the token cache to a JSON string
    let serialized_cache = token_cache.serialize().await?;
    
    // Define the path where you want to save the cache
    let cache_file_path = Path::new("msal_token_cache.json");
    
    // Create or open the file
    let mut file = File::create(cache_file_path)?;
    
    // Write the serialized cache data to the file
    file.write_all(serialized_cache.as_bytes())?;
    
    println!("Token cache written to: {:?}", cache_file_path);
    
    Ok(())
}
