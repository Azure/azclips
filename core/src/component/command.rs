use std::str::FromStr;

use async_trait::async_trait;
use serde::Serialize;

use crate::{
    error::AzError,
    pb::{execute_response, CrsArg, CrsCommand, CrsOutputFormat, CrsParams, None},
    uri::CrsUri,
};

use super::AzComponentCtx;

#[async_trait]
pub trait AzCommandExecute {
    type Output: Serialize;

    async fn run<T: AzComponentCtx>(&self, ctx: &T) -> Result<Option<Self::Output>, AzError>;
}

#[async_trait]
pub trait AzCommand: Sized {
    const URI: &'static str;

    fn uri() -> CrsUri {
        CrsUri::from_str(Self::URI).unwrap()
    }

    fn build_interface(depth: Option<u32>) -> CrsCommand;

    fn args() -> Vec<CrsArg>;

    fn try_from(params: Option<CrsParams>) -> Result<Self, AzError>
    where
        Self: AzCommandExecute;

    async fn execute<T: AzComponentCtx>(
        ctx: &T,
        params: Option<CrsParams>,
        _: Option<CrsOutputFormat>,
    ) -> Result<execute_response::Data, AzError>
    where
        Self: AzCommandExecute,
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
