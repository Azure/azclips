//! Azure CLI Lite Core Library
//!
//! This library provides the core functionality for the Azure CLI Lite application.

use cmd_uri::CmdUri;
use error::AzResult;
use std::sync::Arc;
use tokio::sync::OnceCell;
pub(crate) mod argparse;
pub(crate) mod auth;
pub(crate) mod config;
pub(crate) mod plugin;
pub(crate) mod service;
pub(crate) mod utils;

pub mod cmd_uri;
pub mod error;
pub mod pb;

/// The version of the core library
pub const VERSION: &str = env!("CARGO_PKG_VERSION");

/// The application name will be used the the host part of cmd uri
pub const APPLICATION_NAME: &str = env!("CARGO_APPLICATION_NAME");

pub struct AzCore {
    // Add fields as needed
}

impl AzCore {
    /// Get the global instance of AzCore
    pub async fn instance() -> &'static Arc<AzCore> {
        static INSTANCE: OnceCell<Arc<AzCore>> = OnceCell::const_new();
        INSTANCE
            .get_or_init(|| async { Arc::new(AzCore::new().await) })
            .await
    }

    /// Create a new instance of AzCore
    async fn new() -> Self {
        // Perform async initialization here
        Self {
            // Initialize fields as needed
        }
    }
}
