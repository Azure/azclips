mod telemetry;
mod bearer_token_policy;


use typespec_client_core::http::{
    headers::{HeaderValue, HeaderName},
    policies::{Policy, PolicyResult},
    Context, Request,
};

pub use azure_core::http::policies::{ClientRequestIdPolicy};
pub use telemetry::TelemetryPolicy;
pub use bearer_token_policy::BearerTokenCredentialPolicy;
