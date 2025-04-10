use std::collections::HashMap;

use az_core::pb::command::{CmdCommand, CmdInterface};
use az_core::pb::component::ComponentMetadata;

use crate::help::field::*;
use crate::output::{Row, RowCell, StyledString};

use super::ApplicationDetail;

pub fn default_usage_template() -> HelpUsageTemplate {
    HelpUsageTemplate {
        header: Box::new(UsageHeaderRenderer),
        body: Box::new(UsageBodyRenderer),
        detail: Some(Box::new(UsageDetailRenderer)),
        intent: 2,
    }
}

pub fn default_cmd_template() -> HelpCmdTemplate {
    HelpCmdTemplate {
        name: Box::new(CmdNameRenderer),
        tag: None,
        description: Some(Box::new(CmdDescriptionRenderer)),
        detail: None,
        intent: 2,
    }
}

pub fn default_arg_template() -> HelpArgTemplate {
    HelpArgTemplate {
        short: Box::new(ArgShortFlagRenderer),
        option: Box::new(ArgOptionRenderer),
        tag: Box::new(ArgTagRenderer),
        description: Box::new(ArgHelpRenderer),
        detail: Some(Box::new(ArgDetailRenderer)),
        intent: 2,
    }
}

pub struct HelpUsageTemplate {
    pub header: Box<dyn UsageTemplateFieldRender>,
    pub body: Box<dyn UsageTemplateFieldRender>,
    pub detail: Option<Box<dyn UsageTemplateFieldRender>>,
    pub intent: usize,
}

impl Default for HelpUsageTemplate {
    fn default() -> Self {
        default_usage_template()
    }
}

impl HelpUsageTemplate {
    pub fn render(
        &self,
        cmd: &CmdInterface,
        app: &ApplicationDetail,
        term_width: Option<usize>,
    ) -> Vec<StyledString> {
        let header = self.header.render(cmd, app).unwrap_or("Azure CLI".into());
        let body = self
            .body
            .render(cmd, app)
            .map(|body| StyledString::from(" ".repeat(self.intent)) + body);
        let detail = self
            .detail
            .as_ref()
            .and_then(|f| f.render(cmd, app))
            .map(|detail| detail.soft_wrap(term_width.map(|tw| tw.wrapping_sub(self.intent * 2))))
            .unwrap_or(vec![])
            .into_iter()
            .map(|detail| StyledString::from(" ".repeat(self.intent * 2)) + detail)
            .collect::<Vec<_>>();
        let mut result = vec![header];
        if let Some(body) = body {
            result.push(body);
        }
        result.extend_from_slice(&detail[..]);
        result
    }
}

struct HelpTableLine {
    short: Vec<StyledString>,
    name: Vec<StyledString>,
    tag: Vec<StyledString>,
    description: Vec<StyledString>,
    detail: Vec<StyledString>,
}

impl HelpTableLine {
    fn calc_width(cell: &Vec<StyledString>) -> usize {
        cell.iter().map(|s| s.term_len()).max().unwrap_or(0)
    }

    fn render_groups(
        groups: &HashMap<String, Vec<Self>>,
        term_width: Option<usize>,
        intent: usize,
    ) -> Vec<StyledString> {
        let short_width = groups
            .values()
            .flat_map(|group| group.iter())
            .map(|line| Self::calc_width(&line.short))
            .max()
            .unwrap_or(0);
        let option_width = groups
            .values()
            .flat_map(|group| group.iter())
            .map(|line| Self::calc_width(&line.name))
            .max()
            .unwrap_or(0);
        let tag_width = groups
            .values()
            .flat_map(|group| group.iter())
            .map(|line| Self::calc_width(&line.tag))
            .max()
            .unwrap_or(0);
        let mut result = vec![];
        for (group_name, group) in groups {
            let group_header = StyledString::from(format!("{}", group_name));
            result.push(group_header);
            for line in group {
                let mut row = Row::new();
                row.append_sep(" ".repeat(intent));
                if short_width > 0 {
                    let short_cell = RowCell::from_styled(line.short.clone(), Some(short_width));
                    row.append(short_cell);
                    row.append_sep(" ");
                }
                if option_width > 0 {
                    let option_cell = RowCell::from_styled(line.name.clone(), Some(option_width));
                    row.append(option_cell);
                    row.append_sep(" ");
                }
                if tag_width > 0 {
                    let tag_cell = RowCell::from_styled(line.tag.clone(), Some(tag_width));
                    row.append(tag_cell);
                }
                row.append_one_line(" : ");
                let description_cell = RowCell::from_styled(line.description.clone(), None);
                row.append(description_cell);
                result.append(&mut row.render());
                let detail_width = term_width.map(|tw| tw.saturating_sub(intent * 2));
                let wrapped_detail = line
                    .detail
                    .iter()
                    .flat_map(|detail| detail.soft_wrap(detail_width))
                    .map(|detail| StyledString::from(" ".repeat(intent * 2)) + detail)
                    .collect::<Vec<_>>();
                result.extend(wrapped_detail);
            }
        }
        result
    }
}

