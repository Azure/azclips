use async_trait::async_trait;
use std::env::consts::{ARCH, OS};
use std::sync::Arc;

use super::{Policy, PolicyResult, Context, HeaderValue, Request, HeaderName};
use crate::client::options::TelemetryOptions;
use crate::VERSION;

const USER_AGENT: HeaderName = HeaderName::from_static("user-agent");


#[derive(Debug, Clone)]
pub struct TelemetryPolicy {
    header: String,
}

impl TelemetryPolicy {
    pub fn new(options: &TelemetryOptions) -> Self {
        const UNKNOWN: &str = "unknown";
        let rustc_version = option_env!("AZCORE_RUSTC_VERSION").unwrap_or(UNKNOWN);
        #[cfg(feature = "cli")]
        let product = "cirrus-cli";
        #[cfg(all(feature = "ps", not(feature = "cli")))]
        let product = "cirrus-powershell";
        #[cfg(all(feature = "ansible", not(any(feature = "cli", feature = "ps"))))]
        let product = "cirrus-ansible";
        #[cfg(not(any(feature = "cli", feature = "ps", feature = "ansible")))]
        let product = "cirrus-core";
        let header = format!(
            "{}/{} {product}/{VERSION} azsdk-rust-core/unknown ({rustc_version}; {OS}; {ARCH})",
            options.component_name, options.component_version
        );
        Self { header }
    }
}

#[async_trait]
impl Policy for TelemetryPolicy {
    async fn send(
        &self,
        ctx: &Context,
        request: &mut Request,
        next: &[Arc<dyn Policy>],
    ) -> PolicyResult {
        request.insert_header(USER_AGENT, HeaderValue::from(self.header.to_string()));

        next[0].send(ctx, request, &next[1..]).await
    }
}

#[cfg(test)]
mod tests {
    use crate::client::options::TelemetryOptions;

    use super::*;

    #[test]
    fn test_telemetry_policy() {
        let options = TelemetryOptions {
            component_name: "config".to_string(),
            component_version: "0.1.0".to_string(),
        };

        let policy = TelemetryPolicy::new(&options);
        assert_eq!(
            policy.header,
            "config/0.1.0 cirrus-core/0.1.0 azsdk-rust-core/unknown (unknown; unknown; unknown)"
        );
    }
}
