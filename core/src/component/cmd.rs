use std::str::FromStr;

use async_trait::async_trait;
use serde::Serialize;

use crate::{
    cmd_uri::CmdUri,
    error::AzError,
    pb::{
        self,
        command::{CmdArg, CmdInterface, CmdOutputFormat, CmdParams},
        component::execute_response::Data,
    },
};

use super::AzComponentCtx;

#[async_trait]
pub trait AzCmdExecute {
    type Output: Serialize;

    async fn run<T: AzComponentCtx>(&self, ctx: &T) -> Result<Option<Self::Output>, AzError>;
}

#[async_trait]
pub trait AzCmd: Sized {
    const URI: &'static str;

    fn uri() -> CmdUri {
        CmdUri::from_str(Self::URI).unwrap()
    }

    fn cmd_interface(depth: Option<u32>) -> CmdInterface;

    fn args() -> Vec<CmdArg>;

    fn try_from(params: Option<CmdParams>) -> Result<Self, AzError>
    where
        Self: AzCmdExecute;

    async fn execute<T: AzComponentCtx>(
        ctx: &T,
        params: Option<CmdParams>,
        _: Option<CmdOutputFormat>,
    ) -> Result<Data, AzError>
    where
        Self: AzCmdExecute,
    {
        let result = Self::try_from(params)?.run(ctx).await?;
        if let Some(result) = result {
            Ok(Data::Json(serde_json::to_vec(&result).unwrap()))
        } else {
            Ok(Data::None(pb::component::None {}))
        }
    }
}

#[async_trait]
pub trait AzCmdGroup {
    const URI: &'static str;

    fn uri() -> CmdUri {
        CmdUri::from_str(Self::URI).unwrap()
    }

    fn cmd_interface(depth: Option<u32>) -> CmdInterface;

    fn get_interface(
        depth: Option<u32>,
        segments: impl Iterator<Item = String>,
    ) -> Option<CmdInterface>;

    fn check_uri(segments: impl Iterator<Item = String>) -> Option<CmdUri>;

    async fn execute_group<T: AzComponentCtx>(
        ctx: &T,
        segments: impl Iterator<Item = String> + Send,
        params: Option<CmdParams>,
        output_format: Option<CmdOutputFormat>,
    ) -> Result<Data, AzError>;
}
