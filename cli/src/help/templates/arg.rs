use az_core::pb::CrsArg;

use crate::output::StyledString;

/// Trait for rendering argument help templates.
///
/// We have the following properties for the argument help template:
/// ## Argument Properties
///
/// - `var_name`
/// - `name`
/// - `options(string)`
/// - `group`
/// - `internal`
/// - `required`
/// - `nullable`
/// - `blank`
/// - `blank/value`
/// - `default`
/// - `default/value`
/// - `help(enum)`
/// - `help/short`
/// - `help/long`
/// - `prompt`
/// - `prompt/prompt`
/// - `prompt/secret`
/// - `prompt/confirm`
/// - `arg_type(enum)`
/// - `arg_type(cls)/cls_type`
/// - `arg_type(str)/r#enum`
/// - `arg_type(str)/r#enum/items`
/// - `arg_type(str)/r#enum/items/name`
/// - `arg_type(str)/r#enum/items/value`
/// - `arg_type(str)/r#enum/items/internal`
/// - `arg_type(str)/r#enum/support_extension`
/// - `arg_type(str)/r#enum/case_sensitive`
/// - `arg_type(str)/format(enum)`
/// - `arg_type(str)/format(binary)`
/// - `arg_type(str)/format(byte)`
/// - `arg_type(str)/format(string)/pattern`
/// - `arg_type(str)/format(string)/max_length`
/// - `arg_type(str)/format(string)/min_length`
/// - `arg_type(str)/format(duration)/protocol`
/// - `arg_type(str)/format(date)/protocol`
/// - `arg_type(str)/format(datetime)/protocol`
/// - `arg_type(str)/format(datetime)/ms_length`
/// - `arg_type(str)/format(time)/protocol`
/// - `arg_type(str)/format(time)/ms_length`
/// - `arg_type(str)/format(uuid)/case`
/// - `arg_type(str)/format(uuid)/no_hyphen`
/// - `arg_type(str)/format(uuid)/with_braces`
/// - `arg_type(str)/format(password)`
/// - `arg_type(str)/format(subscription_id)`
/// - `arg_type(str)/format(resource_group_name)`
/// - `arg_type(str)/format(resource_id)/templates`
/// - `arg_type(str)/format(resource_location)/no_rg_default`
/// - `arg_type(number)/r#enum`
/// - `arg_type(number)/r#enum/items`
/// - `arg_type(number)/r#enum/items/name`
/// - `arg_type(number)/r#enum/items/value`
/// - `arg_type(number)/r#enum/items/internal`
/// - `arg_type(number)/format(enum)`
/// - `arg_type(number)/format(i32)/minimum`
/// - `arg_type(number)/format(i32)/maximum`
/// - `arg_type(number)/format(i32)/multiple_of`
/// - `arg_type(number)/format(i64)/minimum`
/// - `arg_type(number)/format(i64)/maximum`
/// - `arg_type(number)/format(i64)/multiple_of`
/// - `arg_type(number)/format(u32)/minimum`
/// - `arg_type(number)/format(u32)/maximum`
/// - `arg_type(number)/format(u32)/multiple_of`
/// - `arg_type(number)/format(u64)/minimum`
/// - `arg_type(number)/format(u64)/maximum`
/// - `arg_type(number)/format(u64)/multiple_of`
/// - `arg_type(number)/format(float)/minimum`
/// - `arg_type(number)/format(float)/maximum`
/// - `arg_type(number)/format(float)/exclusive_minimum`
/// - `arg_type(number)/format(float)/exclusive_maximum`
/// - `arg_type(number)/format(float)/multiple_of`
/// - `arg_type(number)/format(double)/minimum`
/// - `arg_type(number)/format(double)/maximum`
/// - `arg_type(number)/format(double)/exclusive_minimum`
/// - `arg_type(number)/format(double)/exclusive_maximum`
/// - `arg_type(number)/format(double)/multiple_of`
/// - `arg_type(boolean)/reverse_options(vec<str>)`
/// - `arg_type(object)/cls_type`
/// - `arg_type(object)/ps_flatten`
/// - `arg_type(object)/format`
/// - `arg_type(object)/format/min_length`
/// - `arg_type(object)/format/max_length`
/// - `arg_type(object)/props(args)`
/// - `arg_type(object)/additional_props`
/// - `arg_type(object)/additional_props/item(arg)`
/// - `arg_type(array)/cls_type`
/// - `arg_type(array)/item(arg)`
/// - `arg_type(array)/format`
/// - `arg_type(array)/format/unique`
/// - `arg_type(array)/format/min_length`
/// - `arg_type(array)/format/max_length`
/// - `arg_type(any_type)`
pub trait ArgTemplate {
    fn render<'a>(&self, arg: &CrsArg, term_width: Option<usize>) -> Vec<StyledString>;
}
