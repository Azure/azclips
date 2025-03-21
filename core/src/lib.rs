//! Azure CLI Lite Core Library
//!
//! This library provides the core functionality for the Azure CLI Lite application.
pub(crate) mod argparse;
pub(crate) mod auth;
pub(crate) mod config;
pub(crate) mod service;
pub(crate) mod utils;

pub mod cmd_uri;
pub mod error;
pub mod pb;

pub struct AzCore {}

// /// Version information
// pub fn version() -> &'static str {
//     env!("CARGO_PKG_VERSION")
// }

#[cfg(test)]
mod tests {
    #[test]
    fn it_works() {
        assert_eq!(2 + 2, 4);
    }
}
