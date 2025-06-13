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
pub struct DeleteDemo {}

#[async_trait]
impl AzCommandExecute for DeleteDemo {
    type Output = Value;

    async fn run<T: AzComponentCtx>(&self, _: &T) -> Result<Option<Self::Output>, AzError> {
        Ok(Some(json!({})))
    }
}

impl AzCommand for DeleteDemo {
    const URI: &'static str = "crs://azure/plugin/demo/delete";

    fn build_interface(_: Option<u32>) -> CrsCommand {
        CrsCommand {
            name: "delete".to_string(),
            uri: Self::uri().to_string(),
            version: VERSION.to_string(),
            help: Some(CrsHelp {
                short: "Delete a demo resource".to_string(),
                long: Some("Delete a demo resource".to_string()),
            }),
            args: Self::args(),
            ..Default::default()
        }
    }

    fn args() -> Vec<CrsArg> {
        vec![]
    }

    fn try_from(params: Option<CrsParams>) -> Result<Self, AzError> {
        let mut delete = Self::default();
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
        Ok(delete)
    }
}