pub struct HelpCmdTemplate {
    pub name: Box<dyn CmdTemplateFieldRender>,
    pub tag: Option<Box<dyn CmdTemplateFieldRender>>,
    pub description: Option<Box<dyn CmdTemplateFieldRender>>,
    pub detail: Option<Box<dyn CmdTemplateFieldRender>>,
    pub intent: usize,
}

impl Default for HelpCmdTemplate {
    fn default() -> Self {
        default_cmd_template()
    }
}

impl HelpCmdTemplate {
    pub fn render<'a>(
        &self,
        groups: &Vec<CmdInterface>,
        commands: &Vec<CmdInterface>,
        components: Option<Box<dyn Iterator<Item = &'a ComponentMetadata> + 'a>>,
        term_width: Option<usize>,
    ) -> Vec<StyledString> {
        let mut table_groups = HashMap::new();
        let group_lines = groups.iter().map(|group| HelpTableLine {
            short: vec![],
            name: self.name.render(CmdItem::CmdInterface(group)),
            tag: vec![],
            description: self
                .description
                .as_ref()
                .map(|f| f.render(CmdItem::CmdInterface(group)))
                .unwrap_or(vec![]),
            detail: vec![],
        });
        table_groups.insert("Command Group".to_string(), group_lines.collect());
        let command_lines = commands.iter().map(|command| HelpTableLine {
            short: vec![],
            name: self.name.render(CmdItem::CmdInterface(command)),
            tag: vec![],
            description: self
                .description
                .as_ref()
                .map(|f| f.render(CmdItem::CmdInterface(command)))
                .unwrap_or(vec![]),
            detail: vec![],
        });
        table_groups.insert("Command".to_string(), command_lines.collect());
        if let Some(components) = components {
            let component_lines = components.map(|component| HelpTableLine {
                short: vec![],
                name: self.name.render(CmdItem::Component(&component)),
                tag: vec![],
                description: self
                    .description
                    .as_ref()
                    .map(|f| f.render(CmdItem::Component(&component)))
                    .unwrap_or(vec![]),
                detail: vec![],
            });
            table_groups.insert("Component".to_string(), component_lines.collect());
        }

        HelpTableLine::render_groups(&table_groups, term_width, self.intent)
    }
}

pub struct HelpArgTemplate {
    pub short: Box<dyn ArgTemplateFieldRender>,
    pub option: Box<dyn ArgTemplateFieldRender>,
    pub tag: Box<dyn ArgTemplateFieldRender>,
    pub description: Box<dyn ArgTemplateFieldRender>,
    pub detail: Option<Box<dyn ArgTemplateFieldRender>>,
    pub intent: usize,
}

impl Default for HelpArgTemplate {
    fn default() -> Self {
        default_arg_template()
    }
}

impl HelpArgTemplate {
    fn calc_width(cell: &Vec<StyledString>) -> usize {
        cell.iter().map(|s| s.term_len()).max().unwrap_or(0)
    }

