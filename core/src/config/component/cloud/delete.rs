use async_trait::async_trait;
use tracing::error;

use crate::{
    component::{AzCmd, AzCmdExecute, AzComponentCtx},
    config::cloud::AzCloud,
    error::{AzError, AzUserUsageError},
    pb::{
        cmd_arg, cmd_interface, CmdArg, CmdArgHelp, CmdCommand, CmdHelp, CmdInterface, CmdParams,
        CmdStringArg,
    },
    AzCore, VERSION,
};

#[derive(Debug, Default)]
pub struct DeleteCloud {
    name: AzCloud,
}

#[async_trait]
impl AzCmdExecute for DeleteCloud {
    type Output = ();

    async fn run<T: AzComponentCtx>(&self, _: &T) -> Result<Option<Self::Output>, AzError> {
        let core = AzCore::instance().await;
        let mut cfg = core.cfg.write().await;
        let is_active = cfg.clouds.get_active().name == self.name;
        if is_active {
            return Err(AzUserUsageError::CloudActiveError(self.name.to_string()).into());
        }
        cfg.clouds.0.remove(&self.name);
        cfg.save()?;
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
                        delete_cloud.name = serde_json::from_slice::<String>(v)
                            .expect("failed to parse json")
                            .parse()
                            .expect("failed to convert to AzCloud");
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
