use async_trait::async_trait;

use crate::{
    component::{AzCommand, AzCommandExecute, AzComponentCtx},
    error::AzError,
    pb::{cmd_interface, CmdArg, CmdCommand, CmdHelp, CmdInterface, CmdParams},
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
    const URI: &'static str = "cmd://azure/version";

    fn build_interface(_: Option<u32>) -> CmdInterface {
        CmdInterface {
            level: Some(cmd_interface::Level::Command(CmdCommand {
                name: "version".to_string(),
                uri: Self::uri().to_string(),
                version: VERSION.to_string(),
                help: Some(CmdHelp {
                    short: "Show the version of the Azure CLI.".to_string(),
                    long: None,
                }),
                args: Self::args(),
                positional_args: vec![],
            })),
        }
    }

    fn args() -> Vec<CmdArg> {
        vec![]
    }

    fn try_from(_params: Option<CmdParams>) -> Result<Self, AzError> {
        Ok(Self::default())
    }
}
