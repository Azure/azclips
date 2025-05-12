use std::str::FromStr;

use async_trait::async_trait;
use tracing::error;

use crate::{
    component::{
        cmd::{AzCmd, AzCmdExecute},
        AzComponentCtx,
    },
    error::AzError,
    pb::command::{
        cmd_arg, cmd_interface, CmdArg, CmdArgHelp, CmdCommand, CmdHelp, CmdInterface, CmdParams,
        CmdStringArg,
    },
    AzCloud, AzCore, VERSION,
};

#[derive(Debug, Default)]
pub(super) struct DeleteCloud {
    name: String,
}

#[async_trait]
impl AzCmdExecute for DeleteCloud {
    type Output = ();

    async fn run<T: AzComponentCtx>(&self, _: &T) -> Result<Option<Self::Output>, AzError> {
        let core = AzCore::instance().await;
        let name = AzCloud::from_str(&self.name).unwrap();
        core.cfg.write().await.cloud.clouds.remove(&name);
        Ok(None)
    }
}

impl AzCmd for DeleteCloud {
    const URI: &'static str = "cmd://azure/config/cloud/delete";

    fn cmd_interface(_: Option<u32>) -> CmdInterface {
        CmdInterface {
            name: "delete".to_string(),
            help: Some(CmdHelp {
                short: "delete a cloud.".to_string(),
                long: None,
            }),
            uri: Self::uri().to_string(),
            level: Some(cmd_interface::Level::Command(CmdCommand {
                version: VERSION.to_string(),
                args: Self::args(),
                positional_args: vec![],
            })),
        }
    }

    fn args() -> Vec<CmdArg> {
        vec![CmdArg {
            name: "name".to_string(),
            help: Some(CmdArgHelp {
                short: "The name of the cloud to delete.".to_string(),
                long: None,
            }),
            options: vec!["name".to_string()],
            required: true,
            nullable: false,
            arg_type: Some(cmd_arg::ArgType::Str(CmdStringArg {
                ..Default::default()
            })),
            ..Default::default()
        }]
    }

    fn try_from(params: Option<CmdParams>) -> Result<Self, crate::error::AzError> {
        let mut delete_cloud = Self::default();
        if let Some(params) = params {
            for (name, v) in params.params.iter() {
                match name.as_str() {
                    "name" => {
                        delete_cloud.name = serde_json::from_slice(v).expect("failed to parse json")
                    }
                    _ => {
                        error!("unknown argument: {}", name);
                    }
                }
            }
        }
        Ok(delete_cloud)
    }
}
