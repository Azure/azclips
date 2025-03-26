use az_core::{
    argparse::{parse_arg, ArgHelpOp, ArgOp},
    cmd_uri::CmdUri,
    error::{AzError, AzUserUsageError},
    pb::command::{cmd_interface, CmdArg, CmdCommand},
    AzCore,
};

use crate::{help, CmdRawArg};

pub fn execute_command(cmd_uri: CmdUri, raw_args: Vec<CmdRawArg>) {
    let rt = tokio::runtime::Runtime::new().unwrap();
    rt.block_on(async {
        if let Err(e) = execute_command_async(cmd_uri, raw_args).await {
            // TODO: handle the error, need to add error handler in the core
            eprintln!("{}", e);
            std::process::exit(1);
        }
    });
}

async fn execute_command_async(cmd_uri: CmdUri, raw_args: Vec<CmdRawArg>) -> Result<(), AzError> {
    let core = AzCore::instance().await;
    let cmd_uri = core.check_cmd_uri(cmd_uri).await?;

    if !cmd_uri.executable() {
        // if it's a command group, then show the help of it instead of executing it
        help::show_command_help_async(cmd_uri).await?;
        return Err(AzUserUsageError::CommandGroupNotExecutableError.into());
    }

    let cmd_interface = core.get_cmd_interface(&cmd_uri).await?;
    let cmd = match cmd_interface.level.unwrap() {
        cmd_interface::Level::Command(cmd) => cmd,
        _ => {
            unreachable!()
        }
    };

    let mut cmd_arg_ops = Vec::new();

    for raw_arg in raw_args.iter() {
        let arg_name = raw_arg.name_or_alias();
        let values = raw_arg.values();

        let ops = if let Some(arg) = find_arg_in_global(arg_name) {
            parse_arg(None, arg, values)?
        } else if let Some(arg) = find_arg_in_command(arg_name, &cmd) {
            parse_arg(Some(&cmd), arg, values)?
        } else {
            return Err(AzUserUsageError::UnrecognizedArgumentError(raw_arg.flag.clone()).into());
        };

        // show the help of the argument
        if let Some(ArgOp::Help(op)) = ops.iter().find(|op| matches!(op, ArgOp::Help(_))) {
            return help::show_command_arg_help_async(cmd_uri, op.keys.to_owned()).await;
        }

        cmd_arg_ops.extend(ops);
    }

    todo!()
}

fn find_arg_in_global(_: &str) -> Option<&'static CmdArg> {
    // TODO: support global args definition
    return None;
}

fn find_arg_in_command<'a>(name_or_alias: &str, cmd: &'a CmdCommand) -> Option<&'a CmdArg> {
    for arg in cmd.args.iter() {
        if arg.name.eq_ignore_ascii_case(name_or_alias) {
            return Some(arg);
        }
        if arg
            .options
            .iter()
            .any(|o| o.eq_ignore_ascii_case(name_or_alias))
        {
            return Some(arg);
        }
    }
    None
}
