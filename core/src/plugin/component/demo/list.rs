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
pub struct ListDemo {}

#[async_trait]
impl AzCommandExecute for ListDemo {
    type Output = Value;

    async fn run<T: AzComponentCtx>(&self, _: &T) -> Result<Option<Self::Output>, AzError> {
        Ok(Some(json!({})))
    }
}

impl AzCommand for ListDemo {
    const URI: &'static str = "crs://azure/plugin/demo/list";

    fn build_interface(_: Option<u32>) -> CrsCommand {
        CrsCommand {
            name: "list".to_string(),
            uri: Self::uri().to_string(),
            version: VERSION.to_string(),
            help: Some(CrsHelp {
                short: "List a demo resource".to_string(),
                long: Some("List a demo resource".to_string()),
            }),
            args: Self::args(),
            positional_args: vec![],
        }
    }

    fn args() -> Vec<CrsArg> {
        vec![]
    }

    fn try_from(params: Option<CrsParams>) -> Result<Self, AzError> {
        let mut list = Self::default();
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
        Ok(list)
    }
}
