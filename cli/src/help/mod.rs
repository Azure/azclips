use az_core::cmd_uri::CmdUri;

use crate::CmdRawArg;

pub fn show_command_help(cmd_uri: &CmdUri) {}

/// check if the `-h` or `--help` flag is present in the raw args
pub fn has_help_flag(raw_args: &[CmdRawArg]) -> bool {
    raw_args
        .iter()
        .any(|arg| arg.flag == "-h" || arg.flag == "--help")
}
