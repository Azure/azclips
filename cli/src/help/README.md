# Help Display

## Arg Help Design

```text
# Usage
Argument
    Arg Name - [Type] : [Short Help]
                        Default: [default]
    [Options] [Options]
    [Tag] [Tag]
        [Long Help]

[Type] - [Format]
    Enum
        [Enum Key] : [Enum Value]
    Protocol : XXX

Props - [Group]

Additional Props = [Group]

# Body
[Sub Level] (In Arg/Cmd Group, the align is the same)
    [Short Options(one per line)] [Long Options(one per line) or Arg Name] [Tags(one per line)] [:] [Short Help]
        [More Information]

# Example
[Examples]
    [Example Description]
        [Example Command]

# PostHelp
[Global Comment(az find)]
```

## Service Properties

- `name`
- `uri`
- `level(enum)`
- `help/short`
- `help/long`
- `level/version`
- `level/groups(interface)`
- `level/commands(interface)`

## Group Properties

- `name`
- `uri`
- `level(enum)`
- `help/short`
- `help/long`
- `level/groups(interface)`
- `level/commands(interface)`

## Command Properties

- `name`
- `uri`
- `level(enum)`
- `help/short`
- `help/long`
- `level/version`
- `level/args(CmdArg)`
- `level/positional_args(string)`
- `level/args/var_name`
- `level/args/name`
- `level/args/options(string)`
- `level/args/group`
- `level/args/internal`
- `level/args/required`
- `level/args/nullable`
- `level/args/blank`
- `level/args/blank/value`
- `level/args/default`
- `level/args/default/value`
- `level/args/help(enum)`
- `level/args/help/short`
- `level/args/help/long`
- `level/args/prompt`
- `level/args/prompt/prompt`
- `level/args/prompt/secret`
- `level/args/prompt/confirm`
- `level/args/arg_type(enum)`
- `level/args/arg_type(cls)/cls_type`
- `level/args/arg_type(str)/r#enum`
- `level/args/arg_type(str)/r#enum/items`
- `level/args/arg_type(str)/r#enum/items/name`
- `level/args/arg_type(str)/r#enum/items/value`
- `level/args/arg_type(str)/r#enum/items/internal`
- `level/args/arg_type(str)/r#enum/support_extension`
- `level/args/arg_type(str)/r#enum/case_sensitive`
- `level/args/arg_type(str)/format(enum)`
- `level/args/arg_type(str)/format(binary)`
- `level/args/arg_type(str)/format(byte)`
- `level/args/arg_type(str)/format(string)/pattern`
- `level/args/arg_type(str)/format(string)/max_length`
- `level/args/arg_type(str)/format(string)/min_length`
- `level/args/arg_type(str)/format(duration)/protocol`
- `level/args/arg_type(str)/format(date)/protocol`
- `level/args/arg_type(str)/format(datetime)/protocol`
- `level/args/arg_type(str)/format(datetime)/ms_length`
- `level/args/arg_type(str)/format(time)/protocol`
- `level/args/arg_type(str)/format(time)/ms_length`
- `level/args/arg_type(str)/format(uuid)/case`
- `level/args/arg_type(str)/format(uuid)/no_hyphen`
- `level/args/arg_type(str)/format(uuid)/with_braces`
- `level/args/arg_type(str)/format(password)`
- `level/args/arg_type(str)/format(subscription_id)`
- `level/args/arg_type(str)/format(resource_group_name)`
- `level/args/arg_type(str)/format(resource_id)/templates`
- `level/args/arg_type(str)/format(resource_location)/no_rg_default`
- `level/args/arg_type(number)/r#enum`
- `level/args/arg_type(number)/r#enum/items`
- `level/args/arg_type(number)/r#enum/items/name`
- `level/args/arg_type(number)/r#enum/items/value`
- `level/args/arg_type(number)/r#enum/items/internal`
- `level/args/arg_type(number)/format(enum)`
- `level/args/arg_type(number)/format(i32)/minimum`
- `level/args/arg_type(number)/format(i32)/maximum`
- `level/args/arg_type(number)/format(i32)/multiple_of`
- `level/args/arg_type(number)/format(i64)/minimum`
- `level/args/arg_type(number)/format(i64)/maximum`
- `level/args/arg_type(number)/format(i64)/multiple_of`
- `level/args/arg_type(number)/format(u32)/minimum`
- `level/args/arg_type(number)/format(u32)/maximum`
- `level/args/arg_type(number)/format(u32)/multiple_of`
- `level/args/arg_type(number)/format(u64)/minimum`
- `level/args/arg_type(number)/format(u64)/maximum`
- `level/args/arg_type(number)/format(u64)/multiple_of`
- `level/args/arg_type(number)/format(float)/minimum`
- `level/args/arg_type(number)/format(float)/maximum`
- `level/args/arg_type(number)/format(float)/exclusive_minimum`
- `level/args/arg_type(number)/format(float)/exclusive_maximum`
- `level/args/arg_type(number)/format(float)/multiple_of`
- `level/args/arg_type(number)/format(double)/minimum`
- `level/args/arg_type(number)/format(double)/maximum`
- `level/args/arg_type(number)/format(double)/exclusive_minimum`
- `level/args/arg_type(number)/format(double)/exclusive_maximum`
- `level/args/arg_type(number)/format(double)/multiple_of`
- `level/args/arg_type(boolean)/reverse_options(vec<str>)`
- `level/args/arg_type(object)/cls_type`
- `level/args/arg_type(object)/ps_flatten`
- `level/args/arg_type(object)/format`
- `level/args/arg_type(object)/format/min_length`
- `level/args/arg_type(object)/format/max_length`
- `level/args/arg_type(object)/props(args)`
- `level/args/arg_type(object)/additional_props`
- `level/args/arg_type(object)/additional_props/item(arg)`
- `level/args/arg_type(array)/cls_type`
- `level/args/arg_type(array)/item(arg)`
- `level/args/arg_type(array)/format`
- `level/args/arg_type(array)/format/unique`
- `level/args/arg_type(array)/format/min_length`
- `level/args/arg_type(array)/format/max_length`
- `level/args/arg_type(any_type)`

## TODO

- Global Arguments
