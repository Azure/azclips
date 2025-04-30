use webbrowser;


pub fn can_launch_browser() -> bool {
    if webbrowser::Browser::is_available() {
        return true;
    } else {
        return false;
    }
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