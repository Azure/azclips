pub(crate) mod component;

use crate::error::{AzError, AzResult};

/// Authentication provider for Azure
pub struct AuthProvider {
    // token: Option<String>,
    // tenant_id: Option<String>,
    // client_id: Option<String>,
}

impl AuthProvider {}

impl Default for AuthProvider {
    fn default() -> Self {
        Self {}
    }
}
