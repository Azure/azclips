use std::env;
use std::process::Command;


pub fn can_launch_browser() -> bool {
    let platform = get_platform_info();
    if platform != "linux" {
        // Only Linux may have no browser
        return true;
    }
    if Command::new("xdg-open").arg("--version").output().is_ok() {
        return true;
    }
    if is_wsl() {
        return command_exists("powershell.exe");
    }
    false
}

fn get_platform_info() -> String {
    if cfg!(target_os = "linux") {
        "linux".to_string()
    } else if cfg!(target_os = "windows") {
        "windows".to_string()
    } else if cfg!(target_os = "macos") {
        "macos".to_string()
    } else {
        "unknown".to_string()
    }
}

fn is_wsl() -> bool {
    if let Ok(version) = std::fs::read_to_string("/proc/version") {
        let version_lower = version.to_lowercase();
        return version_lower.contains("microsoft") || version_lower.contains("wsl");
    }
    false
}

fn command_exists(command: &str) -> bool {
    if let Ok(paths) = env::var("PATH") {
        for path in env::split_paths(&paths) {
            let full_path = path.join(command);
            if full_path.exists() {
                return true;
            }
        }
    }
    false
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