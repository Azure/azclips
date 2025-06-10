
#[derive(Debug, Clone)]
pub struct TelemetryOptions {
    pub component_name: String,
    pub component_version: String,
}


#[derive(Debug, Clone)]
pub struct ClientOptions {
    pub telemetry: TelemetryOptions,
}