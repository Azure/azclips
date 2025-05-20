pub mod format;
mod table;
mod templates;

use az_core::{
    argparse::{CrsArgWrapper, SHPartialKeys},
    cmd_uri::CmdUri,
    error::AzError,
    pb::crs_interface,
    AzCore,
};
use format::default_help_formatter;

use crate::CmdRawArg;

/// Minimum width for the detail content
/// This is used after considering the intent
/// Use infinite when the width is not available
pub const MIN_DETAIL_WIDTH: usize = 20;

/// check if the `-h` or `--help` flag is present in the raw args
pub fn has_help_flag(raw_args: &[CmdRawArg]) -> bool {
    raw_args
        .iter()
        .any(|arg| arg.flag == "-h" || arg.flag == "--help")
}

pub fn show_command_help(cmd_uri: CmdUri) {
    let rt = tokio::runtime::Runtime::new().unwrap();
    rt.block_on(async {
        if let Err(e) = show_command_help_async(cmd_uri).await {
            // TODO: handle the error, need to add error handler in the core
            eprintln!("{}", e);
            std::process::exit(1);
        }
    });
}

pub(crate) async fn show_command_help_async(cmd_uri: CmdUri) -> Result<(), AzError> {
    let term_width = crossterm::terminal::size().ok().map(|(w, _)| w as usize);
    let core = AzCore::instance().await;
    let invocation = core.resolve_cmd_invocation(cmd_uri).await?;
    if invocation.cmd_uri.executable() {
        let crs_interface = core.get_crs_interface(&invocation).await?;
        // TODO: global arguments
        for line in default_help_formatter().format_cmd(&crs_interface, None, term_width) {
            println!("{}", line);
        }
        Ok(())
    } else {
        let crs_interface = core.get_crs_interface(&invocation).await?;
        let sub_components = core
            .get_metadata_of_sub_components(&invocation.cmd_uri)
            .await?;
        for line in
            default_help_formatter().format_cmd(&crs_interface, sub_components.values(), term_width)
        {
            println!("{}", line);
        }
        Ok(())
    }
}

pub(crate) async fn show_command_arg_help_async(
    cmd_uri: CmdUri,
    keys: SHPartialKeys,
) -> Result<(), AzError> {
    let core = AzCore::instance().await;
    let invocation = core.resolve_cmd_invocation(cmd_uri).await?;
    let crs_interface = core.get_crs_interface(&invocation).await?;
    let cmd = match crs_interface.level.unwrap() {
        crs_interface::Level::Command(cmd) => cmd,
        _ => {
            panic!("Show argument in the command group value is not supported");
        }
    };

    let wrapper = CrsArgWrapper::build_by_keys(&cmd, &keys)?;
    let arg = wrapper.arg();
    let term_width = crossterm::terminal::size().ok().map(|(w, _)| w as usize);
    for line in default_help_formatter().format_arg(arg, term_width) {
        println!("{}", line);
    }
    Ok(())
}
