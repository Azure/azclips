use async_trait::async_trait;

use crate::{
    component::{AzCommand, AzCommandExecute, AzComponentCtx},
    error::AzError,
    pb::{CrsArg, CrsCommand, CrsHelp, CrsParams},
    VERSION,
};

#[derive(Debug, Default)]
pub struct ShowVersion {}

#[async_trait]
impl AzCommandExecute for ShowVersion {
    type Output = String;

    async fn run<T: AzComponentCtx>(&self, _: &T) -> Result<Option<Self::Output>, AzError> {
        Ok(Some(VERSION.to_string()))
    }
}

impl AzCommand for ShowVersion {
    const URI: &'static str = "crs://azure/version";

    fn build_interface(_: Option<u32>) -> CrsCommand {
        CrsCommand {
            name: "version".to_string(),
            uri: Self::uri().to_string(),
            version: VERSION.to_string(),
            help: Some(CrsHelp {
                short: "Show the version of the Azure CLI.".to_string(),
                long: None,
            }),
            args: Self::args(),
            ..Default::default()
        }
    }

    fn args() -> Vec<CrsArg> {
        vec![]
    }

    fn try_from(_params: Option<CrsParams>) -> Result<Self, AzError> {
        Ok(Self::default())
    }
}
