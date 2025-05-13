pub mod cloud;

pub(crate) mod component;

use crate::error::AzInternalError;
use cloud::AzCloudsCfg;
use fs2::FileExt;
use serde::{Deserialize, Serialize};
use serde_json::{Map, Value};
use std::fs::File;
use std::io::{Read, Write};
use std::{env, path::PathBuf, sync::OnceLock};

const ENV_CONFIG_DIR: &str = "AZURE_CONFIG_DIR";
const DEFAULT_CONFIG_DIR: &str = ".cirrus";

#[derive(Debug, Default, Clone, Serialize, Deserialize)]
pub struct AzCfg {
    pub clouds: AzCloudsCfg,
    pub settings: Map<String, Value>,
}

impl AzCfg {
    #[allow(unstable_name_collisions)]
    pub(crate) fn load() -> Result<Self, AzInternalError> {
        let config_file = Self::config_file_path();
        if !config_file.exists() {
            let config = AzCfg::default();
            config.save()?;
            return Ok(config);
        }
        let mut buf = String::new();
        {
            let mut file = File::open(&config_file)?;
            file.lock_shared()?;
            file.read_to_string(&mut buf)?;
        }
        let config = toml::from_str(&buf)?;
        Ok(config)
    }

    pub(crate) fn save(&self) -> Result<(), AzInternalError> {
        let config_str = toml::to_string(self)?;
        {
            let mut file = File::create(Self::config_file_path())?;
            file.lock_exclusive()?;
            file.write_all(config_str.as_bytes())?;
        }
        Ok(())
    }

    /// The default config file path will be `$HOME/.cirrus/config.toml`
    pub(crate) fn config_file_path() -> PathBuf {
        let config_dir = get_config_dir();
        config_dir.join("config.toml")
    }
}

static CONFIG_DIR: OnceLock<PathBuf> = OnceLock::new();

pub(crate) fn get_config_dir() -> PathBuf {
    CONFIG_DIR
        .get_or_init(|| {
            let config_dir = env::var_os(ENV_CONFIG_DIR)
                .map(PathBuf::from)
                .map_or_else(
                    || {
                        dirs::home_dir().map(|p| p.join(DEFAULT_CONFIG_DIR)).ok_or(
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
