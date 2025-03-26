pub(crate) mod service;

use std::{env, path::PathBuf, sync::OnceLock};

use crate::error::AzInternalError;

static CONFIG_DIR: OnceLock<PathBuf> = OnceLock::new();

pub(crate) fn get_config_dir() -> PathBuf {
    CONFIG_DIR
        .get_or_init(|| {
            let config_dir = env::var_os("AZURE_CONFIG_DIR")
                .map(PathBuf::from)
                .map_or_else(
                    || {
                        dirs::home_dir().map(|p| p.join(".azure")).ok_or(
                            AzInternalError::InvalidPathError(
                                "Could not determine home directory".to_string(),
                            ),
                        )
                    },
                    Ok,
                )
                .unwrap_or_else(|e| panic!("Failed to initialize config dir: {}", e));
            if !config_dir.exists() {
                std::fs::create_dir_all(&config_dir)
                    .unwrap_or_else(|e| panic!("Failed to create config dir: {}", e));
            }
            config_dir
        })
        .clone()
}
