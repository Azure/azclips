use webbrowser;
use reqwest::{Client, Url};
use serde_json::Value;


pub fn can_launch_browser() -> bool {
    webbrowser::Browser::is_available()
}

pub fn format_token_expires_on(expires_in: std::time::Duration) -> (String, String) {
    let now = std::time::SystemTime::now();
    let expires_at = now.checked_add(expires_in).unwrap_or(now);
    
    let expires_on = match expires_at.duration_since(std::time::UNIX_EPOCH) {
        Ok(duration) => duration.as_secs().to_string(),
        Err(_) => "unknown".to_string(),
    };
    
    let expires_on_readable = match expires_at.duration_since(std::time::UNIX_EPOCH) {
        Ok(duration) => {
            let utc_datetime = chrono::DateTime::<chrono::Utc>::from_timestamp(
                duration.as_secs() as i64,
                0
            ).unwrap_or_else(|| chrono::Utc::now());
            let local_datetime = utc_datetime.with_timezone(&chrono::Local);
            local_datetime.format("%Y-%m-%d %H:%M:%S %Z").to_string()
        },
        Err(_) => "unknown".to_string(),
    };
    
    (expires_on, expires_on_readable)
}

// This is a fake function for `az account list` to simulate the behavior of getting subscriptions.
// Todo: Move it to account folder
async fn fake_get_subscriptions(access_token: String) -> Result<Vec<String>, &'static str> {
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

pub async fn fake_list_subscriptions(access_token: String) -> Result<(), &'static str> {
    let subscriptions = fake_get_subscriptions(access_token).await?;
    
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