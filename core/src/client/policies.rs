mod bearer_token_policy;
mod telemetry;

use typespec_client_core::http::{
    headers::{HeaderName, HeaderValue},
    policies::{Policy, PolicyResult},
    Context, Request,
};

pub use azure_core::http::policies::ClientRequestIdPolicy;
pub use bearer_token_policy::BearerTokenCredentialPolicy;
pub use telemetry::TelemetryPolicy;
