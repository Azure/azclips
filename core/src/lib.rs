//! Azure CLI Lite Core Library
//! 
//! This library provides the core functionality for the Azure CLI Lite application.

pub mod error;
pub mod auth;
pub mod commands;
pub mod config;
pub mod utils;

/// Version information
pub fn version() -> &'static str {
    env!("CARGO_PKG_VERSION")
}

#[cfg(test)]
mod tests {
    #[test]
    fn it_works() {
        assert_eq!(2 + 2, 4);
    }
} 