use std::env::Args;

use az_core::{cmd_uri::CmdUri, APPLICATION_NAME};

mod help;

pub(crate) const CMD_ARG_FLAG_PREFIX: char = '-';

#[derive(Debug)]
pub(crate) struct CmdRawArg {
    // the flag used in the input
    flag: String,
    // the raw values of the parameter
    values: Vec<String>,
}

impl CmdRawArg {
    pub fn name_or_alias(&self) -> &str {
        self.flag.trim_start_matches('-')
    }

    pub fn values(&self) -> impl Iterator<Item = &str> {
        self.values.iter().map(|s| s.as_str())
    }
}

pub fn run(mut args: Args) {
    let mut next = args.next();

    // retrieve the cmd uri
    let mut cmd_uri = vec![APPLICATION_NAME.to_string()];
    while let Some(arg) = next.take() {
        if arg.starts_with(CMD_ARG_FLAG_PREFIX) {
            break;
        }
        cmd_uri.push(arg.to_lowercase());
        next = args.next();
    }
    let cmd_uri = CmdUri::new(cmd_uri, true); // here we assume the command is executable, and the service will correct it if it's not a executable command

    // parse cmd args
    let mut raw_args = Vec::new();
    while let Some(flag) = next.take() {
        // parse as cmd arg
        if !flag.starts_with(CMD_ARG_FLAG_PREFIX) {
            unreachable!("argument flag should always start with '-' when parsing cmd args");
        }
        let mut raw_arg = CmdRawArg {
            flag,
            values: Vec::new(),
        };
        while let Some(value) = args.next() {
            if value.starts_with(CMD_ARG_FLAG_PREFIX) {
                next.replace(value);
                break;
            } else {
                raw_arg.values.push(value);
            }
        }
        raw_args.push(raw_arg);
    }

    // show help if the help flag is present
    if help::has_help_flag(&raw_args) {
        help::show_command_help(&cmd_uri);
        return;
    }

    
}
