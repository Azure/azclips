use async_trait::async_trait;
use tracing::error;

use crate::{
    component::{AzCommand, AzCommandExecute, AzComponentCtx},
    config::cloud::AzCloud,
    error::AzError,
    pb::{
        crs_arg, crs_string_arg, CrsArg, CrsArgHelp, CrsCommand, CrsHelp, CrsParams, CrsStringArg,
        CrsStringFormat,
    },
    VERSION,
};

#[derive(Debug, Default)]
pub struct RemovePlugin {
    name: String,
    cloud: Option<AzCloud>,
}

#[async_trait]
impl AzCommandExecute for RemovePlugin {
    type Output = ();

    async fn run<T: AzComponentCtx>(&self, _: &T) -> Result<Option<Self::Output>, AzError> {
        println!("remove plugin: {:?}", self);
        Ok(None)
    }
}

impl AzCommand for RemovePlugin {
    const URI: &'static str = "crs://azure/plugin/remove";

    fn build_interface(_: Option<u32>) -> CrsCommand {
        CrsCommand {
            name: "remove".to_string(),
            uri: Self::uri().to_string(),
            version: VERSION.to_string(),
            help: Some(CrsHelp {
                short: "Remove an installed plugin".to_string(),
                long: Some("Remove an installed plugin from the cloud environment".to_string()),
            }),
            args: Self::args(),
            ..Default::default()
        }
    }

    fn args() -> Vec<CrsArg> {
        vec![
            CrsArg {
                var_name: "name".to_string(),
                name: "name".to_string(),
                options: vec![
                    "name".to_string(),
                    "n".to_string(),
                ],
                help: Some(CrsArgHelp {
                    short: "The name of the plugin to remove".to_string(),
                    ..Default::default()
                }),
                required: true,
                arg_type: Some(crs_arg::ArgType::String(CrsStringArg {
                    format: Some(crs_string_arg::Format::String(CrsStringFormat::default())),
                    ..Default::default()
                })),
                ..Default::default()
            },
            CrsArg {
                var_name: "cloud".to_string(),
                name: "cloud".to_string(),
                options: vec![
                    "cloud".to_string(),
                ],
                help: Some(CrsArgHelp {
                    short: "The cloud environment to remove the plugin from, if not specified, the current active cloud will be used".to_string(),
                    ..Default::default()
                }),
                arg_type: Some(crs_arg::ArgType::String(CrsStringArg {
                    format: Some(crs_string_arg::Format::String(CrsStringFormat::default())),
                    ..Default::default()
                })),
                ..Default::default()
            }
        ]
    }

    fn try_from(params: Option<CrsParams>) -> Result<Self, AzError> {
        let mut remove = Self::default();
        if let Some(params) = params {
            for (name, v) in params.params.iter() {
                match name.as_str() {
                    "name" => {
                        remove.name = serde_json::from_slice(v).expect("failed to parse json");
                    }
                    "cloud" => {
                        remove.cloud = serde_json::from_slice(v).expect("failed to parse json");
                    }
                    _ => {
                        error!("unknown argument: {}", name);
                    }
                }
            }
        }
        Ok(remove)
    }
}
