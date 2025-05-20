use async_trait::async_trait;
use tracing::error;

use crate::{
    component::{AzCommand, AzCommandExecute, AzComponentCtx},
    config::cloud::AzCloud,
    error::{AzError, AzUserUsageError},
    pb::{
        crs_arg, crs_interface, CrsArg, CrsArgHelp, CrsCommand, CrsHelp, CrsInterface, CrsParams,
        CrsStringArg,
    },
    AzCore, VERSION,
};

#[derive(Debug, Default)]
pub struct DeleteCloud {
    name: AzCloud,
}

#[async_trait]
impl AzCommandExecute for DeleteCloud {
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

impl AzCommand for DeleteCloud {
    const URI: &'static str = "crs://azure/config/cloud/delete";

    fn build_interface(_: Option<u32>) -> CrsInterface {
        CrsInterface {
            level: Some(crs_interface::Level::Command(CrsCommand {
                name: "delete".to_string(),
                uri: Self::uri().to_string(),
                version: VERSION.to_string(),
                help: Some(CrsHelp {
                    short: "delete a cloud.".to_string(),
                    long: None,
                }),
                args: Self::args(),
                positional_args: vec![],
            })),
        }
    }

    fn args() -> Vec<CrsArg> {
        vec![CrsArg {
            name: "name".to_string(),
            help: Some(CrsArgHelp {
                short: "The name of the cloud to delete.".to_string(),
                long: None,
            }),
            options: vec!["name".to_string()],
            required: true,
            nullable: false,
            arg_type: Some(crs_arg::ArgType::String(CrsStringArg {
                ..Default::default()
            })),
            ..Default::default()
        }]
    }

    fn try_from(params: Option<CrsParams>) -> Result<Self, crate::error::AzError> {
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
