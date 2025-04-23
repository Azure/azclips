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