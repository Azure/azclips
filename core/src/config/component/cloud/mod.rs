mod activate;
mod create;
mod delete;
mod list;
mod show;
mod update;

use activate::ActivateCloud;
use async_trait::async_trait;
use create::CreateCloud;
use delete::DeleteCloud;
use list::ListClouds;
use show::ShowCloud;

use crate::{
    cmd_uri::CmdUri,
    component::{
        cmd::{AzCmd, AzCmdGroup},
        AzComponentCtx,
    },
    error::{AzError, AzInternalError},
    pb::{
        command::{
            cmd_interface, CmdCommandGroup, CmdHelp, CmdInterface, CmdOutputFormat, CmdParams,
        },
        component::execute_response::Data,
    },
};

#[derive(Debug, Default)]
pub(super) struct CloudCommandGroup {}

#[async_trait]
impl AzCmdGroup for CloudCommandGroup {
    const URI: &'static str = "cmd://azure/config/cloud/";

    fn cmd_interface(depth: Option<u32>) -> CmdInterface {
        let groups = vec![];
        let mut commands = vec![];
        if depth.is_none() || depth.unwrap() > 0 {
            let depth = depth.map(|d| d - 1);
            commands.push(ListClouds::cmd_interface(depth));
        }
        CmdInterface {
            name: "cloud".to_string(),
            help: Some(CmdHelp {
                short: "Manage registered Azure clouds configuration".to_string(),
                long: None,
            }),
            uri: Self::uri().to_string(),
            level: Some(cmd_interface::Level::Group(CmdCommandGroup {
                groups,
                commands,
            })),
        }
    }

    fn get_interface(
        depth: Option<u32>,
        mut segments: impl Iterator<Item = String>,
    ) -> Option<CmdInterface> {
        match segments.next().as_deref() {
            None => Some(CloudCommandGroup::cmd_interface(depth)),
            Some("activate") => Some(ActivateCloud::cmd_interface(depth)),
            Some("create") => Some(CreateCloud::cmd_interface(depth)),
            Some("show") => Some(ShowCloud::cmd_interface(depth)),
            Some("delete") => Some(DeleteCloud::cmd_interface(depth)),
            Some("list") => Some(ListClouds::cmd_interface(depth)),
            _ => None,
        }
    }

    fn check_uri(mut segments: impl Iterator<Item = String>) -> Option<CmdUri> {
        match segments.next().as_deref() {
            None => Some(CloudCommandGroup::uri()),
            Some("activate") => Some(ActivateCloud::uri()),
            Some("create") => Some(CreateCloud::uri()),
            Some("show") => Some(ShowCloud::uri()),
            Some("delete") => Some(DeleteCloud::uri()),
            Some("list") => Some(ListClouds::uri()),
            _ => None,
        }
    }

    async fn execute_group<T: AzComponentCtx>(
        ctx: &T,
        mut segments: impl Iterator<Item = String> + Send,
        params: Option<CmdParams>,
        output_format: Option<CmdOutputFormat>,
    ) -> Result<Data, AzError> {
        match segments.next().as_deref() {
            Some("activate") => ActivateCloud::execute(ctx, params, output_format).await,
            Some("create") => CreateCloud::execute(ctx, params, output_format).await,
            Some("show") => ShowCloud::execute(ctx, params, output_format).await,
            Some("delete") => DeleteCloud::execute(ctx, params, output_format).await,
            Some("list") => ListClouds::execute(ctx, params, output_format).await,
            _ => Err(AzInternalError::InvalidCmdUriError(Self::uri().to_string()).into()),
        }
    }
}