    pub fn render(&self, cmd: &CmdCommand, term_width: Option<usize>) -> Vec<StyledString> {
        let mut groups = HashMap::new();
        for arg in cmd.args.iter() {
            let line = HelpTableLine {
                short: self.short.render(arg),
                name: self.option.render(arg),
                tag: self.tag.render(arg),
                description: self.description.render(arg),
                detail: self
                    .detail
                    .as_ref()
                    .map(|f| f.render(arg))
                    .unwrap_or(vec![]),
            };
            let group = groups.entry(arg.group.clone()).or_insert_with(|| vec![]);
            group.push(line);
        }
        HelpTableLine::render_groups(&groups, term_width, self.intent)
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::help::field::{
        ArgDetailRenderer, ArgHelpRenderer, ArgOptionRenderer, ArgShortFlagRenderer,
        ArgTagRenderer, UsageBodyRenderer, UsageDetailRenderer, UsageHeaderRenderer,
    };
    use az_core::pb::command::{CmdArgHelp, CmdHelp, CmdInterface};

    #[test]
    fn test_help_cmd_template_render() {
        let cmd = CmdInterface {
            name: "command".to_string(),
            uri: "/group/command".to_string(),
            level: Some(az_core::pb::command::cmd_interface::Level::Command(
                az_core::pb::command::CmdCommand {
                    version: "1.0.0".to_string(),
                    args: vec![],
                    positional_args: vec![],
                },
            )),
            help: Some(CmdHelp {
                short: "Short description".to_string(),
                long: Some("Long description of the command. ".repeat(3)),
            }),
        };

        let app = ApplicationDetail {
            executable: "az".to_string(),
            version: "1.0.0".to_string(),
        };

        let template = HelpUsageTemplate {
            header: Box::new(UsageHeaderRenderer),
            body: Box::new(UsageBodyRenderer),
            detail: Some(Box::new(UsageDetailRenderer)),
            intent: 2,
        };

        let result = template.render(&cmd, &app, Some(80));

        for line in &result {
            println!("{}", line);
        }

        assert_eq!(result.len(), 4); // header, body, detail
        assert_eq!(result[0].to_string(), "Command"); // Header
        assert!(result[1].to_string().contains("az group command")); // Body
        assert!(result[1].to_string().contains("Short description")); // Body short description
        assert!(result[1].to_string().starts_with("  ")); // Indent for body
        assert!(result[2]
            .to_string()
            .contains("Long description of the command.")); // Detail
        assert!(result[2].to_string().starts_with("    ")); // Indent for detail
    }

    #[test]
    fn test_help_arg_template_render() {
        let cmd = az_core::pb::command::CmdCommand {
            version: "1.0.0".to_string(),
            args: vec![
                az_core::pb::command::CmdArg {
                    var_name: "arg1".to_string(),
                    options: vec![
                        "-a".to_string(),
                        "--arg1".to_string(),
                        "--arg1option".to_string(),
                    ],
                    name: "Argument 1".to_string(),
                    group: "Group 1".to_string(),
                    internal: false,
                    required: true,
                    default: None,
                    help: Some(CmdArgHelp {
                        short: "Short description".to_string(),
                        long: Some("Long description of the argument.".to_string()),
                    }),
                    nullable: false,
                    blank: None,
                    prompt: None,
                    arg_type: None,
                },
                az_core::pb::command::CmdArg {
                    var_name: "arg2".to_string(),
                    options: vec!["-b".to_string(), "--arg2".to_string()],
                    name: "Argument 2".to_string(),
                    group: "Group 1".to_string(),
                    internal: false,
                    required: false,
                    default: None,
                    help: Some(CmdArgHelp {
                        short: "Short description".to_string(),
                        long: Some("Long description of the argument.".to_string()),
                    }),
                    nullable: false,
                    blank: None,
                    prompt: None,
                    arg_type: None,
                },
            ],
            positional_args: vec![],
        };

        let app = ApplicationDetail {
            executable: "az".to_string(),
            version: "1.0.0".to_string(),
        };

        let template = HelpArgTemplate {
            short: Box::new(ArgShortFlagRenderer),
            option: Box::new(ArgOptionRenderer),
            tag: Box::new(ArgTagRenderer),
            description: Box::new(ArgHelpRenderer),
            detail: Some(Box::new(ArgDetailRenderer)),
            intent: 2,
        };

        let result = template.render(&cmd, Some(80));
        for line in &result {
            println!("{}", line);
        }

        assert_eq!(result.len(), 4);
        assert_eq!(result[0].to_string(), "Group 1");
        assert!(result[1].to_string().contains("-a"));
        assert!(result[1].to_string().contains("--arg1"));
        assert!(result[2].to_string().contains("--arg1option"));
    }
}
