use az_core::{cmd_uri::CmdUri, AzCore};

use crate::CmdRawArg;

/// check if the `-h` or `--help` flag is present in the raw args
pub fn has_help_flag(raw_args: &[CmdRawArg]) -> bool {
    raw_args
        .iter()
        .any(|arg| arg.flag == "-h" || arg.flag == "--help")
}

pub fn show_command_help(cmd_uri: &CmdUri) {
    let rt = tokio::runtime::Runtime::new().unwrap();
    rt.block_on(show_command_help_async(cmd_uri));
}

async fn show_command_help_async(cmd_uri: &CmdUri) {
    let core = AzCore::instance().await;

    unimplemented!()
}
