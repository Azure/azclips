//! Azure CLI Lite Core Library
//!
//! This library provides the core functionality for the Azure CLI Lite application.
pub mod argparse;
pub mod client;
pub mod component;
pub mod config;
pub mod error;
pub mod pb;
pub mod uri;

mod account;
mod core;
mod param;
mod plugin;
/// The module defines the top level commands
mod top;
mod utils;

/// The version of the core library
pub const VERSION: &str = env!("CARGO_PKG_VERSION");

/// The application name will be used the the host part of uri
pub const APPLICATION_NAME: &str = env!("CARGO_APPLICATION_NAME");

pub use core::{AzCore, AzInvocation};
