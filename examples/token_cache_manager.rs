use azure_identity::msal_token_cache::MsalTokenCache;
use std::fs::{self, File};
use std::io::{Read, Write};
use std::path::{Path, PathBuf};
use std::sync::Arc;
use tokio::sync::RwLock;

struct TokenCacheManager {
    token_cache: Arc<MsalTokenCache>,
    cache_file_path: PathBuf,
}

impl TokenCacheManager {
    pub fn new(cache_file_path: impl AsRef<Path>) -> Self {
        let path = cache_file_path.as_ref().to_path_buf();
        Self {
            token_cache: Arc::new(MsalTokenCache::new()),
            cache_file_path: path,
        }
    }
    
    pub async fn load_cache(&self) -> Result<(), Box<dyn std::error::Error>> {
        if self.cache_file_path.exists() {
            let mut file = File::open(&self.cache_file_path)?;
            let mut content = String::new();
            file.read_to_string(&mut content)?;
            
            self.token_cache.deserialize(&content).await?;
            println!("Token cache loaded from: {:?}", self.cache_file_path);
        } else {
            println!("No token cache file found at: {:?}", self.cache_file_path);
        }
        
        Ok(())
    }
    
    pub async fn save_cache(&self) -> Result<(), Box<dyn std::error::Error>> {
        // Only save if the cache has changed
        if self.token_cache.has_state_changed().await {
            let serialized_cache = self.token_cache.serialize().await?;
            
            // Ensure the directory exists
            if let Some(parent) = self.cache_file_path.parent() {
                fs::create_dir_all(parent)?;
            }
            
            let mut file = File::create(&self.cache_file_path)?;
            file.write_all(serialized_cache.as_bytes())?;
            
            println!("Token cache written to: {:?}", self.cache_file_path);
        } else {
            println!("Token cache unchanged, not writing to disk");
        }
        
        Ok(())
    }
    
    pub fn get_token_cache(&self) -> Arc<MsalTokenCache> {
        self.token_cache.clone()
    }
}

#[tokio::main]
async fn main() -> Result<(), Box<dyn std::error::Error>> {
    // Create a cache manager
    let cache_manager = TokenCacheManager::new("./msal_token_cache.json");
    
    // Load the cache from disk if it exists
    cache_manager.load_cache().await?;
    
    // Get the token cache for use in your application
    let token_cache = cache_manager.get_token_cache();
    
    // Use the token cache
    // For example:
    // let scopes = &["https://graph.microsoft.com/.default"];
    // let client_id = "your-client-id";
    // let token_result = token_cache.get_token(scopes, None, None, Some(client_id), None).await;
    
    // After using the token cache, save any changes back to disk
    cache_manager.save_cache().await?;
    
    Ok(())
}
