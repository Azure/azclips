use reqwest::{Client, Url};
use serde_json::Value;

pub fn can_launch_browser() -> bool {
    webbrowser::Browser::is_available()
}

pub fn format_token_expires_on(expires_in: std::time::Duration) -> u64 {
    let now = std::time::SystemTime::now();

    now.checked_add(expires_in)
        .and_then(|expires_at| expires_at.duration_since(std::time::UNIX_EPOCH).ok())
        .map(|duration| duration.as_secs())
        .unwrap_or(0)
}

pub fn format_token_expires_on_readable(expires_on: u64) -> String {
    if expires_on == 0 {
        return "unknown".to_string();
    }

    match chrono::DateTime::<chrono::Utc>::from_timestamp(expires_on as i64, 0) {
        Some(utc) => {
            let local = utc.with_timezone(&chrono::Local);
            local.format("%Y-%m-%d %H:%M:%S %Z").to_string()
        }
        None => "invalid timestamp".to_string(),
    }
}

pub fn load_landing_page_templates() -> (String, String) {
    let success_template = include_str!("landing_pages/success.html").to_string();
    let error_template = include_str!("landing_pages/error.html").to_string();

    (success_template, error_template)
}

// This is a fake function for `az account list` to simulate the behavior of getting subscriptions.
// Todo: Move it to account folder
async fn fake_get_subscriptions(access_token: String) -> Result<Vec<String>, &'static str> {
    let url = match Url::parse("https://management.azure.com/subscriptions?api-version=2022-12-01")
    {
        Ok(url) => url,
        Err(_) => return Err("Failed to parse URL"),
    };

    let response = match Client::new()
        .get(url)
        .header("Authorization", format!("Bearer {}", access_token))
        .send()
        .await
    {
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
                    if let Some(subscription_id) =
                        item.get("subscriptionId").and_then(|id| id.as_str())
                    {
                        subscription_ids.push(subscription_id.to_string());
                    }
                }
                subscription_ids
            } else {
                return Err("API response format invalid: 'value' is not an array");
            }
        }
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
            println!("  {}. {}", i + 1, subscription_id);
        }
    }

    Ok(())
}
