pub(crate) mod cloud_cfg;
pub(crate) mod component;

use std::{env, path::PathBuf, sync::OnceLock};

use cloud_cfg::AzCloudCfg;
use serde::{Deserialize, Serialize};

use crate::error::AzInternalError;

#[derive(Debug, Default, Serialize, Deserialize)]
pub(crate) struct AzConfig {
    pub(crate) cloud: AzCloudCfg,
}

impl AzConfig {
    pub(crate) fn load() -> Result<Self, AzInternalError> {
        let config_dir = get_config_dir();
        let config_file = config_dir.join("config.toml");
        if !config_file.exists() {
            let config = AzConfig::default();
            config.save()?;
            return Ok(config);
        }
        let config = toml::from_str(&std::fs::read_to_string(config_file)?)?;
        Ok(config)
    }

    pub(crate) fn save(&self) -> Result<(), AzInternalError> {
        let config_dir = get_config_dir();
        let config_file = config_dir.join("config.toml");
        let config_str = toml::to_string(self)?;
        std::fs::write(config_file, config_str)?;
        Ok(())
    }
}

static CONFIG_DIR: OnceLock<PathBuf> = OnceLock::new();

pub(crate) fn get_config_dir() -> PathBuf {
    CONFIG_DIR
        .get_or_init(|| {
            let config_dir = env::var_os("AZURE_CONFIG_DIR")
                .map(PathBuf::from)
                .map_or_else(
                    || {
                        dirs::home_dir().map(|p| p.join(".cirrus")).ok_or(
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
