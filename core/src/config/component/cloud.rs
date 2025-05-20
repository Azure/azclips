mod activate;
mod create;
mod delete;
mod list;
mod show;
mod update;

use async_trait::async_trait;

use activate::ActivateCloud;
use create::CreateCloud;
use delete::DeleteCloud;
use list::ListClouds;
use show::ShowCloud;
use update::UpdateCloud;

use crate::{
    cmd_uri::CmdUri,
    component::{AzCommand, AzCommandGroup, AzComponentCtx},
    error::{AzError, AzInternalError},
    pb::{
        cmd_interface, execute_response, CmdCommandGroup, CmdHelp, CmdInterface, CmdOutputFormat,
        CmdParams,
    },
};

#[derive(Debug, Default)]
pub struct CloudCommandGroup {}

#[async_trait]
impl AzCommandGroup for CloudCommandGroup {
    const URI: &'static str = "cmd://azure/config/cloud/";

    fn build_interface(depth: Option<u32>) -> CmdInterface {
        let groups = vec![];
        let mut commands = vec![];
        if depth.is_none() || depth.unwrap() > 0 {
            let depth = depth.map(|d| d - 1);
            commands.push(ListClouds::build_interface(depth).as_command().unwrap().to_owned());
            commands.push(ShowCloud::build_interface(depth).as_command().unwrap().to_owned());
            commands.push(DeleteCloud::build_interface(depth).as_command().unwrap().to_owned());
            commands.push(ActivateCloud::build_interface(depth).as_command().unwrap().to_owned());
            commands.push(CreateCloud::build_interface(depth).as_command().unwrap().to_owned());
            commands.push(UpdateCloud::build_interface(depth).as_command().unwrap().to_owned());
        }
        CmdInterface {
            level: Some(cmd_interface::Level::CommandGroup(CmdCommandGroup {
                name: "cloud".to_string(),
                uri: Self::uri().to_string(),
                help: Some(CmdHelp {
                    short: "Manage registered Azure clouds configuration".to_string(),
                    long: None,
                }),
                groups,
                commands,
            })),
        }
    }

    fn find_interface(
        depth: Option<u32>,
        mut segments: impl Iterator<Item = String>,
    ) -> Option<CmdInterface> {
        match segments.next().as_deref() {
            None => Some(CloudCommandGroup::build_interface(depth)),
            Some("activate") => Some(ActivateCloud::build_interface(depth)),
            Some("create") => Some(CreateCloud::build_interface(depth)),
            Some("update") => Some(UpdateCloud::build_interface(depth)),
            Some("show") => Some(ShowCloud::build_interface(depth)),
            Some("delete") => Some(DeleteCloud::build_interface(depth)),
            Some("list") => Some(ListClouds::build_interface(depth)),
            _ => None,
        }
    }

    fn resolve_cmd_uri(mut segments: impl Iterator<Item = String>) -> Option<CmdUri> {
        match segments.next().as_deref() {
            None => Some(CloudCommandGroup::uri()),
            Some("activate") => Some(ActivateCloud::uri()),
            Some("create") => Some(CreateCloud::uri()),
            Some("update") => Some(UpdateCloud::uri()),
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
    ) -> Result<execute_response::Data, AzError> {
        match segments.next().as_deref() {
            Some("activate") => ActivateCloud::execute(ctx, params, output_format).await,
            Some("create") => CreateCloud::execute(ctx, params, output_format).await,
            Some("update") => UpdateCloud::execute(ctx, params, output_format).await,
            Some("show") => ShowCloud::execute(ctx, params, output_format).await,
            Some("delete") => DeleteCloud::execute(ctx, params, output_format).await,
            Some("list") => ListClouds::execute(ctx, params, output_format).await,
            _ => Err(AzInternalError::InvalidCmdUriError(Self::uri().to_string()).into()),
        }
    }
}
