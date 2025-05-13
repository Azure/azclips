//! Azure CLI Lite Core Library
//!
//! This library provides the core functionality for the Azure CLI Lite application.
pub mod argparse;
pub mod cmd_uri;
pub mod error;
pub mod pb;

mod account;
mod component;
mod config;
mod core;
mod param;
mod plugin;
/// The module defines the top level commands
mod top;
mod utils;

use config::{AzCfg, AzCloud};
use error::AzResult;

/// The version of the core library
pub const VERSION: &str = env!("CARGO_PKG_VERSION");

/// The application name will be used the the host part of cmd uri
pub const APPLICATION_NAME: &str = env!("CARGO_APPLICATION_NAME");

pub use core::AzCore;
