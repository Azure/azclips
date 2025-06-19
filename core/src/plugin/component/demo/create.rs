use async_trait::async_trait;
use serde_json::{json, Value};
use tracing::error;

use crate::{
    component::{AzCommand, AzCommandExecute, AzComponentCtx},
    error::AzError,
    pb::{CrsArg, CrsCommand, CrsHelp, CrsParams},
    VERSION,
};

#[derive(Debug, Default)]
pub struct CreateDemo {
}

#[async_trait]
impl AzCommandExecute for CreateDemo {
    type Output = Value;

    async fn run<T: AzComponentCtx>(&self, _: &T) -> Result<Option<Self::Output>, AzError> {
        Ok(Some(json!({})))
    }
}

impl AzCommand for CreateDemo {
    const URI: &'static str = "crs://azure/plugin/demo/create";

    fn build_interface(_: Option<u32>) -> CrsCommand {
        CrsCommand {
            name: "create".to_string(),
            uri: Self::uri().to_string(),
            version: VERSION.to_string(),
            help: Some(CrsHelp {
                short: "Create a demo resource".to_string(),
                long: Some("Create a demo resource".to_string()),
            }),
            args: Self::args(),
            ..Default::default()
        }
    }

    fn args() -> Vec<CrsArg> {
        vec![]
    }

    fn try_from(params: Option<CrsParams>) -> Result<Self, AzError> {
        let mut create = Self::default();
        if let Some(params) = params {
            for (name, v) in params.params.iter() {
                match name.as_str() {
                    // "name" => {
                    //     create.name = serde_json::from_slice(v).expect("failed to parse json");
                    // }
                    _ => {
                        error!("unknown argument: {}", name);
                    }
                }
            }
        }
        Ok(create)
    }
}
