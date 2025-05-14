use std::str::FromStr;

use async_trait::async_trait;
use serde::Serialize;

use crate::{
    cmd_uri::CmdUri,
    error::AzError,
    pb::{
        execute_response, CmdArg, CmdInterface, CmdOutputFormat, CmdParams, ComponentMetadata,
        ExecuteRequest, ExecuteResponse, InterfaceRequest, InterfaceResponse, MetadataRequest,
        MetadataResponse, None, ValidateRequest, ValidateResponse,
    },
    VERSION,
};

use super::{AzComponent, AzComponentCtx};

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
    ) -> Result<execute_response::Data, AzError>
    where
        Self: AzCmdExecute,
    {
        let result = Self::try_from(params)?.run(ctx).await?;
        if let Some(result) = result {
            Ok(execute_response::Data::Json(
                serde_json::to_vec(&result).unwrap(),
            ))
        } else {
            Ok(execute_response::Data::None(None {}))
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

    fn resolve_cmd_uri(segments: impl Iterator<Item = String>) -> Option<CmdUri>;

    async fn execute_group<T: AzComponentCtx>(
        ctx: &T,
        segments: impl Iterator<Item = String> + Send,
        params: Option<CmdParams>,
        output_format: Option<CmdOutputFormat>,
    ) -> Result<execute_response::Data, AzError>;
}

pub trait AzCmdComponent<T: AzComponentCtx>: AzCmdGroup + Send + Sync + 'static {}

#[async_trait]
impl<T: AzComponentCtx, C: AzCmdComponent<T>> AzComponent<T> for C {
    async fn metadata(&self, _: &T, _: MetadataRequest) -> Result<MetadataResponse, AzError> {
        let interface = Self::cmd_interface(Some(0));
        Ok(MetadataResponse {
            metadata: Some(ComponentMetadata {
                name: interface.name,
                version: VERSION.to_string(),
                uri: interface.uri,
                help: interface.help,
            }),
        })
    }

    async fn interface(
        &self,
        _: &T,
        request: InterfaceRequest,
    ) -> Result<InterfaceResponse, AzError> {
        let skip = Self::uri().segments().count();
        let cmd_uri = CmdUri::from_str(&request.uri).unwrap();
        let depth = match request.depth {
            0 => None, // None means all the depth
            _ => Some(request.depth),
        };
        let segments = cmd_uri.segments().skip(skip).map(|s| s.to_string());
        let interface = Self::get_interface(depth, segments);
        Ok(InterfaceResponse { interface })
    }

    async fn execute(&self, ctx: &T, request: ExecuteRequest) -> Result<ExecuteResponse, AzError> {
        let skip = Self::uri().segments().count();
        let cmd_uri = CmdUri::from_str(&request.uri).unwrap();
        let segments = cmd_uri.segments().skip(skip).map(|s| s.to_string());
        let result =
            Self::execute_group(ctx, segments, request.params, request.output_format).await?;
        Ok(ExecuteResponse { data: Some(result) })
    }

    async fn validate(&self, _: &T, request: ValidateRequest) -> Result<ValidateResponse, AzError> {
        let skip = Self::uri().segments().count();
        let cmd_uri = CmdUri::from_str(&request.uri).unwrap();
        let segments = cmd_uri.segments().skip(skip).map(|s| s.to_string());
        let uri = Self::resolve_cmd_uri(segments).map(|u| {
            // add version and params from the cmd_uri
            let mut uri = u.with_version(VERSION);
            for (key, value) in cmd_uri.params() {
                uri = uri.with_param_values(key, value.collect::<Vec<_>>());
            }
            uri.to_string()
        });
        Ok(ValidateResponse { uri })
    }
}
