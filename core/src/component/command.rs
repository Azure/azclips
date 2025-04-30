use std::{collections::HashMap, str::FromStr};

use async_trait::async_trait;

use crate::{cmd_uri::CmdUri, pb::command::CmdInterface};

pub trait AzCmdRoute {
    const URI: &'static str;

    fn uri() -> CmdUri {
        CmdUri::from_str(Self::URI).unwrap()
    }

    fn cmd_interface(depth: Option<u32>) -> CmdInterface;
}

// #[async_trait]
pub trait AzCmdGroup {
    fn get_interface(
        depth: Option<u32>,
        segments: &mut dyn Iterator<Item = &str>,
    ) -> Option<CmdInterface>;

    fn check_uri(segments: &mut dyn Iterator<Item = &str>) -> Option<CmdUri>;
}
