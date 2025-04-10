use std::{result, vec};

use super::ApplicationDetail;
use crate::output::StyledString;
use az_core::pb::{command::{cmd_interface::Level, CmdArg, CmdInterface}, component::ComponentMetadata};
use crossterm::style::{StyledContent, Stylize};

pub trait UsageTemplateFieldRender {
    fn render(&self, cmd: &CmdInterface, app: &ApplicationDetail) -> Option<StyledString>;
}

pub struct UsageHeaderRenderer;
impl UsageTemplateFieldRender for UsageHeaderRenderer {
    fn render(&self, cmd: &CmdInterface, _app: &ApplicationDetail) -> Option<StyledString> {
        let header = match cmd.level {
            Some(Level::Command(_)) => "Command",
            Some(Level::Group(_)) => "Group",
            Some(Level::Component(_)) => "Service",
            None => return None,
        };
        Some(StyledString::from(header))
    }
}

pub struct UsageBodyRenderer;
impl UsageTemplateFieldRender for UsageBodyRenderer {
    fn render(&self, cmd: &CmdInterface, app: &ApplicationDetail) -> Option<StyledString> {
        let uri = &cmd.uri;
        let exe = &app.executable;
        let uri_parts = uri.split('/').filter(|s| !s.is_empty());
        let cmd_sig = format!("{} {}", exe, uri_parts.collect::<Vec<_>>().join(" "));
        let styled_cmd = cmd_sig.bold();
        let mut result = vec![styled_cmd];
        let short = cmd.help.as_ref().map(|help| &help.short);
        if let Some(short) = short {
            result.push(String::from(" : ").stylize());
            result.push(short.clone().italic());
        };
        Some(StyledString::from_vec(result))
    }
}

pub struct UsageDetailRenderer;
impl UsageTemplateFieldRender for UsageDetailRenderer {
    fn render(&self, cmd: &CmdInterface, _app: &ApplicationDetail) -> Option<StyledString> {
        let long = cmd.help.as_ref().and_then(|help| help.long.as_ref());
        long.map(|long| long.into())
    }
}

pub enum CmdItem<'a> {
    CmdInterface(&'a CmdInterface),
    Component(&'a ComponentMetadata)
}

pub trait CmdTemplateFieldRender {
    fn render(&self, cmd: CmdItem) -> Vec<StyledString>;
}

pub struct CmdNameRenderer;
impl CmdTemplateFieldRender for CmdNameRenderer {
    fn render(&self, cmd: CmdItem) -> Vec<StyledString> {
        match cmd {
            CmdItem::CmdInterface(cmd) => vec![StyledString::from_content(&cmd.name.clone().bold())],
            CmdItem::Component(component) => vec![StyledString::from_content(&component.name.clone().bold())],
        }
    }
}

pub struct CmdDescriptionRenderer;
impl CmdTemplateFieldRender for CmdDescriptionRenderer {
    fn render(&self, cmd: CmdItem) -> Vec<StyledString> {
        match cmd {
            CmdItem::CmdInterface(cmd) => {
                let description = cmd.help.as_ref().map(|help| help.short.clone());
                description.map(|desc| StyledString::from_content(&desc.italic()).split_lines()).unwrap_or(vec![])
            }
            CmdItem::Component(component) => {
                let description = component.help.as_ref().map(|help| help.short.clone());
                description.map(|desc| StyledString::from_content(&desc.italic()).split_lines()).unwrap_or(vec![])
            }
        }
    }
}

pub trait ArgTemplateFieldRender {
    fn render(&self, arg: &CmdArg) -> Vec<StyledString>;
}

pub struct ArgShortFlagRenderer;
impl ArgTemplateFieldRender for ArgShortFlagRenderer {
    fn render(&self, arg: &CmdArg) -> Vec<StyledString> {
        arg.options
            .iter()
            .filter(|o| o.len() == 2)
            .map(|s| s.clone().bold())
            .map(|s| StyledString::from_vec(vec![s]))
            .collect::<Vec<_>>()
    }
}

pub struct ArgOptionRenderer;
impl ArgTemplateFieldRender for ArgOptionRenderer {
    fn render(&self, arg: &CmdArg) -> Vec<StyledString> {
        let mut args = arg.options
            .iter()
            .filter(|o| o.len() != 2)
            .collect::<Vec<_>>();
        if args.is_empty() {
            args.push(&arg.name);
        }
        let result = args
            .iter()
            .map(|s| (*s).clone().bold())
            .map(|s| StyledString::from_vec(vec![s]))
            .collect::<Vec<_>>();
        if result.is_empty() {
            vec![]
        } else {
            result
        }
    }
}

pub struct ArgTagRenderer;
impl ArgTemplateFieldRender for ArgTagRenderer {
    fn render(&self, arg: &CmdArg) -> Vec<StyledString> {
        let mut result = vec![];
        if arg.required {
            result.push(StyledString::from_content(&"[Required]".yellow()));
        }
        result
    }
}

pub struct ArgHelpRenderer;
impl ArgTemplateFieldRender for ArgHelpRenderer {
    fn render(&self, arg: &CmdArg) -> Vec<StyledString> {
        let mut result = vec![];
        if let Some(help) = &arg.help {
            let styled_help: StyledString = help.short.clone().into();
            let mut styled_help = styled_help.split_lines();
            result.append(&mut styled_help);
        }
        if let Some(default) = &arg.default {
            let value = default.value.clone().blue().on_yellow();
            let styled_default = StyledString::from_vec(vec![
                "Default: ".to_owned().stylize(),
                value,
            ]);
            result.push(styled_default);
        }
        result
    }
}

pub struct ArgDetailRenderer;
impl ArgTemplateFieldRender for ArgDetailRenderer {
    fn render(&self, _arg: &CmdArg) -> Vec<StyledString> {
        let result = vec![];
        result
    }
}
