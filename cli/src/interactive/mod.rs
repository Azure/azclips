use az_core::{cmd_uri::CmdUri, AzCore};

use crate::CmdRawArg;

/// check if the `-h` or `--help` flag is present in the raw args
pub fn has_interactive_flag(raw_args: &[CmdRawArg]) -> bool {
    raw_args.iter().any(|arg| arg.flag == "--cli-interactive")
}

pub fn run_interactive(cmd_uri: &CmdUri, raw_args: &[CmdRawArg]) {
    let rt = tokio::runtime::Runtime::new().unwrap();
    rt.block_on(run_interactive_async(cmd_uri, raw_args));
}

async fn run_interactive_async(cmd_uri: &CmdUri, raw_args: &[CmdRawArg]) {
    let core = AzCore::instance().await;

    unimplemented!()
}
