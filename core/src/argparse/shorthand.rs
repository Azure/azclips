use std::str::Chars;
use std::sync::LazyLock;

use base64::prelude::BASE64_STANDARD;
use base64::Engine;
use regex::Regex;
use serde_json::{Map, Value};
use tracing::debug;

use crate::error::{AzError, AzResult, AzUserUsageError};
use crate::pb::{cmd_arg, cmd_string_arg};

use super::arg::CmdArgWrapper;
use super::partial::{SHPartialKeyPart, SHPartialKeys};

pub(crate) struct ShorthandSyntaxParser {
    null_expr: &'static str,
    help_expr: &'static str,
    single_quote_char: char,
    double_quote_char: char,
    quote_escape_char: char,
    dict_start_char: char,
    dict_end_char: char,
    list_start_char: char,
    list_end_char: char,
    key_value_separator: char,
    prop_name_separator: char,
    item_separator: char,
    file_uri_prefix: Option<&'static str>,
    partial_value_key_pattern: Regex,
}

pub(crate) static SHORTHAND_SYNTAX_PARSER: LazyLock<ShorthandSyntaxParser> =
    LazyLock::new(ShorthandSyntaxParser::default);

/// The regex pattern for the partial value key, which is used to parse the partial value key from the input string.
const PARTIAL_VALUE_KEY_PATTERN: &'static str = concat!(
    "^",                               // start
    "(",                               // start of capture group 1 - key path
    "(?:",                             //
    "(?:",                             // non-capturing group - first key
    "(?:[a-zA-Z0-9_\\-]+)",            // normal identifier
    "|",                               // or
    "(?:'(?:[^']*)'(?:/(?:[^']*)')*)", // quoted key
    "|",                               // or
    "(?:\\[(?:[0-9]*)\\])", // array index, in the expression, match the [], but raise error when part key, the index support [5]
    ")",
    "(?:\\[(?:[0-9]*)\\])*", // array index, in the expression, match the [], but raise error when part key, the index support [5]
    "(?:",                   // non-capturing group - subsequent keys
    "\\.",                   // dot separator
    "(?:",                   // non-capturing group
    "(?:[a-zA-Z0-9_\\-]*)", // normal identifier, in the expression, match the ., but raise error when part key
    "|",                    // or
    "(?:'(?:[^']*)'(?:/(?:[^']*)')*)", // quoted key
    ")",                    // end of capture group 2
    "(?:\\[(?:[0-9]*)\\])*", // array index, in the expression, match the [], but raise error when part key, the index support [5]
    ")*",                    // repeat zero or more times
    "(?:\\+)?", // the tailing `+` means extend the array which support assign express like `array+=[1,2,3]` or "a.array+=[1,2,3]" or "matrix[0]+=[1,2,3]"
    ")",        // end of capture group 1
    "|",
    "(?:\\+)", // the tailing `+` means extend the array which support assign express like `+=[1,2,3]`
    ")",       // end of capture group 1
    "=",       // equals separator
    "(.*)",    // capture group 2 - value part
    "$"        // end
);

impl Default for ShorthandSyntaxParser {
    fn default() -> Self {
        ShorthandSyntaxParser {
            null_expr: "null",
            help_expr: "??",
            single_quote_char: '\'',
            double_quote_char: '"',
            quote_escape_char: '/',
            dict_start_char: '{',
            dict_end_char: '}',
            list_start_char: '[',
            list_end_char: ']',
            key_value_separator: ':',
            prop_name_separator: '.',
            item_separator: ',',
            file_uri_prefix: Some("file://"),
            partial_value_key_pattern: Regex::new(PARTIAL_VALUE_KEY_PATTERN).unwrap(),
        }
    }
}

/// TODO: using config or env to disable the file uri parse
impl ShorthandSyntaxParser {
    pub(crate) fn disable_file_uri_parse(&mut self) {
        self.file_uri_prefix = None;
    }

    pub(crate) fn enable_file_uri_parse(&mut self) {
        self.file_uri_prefix = Some("file://");
    }
}

/// The result of the shorthand syntax parser, which is used to parse the shorthand syntax.
#[derive(Debug, PartialEq)]
pub(crate) enum SHResult {
    /// contains the index keys for the help message in reverse order, should convert to the normal order in the caller
    Help(SHPartialKeys),
    Value(Value),
}

impl ShorthandSyntaxParser {
    pub(super) fn parse(&self, value: &str, arg: Option<CmdArgWrapper>) -> AzResult<SHResult> {
        let mut remain = value.chars();
        let (result, len) = self.parse_value(&mut remain, arg, 0)?;
        if remain.as_str().len() > 0 {
            Err(AzUserUsageError::InvalidShorthandSyntaxError {
                msg: "Redundant tail".to_string(),
                data: value.to_string(),
                at: len,
                range: remain.count(),
            }
            .into())
        } else {
            Ok(result)
        }
    }

    fn parse_value(
        &self,
        remain: &mut Chars<'_>,
        arg: Option<CmdArgWrapper>,
        depth: usize,
    ) -> AzResult<(SHResult, usize)> {
        let str = remain.as_str();

        if arg.is_none() {
            return if str.starts_with(self.dict_start_char) {
                self.parse_dict(remain, None, depth)
            } else if str.starts_with(self.list_start_char) {
                self.parse_list(remain, None, depth)
            } else {
                // by string
                let (result, len) = self.parse_string(remain, true, depth > 0)?;
                let mut value = match result {
                    Some(v) => v,
                    None => {
                        return Ok((SHResult::Help(SHPartialKeys::default()), len));
                    }
                };
                if let Some(v_str) = value.as_str() {
                    if self
                        .file_uri_prefix
                        .map_or(false, |prefix| v_str.starts_with(prefix))
                    {
                        if let Some(file_value) = self.parse_file_uri(v_str, None)? {
                            value = file_value;
                        }
                    }
                }
                Ok((SHResult::Value(value), len))
            };
        }

        let arg = arg.unwrap();

        let arg_type = arg.unwrapped_arg_type();

        match arg_type {
            cmd_arg::ArgType::AnyType(_) => {
                if str.starts_with(self.dict_start_char) {
                    match self.parse_dict(remain, None, depth)? {
                        // convert help prefix keys for any type inner, usually the returned keys from inner parse should be empty
                        (SHResult::Help(keys), len) => Ok((
                            SHResult::Help(arg.prefix_keys().with_extend(keys.parts())),
                            len,
                        )),
                        (result, len) => Ok((result, len)),
                    }
                } else if str.starts_with(self.list_start_char) {
                    match self.parse_list(remain, None, depth)? {
                        // convert help prefix keys for any type inner, usually the returned keys from inner parse should be empty
                        (SHResult::Help(keys), len) => Ok((
                            SHResult::Help(arg.prefix_keys().with_extend(keys.parts())),
                            len,
                        )),
                        (result, len) => Ok((result, len)),
                    }
                } else {
                    // by string
                    let (result, len) = self.parse_string(remain, true, depth > 0)?;
                    let mut value = match result {
                        Some(v) => v,
                        None => {
                            return Ok((SHResult::Help(arg.prefix_keys().to_owned()), len));
                        }
                    };
                    if let Some(v_str) = value.as_str() {
                        if self
                            .file_uri_prefix
                            .map_or(false, |prefix| v_str.starts_with(prefix))
                        {
                            if let Some(file_value) = self.parse_file_uri(v_str, None)? {
                                value = file_value;
                            }
                        }
                    }
                    let value = arg.verify_value(value).map_err(|e| {
                        if let AzError::UserUsage(e) = e {
                            AzUserUsageError::InvalidShorthandSyntaxError {
                                msg: e.to_string(),
                                data: str.to_string(),
                                at: 0,
                                range: str.len(),
                            }
                            .into()
                        } else {
                            e
                        }
                    })?;
                    Ok((SHResult::Value(value), len))
                }
            }
            cmd_arg::ArgType::Number(_) | cmd_arg::ArgType::Boolean(_) => {
                let (result, len) = self.parse_string(remain, true, depth > 0)?;
                let mut value = match result {
                    Some(v) => v,
                    None => {
                        return Ok((SHResult::Help(arg.prefix_keys().to_owned()), len));
                    }
                };
                if let Some(v_str) = value.as_str() {
                    if self
                        .file_uri_prefix
                        .map_or(false, |prefix| v_str.starts_with(prefix))
                    {
                        if let Some(file_value) = self.parse_file_uri(v_str, None)? {
                            value = file_value;
                        }
                    }
                }

                let value = arg.verify_value(value).map_err(|e| {
                    if let AzError::UserUsage(e) = e {
                        AzUserUsageError::InvalidShorthandSyntaxError {
                            msg: e.to_string(),
                            data: str.to_string(),
                            at: 0,
                            range: str.len(),
                        }
                        .into()
                    } else {
                        e
                    }
                })?;
                Ok((SHResult::Value(value), len))
            }
            cmd_arg::ArgType::Str(str_type) => {
                let (result, len) = self.parse_string(remain, false, depth > 0)?;
                let mut value = match result {
                    Some(v) => v,
                    None => {
                        return Ok((SHResult::Help(arg.prefix_keys().to_owned()), len));
                    }
                };
                if let Some(v_str) = value.as_str() {
                    if self
                        .file_uri_prefix
                        .map_or(false, |prefix| v_str.starts_with(prefix))
                    {
                        if let Some(file_value) =
                            self.parse_file_uri(v_str, str_type.format.as_ref())?
                        {
                            value = file_value;
                        }
                    }
                }
                let value = arg.verify_value(value).map_err(|e| {
                    if let AzError::UserUsage(e) = e {
                        AzUserUsageError::InvalidShorthandSyntaxError {
                            msg: e.to_string(),
                            data: str.to_string(),
                            at: 0,
                            range: str.len(),
                        }
                        .into()
                    } else {
                        e
                    }
                })?;
                Ok((SHResult::Value(value), len))
            }
            cmd_arg::ArgType::Object(_) => {
                if str.starts_with(self.dict_start_char) {
                    self.parse_dict(remain, Some(arg), depth)
                } else if str.starts_with(self.list_start_char) {
                    return Err(AzUserUsageError::InvalidShorthandSyntaxError {
                        msg: arg.invalid_value_type().err().unwrap().to_string(),
                        data: str.to_string(),
                        at: 0,
                        range: 1,
                    }
                    .into());
                } else {
                    // by string
                    let (result, len) = self.parse_string(remain, true, depth > 0)?;
                    let mut value = match result {
                        Some(v) => v,
                        None => {
                            return Ok((SHResult::Help(arg.prefix_keys().to_owned()), len));
                        }
                    };
                    if let Some(v_str) = value.as_str() {
                        if self
                            .file_uri_prefix
                            .map_or(false, |prefix| v_str.starts_with(prefix))
                        {
                            if let Some(file_value) = self.parse_file_uri(v_str, None)? {
                                value = file_value;
                            }
                        }
                    }

                    let value = arg.verify_value(value).map_err(|e| {
                        if let AzError::UserUsage(e) = e {
                            AzUserUsageError::InvalidShorthandSyntaxError {
                                msg: e.to_string(),
                                data: str.to_string(),
                                at: 0,
                                range: str.len(),
                            }
                            .into()
                        } else {
                            e
                        }
                    })?;
                    Ok((SHResult::Value(value), len))
                }
            }
            cmd_arg::ArgType::Array(_) => {
                if str.starts_with(self.dict_start_char) {
                    return Err(AzUserUsageError::InvalidShorthandSyntaxError {
                        msg: arg.invalid_value_type().err().unwrap().to_string(),
                        data: str.to_string(),
                        at: 0,
                        range: 1,
                    }
                    .into());
                } else if str.starts_with(self.list_start_char) {
                    self.parse_list(remain, Some(arg), depth)
                } else {
                    // by string
                    let (result, len) = self.parse_string(remain, true, depth > 0)?;
                    let mut value = match result {
                        Some(v) => v,
                        None => {
                            return Ok((SHResult::Help(arg.prefix_keys().to_owned()), len));
                        }
                    };

                    if let Some(v_str) = value.as_str() {
                        if self
                            .file_uri_prefix
                            .map_or(false, |prefix| v_str.starts_with(prefix))
                        {
                            if let Some(file_value) = self.parse_file_uri(v_str, None)? {
                                value = file_value;
                            }
                        }
                    }

                    let value = arg.verify_value(value).map_err(|e| {
                        if let AzError::UserUsage(e) = e {
                            AzUserUsageError::InvalidShorthandSyntaxError {
                                msg: e.to_string(),
                                data: str.to_string(),
                                at: 0,
                                range: str.len(),
                            }
                            .into()
                        } else {
                            e
                        }
                    })?;
                    Ok((SHResult::Value(value), len))
                }
            }
            cmd_arg::ArgType::Cls(_) => {
                unreachable!()
            }
        }
    }

    /// Parse from `json` or `yaml` format file and return the value
    /// if the file format is not `json` or `yaml`, it will directly return None
    /// if not enable the uri parse, it will directly return None as well
    /// Raise the error if failed to parse the file
    fn parse_file_uri(
        &self,
        uri: &str,
        str_format: Option<&cmd_string_arg::Format>,
    ) -> Result<Option<Value>, AzUserUsageError> {
        if self.file_uri_prefix.is_none() {
            return Ok(None);
        }
        let prefix = self.file_uri_prefix.unwrap();

        assert!(uri.starts_with(prefix));
        let file_path = uri.strip_prefix(prefix).unwrap();
        if file_path.ends_with(".json") {
            debug!("Start parse json file {}", file_path);
            match ShorthandSyntaxParser::read_json_file_to_value(file_path) {
                Ok(v) => Ok(Some(v)),
                Err(AzUserUsageError::InvalidShorthandSyntaxError {
                    msg,
                    data: _,
                    at: _,
                    range: _,
                }) => Err(AzUserUsageError::InvalidShorthandSyntaxError {
                    msg,
                    data: uri.to_string(),
                    at: 0,
                    range: uri.len(),
                }),
                Err(e) => Err(e),
            }
        } else if file_path.ends_with(".yaml") || file_path.ends_with(".yml") {
            debug!("Start parse yaml file {}", file_path);
            match ShorthandSyntaxParser::read_yaml_file_to_value(file_path) {
                Ok(v) => Ok(Some(v)),
                Err(AzUserUsageError::InvalidShorthandSyntaxError {
                    msg,
                    data: _,
                    at: _,
                    range: _,
                }) => Err(AzUserUsageError::InvalidShorthandSyntaxError {
                    msg,
                    data: uri.to_string(),
                    at: 0,
                    range: uri.len(),
                }),
                Err(e) => Err(e),
            }
        } else {
            match str_format {
                Some(cmd_string_arg::Format::Binary(_)) => {
                    debug!("Convert to binary file path {}", file_path);
                    // for binary format data, it return file path as value
                    // we should not read any binary files because those files can be huge,
                    // they should be handled in the command logic.
                    Ok(Some(Value::String(file_path.to_string())))
                }
                Some(cmd_string_arg::Format::Byte(_)) => {
                    debug!("Read file content as base64 encoded string {}", file_path);
                    // the value of bytes are base64 encoded string
                    let content = ShorthandSyntaxParser::read_file_content_as_bytes(file_path)?;
                    Ok(Some(content))
                }
                Some(_) => {
                    debug!("Read file content as text {}", file_path);
                    // try to read the raw file content
                    let content = ShorthandSyntaxParser::read_file_content_as_str(file_path)?;
                    Ok(Some(content))
                }
                None => Ok(None),
            }
        }
    }

    fn parse_dict(
        &self,
        remain: &mut Chars<'_>,
        arg: Option<CmdArgWrapper>,
        depth: usize,
    ) -> AzResult<(SHResult, usize)> {
        let data = remain.as_str();
        let c = remain.next().unwrap();
        assert!(c == self.dict_start_char);
        let mut idx = 1;
        let mut str = remain.as_str();
        let mut result = Map::new();

        while str.len() > 0 {
            if str.starts_with(self.dict_end_char) {
                _ = remain.next().unwrap();
                idx += 1;
                return Ok((SHResult::Value(Value::Object(result)), idx));
            }

            let (k, len) = self
                .parse_string(remain, false, true)
                .map_err(|e| match e {
                    AzUserUsageError::InvalidShorthandSyntaxError {
                        msg,
                        data: _,
                        at,
                        range,
                    } => AzUserUsageError::InvalidShorthandSyntaxError {
                        msg,
                        data: data.to_string(),
                        at: at + idx,
                        range,
                    },
                    e => e,
                })?;

            let k = match k {
                Some(value) => value.as_str().unwrap().to_string(),
                None => {
                    return Ok((
                        SHResult::Help(
                            arg.map_or(SHPartialKeys::default(), |a| a.prefix_keys().to_owned()),
                        ),
                        idx,
                    ));
                }
            };

            if result.contains_key(&k) {
                return Err(AzUserUsageError::InvalidShorthandSyntaxError {
                    msg: format!("Duplicate defined dict key '{}'", k),
                    data: data.to_string(),
                    at: idx,
                    range: len,
                }
                .into());
            }

            idx += len;
            str = remain.as_str();

            if !str.starts_with(self.key_value_separator) {
                return Err(AzUserUsageError::InvalidShorthandSyntaxError {
                    msg: "Expect character ':'".to_string(),
                    data: data.to_string(),
                    at: idx,
                    range: 1,
                }
                .into());
            }
            idx += 1;
            _ = remain.next().unwrap();
            str = remain.as_str();

            if str.len() == 0 {
                return Err(AzUserUsageError::InvalidShorthandSyntaxError {
                    msg: "Cannot parse empty".to_string(),
                    data: data.to_string(),
                    at: idx,
                    range: 1,
                }
                .into());
            }

            let key = SHPartialKeyPart::parse_name(&k)?;
            let sub_arg = arg
                .as_ref()
                .map(|a| a.retrieve_sub_arg([&key]))
                .transpose()?;

            let (v, len) = self
                .parse_value(remain, sub_arg, depth + 1)
                .map_err(|e| match e {
                    AzError::UserUsage(AzUserUsageError::InvalidShorthandSyntaxError {
                        msg,
                        data: _,
                        at,
                        range,
                    }) => AzUserUsageError::InvalidShorthandSyntaxError {
                        msg,
                        data: data.to_string(),
                        at: at + idx,
                        range,
                    }
                    .into(),
                    e => e,
                })?;

            match v {
                SHResult::Help(mut keys) => {
                    keys.push(key);
                    return Ok((SHResult::Help(keys), idx));
                }
                SHResult::Value(value) => {
                    result.insert(k, value);
                    idx += len;
                    str = remain.as_str();
                }
            };

            if str.starts_with(self.item_separator) {
                _ = remain.next().unwrap();
                idx += 1;
                str = remain.as_str();
            } else if !str.starts_with(self.dict_end_char) {
                return Err(AzUserUsageError::InvalidShorthandSyntaxError {
                    msg: "Expect character ',' or '}'".to_string(),
                    data: data.to_string(),
                    at: idx,
                    range: 1,
                }
                .into());
            }
        }

        Err(AzUserUsageError::InvalidShorthandSyntaxError {
            msg: "Expect character '}'".to_string(),
            data: data.to_string(),
            at: idx,
            range: 1,
        }
        .into())
    }

    fn parse_list(
        &self,
        remain: &mut Chars<'_>,
        arr_arg: Option<CmdArgWrapper>,
        depth: usize,
    ) -> AzResult<(SHResult, usize)> {
        let data = remain.as_str();
        let c = remain.next().unwrap();
        assert!(c == self.list_start_char);
        let mut idx = 1;
        let mut str = remain.as_str();
        let mut result = Vec::new();

        while str.len() > 0 {
            if str.starts_with(self.list_end_char) {
                _ = remain.next().unwrap();
                idx += 1;
                return Ok((SHResult::Value(Value::Array(result)), idx));
            }

            let key = SHPartialKeyPart::Index(result.len() as u16);

            let item_arg = arr_arg
                .as_ref()
                .map(|a| a.retrieve_sub_arg([&key]))
                .transpose()?;

            let (v, len) = self
                .parse_value(remain, item_arg, depth + 1)
                .map_err(|e| match e {
                    AzError::UserUsage(AzUserUsageError::InvalidShorthandSyntaxError {
                        msg,
                        data: _,
                        at,
                        range,
                    }) => AzUserUsageError::InvalidShorthandSyntaxError {
                        msg,
                        data: data.to_string(),
                        at: at + idx,
                        range,
                    }
                    .into(),
                    e => e,
                })?;
            match v {
                SHResult::Help(mut keys) => {
                    keys.push(key);
                    return Ok((SHResult::Help(keys), idx));
                }
                SHResult::Value(value) => {
                    result.push(value);
                    idx += len;
                    str = remain.as_str();
                }
            }
            if str.starts_with(self.item_separator) {
                _ = remain.next().unwrap();
                idx += 1;
                str = remain.as_str();
            } else if !str.starts_with(self.list_end_char) {
                return Err(AzUserUsageError::InvalidShorthandSyntaxError {
                    msg: "Expect character ',' or ']'".to_string(),
                    data: data.to_string(),
                    at: idx,
                    range: 1,
                }
                .into());
            }
        }

        Err(AzUserUsageError::InvalidShorthandSyntaxError {
            msg: "Expect character ']'".to_string(),
            data: data.to_string(),
            at: idx,
            range: 1,
        }
        .into())
    }

    // return None if the string is help expression
    fn parse_string(
        &self,
        remain: &mut Chars<'_>,
        convert_simple_type: bool, // whether to convert the string into the number, boolean type.
        internal: bool, // whether to parse a string in the internal of the shorthand syntax
    ) -> Result<(Option<Value>, usize), AzUserUsageError> {
        let data = remain.as_str();
        let mut str = remain.as_str();
        if str.starts_with(self.single_quote_char) {
            let (result, len) = self.parse_single_quotes_string(remain)?;
            return Ok((Some(Value::String(result)), len));
        }

        let mut idx = 0;
        let mut result = String::new();

        if internal {
            while str.len() > 0 {
                if str.starts_with(self.dict_end_char)
                    || str.starts_with(self.list_end_char)
                    || str.starts_with(self.item_separator)
                    || str.starts_with(self.key_value_separator)
                {
                    break;
                }

                let c = remain.next().unwrap();
                if c.is_whitespace() {
                    return Err(AzUserUsageError::InvalidShorthandSyntaxError {
                        msg: "Failed to parse string, please wrap whitespace character in single quotes".to_string(),
                        data: data.to_string(),
                        at: idx,
                        range: 1,
                    });
                }
                if c == self.single_quote_char
                    || c == self.double_quote_char
                    || c == self.dict_start_char
                    || c == self.list_start_char
                {
                    return Err(AzUserUsageError::InvalidShorthandSyntaxError {
                        msg: format!(
                            "Failed to parse string, please wrap '{}' character in single quotes",
                            c
                        ),
                        data: data.to_string(),
                        at: idx,
                        range: 1,
                    });
                }

                idx += 1;
                result.push(c);
                str = remain.as_str();
            }
        } else {
            result.reserve(str.len());
            while str.len() > 0 {
                let c = remain.next().unwrap();
                idx += 1;
                result.push(c);
                str = remain.as_str();
            }
        }

        if idx == 0 {
            return Err(AzUserUsageError::InvalidShorthandSyntaxError {
                msg: "Failed to parse string, please use '' wrapped empty string".to_string(),
                data: data.to_string(),
                at: idx,
                range: 1,
            });
        }

        if result == self.null_expr {
            Ok((Some(Value::Null), idx))
        } else if result == self.help_expr {
            Ok((None, idx))
        } else if convert_simple_type {
            match serde_json::from_str::<Value>(&result) {
                Ok(Value::Bool(v)) => Ok((Some(Value::Bool(v)), idx)),
                Ok(Value::Number(v)) => Ok((Some(Value::Number(v)), idx)),
                _ => Ok((Some(Value::String(result)), idx)),
            }
        } else {
            Ok((Some(Value::String(result)), idx))
        }
    }

    fn parse_single_quotes_string(
        &self,
        remain: &mut Chars<'_>,
    ) -> Result<(String, usize), AzUserUsageError> {
        let data = remain.as_str();
        assert!(remain.next().unwrap() == self.single_quote_char);
        let mut idx = 1;
        let mut result: String = String::new();
        while let Some(c) = remain.next() {
            if c == self.single_quote_char {
                let str = remain.as_str();
                if str.starts_with(self.quote_escape_char) {
                    // the quote is escaped, so not end of the quote
                    _ = remain.next().unwrap();
                    result.push(c);
                    idx += 2;
                } else {
                    idx += 1;
                    return Ok((result, idx));
                }
            } else {
                result.push(c);
                idx += 1;
            }
        }

        Err(AzUserUsageError::InvalidShorthandSyntaxError {
            msg: format!(
                "Miss the end single quote character: {}",
                self.single_quote_char
            ),
            data: data.to_string(),
            at: idx,
            range: 1,
        })
    }
}

impl ShorthandSyntaxParser {
    /// split the partial value into the key and the value
    /// when the value is empty, return None to represent it's a blank value
    pub(super) fn split_partial_value<'a>(
        &self,
        value: &'a str,
    ) -> Option<(&'a str, Option<&'a str>)> {
        if let Some(captures) = self.partial_value_key_pattern.captures(value) {
            let key = captures.get(1).unwrap().as_str();
            let value = captures.get(2).unwrap().as_str();
            if value.is_empty() {
                // the value is empty, so it is a blank value
                Some((key, None))
            } else {
                Some((key, Some(value)))
            }
        } else {
            None
        }
    }

    pub(super) fn parse_partial_value_key_parts(
        &self,
        mut key: &str,
    ) -> Result<(SHPartialKeys, bool), AzUserUsageError> {
        let is_append = key.ends_with('+');
        if is_append {
            key = &key[..key.len() - 1];
        }

        let mut keys = SHPartialKeys::default();
        let mut remain = key.chars();
        let mut idx = 0;
        let mut str = remain.as_str();
        while str.len() > 0 {
            if str.starts_with(self.list_start_char) {
                match self.parse_partial_value_idx_key(&mut remain) {
                    Ok((part, len)) => {
                        keys.push(part);
                        idx += len;
                        str = remain.as_str();
                    }
                    Err(AzUserUsageError::InvalidShorthandSyntaxError {
                        msg,
                        data: _,
                        at,
                        range,
                    }) => {
                        return Err(AzUserUsageError::InvalidShorthandSyntaxError {
                            msg,
                            data: key.to_string(),
                            at: at + idx,
                            range,
                        })
                    }
                    Err(e) => return Err(e),
                }
            } else {
                match self.parse_partial_value_prop_key(&mut remain) {
                    Ok((part, len)) => {
                        keys.push(part);
                        idx += len;
                        str = remain.as_str();
                    }
                    Err(AzUserUsageError::InvalidShorthandSyntaxError {
                        msg,
                        data: _,
                        at,
                        range,
                    }) => {
                        return Err(AzUserUsageError::InvalidShorthandSyntaxError {
                            msg,
                            data: key.to_string(),
                            at: at + idx,
                            range,
                        })
                    }
                    Err(e) => return Err(e),
                }
            }
        }

        Ok((keys, is_append))
    }

    fn parse_partial_value_idx_key(
        &self,
        remain: &mut Chars<'_>,
    ) -> Result<(SHPartialKeyPart, usize), AzUserUsageError> {
        let data = remain.as_str();
        assert_eq!(remain.next().unwrap(), self.list_start_char);
        let mut str = remain.as_str();
        let mut idx = 1;

        let mut part = String::new();
        while str.len() > 0 {
            if str.starts_with(self.list_end_char) || str.starts_with(self.prop_name_separator) {
                break;
            }
            part.push(remain.next().unwrap());
            idx += 1;
            str = remain.as_str();
        }

        if !str.starts_with(self.list_end_char) {
            return Err(AzUserUsageError::InvalidShorthandSyntaxError {
                msg: "Expect character ']'".to_string(),
                data: data.to_string(),
                at: idx,
                range: 1,
            });
        }
        _ = remain.next().unwrap();
        // str = remain.as_str();
        idx += 1;
        if part.is_empty() {
            return Err(AzUserUsageError::InvalidShorthandSyntaxError {
                msg: "Miss prop name".to_string(),
                data: data.to_string(),
                at: 0,
                range: idx,
            });
        }
        match SHPartialKeyPart::parse_index(&part) {
            Ok(part) => Ok((part, idx)),
            Err(AzUserUsageError::InvalidShorthandSyntaxError {
                msg,
                data: _,
                at: _,
                range: _,
            }) => Err(AzUserUsageError::InvalidShorthandSyntaxError {
                msg,
                data: data.to_string(),
                at: 0,
                range: idx,
            }),
            Err(e) => Err(e),
        }
    }

    fn parse_partial_value_prop_key(
        &self,
        remain: &mut Chars<'_>,
    ) -> Result<(SHPartialKeyPart, usize), AzUserUsageError> {
        let data = remain.as_str();
        let mut str = remain.as_str();
        let mut idx = 0;
        if str.starts_with(self.prop_name_separator) {
            _ = remain.next().unwrap();
            idx += 1;
            str = remain.as_str();
        }
        let part = if str.starts_with(self.single_quote_char) {
            let (part, len) = self.parse_single_quotes_string(remain)?;
            idx += len;
            // str = remain.as_str();
            part
        } else {
            let mut part = String::new();
            while str.len() > 0 {
                if str.starts_with(self.prop_name_separator)
                    || str.starts_with(self.list_start_char)
                {
                    break;
                }
                part.push(remain.next().unwrap());
                idx += 1;
                str = remain.as_str();
            }
            part
        };

        match SHPartialKeyPart::parse_name(&part) {
            Ok(part) => Ok((part, idx)),
            Err(AzUserUsageError::InvalidShorthandSyntaxError {
                msg,
                data: _,
                at: _,
                range: _,
            }) => Err(AzUserUsageError::InvalidShorthandSyntaxError {
                msg,
                data: data.to_string(),
                at: 0,
                range: idx,
            }),
            Err(e) => Err(e),
        }
    }
}

/// The file value read happens only in the shorthand syntax expression values
impl ShorthandSyntaxParser {
    /// Read the json file, and return the value if the file is a valid serde value.
    fn read_json_file_to_value(file_path: &str) -> Result<Value, AzUserUsageError> {
        assert!(file_path.ends_with(".json"));
        let rdr = std::fs::File::open(file_path).map_err(|e| {
            AzUserUsageError::InvalidShorthandSyntaxError {
                msg: format!("Failed to open json file: {}", e),
                data: file_path.to_string(),
                at: 0,
                range: file_path.len(),
            }
        })?;
        serde_json::from_reader(rdr).map_err(|e| AzUserUsageError::InvalidShorthandSyntaxError {
            msg: format!("Failed to parse json file: {}", e),
            data: file_path.to_string(),
            at: 0,
            range: file_path.len(),
        })
    }

    /// Read the yaml file, and return the value if the file is a valid serde value.
    /// TODO: not support yaml file yet, the serde_yaml is not maintained,
    /// the https://github.com/Ethiraric/yaml-rust2 is not compatible with the serde
    /// need the additional wrapper to make it compatible with the serde
    fn read_yaml_file_to_value(file_path: &str) -> Result<Value, AzUserUsageError> {
        assert!(file_path.ends_with(".yaml") || file_path.ends_with(".yml"));
        let _ = std::fs::read_to_string(file_path).map_err(|e| {
            AzUserUsageError::InvalidShorthandSyntaxError {
                msg: format!("Failed to read yaml file: {}", e),
                data: file_path.to_string(),
                at: 0,
                range: file_path.len(),
            }
        })?;

        return Err(AzUserUsageError::InvalidShorthandSyntaxError {
            msg: "yaml file input is not supported yet, please use json file instead".to_string(),
            data: file_path.to_string(),
            at: 0,
            range: file_path.len(),
        });
    }

    fn read_file_content_as_str(file_path: &str) -> Result<Value, AzUserUsageError> {
        let content = std::fs::read_to_string(file_path).map_err(|e| {
            AzUserUsageError::InvalidShorthandSyntaxError {
                msg: format!("Failed to read file: {}", e),
                data: file_path.to_string(),
                at: 0,
                range: file_path.len(),
            }
        })?;
        Ok(Value::String(content))
    }

    fn read_file_content_as_bytes(file_path: &str) -> Result<Value, AzUserUsageError> {
        let content = std::fs::read(file_path).map_err(|e| {
            AzUserUsageError::InvalidShorthandSyntaxError {
                msg: format!("Failed to read file: {}", e),
                data: file_path.to_string(),
                at: 0,
                range: file_path.len(),
            }
        })?;

        let trimmed_content = content.trim_ascii();

        // Check if the content is already a base64 encoded string
        let encoded = if let Ok(_) = BASE64_STANDARD.decode(trimmed_content) {
            String::from_utf8(trimmed_content.to_vec()).unwrap()
        } else {
            BASE64_STANDARD.encode(&content)
        };

        // Otherwise, encode the content to base64 and return
        Ok(Value::String(encoded))
    }
}

#[cfg(test)]
mod tests {
    use rand::Rng;
    use serde_json::json;
    use std::io::Write;
    use tempfile::NamedTempFile;

    use crate::pb::{
        cmd_number_arg, cmd_string_arg, CmdAnyTypeArg, CmdArg, CmdArgEnum, CmdArgEnumItem,
        CmdArrayArg, CmdBinaryFormat, CmdBooleanArg, CmdByteFormat, CmdCommand, CmdInt32Format,
        CmdNumberArg, CmdObjectArg, CmdObjectArgAdditionalProperties, CmdStringArg,
        CmdStringFormat,
    };

    use super::*;

    #[derive(Debug)]
    struct ErrorData(&'static str, &'static str, usize, usize);

    #[test]
    fn test_parse_null_without_arg() {
        let parser = ShorthandSyntaxParser::default();
        for (arg, value, expected) in vec![
            (None, "null", Ok(Value::Null)),
            (None, "NULL", Ok(Value::String("NULL".to_string()))),
            (None, "Null", Ok(Value::String("Null".to_string()))),
            (None, "'null'", Ok(Value::String("null".to_string()))),
            (None, "'NULL'", Ok(Value::String("NULL".to_string()))),
            (None, "'Null'", Ok(Value::String("Null".to_string()))),
            (None, "null,abc", Ok(Value::String("null,abc".to_string()))),
            // error cases
            (
                None,
                "'null'abc",
                Err(ErrorData("Redundant tail", "'null'abc", 6, 3)),
            ),
        ] {
            match expected {
                Ok(expected) => {
                    let result = parser.parse(value, arg.to_owned()).unwrap();
                    assert_eq!(result, SHResult::Value(expected));
                }
                Err(ErrorData(msg_target, data_target, at_target, range_target)) => {
                    let result = parser.parse(value, arg.to_owned());
                    match result {
                        Err(AzError::UserUsage(
                            AzUserUsageError::InvalidShorthandSyntaxError {
                                msg,
                                data,
                                at,
                                range,
                            },
                        )) => {
                            assert_eq!(
                                (msg, data, at, range),
                                (
                                    msg_target.to_string(),
                                    data_target.to_string(),
                                    at_target,
                                    range_target
                                )
                            );
                        }
                        _ => {
                            panic!("Unexpected result: {:?}", result);
                        }
                    }
                }
            }
        }
    }

    #[test]
    fn test_parse_nullable_arg() {
        let parser = ShorthandSyntaxParser::default();

        let mut cmd = CmdCommand::default();

        // nullable arg
        let mut str_arg = CmdArg::default();
        str_arg.var_name = "$Instance.alias".to_string();
        str_arg.name = "alias".to_string();
        str_arg.nullable = true;
        str_arg.options = vec!["alais".to_string()];
        let mut str_type = CmdStringArg::default();
        str_type.format = Some(cmd_string_arg::Format::String(CmdStringFormat::default()));
        str_arg.arg_type = Some(cmd_arg::ArgType::Str(str_type));
        cmd.args.push(str_arg);
        // Not nullable arg
        let mut str_arg = CmdArg::default();
        str_arg.var_name = "$Path.name".to_string();
        str_arg.name = "name".to_string();
        str_arg.options = vec!["name".to_string(), "n".to_string()];
        let mut str_type = CmdStringArg::default();
        str_type.format = Some(cmd_string_arg::Format::String(CmdStringFormat::default()));
        str_arg.arg_type = Some(cmd_arg::ArgType::Str(str_type));
        cmd.args.push(str_arg);

        let arg1 = Some(CmdArgWrapper::new(Some(&cmd), &cmd.args[0]).unwrap());
        let arg2 = Some(CmdArgWrapper::new(Some(&cmd), &cmd.args[1]).unwrap());

        for (arg, value, expected) in vec![
            (&arg1, "null", Ok(Value::Null)),
            (&arg1, "NULL", Ok(Value::String("NULL".to_string()))),
            (&arg1, "Null", Ok(Value::String("Null".to_string()))),
            (&arg1, "'null'", Ok(Value::String("null".to_string()))),
            (&arg1, "'NULL'", Ok(Value::String("NULL".to_string()))),
            (&arg1, "'Null'", Ok(Value::String("Null".to_string()))),
            (&arg1, "null,abc", Ok(Value::String("null,abc".to_string()))),
            // error cases
            (
                &arg2,
                "null",
                Err(ErrorData(
                    "Invalid argument value: 'name' argument is not allowed to be null",
                    "null",
                    0,
                    4,
                )),
            ),
            (
                &arg1,
                "'null'abc",
                Err(ErrorData("Redundant tail", "'null'abc", 6, 3)),
            ),
        ] {
            match expected {
                Ok(expected) => {
                    let result = parser.parse(value, arg.to_owned()).unwrap();
                    assert_eq!(result, SHResult::Value(expected));
                }
                Err(ErrorData(msg_target, data_target, at_target, range_target)) => {
                    let result = parser.parse(value, arg.to_owned());
                    match result {
                        Err(AzError::UserUsage(
                            AzUserUsageError::InvalidShorthandSyntaxError {
                                msg,
                                data,
                                at,
                                range,
                            },
                        )) => {
                            assert_eq!(
                                (msg, data, at, range),
                                (
                                    msg_target.to_string(),
                                    data_target.to_string(),
                                    at_target,
                                    range_target
                                )
                            );
                        }
                        _ => {
                            panic!("Unexpected result: {:?}", result);
                        }
                    }
                }
            }
        }
    }

    #[test]
    fn test_parse_bool_without_arg() {
        let parser = ShorthandSyntaxParser::default();

        for (arg, value, expected) in vec![
            (None, "false", Ok(Value::Bool(false))),
            (None, "true", Ok(Value::Bool(true))),
            // treat as string
            (None, "'false'", Ok(Value::String("false".to_string()))),
            (None, "'true'", Ok(Value::String("true".to_string()))),
            (None, "F", Ok(Value::String("F".to_string()))),
            (None, "f", Ok(Value::String("f".to_string()))),
            (None, "False", Ok(Value::String("False".to_string()))),
            (None, "FALSE", Ok(Value::String("FALSE".to_string()))),
            (None, "no", Ok(Value::String("no".to_string()))),
            (None, "NO", Ok(Value::String("NO".to_string()))),
            (None, "No", Ok(Value::String("No".to_string()))),
            (None, "n", Ok(Value::String("n".to_string()))),
            (None, "N", Ok(Value::String("N".to_string()))),
            (None, "T", Ok(Value::String("T".to_string()))),
            (None, "t", Ok(Value::String("t".to_string()))),
            (None, "True", Ok(Value::String("True".to_string()))),
            (None, "TRUE", Ok(Value::String("TRUE".to_string()))),
            (None, "yes", Ok(Value::String("yes".to_string()))),
            (None, "YES", Ok(Value::String("YES".to_string()))),
            (None, "Yes", Ok(Value::String("Yes".to_string()))),
            (None, "y", Ok(Value::String("y".to_string()))),
            (None, "Y", Ok(Value::String("Y".to_string()))),
            (None, "'F'", Ok(Value::String("F".to_string()))),
            (None, "'f'", Ok(Value::String("f".to_string()))),
            (None, "'False'", Ok(Value::String("False".to_string()))),
            (None, "'FALSE'", Ok(Value::String("FALSE".to_string()))),
            (None, "'no'", Ok(Value::String("no".to_string()))),
            (None, "'NO'", Ok(Value::String("NO".to_string()))),
            (None, "'No'", Ok(Value::String("No".to_string()))),
            (None, "'n'", Ok(Value::String("n".to_string()))),
            (None, "'N'", Ok(Value::String("N".to_string()))),
            (None, "'T'", Ok(Value::String("T".to_string()))),
            (None, "'t'", Ok(Value::String("t".to_string()))),
            (None, "'True'", Ok(Value::String("True".to_string()))),
            (None, "'TRUE'", Ok(Value::String("TRUE".to_string()))),
            (None, "'yes'", Ok(Value::String("yes".to_string()))),
            (None, "'YES'", Ok(Value::String("YES".to_string()))),
            (None, "'Yes'", Ok(Value::String("Yes".to_string()))),
            (None, "'y'", Ok(Value::String("y".to_string()))),
            (None, "'Y'", Ok(Value::String("Y".to_string()))),
            // the null value
            (None, "null", Ok(Value::Null)),
            // error cases
            (
                None,
                "'true'abc",
                Err(ErrorData("Redundant tail", "'true'abc", 6, 3)),
            ),
        ] {
            match expected {
                Ok(expected) => {
                    let result = parser.parse(value, arg.to_owned()).unwrap();
                    assert_eq!(result, SHResult::Value(expected));
                }
                Err(ErrorData(msg_target, data_target, at_target, range_target)) => {
                    let result = parser.parse(value, arg.to_owned());
                    match result {
                        Err(AzError::UserUsage(
                            AzUserUsageError::InvalidShorthandSyntaxError {
                                msg,
                                data,
                                at,
                                range,
                            },
                        )) => {
                            assert_eq!(
                                (msg, data, at, range),
                                (
                                    msg_target.to_string(),
                                    data_target.to_string(),
                                    at_target,
                                    range_target
                                )
                            );
                        }
                        _ => {
                            panic!("Unexpected result: {:?}", result);
                        }
                    }
                }
            }
        }
    }

    #[test]
    fn test_parse_bool_arg() {
        let parser = ShorthandSyntaxParser::default();

        let mut cmd = CmdCommand::default();

        let mut bool_arg = CmdArg::default();
        bool_arg.var_name = "$Instance.enabled".to_string();
        bool_arg.name = "enabled".to_string();
        bool_arg.options = vec!["enabled".to_string()];
        bool_arg.nullable = true;
        let bool_type = CmdBooleanArg::default();
        bool_arg.arg_type = Some(cmd_arg::ArgType::Boolean(bool_type));
        cmd.args.push(bool_arg);

        // test not nullable arg
        let mut bool_arg = CmdArg::default();
        bool_arg.var_name = "$Instance.unnullable".to_string();
        bool_arg.name = "unnullable".to_string();
        bool_arg.options = vec!["unnullable".to_string()];
        let bool_type = CmdBooleanArg::default();
        bool_arg.arg_type = Some(cmd_arg::ArgType::Boolean(bool_type));
        cmd.args.push(bool_arg);

        let arg1 = Some(CmdArgWrapper::new(Some(&cmd), &cmd.args[0]).unwrap());

        let arg2 = Some(CmdArgWrapper::new(Some(&cmd), &cmd.args[1]).unwrap());

        for (arg, value, expected) in vec![
            (&arg1, "F", Ok(Value::Bool(false))),
            (&arg1, "f", Ok(Value::Bool(false))),
            (&arg1, "false", Ok(Value::Bool(false))),
            (&arg1, "False", Ok(Value::Bool(false))),
            (&arg1, "FALSE", Ok(Value::Bool(false))),
            (&arg1, "no", Ok(Value::Bool(false))),
            (&arg1, "NO", Ok(Value::Bool(false))),
            (&arg1, "No", Ok(Value::Bool(false))),
            (&arg1, "n", Ok(Value::Bool(false))),
            (&arg1, "N", Ok(Value::Bool(false))),
            (&arg1, "T", Ok(Value::Bool(true))),
            (&arg1, "t", Ok(Value::Bool(true))),
            (&arg1, "true", Ok(Value::Bool(true))),
            (&arg1, "True", Ok(Value::Bool(true))),
            (&arg1, "TRUE", Ok(Value::Bool(true))),
            (&arg1, "yes", Ok(Value::Bool(true))),
            (&arg1, "YES", Ok(Value::Bool(true))),
            (&arg1, "Yes", Ok(Value::Bool(true))),
            (&arg1, "y", Ok(Value::Bool(true))),
            (&arg1, "Y", Ok(Value::Bool(true))),
            (&arg1, "'F'", Ok(Value::Bool(false))),
            (&arg1, "'f'", Ok(Value::Bool(false))),
            (&arg1, "'false'", Ok(Value::Bool(false))),
            (&arg1, "'False'", Ok(Value::Bool(false))),
            (&arg1, "'FALSE'", Ok(Value::Bool(false))),
            (&arg1, "'no'", Ok(Value::Bool(false))),
            (&arg1, "'NO'", Ok(Value::Bool(false))),
            (&arg1, "'No'", Ok(Value::Bool(false))),
            (&arg1, "'n'", Ok(Value::Bool(false))),
            (&arg1, "'N'", Ok(Value::Bool(false))),
            (&arg1, "'T'", Ok(Value::Bool(true))),
            (&arg1, "'t'", Ok(Value::Bool(true))),
            (&arg1, "'true'", Ok(Value::Bool(true))),
            (&arg1, "'True'", Ok(Value::Bool(true))),
            (&arg1, "'TRUE'", Ok(Value::Bool(true))),
            (&arg1, "'yes'", Ok(Value::Bool(true))),
            (&arg1, "'YES'", Ok(Value::Bool(true))),
            (&arg1, "'Yes'", Ok(Value::Bool(true))),
            (&arg1, "'y'", Ok(Value::Bool(true))),
            (&arg1, "'Y'", Ok(Value::Bool(true))),
            (&arg1, "null", Ok(Value::Null)),
            // error cases
            (
                &arg2,
                "null",
                Err(ErrorData(
                    "Invalid argument value: 'unnullable' argument is not allowed to be null",
                    "null",
                    0,
                    4,
                )),
            ),
            (&arg1, "abc", Err(ErrorData("Invalid argument value: 'enabled' argument has invalid value: invalid value \"abc\", accept enum items: true, t, yes, y, false, f, no, n", "abc", 0, 3))),
        ] {
            match expected {
                Ok(expected) => {
                    let result = parser.parse(value, arg.to_owned()).unwrap();
                    assert_eq!(result, SHResult::Value(expected));
                }
                Err(ErrorData(msg_target, data_target, at_target, range_target)) => {
                    let result = parser.parse(value, arg.to_owned());
                    match result {
                        Err(AzError::UserUsage(
                            AzUserUsageError::InvalidShorthandSyntaxError {
                                msg,
                                data,
                                at,
                                range,
                            },
                        )) => {
                            assert_eq!(
                                (msg, data, at, range),
                                (
                                    msg_target.to_string(),
                                    data_target.to_string(),
                                    at_target,
                                    range_target
                                )
                            );
                        }
                        _ => {
                            panic!("Unexpected result: {:?}", result);
                        }
                    }
                }
            }
        }
    }

    #[test]
    fn test_parse_bool_arg_from_file() {
        let parser = ShorthandSyntaxParser::default();

        let mut cmd = CmdCommand::default();
        // test nullable arg
        let mut bool_arg = CmdArg::default();
        bool_arg.var_name = "$Instance.enabled".to_string();
        bool_arg.name = "enabled".to_string();
        bool_arg.options = vec!["enabled".to_string()];
        bool_arg.nullable = true;
        let bool_type = CmdBooleanArg::default();
        bool_arg.arg_type = Some(cmd_arg::ArgType::Boolean(bool_type));
        cmd.args.push(bool_arg);
        // test not nullable arg
        let mut bool_arg = CmdArg::default();
        bool_arg.var_name = "$Instance.enabled2".to_string();
        bool_arg.name = "enabled2".to_string();
        bool_arg.options = vec!["enabled2".to_string()];
        let bool_type = CmdBooleanArg::default();
        bool_arg.arg_type = Some(cmd_arg::ArgType::Boolean(bool_type));
        cmd.args.push(bool_arg);

        let arg1 = Some(CmdArgWrapper::new(Some(&cmd), &cmd.args[0]).unwrap());
        let arg2 = Some(CmdArgWrapper::new(Some(&cmd), &cmd.args[1]).unwrap());

        for (arg, value, expected) in vec![
            (&arg1, json!(false), Ok(Value::Bool(false))),
            (&arg1, json!("F"), Ok(Value::Bool(false))),
            (&arg1, json!("f"), Ok(Value::Bool(false))),
            (&arg1, json!("false"), Ok(Value::Bool(false))),
            (&arg1, json!("False"), Ok(Value::Bool(false))),
            (&arg1, json!("FALSE"), Ok(Value::Bool(false))),
            (&arg1, json!("no"), Ok(Value::Bool(false))),
            (&arg1, json!("NO"), Ok(Value::Bool(false))),
            (&arg1, json!("No"), Ok(Value::Bool(false))),
            (&arg1, json!("n"), Ok(Value::Bool(false))),
            (&arg1, json!("N"), Ok(Value::Bool(false))),
            (&arg1, json!("T"), Ok(Value::Bool(true))),
            (&arg1, json!("t"), Ok(Value::Bool(true))),
            (&arg1, json!(true), Ok(Value::Bool(true))),
            (&arg1, json!("true"), Ok(Value::Bool(true))),
            (&arg1, json!("True"), Ok(Value::Bool(true))),
            (&arg1, json!("TRUE"), Ok(Value::Bool(true))),
            (&arg1, json!("yes"), Ok(Value::Bool(true))),
            (&arg1, json!("YES"), Ok(Value::Bool(true))),
            (&arg1, json!("Yes"), Ok(Value::Bool(true))),
            (&arg1, json!("y"), Ok(Value::Bool(true))),
            (&arg1, json!("Y"), Ok(Value::Bool(true))),
            (&arg1, json!(null), Ok(Value::Null)),
            // error cases
            (
                &arg2,
                json!(null),
                Err(ErrorData(
                    "Invalid argument value: 'enabled2' argument is not allowed to be null",
                    "null",
                    0,
                    4,
                )),
            ),
            (&arg1, json!("abc"), Err(ErrorData("Invalid argument value: 'enabled' argument has invalid value: invalid value \"abc\", accept enum items: true, t, yes, y, false, f, no, n", "abc", 0, 3))),
            (&arg1, json!([1, 2, 3]), Err(ErrorData("Invalid argument value: 'enabled' argument has invalid value: invalid value [1,2,3], accept enum items: true, t, yes, y, false, f, no, n", "[1,2,3]", 0, 4))),
        ] {
            let mut file = NamedTempFile::with_suffix(".json").expect("Failed to create temp file");
            writeln!(file, "{}", value).expect("Failed to write to temp file");
            file.flush().expect("Failed to flush temp file");
            let file_value = format!("file://{}", file.path().to_str().unwrap());

            match expected {
                Ok(expected) => {
                    let result = parser.parse(file_value.as_str(), arg.to_owned()).unwrap();
                    assert_eq!(result, SHResult::Value(expected));
                }
                Err(ErrorData(msg_target, _, at_target, _)) => {
                    let result = parser.parse(file_value.as_str(), arg.clone());
                    match result {
                        Err(AzError::UserUsage(
                            AzUserUsageError::InvalidShorthandSyntaxError {
                                msg,
                                data,
                                at,
                                range,
                            },
                        )) => {
                            assert_eq!(
                                (msg, data, at, range),
                                (
                                    msg_target.to_string(),
                                    file_value.to_string(),
                                    at_target,
                                    file_value.len()
                                )
                            );
                        }
                        _ => {
                            panic!("Unexpected result: {:?}", result);
                        }
                    }
                }
            }
        }
    }

    #[test]
    fn test_parse_number_without_arg() {
        let parser = ShorthandSyntaxParser::default();

        for (arg, value, expected) in vec![
            (None, "0", Ok(Value::Number(serde_json::Number::from(0)))),
            (
                None,
                "0.0",
                Ok(Value::Number(serde_json::Number::from_f64(0.0).unwrap())),
            ),
            (
                None,
                "123",
                Ok(Value::Number(serde_json::Number::from(123))),
            ),
            (
                None,
                "-123",
                Ok(Value::Number(serde_json::Number::from(-123))),
            ),
            (
                None,
                "123.456",
                Ok(Value::Number(
                    serde_json::Number::from_f64(123.456).unwrap(),
                )),
            ),
            (
                None,
                "-123.456",
                Ok(Value::Number(
                    serde_json::Number::from_f64(-123.456).unwrap(),
                )),
            ),
            (None, "'0'", Ok(Value::String("0".to_string()))),
            (None, "'0.0'", Ok(Value::String("0.0".to_string()))),
            (None, "'123'", Ok(Value::String("123".to_string()))),
            (None, "'-123'", Ok(Value::String("-123".to_string()))),
            (None, "'123.456'", Ok(Value::String("123.456".to_string()))),
            (
                None,
                "'-123.456'",
                Ok(Value::String("-123.456".to_string())),
            ),
            (
                None,
                "'123.456'abc",
                Err(ErrorData("Redundant tail", "'123.456'abc", 9, 3)),
            ),
        ] {
            match expected {
                Ok(expected) => {
                    let result = parser.parse(value, arg.to_owned()).unwrap();
                    assert_eq!(result, SHResult::Value(expected));
                }
                Err(ErrorData(msg_target, data_target, at_target, range_target)) => {
                    let result = parser.parse(value, arg.to_owned());
                    match result {
                        Err(AzError::UserUsage(
                            AzUserUsageError::InvalidShorthandSyntaxError {
                                msg,
                                data,
                                at,
                                range,
                            },
                        )) => {
                            assert_eq!(
                                (msg, data, at, range),
                                (
                                    msg_target.to_string(),
                                    data_target.to_string(),
                                    at_target,
                                    range_target
                                )
                            );
                        }
                        _ => {
                            panic!("Unexpected result: {:?}", result);
                        }
                    }
                }
            }
        }
    }

    #[test]
    fn test_parse_number_with_arg() {
        let parser = ShorthandSyntaxParser::default();
        let mut cmd = CmdCommand::default();
        let mut num_arg = CmdArg::default();
        num_arg.var_name = "$Instance.size".to_string();
        num_arg.name = "size".to_string();
        num_arg.options = vec!["size".to_string()];
        num_arg.nullable = true;
        let mut num_type = CmdNumberArg::default();
        num_type.format = Some(cmd_number_arg::Format::Int32(CmdInt32Format::default()));
        num_arg.arg_type = Some(cmd_arg::ArgType::Number(num_type));
        cmd.args.push(num_arg);
        // test arg with enum
        let mut num_arg = CmdArg::default();
        num_arg.var_name = "$Instance.vcore".to_string();
        num_arg.name = "vcore".to_string();
        num_arg.options = vec!["vcore".to_string()];
        num_arg.nullable = true;
        let mut num_type = CmdNumberArg::default();
        num_type.format = Some(cmd_number_arg::Format::Int32(CmdInt32Format::default()));
        let mut enum_type = CmdArgEnum::default();
        enum_type.items = vec![
            CmdArgEnumItem {
                name: "sm".to_string(),
                value: "1".to_string(),
                internal: false,
            },
            CmdArgEnumItem {
                name: "md".to_string(),
                value: "2".to_string(),
                internal: false,
            },
            CmdArgEnumItem {
                name: "lg".to_string(),
                value: "4".to_string(),
                internal: false,
            },
            CmdArgEnumItem {
                name: "none".to_string(),
                value: "null".to_string(),
                internal: false,
            },
        ];
        num_type.r#enum = Some(enum_type);
        num_arg.arg_type = Some(cmd_arg::ArgType::Number(num_type));
        cmd.args.push(num_arg);
        // test not nullable arg
        let mut num_arg = CmdArg::default();
        num_arg.var_name = "$Instance.unnullable".to_string();
        num_arg.name = "unnullable".to_string();
        num_arg.options = vec!["unnullable".to_string()];
        let mut num_type = CmdNumberArg::default();
        num_type.format = Some(cmd_number_arg::Format::Int32(CmdInt32Format::default()));
        num_arg.arg_type = Some(cmd_arg::ArgType::Number(num_type));
        cmd.args.push(num_arg);

        let arg1 = Some(CmdArgWrapper::new(Some(&cmd), &cmd.args[0]).unwrap());
        let arg2 = Some(CmdArgWrapper::new(Some(&cmd), &cmd.args[1]).unwrap());
        let arg3 = Some(CmdArgWrapper::new(Some(&cmd), &cmd.args[2]).unwrap());

        for (arg, value, expected) in vec![
            (&arg1, "0", Ok(Value::Number(serde_json::Number::from(0)))),
            (
                &arg1,
                "0.0",
                Ok(Value::Number(serde_json::Number::from_f64(0.0).unwrap())),
            ),
            (
                &arg1,
                "123",
                Ok(Value::Number(serde_json::Number::from(123))),
            ),
            (
                &arg1,
                "-123",
                Ok(Value::Number(serde_json::Number::from(-123))),
            ),
            (
                &arg1,
                "123.456",
                Ok(Value::Number(
                    serde_json::Number::from_f64(123.456).unwrap(),
                )),
            ),
            (
                &arg1,
                "-123.456",
                Ok(Value::Number(
                    serde_json::Number::from_f64(-123.456).unwrap(),
                )),
            ),
            (&arg1, "null", Ok(Value::Null)),
            (&arg2, "sm", Ok(Value::Number(serde_json::Number::from(1)))),
            (&arg2, "SM", Ok(Value::Number(serde_json::Number::from(1)))),
            (&arg2, "1", Ok(Value::Number(serde_json::Number::from(1)))),
            (&arg2, "md", Ok(Value::Number(serde_json::Number::from(2)))),
            (&arg2, "MD", Ok(Value::Number(serde_json::Number::from(2)))),
            (&arg2, "2", Ok(Value::Number(serde_json::Number::from(2)))),
            (&arg2, "lg", Ok(Value::Number(serde_json::Number::from(4)))),
            (&arg2, "LG", Ok(Value::Number(serde_json::Number::from(4)))),
            (&arg2, "4", Ok(Value::Number(serde_json::Number::from(4)))),
            (&arg2, "none", Ok(Value::Null)),
            (&arg2, "NONE", Ok(Value::Null)),
            (&arg2, "null", Ok(Value::Null)),
            // error cases
            (
                &arg1,
                "'0'",
                Err(ErrorData(
                    "Invalid argument value: 'size' argument has invalid value type, expect [int] type value",
                    "'0'",
                    0,
                    3,
                )),
            ),
            (
                &arg1,
                "'0.0'",
                Err(ErrorData(
                    "Invalid argument value: 'size' argument has invalid value type, expect [int] type value",
                    "'0.0'",
                    0,
                    5,
                )),
            ),
            (
                &arg1,
                "'123'",
                Err(ErrorData(
                    "Invalid argument value: 'size' argument has invalid value type, expect [int] type value",
                    "'123'",
                    0,
                    5,
                )),
            ),
            (
                &arg1,
                "'-123'",
                Err(ErrorData(
                    "Invalid argument value: 'size' argument has invalid value type, expect [int] type value",
                    "'-123'",
                    0,
                    6,
                )),
            ),
            (
                &arg1,
                "'123.456'",
                Err(ErrorData(
                    "Invalid argument value: 'size' argument has invalid value type, expect [int] type value",
                    "'123.456'",
                    0,
                    9,
                )),
            ),
            (
                &arg1,
                "'-123.456'",
                Err(ErrorData(
                    "Invalid argument value: 'size' argument has invalid value type, expect [int] type value",
                    "'-123.456'",
                    0,
                    10,
                )),
            ),
            (
                &arg2,
                "5",
                Err(ErrorData(
                    "Invalid argument value: 'vcore' argument has invalid value: invalid value 5, accept enum items: sm, md, lg, none",
                    "5",
                    0,
                    1,
                )),
            ),
            (
                &arg2,
                "xl",
                Err(ErrorData(
                    "Invalid argument value: 'vcore' argument has invalid value: invalid value \"xl\", accept enum items: sm, md, lg, none",
                    "xl",
                    0,
                    2,
                )),
            ),
            (
                &arg3,
                "null",
                Err(ErrorData(
                    "Invalid argument value: 'unnullable' argument is not allowed to be null",
                    "null",
                    0,
                    4,
                )),
            ),
        ] {
            match expected {
                Ok(expected) => {
                    let result = parser.parse(value, arg.to_owned()).unwrap();
                    assert_eq!(result, SHResult::Value(expected));
                }
                Err(ErrorData(msg_target, data_target, at_target, range_target)) => {
                    let result = parser.parse(value, arg.to_owned());
                    match result {
                        Err(AzError::UserUsage(
                            AzUserUsageError::InvalidShorthandSyntaxError {
                                msg,
                                data,
                                at,
                                range,
                            },
                        )) => {
                            assert_eq!(
                                (msg, data, at, range),
                                (
                                    msg_target.to_string(),
                                    data_target.to_string(),
                                    at_target,
                                    range_target
                                )
                            );
                        }
                        _ => {
                            panic!("Unexpected result: {:?}", result);
                        }
                    }
                }
            }
        }
    }

    #[test]
    fn test_parse_number_arg_from_file() {
        let parser = ShorthandSyntaxParser::default();
        let mut cmd = CmdCommand::default();
        let mut num_arg = CmdArg::default();
        num_arg.var_name = "$Instance.size".to_string();
        num_arg.name = "size".to_string();
        num_arg.options = vec!["size".to_string()];
        num_arg.nullable = true;
        let mut num_type = CmdNumberArg::default();
        num_type.format = Some(cmd_number_arg::Format::Int32(CmdInt32Format::default()));
        num_arg.arg_type = Some(cmd_arg::ArgType::Number(num_type));
        cmd.args.push(num_arg);
        // test arg with enum
        let mut num_arg = CmdArg::default();
        num_arg.var_name = "$Instance.vcore".to_string();
        num_arg.name = "vcore".to_string();
        num_arg.options = vec!["vcore".to_string()];
        num_arg.nullable = true;
        let mut num_type = CmdNumberArg::default();
        num_type.format = Some(cmd_number_arg::Format::Int32(CmdInt32Format::default()));
        let mut enum_type = CmdArgEnum::default();
        enum_type.items = vec![
            CmdArgEnumItem {
                name: "sm".to_string(),
                value: "1".to_string(),
                internal: false,
            },
            CmdArgEnumItem {
                name: "md".to_string(),
                value: "2".to_string(),
                internal: false,
            },
            CmdArgEnumItem {
                name: "lg".to_string(),
                value: "4".to_string(),
                internal: false,
            },
            CmdArgEnumItem {
                name: "none".to_string(),
                value: "null".to_string(),
                internal: false,
            },
        ];
        num_type.r#enum = Some(enum_type);
        num_arg.arg_type = Some(cmd_arg::ArgType::Number(num_type));
        cmd.args.push(num_arg);
        // test not nullable arg
        let mut num_arg = CmdArg::default();
        num_arg.var_name = "$Instance.unnullable".to_string();
        num_arg.name = "unnullable".to_string();
        num_arg.options = vec!["unnullable".to_string()];
        let mut num_type = CmdNumberArg::default();
        num_type.format = Some(cmd_number_arg::Format::Int32(CmdInt32Format::default()));
        num_arg.arg_type = Some(cmd_arg::ArgType::Number(num_type));
        cmd.args.push(num_arg);

        let arg1 = Some(CmdArgWrapper::new(Some(&cmd), &cmd.args[0]).unwrap());
        let arg2 = Some(CmdArgWrapper::new(Some(&cmd), &cmd.args[1]).unwrap());
        let arg3 = Some(CmdArgWrapper::new(Some(&cmd), &cmd.args[2]).unwrap());

        for (arg, value, expected) in vec![
            (
                &arg1,
                json!(0),
                Ok(Value::Number(serde_json::Number::from(0))),
            ),
            (
                &arg1,
                json!(0.0),
                Ok(Value::Number(serde_json::Number::from_f64(0.0).unwrap())),
            ),
            (
                &arg1,
                json!(123),
                Ok(Value::Number(serde_json::Number::from(123))),
            ),
            (
                &arg1,
                json!(-123),
                Ok(Value::Number(serde_json::Number::from(-123))),
            ),
            (
                &arg1,
                json!(123.456),
                Ok(Value::Number(
                    serde_json::Number::from_f64(123.456).unwrap(),
                )),
            ),
            (
                &arg1,
                json!(-123.456),
                Ok(Value::Number(
                    serde_json::Number::from_f64(-123.456).unwrap(),
                )),
            ),
            (&arg1, json!(null), Ok(Value::Null)),
            (
                &arg2,
                json!("sm"),
                Ok(Value::Number(serde_json::Number::from(1))),
            ),
            (
                &arg2,
                json!("SM"),
                Ok(Value::Number(serde_json::Number::from(1))),
            ),
            (
                &arg2,
                json!(1),
                Ok(Value::Number(serde_json::Number::from(1))),
            ),
            (
                &arg2,
                json!("md"),
                Ok(Value::Number(serde_json::Number::from(2))),
            ),
            (
                &arg2,
                json!("MD"),
                Ok(Value::Number(serde_json::Number::from(2))),
            ),
            (
                &arg2,
                json!(2),
                Ok(Value::Number(serde_json::Number::from(2))),
            ),
            (
                &arg2,
                json!("lg"),
                Ok(Value::Number(serde_json::Number::from(4))),
            ),
            (
                &arg2,
                json!("LG"),
                Ok(Value::Number(serde_json::Number::from(4))),
            ),
            (
                &arg2,
                json!(4),
                Ok(Value::Number(serde_json::Number::from(4))),
            ),
            (&arg2, json!("none"), Ok(Value::Null)),
            (&arg2, json!("NONE"), Ok(Value::Null)),
            (&arg2, json!(null), Ok(Value::Null)),
            // error cases
            (
                &arg1,
                json!("0"),
                Err(ErrorData(
                    "Invalid argument value: 'size' argument has invalid value type, expect [int] type value",
                    "0",
                    0,
                    3,
                )),
            ),
            (
                &arg1,
                json!("0.0"),
                Err(ErrorData(
                    "Invalid argument value: 'size' argument has invalid value type, expect [int] type value",
                    "0.0",
                    0,
                    5,
                )),
            ),
            (
                &arg1,
                json!("123"),
                Err(ErrorData(
                    "Invalid argument value: 'size' argument has invalid value type, expect [int] type value",
                    "123",
                    0,
                    5,
                )),
            ),
            (
                &arg1,
                json!("-123"),
                Err(ErrorData(
                    "Invalid argument value: 'size' argument has invalid value type, expect [int] type value",
                    "-123",
                    0,
                    6,
                )),
            ),
            (
                &arg1,
                json!("123.456"),
                Err(ErrorData(
                    "Invalid argument value: 'size' argument has invalid value type, expect [int] type value",
                    "123.456",
                    0,
                    9,
                )),
            ),
            (
                &arg1,
                json!("-123.456"),
                Err(ErrorData(
                    "Invalid argument value: 'size' argument has invalid value type, expect [int] type value",
                    "-123.456",
                    0,
                    10,
                )),
            ),
            (
                &arg1,
                json!([1,2,3]),
                Err(ErrorData(
                    "Invalid argument value: 'size' argument has invalid value type, expect [int] type value",
                    "[1,2,3]",
                    0,
                    4,
                )),
            ),
            (
                &arg2,
                json!(5),
                Err(ErrorData(
                    "Invalid argument value: 'vcore' argument has invalid value: invalid value 5, accept enum items: sm, md, lg, none",
                    "5",
                    0,
                    1,
                )),
            ),
            (
                &arg2,
                json!("xl"),
                Err(ErrorData(
                    "Invalid argument value: 'vcore' argument has invalid value: invalid value \"xl\", accept enum items: sm, md, lg, none",
                    "xl",
                    0,
                    2,
                )),
            ),
            (
                &arg3,
                json!(null),
                Err(ErrorData(
                    "Invalid argument value: 'unnullable' argument is not allowed to be null",
                    "null",
                    0,
                    4,
                )),
            ),
        ] {
            let mut file = NamedTempFile::with_suffix(".json").expect("Failed to create temp file");
            writeln!(file, "{}", value).expect("Failed to write to temp file");
            file.flush().expect("Failed to flush temp file");
            let file_value = format!("file://{}", file.path().to_str().unwrap());

            match expected {
                Ok(expected) => {
                    let result = parser.parse(file_value.as_str(), arg.to_owned()).unwrap();
                    assert_eq!(result, SHResult::Value(expected));
                }
                Err(ErrorData(msg_target, _, at_target, _)) => {
                    let result = parser.parse(file_value.as_str(), arg.clone());
                    match result {
                        Err(AzError::UserUsage(
                            AzUserUsageError::InvalidShorthandSyntaxError {
                                msg,
                                data,
                                at,
                                range,
                            },
                        )) => {
                            assert_eq!(
                                (msg, data, at, range),
                                (
                                    msg_target.to_string(),
                                    file_value.to_string(),
                                    at_target,
                                    file_value.len()
                                )
                            );
                        }
                        _ => {
                            panic!("Unexpected result: {:?}", result);
                        }
                    }
                }
            }
        }
    }

    #[test]
    fn test_parse_string_without_arg() {
        let parser = ShorthandSyntaxParser::default();
        for (arg, value, expected) in vec![
            (None, "'hello'", Ok(Value::String("hello".to_string()))),
            (
                None,
                "'hello world'",
                Ok(Value::String("hello world".to_string())),
            ),
            (None, "1, 2, 3", Ok(Value::String("1, 2, 3".to_string()))),
            (None, "1,2,3", Ok(Value::String("1,2,3".to_string()))),
            (None, "a=b,b=c", Ok(Value::String("a=b,b=c".to_string()))),
            (
                None,
                "hello world",
                Ok(Value::String("hello world".to_string())),
            ),
            (None, "'{a:b}'", Ok(Value::String("{a:b}".to_string()))),
            (
                None,
                "'{\"a\":\"b\"}'",
                Ok(Value::String("{\"a\":\"b\"}".to_string())),
            ),
            (
                None,
                "'[1, 2, 3]'",
                Ok(Value::String("[1, 2, 3]".to_string())),
            ),
            (None, "'['", Ok(Value::String("[".to_string()))),
            (None, "']'", Ok(Value::String("]".to_string()))),
            (None, "'true'", Ok(Value::String("true".to_string()))),
            (None, "'false'", Ok(Value::String("false".to_string()))),
            (None, "'null'", Ok(Value::String("null".to_string()))),
            (None, "'123'", Ok(Value::String("123".to_string()))),
            (None, "'-123'", Ok(Value::String("-123".to_string()))),
            (None, "'123.456'", Ok(Value::String("123.456".to_string()))),
            (
                None,
                "'-123.456'",
                Ok(Value::String("-123.456".to_string())),
            ),
            // error cases
            (
                None,
                "'hello world'abc",
                Err(ErrorData("Redundant tail", "'hello world'abc", 13, 3)),
            ),
        ] {
            match expected {
                Ok(expected) => {
                    let result = parser.parse(value, arg.to_owned()).unwrap();
                    assert_eq!(result, SHResult::Value(expected));
                }
                Err(ErrorData(msg_target, data_target, at_target, range_target)) => {
                    let result = parser.parse(value, arg.to_owned());
                    match result {
                        Err(AzError::UserUsage(
                            AzUserUsageError::InvalidShorthandSyntaxError {
                                msg,
                                data,
                                at,
                                range,
                            },
                        )) => {
                            assert_eq!(
                                (msg, data, at, range),
                                (
                                    msg_target.to_string(),
                                    data_target.to_string(),
                                    at_target,
                                    range_target
                                )
                            );
                        }
                        _ => {
                            panic!("Unexpected result: {:?}", result);
                        }
                    }
                }
            }
        }
    }

    #[test]
    fn test_parse_string_arg() {
        let parser = ShorthandSyntaxParser::default();

        let mut cmd = CmdCommand::default();
        let mut str_arg = CmdArg::default();
        str_arg.var_name = "$Path.name".to_string();
        str_arg.name = "name".to_string();
        str_arg.options = vec!["name".to_string(), "n".to_string()];
        str_arg.nullable = true;
        let mut str_type = CmdStringArg::default();
        str_type.format = Some(cmd_string_arg::Format::String(CmdStringFormat::default()));
        str_arg.arg_type = Some(cmd_arg::ArgType::Str(str_type));
        cmd.args.push(str_arg);

        // test str arg with enum
        let mut str_arg = CmdArg::default();
        str_arg.var_name = "$Instance.vcore".to_string();
        str_arg.name = "vcore".to_string();
        str_arg.options = vec!["vcore".to_string()];
        str_arg.nullable = true;
        let mut str_type = CmdStringArg::default();
        str_type.format = Some(cmd_string_arg::Format::String(CmdStringFormat::default()));
        let mut enum_type = CmdArgEnum::default();
        enum_type.items = vec![
            CmdArgEnumItem {
                name: "sm".to_string(),
                value: "\"S\"".to_string(),
                internal: false,
            },
            CmdArgEnumItem {
                name: "md".to_string(),
                value: "\"M\"".to_string(),
                internal: false,
            },
            CmdArgEnumItem {
                name: "lg".to_string(),
                value: "\"L\"".to_string(),
                internal: false,
            },
            CmdArgEnumItem {
                name: "none".to_string(),
                value: "null".to_string(),
                internal: false,
            },
        ];
        str_type.r#enum = Some(enum_type);
        str_arg.arg_type = Some(cmd_arg::ArgType::Str(str_type));
        cmd.args.push(str_arg);

        // test str not allow null
        let mut str_arg = CmdArg::default();
        str_arg.var_name = "$Instance.other".to_string();
        str_arg.name = "other".to_string();
        str_arg.options = vec!["other".to_string()];
        str_arg.nullable = false;
        let mut str_type = CmdStringArg::default();
        str_type.format = Some(cmd_string_arg::Format::String(CmdStringFormat::default()));
        str_arg.arg_type = Some(cmd_arg::ArgType::Str(str_type));
        cmd.args.push(str_arg);

        let arg1 = Some(CmdArgWrapper::new(Some(&cmd), &cmd.args[0]).unwrap());
        let arg2 = Some(CmdArgWrapper::new(Some(&cmd), &cmd.args[1]).unwrap());
        let arg3 = Some(CmdArgWrapper::new(Some(&cmd), &cmd.args[2]).unwrap());

        for (arg, value, expected) in [
            (&arg1, "'hello'", Ok(Value::String("hello".to_string()))),
            (
                &arg1,
                "hello world",
                Ok(Value::String("hello world".to_string())),
            ),
            (
                &arg1,
                "'hello world'",
                Ok(Value::String("hello world".to_string())),
            ),
            (&arg1, "'1, 2, 3'", Ok(Value::String("1, 2, 3".to_string()))),
            (&arg1, "'a=b,b=c'", Ok(Value::String("a=b,b=c".to_string()))),
            (&arg1, "'{a:b}'", Ok(Value::String("{a:b}".to_string()))),
            (&arg1, "{a:b}", Ok(Value::String("{a:b}".to_string()))),
            (
                &arg1,
                "'{\"a\":\"b\"}'",
                Ok(Value::String("{\"a\":\"b\"}".to_string())),
            ),
            (
                &arg1,
                "{\"a\":\"b\"}",
                Ok(Value::String("{\"a\":\"b\"}".to_string())),
            ),
            (
                &arg1,
                "'[1, 2, 3]'",
                Ok(Value::String("[1, 2, 3]".to_string())),
            ),
            (
                &arg1,
                "[1, 2, 3]",
                Ok(Value::String("[1, 2, 3]".to_string())),
            ),
            (&arg1, "'['", Ok(Value::String("[".to_string()))),
            (&arg1, "']'", Ok(Value::String("]".to_string()))),
            (&arg1, "'true'", Ok(Value::String("true".to_string()))),
            (&arg1, "true", Ok(Value::String("true".to_string()))),
            (&arg1, "'false'", Ok(Value::String("false".to_string()))),
            (&arg1, "false", Ok(Value::String("false".to_string()))),
            (&arg1, "'null'", Ok(Value::String("null".to_string()))),
            (&arg1, "null", Ok(Value::Null)),
            (&arg2, "sm", Ok(Value::String("S".to_string()))),
            (&arg2, "SM", Ok(Value::String("S".to_string()))),
            (&arg2, "S", Ok(Value::String("S".to_string()))),
            (&arg2, "s", Ok(Value::String("S".to_string()))),
            (&arg2, "md", Ok(Value::String("M".to_string()))),
            (&arg2, "MD", Ok(Value::String("M".to_string()))),
            (&arg2, "M", Ok(Value::String("M".to_string()))),
            (&arg2, "m", Ok(Value::String("M".to_string()))),
            (&arg2, "lg", Ok(Value::String("L".to_string()))),
            (&arg2, "LG", Ok(Value::String("L".to_string()))),
            (&arg2, "L", Ok(Value::String("L".to_string()))),
            (&arg2, "l", Ok(Value::String("L".to_string()))),
            (&arg2, "none", Ok(Value::Null)),
            (&arg2, "NONE", Ok(Value::Null)),
            (&arg2, "null", Ok(Value::Null)),
            // error cases
            (
                &arg1,
                "'hello world'abc",
                Err(ErrorData("Redundant tail", "'hello world'abc", 13, 3)),
            ),
            (
                &arg2,
                "X",
                Err(ErrorData("Invalid argument value: 'vcore' argument has invalid value: invalid value \"X\", accept enum items: sm, md, lg, none", "X", 0, 1)),
            ),
            (&arg3, "null", Err(ErrorData("Invalid argument value: 'other' argument is not allowed to be null", "null", 0, 4))),
        ] {
            match expected {
                Ok(expected) => {
                    let result = parser.parse(value, arg.to_owned()).unwrap();
                    assert_eq!(result, SHResult::Value(expected));
                }
                Err(ErrorData(msg_target, data_target, at_target, range_target)) => {
                    let result = parser.parse(value, arg.to_owned());
                    match result {
                        Err(AzError::UserUsage(
                            AzUserUsageError::InvalidShorthandSyntaxError {
                                msg,
                                data,
                                at,
                                range,
                            },
                        )) => {
                            assert_eq!(
                                (msg, data, at, range),
                                (
                                    msg_target.to_string(),
                                    data_target.to_string(),
                                    at_target,
                                    range_target
                                )
                            );
                        }
                        _ => {
                            panic!("Unexpected result: {:?}", result);
                        }
                    }
                }
            }
        }
    }

    #[test]
    fn test_parse_string_arg_from_file() {
        let parser = ShorthandSyntaxParser::default();

        let mut cmd = CmdCommand::default();
        let mut str_arg = CmdArg::default();
        str_arg.var_name = "$Path.name".to_string();
        str_arg.name = "name".to_string();
        str_arg.options = vec!["name".to_string(), "n".to_string()];
        str_arg.nullable = true;
        let mut str_type = CmdStringArg::default();
        str_type.format = Some(cmd_string_arg::Format::String(CmdStringFormat::default()));
        str_arg.arg_type = Some(cmd_arg::ArgType::Str(str_type));
        cmd.args.push(str_arg);

        // test str arg with enum
        let mut str_arg = CmdArg::default();
        str_arg.var_name = "$Instance.vcore".to_string();
        str_arg.name = "vcore".to_string();
        str_arg.options = vec!["vcore".to_string()];
        str_arg.nullable = true;
        let mut str_type = CmdStringArg::default();
        str_type.format = Some(cmd_string_arg::Format::String(CmdStringFormat::default()));
        let mut enum_type = CmdArgEnum::default();
        enum_type.items = vec![
            CmdArgEnumItem {
                name: "sm".to_string(),
                value: "\"S\"".to_string(),
                internal: false,
            },
            CmdArgEnumItem {
                name: "md".to_string(),
                value: "\"M\"".to_string(),
                internal: false,
            },
            CmdArgEnumItem {
                name: "lg".to_string(),
                value: "\"L\"".to_string(),
                internal: false,
            },
            CmdArgEnumItem {
                name: "none".to_string(),
                value: "null".to_string(),
                internal: false,
            },
        ];
        str_type.r#enum = Some(enum_type);
        str_arg.arg_type = Some(cmd_arg::ArgType::Str(str_type));
        cmd.args.push(str_arg);

        // test bytes arg
        let mut str_arg = CmdArg::default();
        str_arg.var_name = "$Instance.byte".to_string();
        str_arg.name = "byte".to_string();
        str_arg.options = vec!["byte".to_string()];
        let mut str_type = CmdStringArg::default();
        str_type.format = Some(cmd_string_arg::Format::Byte(CmdByteFormat::default()));
        str_arg.arg_type = Some(cmd_arg::ArgType::Str(str_type));
        cmd.args.push(str_arg);

        // test binary arg
        let mut str_arg = CmdArg::default();
        str_arg.var_name = "$Instance.binary".to_string();
        str_arg.name = "binary".to_string();
        str_arg.options = vec!["binary".to_string()];
        let mut str_type = CmdStringArg::default();
        str_type.format = Some(cmd_string_arg::Format::Binary(CmdBinaryFormat::default()));
        str_arg.arg_type = Some(cmd_arg::ArgType::Str(str_type));
        cmd.args.push(str_arg);

        let arg1 = Some(CmdArgWrapper::new(Some(&cmd), &cmd.args[0]).unwrap());
        let arg2 = Some(CmdArgWrapper::new(Some(&cmd), &cmd.args[1]).unwrap());
        let byte_arg = Some(CmdArgWrapper::new(Some(&cmd), &cmd.args[2]).unwrap());
        let binary_arg = Some(CmdArgWrapper::new(Some(&cmd), &cmd.args[3]).unwrap());

        let mut rng = rand::thread_rng();
        let random_bytes: Vec<u8> = (0..100).map(|_| rng.gen()).collect();
        let mut file = NamedTempFile::with_suffix(".bin").expect("Failed to create temp file");
        file.write_all(&random_bytes)
            .expect("Failed to write to temp file");
        file.flush().expect("Failed to flush temp file");
        let binary_file_path = file.path().to_str().unwrap();
        let binary_base64 = BASE64_STANDARD.encode(&random_bytes);

        // json file
        for (arg, value, expected) in vec![
            (
                &arg1,
                json!("hello"),
                Ok(Value::String("hello".to_string())),
            ),
            (
                &arg1,
                json!("hello world"),
                Ok(Value::String("hello world".to_string())),
            ),
            (
                &arg1,
                json!("[1, 2, 3]"),
                Ok(Value::String("[1, 2, 3]".to_string())),
            ),
            (
                &arg1,
                json!("{\"a\":\"b\"}"),
                Ok(Value::String("{\"a\":\"b\"}".to_string())),
            ),
            (&arg1, json!("true"), Ok(Value::String("true".to_string()))),
            (
                &arg1,
                json!("false"),
                Ok(Value::String("false".to_string())),
            ),
            (&arg1, json!(null), Ok(Value::Null)),
            (&arg1, json!("null"), Ok(Value::String("null".to_string()))),
            (&arg1, json!(""), Ok(Value::String("".to_string()))),
            (&arg2, json!("sm"), Ok(Value::String("S".to_string()))),
            (&arg2, json!("SM"), Ok(Value::String("S".to_string()))),
            (&arg2, json!("S"), Ok(Value::String("S".to_string()))),
            (&arg2, json!("s"), Ok(Value::String("S".to_string()))),
            (&arg2, json!("md"), Ok(Value::String("M".to_string()))),
            (&arg2, json!("MD"), Ok(Value::String("M".to_string()))),
            (&arg2, json!("M"), Ok(Value::String("M".to_string()))),
            (&arg2, json!("m"), Ok(Value::String("M".to_string()))),
            (&arg2, json!("lg"), Ok(Value::String("L".to_string()))),
            (&arg2, json!("LG"), Ok(Value::String("L".to_string()))),
            (&arg2, json!("L"), Ok(Value::String("L".to_string()))),
            (&arg2, json!("l"), Ok(Value::String("L".to_string()))),
            (&arg2, json!("none"), Ok(Value::Null)),
            (&arg2, json!("NONE"), Ok(Value::Null)),
            (&arg2, json!(null), Ok(Value::Null)),
            (&byte_arg, json!(binary_base64.as_str()), Ok(Value::String(binary_base64.clone()))),
            // pass a binary file path as string value into the json, should retrieve the path value from json
            (&binary_arg, json!(binary_file_path), Ok(Value::String(binary_file_path.to_string()))),
            // error cases
            (
                &arg1,
                json!(1),
                Err(ErrorData(
                    "Invalid argument value: 'name' argument has invalid value type, expect [string] type value",
                    "1",
                    0,
                    1,
                )),
            ),
            (
                &arg1,
                json!(true),
                Err(ErrorData(
                    "Invalid argument value: 'name' argument has invalid value type, expect [string] type value",
                    "true",
                    0,
                    4,
                )),
            ),
            (
                &arg1,
                json!(false),
                Err(ErrorData(
                    "Invalid argument value: 'name' argument has invalid value type, expect [string] type value",
                    "false",
                    0,
                    5,
                )),
            ),
            (
                &arg1,
                json!([1, 2, 3]),
                Err(ErrorData(
                    "Invalid argument value: 'name' argument has invalid value type, expect [string] type value",
                    "[1, 2, 3]",
                    0,
                    9,
                )),
            ),
            (
                &arg1,
                json!({"a": "b"}),
                Err(ErrorData(
                    "Invalid argument value: 'name' argument has invalid value type, expect [string] type value",
                    "{\"a\":\"b\"}",
                    0,
                    9,
                )),
            ),
            (&arg2, json!("X"), Err(ErrorData("Invalid argument value: 'vcore' argument has invalid value: invalid value \"X\", accept enum items: sm, md, lg, none", "X", 0, 1))),
            (&byte_arg, json!(null), Err(ErrorData("Invalid argument value: 'byte' argument is not allowed to be null", "null", 0, 4))),
        ] {
            let mut file = NamedTempFile::with_suffix(".json").expect("Failed to create temp file");
            writeln!(file, "{}", value).expect("Failed to write to temp file");
            file.flush().expect("Failed to flush temp file");
            let file_value = format!("file://{}", file.path().to_str().unwrap());

            match expected {
                Ok(expected) => {
                    let result = parser.parse(file_value.as_str(), arg.to_owned()).unwrap();
                    assert_eq!(result, SHResult::Value(expected));
                }
                Err(ErrorData(msg_target, _, at_target, _)) => {
                    let result = parser.parse(file_value.as_str(), arg.clone());
                    match result {
                        Err(AzError::UserUsage(
                            AzUserUsageError::InvalidShorthandSyntaxError {
                                msg,
                                data,
                                at,
                                range,
                            },
                        )) => {
                            assert_eq!(
                                (msg, data, at, range),
                                (
                                    msg_target.to_string(),
                                    file_value.to_string(),
                                    at_target,
                                    file_value.len()
                                )
                            );
                        }
                        _ => {
                            panic!("Unexpected result: {:?}", result);
                        }
                    }
                }
            }
        }

        // test string from text
        let mut file = NamedTempFile::with_suffix(".txt").expect("Failed to create temp file");
        write!(file, "\ncontent data\n").expect("Failed to write to temp file");
        file.flush().expect("Failed to flush temp file");
        let file_value = format!("file://{}", file.path().to_str().unwrap());

        let result = parser.parse(file_value.as_str(), arg1.to_owned()).unwrap();
        assert_eq!(
            result,
            SHResult::Value(Value::String("\ncontent data\n".to_string()))
        );

        // test bytes file
        // try to parse already encoded base64 string
        let mut file =
            NamedTempFile::with_suffix(".base64.txt").expect("Failed to create temp file");
        writeln!(file, "\n{}\n", binary_base64).expect("Failed to write to temp file");
        file.flush().expect("Failed to flush temp file");
        let file_value = format!("file://{}", file.path().to_str().unwrap());

        let result = parser
            .parse(file_value.as_str(), byte_arg.to_owned())
            .unwrap();
        assert_eq!(
            result,
            SHResult::Value(Value::String(binary_base64.clone()))
        );

        // try to parse binary from file
        let file_value = format!("file://{}", binary_file_path);
        let result = parser
            .parse(file_value.as_str(), byte_arg.to_owned())
            .unwrap();
        assert_eq!(
            result,
            SHResult::Value(Value::String(binary_base64.clone()))
        );

        // test binary file
        let file_value = format!("file://{}", binary_file_path);
        let result = parser
            .parse(file_value.as_str(), binary_arg.to_owned())
            .unwrap();
        assert_eq!(
            result,
            SHResult::Value(Value::String(binary_file_path.to_string()))
        );
    }

    #[test]
    fn test_parse_any_type_arg() {
        let parser = ShorthandSyntaxParser::default();

        let mut cmd = CmdCommand::default();
        let mut any_arg = CmdArg::default();
        any_arg.var_name = "$Instance.any".to_string();
        any_arg.name = "any".to_string();
        any_arg.nullable = true;
        any_arg.options = vec!["any".to_string()];
        any_arg.arg_type = Some(cmd_arg::ArgType::AnyType(CmdAnyTypeArg::default()));
        cmd.args.push(any_arg);
        // test not nullable any type arg
        let mut any_arg = CmdArg::default();
        any_arg.var_name = "$Instance.unNullableAny".to_string();
        any_arg.name = "unNullableAny".to_string();
        any_arg.options = vec!["unnullable-any".to_string()];
        any_arg.arg_type = Some(cmd_arg::ArgType::AnyType(CmdAnyTypeArg::default()));
        cmd.args.push(any_arg);

        let arg1 = Some(CmdArgWrapper::new(Some(&cmd), &cmd.args[0]).unwrap());
        let arg2 = Some(CmdArgWrapper::new(Some(&cmd), &cmd.args[1]).unwrap());

        for (arg, value, expected) in [
            (&arg1, "null", Ok(Value::Null)),
            (
                &arg1,
                "123",
                Ok(Value::Number(serde_json::Number::from(123))),
            ),
            (
                &arg1,
                "-123",
                Ok(Value::Number(serde_json::Number::from(-123))),
            ),
            (
                &arg1,
                "123.456",
                Ok(Value::Number(
                    serde_json::Number::from_f64(123.456).unwrap(),
                )),
            ),
            (&arg1, "true", Ok(Value::Bool(true))),
            (&arg1, "false", Ok(Value::Bool(false))),
            (&arg1, "hello", Ok(Value::String("hello".to_string()))),
            (&arg1, "'hello'", Ok(Value::String("hello".to_string()))),
            (
                &arg1,
                "hello world",
                Ok(Value::String("hello world".to_string())),
            ),
            (
                &arg1,
                "'hello world'",
                Ok(Value::String("hello world".to_string())),
            ),
            (&arg1, "{a:b}", Ok(Value::Object(json!({"a": "b"}).as_object().unwrap().clone()))),
            (&arg1, "[1,2,3]", Ok(Value::Array(json!([1, 2, 3]).as_array().unwrap().clone()))),
            (&arg1, "{windows:'c:\\temp\\filter.json',unix1:'/tmp/filter.json',unix2:/var/tmp/filter.json}", Ok(Value::Object(
                json!({
                    "windows":"c:\\temp\\filter.json",
                    "unix1":"/tmp/filter.json",
                    "unix2":"/var/tmp/filter.json"
                })
                .as_object()
                .unwrap()
                .clone()
            ))),
            (&arg1, "{a:1,c:d,d0:{},l0:[],l1:['1','2','3'],l2:[{a:b,c:d},{e:f},null,'null',true,false],l3:[1,2,3],str1:'[1,2,3]',str2:'{a:b,c:d}'}", Ok(Value::Object(
                json!({
                    "a": 1,
                    "c": "d",
                    "d0": {},
                    "l0": [],
                    "l1": ["1", "2", "3"],
                    "l2": [
                        {"a": "b", "c": "d"},
                        {"e": "f"},
                        null,
                        "null",
                        true,
                        false,
                    ],
                    "l3": [1, 2, 3],
                    "str1":"[1,2,3]",
                    "str2":"{a:b,c:d}",
                })
                .as_object()
                .unwrap()
                .clone()
            ))),
            (&arg1, "{']':']','[':'[','}':'}','{':'{'}", Ok(Value::Object(
                json!({"]": "]", "[": "[", "}": "}", "{": "{"})
                    .as_object()
                    .unwrap()
                    .clone()
            ))),
            (&arg1, "{'\"':'\"'}", Ok(Value::Object(
                json!({"\"": "\""}).as_object().unwrap().clone()
            ))),
            (&arg1, "{''/':''/'}", Ok(Value::Object(
                json!({"'": "'"}).as_object().unwrap().clone()
            ))),
            // error cases
            (&arg1, "{a:b,{:2}", Err(ErrorData("Failed to parse string, please wrap '{' character in single quotes", "{a:b,{:2}", 5, 1))),
            (&arg1, "{a:b,[:2}", Err(ErrorData("Failed to parse string, please wrap '[' character in single quotes", "{a:b,[:2}", 5, 1))),
            (&arg1, "[1, 2, 3]", Err(ErrorData("Failed to parse string, please wrap whitespace character in single quotes", "[1, 2, 3]", 3, 1))),
            (&arg1, "{a:1,c:2},3", Err(ErrorData("Redundant tail", "{a:1,c:2},3", 9, 2))),
            (&arg2, "null", Err(ErrorData("Invalid argument value: 'unNullableAny' argument is not allowed to be null", "null", 0, 4))),
        ] {
            match expected {
                Ok(expected) => {
                    let result = parser.parse(value, arg.to_owned()).unwrap();
                    assert_eq!(result, SHResult::Value(expected));
                }
                Err(ErrorData(msg_target, data_target, at_target, range_target)) => {
                    let result = parser.parse(value, arg.to_owned());
                    match result {
                        Err(AzError::UserUsage(
                            AzUserUsageError::InvalidShorthandSyntaxError {
                                msg,
                                data,
                                at,
                                range,
                            },
                        )) => {
                            assert_eq!(
                                (msg, data, at, range),
                                (
                                    msg_target.to_string(),
                                    data_target.to_string(),
                                    at_target,
                                    range_target
                                )
                            );
                        }
                        _ => {
                            panic!("Unexpected result: {:?}", result);
                        }
                    }
                }
            }
        }
    }

    #[test]
    fn test_parse_any_type_arg_from_file() {
        let parser = ShorthandSyntaxParser::default();

        let mut cmd = CmdCommand::default();
        let mut any_arg = CmdArg::default();
        any_arg.var_name = "$Instance.any".to_string();
        any_arg.name = "any".to_string();
        any_arg.nullable = true;
        any_arg.options = vec!["any".to_string()];
        any_arg.arg_type = Some(cmd_arg::ArgType::AnyType(CmdAnyTypeArg::default()));
        cmd.args.push(any_arg);
        // test not nullable any type arg
        let mut any_arg = CmdArg::default();
        any_arg.var_name = "$Instance.unNullableAny".to_string();
        any_arg.name = "unNullableAny".to_string();
        any_arg.options = vec!["unnullable-any".to_string()];
        any_arg.arg_type = Some(cmd_arg::ArgType::AnyType(CmdAnyTypeArg::default()));
        cmd.args.push(any_arg);

        let arg1 = Some(CmdArgWrapper::new(Some(&cmd), &cmd.args[0]).unwrap());
        let arg2 = Some(CmdArgWrapper::new(Some(&cmd), &cmd.args[1]).unwrap());

        for (arg, value, expected) in [
            (&arg1, json!(null), Ok(Value::Null)),
            (
                &arg1,
                json!(123),
                Ok(Value::Number(serde_json::Number::from(123))),
            ),
            (
                &arg1,
                json!(-123),
                Ok(Value::Number(serde_json::Number::from(-123))),
            ),
            (
                &arg1,
                json!(123.456),
                Ok(Value::Number(
                    serde_json::Number::from_f64(123.456).unwrap(),
                )),
            ),
            (&arg1, json!(true), Ok(Value::Bool(true))),
            (&arg1, json!(false), Ok(Value::Bool(false))),
            (
                &arg1,
                json!("hello"),
                Ok(Value::String("hello".to_string())),
            ),
            (
                &arg1,
                json!("hello world"),
                Ok(Value::String("hello world".to_string())),
            ),
            (
                &arg1,
                json!({"a": "b"}),
                Ok(Value::Object(
                    json!({"a": "b"}).as_object().unwrap().clone(),
                )),
            ),
            (
                &arg1,
                json!([1, 2, 3]),
                Ok(Value::Array(json!([1, 2, 3]).as_array().unwrap().clone())),
            ),
            (
                &arg1,
                json!({
                    "windows":"c:\\temp\\filter.json",
                    "unix1":"/tmp/filter.json",
                    "unix2":"/var/tmp/filter.json"
                }),
                Ok(Value::Object(
                    json!({
                        "windows":"c:\\temp\\filter.json",
                        "unix1":"/tmp/filter.json",
                        "unix2":"/var/tmp/filter.json"
                    })
                    .as_object()
                    .unwrap()
                    .clone(),
                )),
            ),
            (
                &arg1,
                json!({
                    "a": 1,
                    "c": "d",
                    "d0": {},
                    "l0": [],
                    "l1": ["1", "2", "3"],
                    "l2": [
                        {"a": "b", "c": "d"},
                        {"e": "f"},
                        null,
                        "null",
                        true,
                        false,
                    ],
                    "l3": [1, 2, 3],
                    "str1":"[1,2,3]",
                    "str2":"{a:b,c:d}",
                }),
                Ok(Value::Object(
                    json!({
                        "a": 1,
                        "c": "d",
                        "d0": {},
                        "l0": [],
                        "l1": ["1", "2", "3"],
                        "l2": [
                            {"a": "b", "c": "d"},
                            {"e": "f"},
                            null,
                            "null",
                            true,
                            false,
                        ],
                        "l3": [1, 2, 3],
                        "str1":"[1,2,3]",
                        "str2":"{a:b,c:d}",
                    })
                    .as_object()
                    .unwrap()
                    .clone(),
                )),
            ),
            (
                &arg1,
                json!({"]": "]", "[": "[", "}": "}", "{": "{"}),
                Ok(Value::Object(
                    json!({"]": "]", "[": "[", "}": "}", "{": "{"})
                        .as_object()
                        .unwrap()
                        .clone(),
                )),
            ),
            (
                &arg1,
                json!({"\"": "\""}),
                Ok(Value::Object(
                    json!({"\"": "\""}).as_object().unwrap().clone(),
                )),
            ),
            (
                &arg1,
                json!({"'": "'"}),
                Ok(Value::Object(
                    json!({"'": "'"}).as_object().unwrap().clone(),
                )),
            ),
            // error cases
            (
                &arg2,
                json!(null),
                Err(ErrorData(
                    "Invalid argument value: 'unNullableAny' argument is not allowed to be null",
                    "null",
                    0,
                    4,
                )),
            ),
        ] {
            let mut file = NamedTempFile::with_suffix(".json").expect("Failed to create temp file");
            writeln!(file, "{}", value).expect("Failed to write to temp file");
            file.flush().expect("Failed to flush temp file");
            let file_value = format!("file://{}", file.path().to_str().unwrap());

            match expected {
                Ok(expected) => {
                    let result = parser.parse(file_value.as_str(), arg.to_owned()).unwrap();
                    assert_eq!(result, SHResult::Value(expected));
                }
                Err(ErrorData(msg_target, _, at_target, _)) => {
                    let result = parser.parse(file_value.as_str(), arg.clone());
                    match result {
                        Err(AzError::UserUsage(
                            AzUserUsageError::InvalidShorthandSyntaxError {
                                msg,
                                data,
                                at,
                                range,
                            },
                        )) => {
                            assert_eq!(
                                (msg, data, at, range),
                                (
                                    msg_target.to_string(),
                                    file_value.to_string(),
                                    at_target,
                                    file_value.len()
                                )
                            );
                        }
                        _ => {
                            panic!("Unexpected result: {:?}", result);
                        }
                    }
                }
            }
        }
    }

    #[test]
    fn test_parse_complex_value_without_arg() {
        let parser = ShorthandSyntaxParser::default();

        let arg = None;

        for (arg, value, expected) in [
            (&arg, "abc", Ok(Value::String("abc".to_string()))),
            (
                &arg,
                "123",
                Ok(Value::Number(serde_json::Number::from(123))),
            ),
            (&arg, "true", Ok(Value::Bool(true))),
            (&arg, "false", Ok(Value::Bool(false))),
            (&arg, "null", Ok(Value::Null)),
            (
                &arg,
                "{a:b}",
                Ok(Value::Object(
                    json!({"a": "b"}).as_object().unwrap().clone(),
                )),
            ),
            (
                &arg,
                "[1,2,3]",
                Ok(Value::Array(json!([1, 2, 3]).as_array().unwrap().clone())),
            ),
            (&arg, "{windows:'c:\\temp\\filter.json',unix1:'/tmp/filter.json',unix2:/var/tmp/filter.json}", Ok(Value::Object(
                json!({
                    "windows":"c:\\temp\\filter.json",
                    "unix1":"/tmp/filter.json",
                    "unix2":"/var/tmp/filter.json"
                })
                .as_object()
                .unwrap()
                .clone()
            ))),
            (&arg, "{a:1,c:d,d0:{},l0:[],l1:['1','2','3'],l2:[{a:b,c:d},{e:f},null,'null',true,false],l3:[1,2,3],str1:'[1,2,3]',str2:'{a:b,c:d}'}", Ok(Value::Object(
                json!({
                    "a": 1,
                    "c": "d",
                    "d0": {},
                    "l0": [],
                    "l1": ["1", "2", "3"],
                    "l2": [
                        {"a": "b", "c": "d"},
                        {"e": "f"},
                        null,
                        "null",
                        true,
                        false,
                    ],
                    "l3": [1, 2, 3],
                    "str1":"[1,2,3]",
                    "str2":"{a:b,c:d}",
                })
                .as_object()
                .unwrap()
                .clone()
            ))),
            (
                &arg,
                "{']':']','[':'[','}':'}','{':'{'}",
                Ok(Value::Object(
                    json!({"]": "]", "[": "[", "}": "}", "{": "{"})
                        .as_object()
                        .unwrap()
                        .clone(),
                )),
            ),
            (
                &arg,
                "{'\"':'\"'}",
                Ok(Value::Object(
                    json!({"\"": "\""}).as_object().unwrap().clone(),
                )),
            ),
            (
                &arg,
                "{''/':''/'}",
                Ok(Value::Object(
                    json!({"'": "'"}).as_object().unwrap().clone(),
                )),
            ),
            // error cases
            (
                &arg,
                "{a:b,{:2}",
                Err(ErrorData(
                    "Failed to parse string, please wrap '{' character in single quotes",
                    "{a:b,{:2}",
                    5,
                    1,
                )),
            ),
            (
                &arg,
                "{a:b,[:2}",
                Err(ErrorData(
                    "Failed to parse string, please wrap '[' character in single quotes",
                    "{a:b,[:2}",
                    5,
                    1,
                )),
            ),
            (
                &arg,
                "[1, 2, 3]",
                Err(ErrorData(
                    "Failed to parse string, please wrap whitespace character in single quotes",
                    "[1, 2, 3]",
                    3,
                    1,
                )),
            ),
            (
                &arg,
                "{a:1,c:2},3",
                Err(ErrorData("Redundant tail", "{a:1,c:2},3", 9, 2)),
            ),
        ] {
            match expected {
                Ok(expected) => {
                    let result = parser.parse(value, arg.to_owned()).unwrap();
                    assert_eq!(result, SHResult::Value(expected));
                }
                Err(ErrorData(msg_target, data_target, at_target, range_target)) => {
                    let result = parser.parse(value, arg.to_owned());
                    match result {
                        Err(AzError::UserUsage(
                            AzUserUsageError::InvalidShorthandSyntaxError {
                                msg,
                                data,
                                at,
                                range,
                            },
                        )) => {
                            assert_eq!(
                                (msg, data, at, range),
                                (
                                    msg_target.to_string(),
                                    data_target.to_string(),
                                    at_target,
                                    range_target
                                )
                            );
                        }
                        _ => {
                            panic!("Unexpected result: {:?}", result);
                        }
                    }
                }
            }
        }
    }

    #[test]
    fn test_parse_object_arg() {
        let parser = ShorthandSyntaxParser::default();

        let mut cmd = CmdCommand::default();
        let mut object_arg = CmdArg::default();
        object_arg.var_name = "$Instance.object".to_string();
        object_arg.name = "object".to_string();
        object_arg.options = vec!["object".to_string()];
        object_arg.nullable = true;
        let mut object_type = CmdObjectArg::default();
        // define the string property type
        let mut str_arg = CmdArg::default();
        str_arg.name = "str".to_string();
        str_arg.options = vec!["str".to_string()];
        str_arg.nullable = true;
        str_arg.arg_type = Some(cmd_arg::ArgType::Str(CmdStringArg::default()));
        object_type.props.push(str_arg);
        // define the number property type
        let mut num_arg = CmdArg::default();
        num_arg.name = "num".to_string();
        num_arg.options = vec!["num".to_string()];
        num_arg.nullable = true;
        num_arg.arg_type = Some(cmd_arg::ArgType::Number(CmdNumberArg::default()));
        object_type.props.push(num_arg);
        // define the bool property type
        let mut bool_arg = CmdArg::default();
        bool_arg.name = "bool".to_string();
        bool_arg.options = vec!["bool".to_string()];
        bool_arg.nullable = true;
        bool_arg.arg_type = Some(cmd_arg::ArgType::Boolean(CmdBooleanArg::default()));
        object_type.props.push(bool_arg);
        // define the array property type
        let mut array_arg = CmdArg::default();
        array_arg.name = "array".to_string();
        array_arg.options = vec!["array".to_string()];
        array_arg.nullable = true;
        let mut array_type = CmdArrayArg::default();
        let mut array_item_arg = CmdArg::default();
        array_item_arg.nullable = true;
        array_item_arg.arg_type = Some(cmd_arg::ArgType::Str(CmdStringArg::default()));
        array_type.item = Some(Box::new(array_item_arg));
        array_arg.arg_type = Some(cmd_arg::ArgType::Array(Box::new(array_type)));
        object_type.props.push(array_arg);
        // define the dict property type
        let mut dict_arg = CmdArg::default();
        dict_arg.name = "tags".to_string();
        dict_arg.options = vec!["tags".to_string()];
        dict_arg.nullable = true;
        let mut dict_type = CmdObjectArg::default();
        let mut tag_arg = CmdArg::default();
        tag_arg.nullable = true;
        tag_arg.arg_type = Some(cmd_arg::ArgType::Str(CmdStringArg::default()));
        dict_type.additional_props = Some(Box::new(CmdObjectArgAdditionalProperties {
            item: Some(Box::new(tag_arg)),
        }));
        dict_arg.arg_type = Some(cmd_arg::ArgType::Object(Box::new(dict_type)));
        object_type.props.push(dict_arg);
        // define the additional properties
        let mut additional_props = CmdArg::default();
        additional_props.nullable = true;
        additional_props.arg_type = Some(cmd_arg::ArgType::AnyType(CmdAnyTypeArg::default()));
        object_type.additional_props = Some(Box::new(CmdObjectArgAdditionalProperties {
            item: Some(Box::new(additional_props)),
        }));
        object_arg.arg_type = Some(cmd_arg::ArgType::Object(Box::new(object_type)));
        cmd.args.push(object_arg);

        let arg1 = Some(CmdArgWrapper::new(Some(&cmd), &cmd.args[0]).unwrap());

        for (arg, value, expected) in [
            (&arg1, "{str:abc,num:123,bool:true,array:[1,b,false,true,null],tags:{a:b,c:1,d:false,e:true,f:null},additional1:1,additional2:false,additional3:[1,b,false,true,null],additional4:{a:b,c:1,d:false,e:true,f:null},additional5:[{a:b,c:d}]}", Ok(Value::Object(
                json!({
                    "str": "abc",
                    "num": 123,
                    "bool": true,
                    "array": ["1", "b", "false", "true", null],  // as the element of array is string so number and boolean are converted to string
                    "tags": {"a": "b", "c": "1", "d": "false", "e": "true", "f": null},  // as the item of dict is string so number and boolean are converted to string
                    "additional1": 1,
                    "additional2": false,
                    "additional3": [1, "b", false, true, null],
                    "additional4": {"a": "b", "c": 1, "d": false, "e": true, "f": null}, // as the item additional is any type so number and boolean are kept as number and boolean
                    "additional5": [{"a": "b", "c": "d"}]
                })
                .as_object()
                .unwrap()
                .clone()
            ))),
            (&arg1, "null", Ok(Value::Null)),
            (&arg1, "{str:null,num:null,bool:null,array:null,tags:null,additional1:null,additional2:null,additional3:null,additional4:null,additional5:null}", Ok(Value::Object(
                json!({
                    "str": null,
                    "num": null,
                    "bool": null,
                    "array": null,
                    "tags": null,
                    "additional1": null,
                    "additional2": null,
                    "additional3": null,
                    "additional4": null,
                    "additional5": null
                })
                .as_object()
                .unwrap()
                .clone()
            ))),
            // test error cases
            (&arg1, "[1,2,3]", Err(ErrorData("Invalid argument value: 'object' argument has invalid value type, expect [object] type value", "[1,2,3]", 0, 1))),
            (&arg1, "{num:abc}", Err(ErrorData("Invalid argument value: 'object.num' argument has invalid value type, expect [int] type value", "{num:abc}", 5, 4))),
            (&arg1, "{tags:{a:[1,2,3]}}", Err(ErrorData("Failed to parse string, please wrap '[' character in single quotes", "{tags:{a:[1,2,3]}}", 9, 1))),
            (&arg1, "{tags:{a:{a:b}}}", Err(ErrorData("Failed to parse string, please wrap '{' character in single quotes", "{tags:{a:{a:b}}}", 9, 1))),
        ] {
            match expected {
                Ok(expected) => {
                    let result = parser.parse(value, arg.to_owned()).unwrap();
                    assert_eq!(result, SHResult::Value(expected));
                }
                Err(ErrorData(msg_target, data_target, at_target, range_target)) => {
                    let result = parser.parse(value, arg.to_owned());
                    match result {
                        Err(AzError::UserUsage(
                            AzUserUsageError::InvalidShorthandSyntaxError {
                                msg,
                                data,
                                at,
                                range,
                            },
                        )) => {
                            assert_eq!(
                                (msg, data, at, range),
                                (
                                    msg_target.to_string(),
                                    data_target.to_string(),
                                    at_target,
                                    range_target
                                )
                            );
                        }
                        _ => {
                            panic!("Unexpected result: {:?}", result);
                        }
                    }
                }
            }
        }
    }

    #[test]
    fn test_parse_object_arg_from_file() {
        let parser = ShorthandSyntaxParser::default();

        let mut cmd = CmdCommand::default();
        let mut object_arg = CmdArg::default();
        object_arg.var_name = "$Instance.object".to_string();
        object_arg.name = "object".to_string();
        object_arg.options = vec!["object".to_string()];
        object_arg.nullable = true;
        let mut object_type = CmdObjectArg::default();
        // define the string property type
        let mut str_arg = CmdArg::default();
        str_arg.name = "str".to_string();
        str_arg.options = vec!["str".to_string()];
        str_arg.nullable = true;
        str_arg.arg_type = Some(cmd_arg::ArgType::Str(CmdStringArg::default()));
        object_type.props.push(str_arg);
        // define the number property type
        let mut num_arg = CmdArg::default();
        num_arg.name = "num".to_string();
        num_arg.options = vec!["num".to_string()];
        num_arg.nullable = true;
        num_arg.arg_type = Some(cmd_arg::ArgType::Number(CmdNumberArg::default()));
        object_type.props.push(num_arg);
        // define the bool property type
        let mut bool_arg = CmdArg::default();
        bool_arg.name = "bool".to_string();
        bool_arg.options = vec!["bool".to_string()];
        bool_arg.nullable = true;
        bool_arg.arg_type = Some(cmd_arg::ArgType::Boolean(CmdBooleanArg::default()));
        object_type.props.push(bool_arg);
        // define the array property type
        let mut array_arg = CmdArg::default();
        array_arg.name = "array".to_string();
        array_arg.options = vec!["array".to_string()];
        array_arg.nullable = true;
        let mut array_type = CmdArrayArg::default();
        let mut array_item_arg = CmdArg::default();
        array_item_arg.nullable = true;
        array_item_arg.arg_type = Some(cmd_arg::ArgType::Str(CmdStringArg::default()));
        array_type.item = Some(Box::new(array_item_arg));
        array_arg.arg_type = Some(cmd_arg::ArgType::Array(Box::new(array_type)));
        object_type.props.push(array_arg);
        // define the dict property type
        let mut dict_arg = CmdArg::default();
        dict_arg.name = "tags".to_string();
        dict_arg.options = vec!["tags".to_string()];
        dict_arg.nullable = true;
        let mut dict_type = CmdObjectArg::default();
        let mut tag_arg = CmdArg::default();
        tag_arg.nullable = true;
        tag_arg.arg_type = Some(cmd_arg::ArgType::Str(CmdStringArg::default()));
        dict_type.additional_props = Some(Box::new(CmdObjectArgAdditionalProperties {
            item: Some(Box::new(tag_arg)),
        }));
        dict_arg.arg_type = Some(cmd_arg::ArgType::Object(Box::new(dict_type)));
        object_type.props.push(dict_arg);
        // define the additional properties
        let mut additional_props = CmdArg::default();
        additional_props.nullable = true;
        additional_props.arg_type = Some(cmd_arg::ArgType::AnyType(CmdAnyTypeArg::default()));
        object_type.additional_props = Some(Box::new(CmdObjectArgAdditionalProperties {
            item: Some(Box::new(additional_props)),
        }));
        object_arg.arg_type = Some(cmd_arg::ArgType::Object(Box::new(object_type)));
        cmd.args.push(object_arg);

        let arg1 = Some(CmdArgWrapper::new(Some(&cmd), &cmd.args[0]).unwrap());

        for (arg, value, expected) in [
            (
                &arg1,
                json!({
                    "str": "abc",
                    "num": 123,
                    "bool": true,
                    "array": ["1", "b", "false", "true", null],  // as the element of array is string so number and boolean are converted to string
                    "tags": {"a": "b", "c": "1", "d": "false", "e": "true", "f": null},  // as the item of dict is string so number and boolean are converted to string
                    "additional1": 1,
                    "additional2": false,
                    "additional3": [1, "b", false, true, null],
                    "additional4": {"a": "b", "c": 1, "d": false, "e": true, "f": null}, // as the item additional is any type so number and boolean are kept as number and boolean
                    "additional5": [{"a": "b", "c": "d"}]
                }),
                Ok(Value::Object(
                    json!({
                        "str": "abc",
                        "num": 123,
                        "bool": true,
                        "array": ["1", "b", "false", "true", null],  // as the element of array is string so number and boolean are converted to string
                        "tags": {"a": "b", "c": "1", "d": "false", "e": "true", "f": null},  // as the item of dict is string so number and boolean are converted to string
                        "additional1": 1,
                        "additional2": false,
                        "additional3": [1, "b", false, true, null],
                        "additional4": {"a": "b", "c": 1, "d": false, "e": true, "f": null}, // as the item additional is any type so number and boolean are kept as number and boolean
                        "additional5": [{"a": "b", "c": "d"}]
                    })
                    .as_object()
                    .unwrap()
                    .clone(),
                )),
            ),
            (&arg1, json!(null), Ok(Value::Null)),
            (
                &arg1,
                json!({
                    "str": null,
                    "num": null,
                    "bool": null,
                    "array": null,
                    "tags": null,
                    "additional1": null,
                    "additional2": null,
                    "additional3": null,
                    "additional4": null,
                    "additional5": null
                }),
                Ok(Value::Object(
                    json!({
                        "str": null,
                        "num": null,
                        "bool": null,
                        "array": null,
                        "tags": null,
                        "additional1": null,
                        "additional2": null,
                        "additional3": null,
                        "additional4": null,
                        "additional5": null
                    })
                    .as_object()
                    .unwrap()
                    .clone(),
                )),
            ),
            // test error cases
            (
                &arg1,
                json!([1, 2, 3]),
                Err(ErrorData(
                    "Invalid argument value: 'object' argument has invalid value type, expect [object] type value",
                    "[1, 2, 3]",
                    0,
                    8,
                )),
            ),
            (
                &arg1,
                json!({"num":"abc"}),
                Err(ErrorData(
                    "Invalid argument value: 'object.num' argument has invalid value type, expect [int] type value",
                    "{\"num\":\"abc\"}",
                    0,
                    4,
                )),
            ),
            (
                &arg1,
                json!({"tags":{"a":[1,2,3]}}),
                Err(ErrorData(
                    "Invalid argument value: 'object.tags.a' argument has invalid value type, expect [string] type value",
                    "{\"tags\":{\"a\":[1,2,3]}}}",
                    0,
                    1,
                )),
            ),
            (
                &arg1,
                json!({"tags":{"a":{"a":"b"}}}),
                Err(ErrorData(
                    "Invalid argument value: 'object.tags.a' argument has invalid value type, expect [string] type value",
                    "{\"tags\":{\"a\":{\"a\":\"b\"}}}}",
                    0,
                    1,
                )),
            ),
        ] {
            let mut file = NamedTempFile::with_suffix(".json").expect("Failed to create temp file");
            writeln!(file, "{}", value).expect("Failed to write to temp file");
            file.flush().expect("Failed to flush temp file");
            let file_value = format!("file://{}", file.path().to_str().unwrap());

            match expected {
                Ok(expected) => {
                    let result = parser.parse(file_value.as_str(), arg.to_owned()).unwrap();
                    assert_eq!(result, SHResult::Value(expected));
                }
                Err(ErrorData(msg_target, _, at_target, _)) => {
                    let result = parser.parse(file_value.as_str(), arg.clone());
                    match result {
                        Err(AzError::UserUsage(
                            AzUserUsageError::InvalidShorthandSyntaxError {
                                msg,
                                data,
                                at,
                                range,
                            },
                        )) => {
                            assert_eq!(
                                (msg, data, at, range),
                                (
                                    msg_target.to_string(),
                                    file_value.to_string(),
                                    at_target,
                                    file_value.len()
                                )
                            );
                        }
                        _ => {
                            panic!("Unexpected result: {:?}", result);
                        }
                    }
                }
            }
        }

        // test invalid json file
        let mut file = NamedTempFile::with_suffix(".json").expect("Failed to create temp file");
        writeln!(file, "{}", "{\"a\": \"b").expect("Failed to write to temp file");
        file.flush().expect("Failed to flush temp file");
        let file_value = format!("file://{}", file.path().to_str().unwrap());
        let result = parser.parse(file_value.as_str(), arg1.to_owned());
        match result {
            Err(AzError::UserUsage(AzUserUsageError::InvalidShorthandSyntaxError {
                msg,
                data,
                at,
                range,
            })) => {
                assert!(msg.starts_with("Failed to parse json file:"));
                assert_eq!(
                    (data, at, range),
                    (file_value.to_string(), 0, file_value.len())
                );
            }
            _ => {
                panic!("Unexpected result: {:?}", result);
            }
        }
    }

    #[test]
    fn test_parse_dict_arg() {
        let parser = ShorthandSyntaxParser::default();

        let mut cmd = CmdCommand::default();
        let mut dict_arg = CmdArg::default();
        dict_arg.var_name = "$Instance.dict".to_string();
        dict_arg.name = "dict".to_string();
        dict_arg.options = vec!["dict".to_string()];
        dict_arg.nullable = true;
        let mut dict_type = CmdObjectArg::default();
        let mut key_arg = CmdArg::default();
        key_arg.nullable = true;
        key_arg.arg_type = Some(cmd_arg::ArgType::AnyType(CmdAnyTypeArg::default()));
        dict_type.additional_props = Some(Box::new(CmdObjectArgAdditionalProperties {
            item: Some(Box::new(key_arg)),
        }));
        dict_arg.arg_type = Some(cmd_arg::ArgType::Object(Box::new(dict_type)));
        cmd.args.push(dict_arg);
        // not allowed to be null
        let mut dict_arg = CmdArg::default();
        dict_arg.var_name = "$Instance.dict2".to_string();
        dict_arg.name = "dict2".to_string();
        dict_arg.options = vec!["dict2".to_string()];
        let mut dict_type = CmdObjectArg::default();
        let mut key_arg = CmdArg::default();
        key_arg.arg_type = Some(cmd_arg::ArgType::Str(CmdStringArg::default()));
        dict_type.additional_props = Some(Box::new(CmdObjectArgAdditionalProperties {
            item: Some(Box::new(key_arg)),
        }));
        dict_arg.arg_type = Some(cmd_arg::ArgType::Object(Box::new(dict_type)));
        cmd.args.push(dict_arg);

        let arg1 = Some(CmdArgWrapper::new(Some(&cmd), &cmd.args[0]).unwrap());
        let arg2 = Some(CmdArgWrapper::new(Some(&cmd), &cmd.args[1]).unwrap());

        for (arg, value, expected) in [
            (&arg1, "{a:b,c:1,d:true,e:null,f:[1,b,false,true,null],g:{a:b,c:1,d:false,e:true,f:null}}", Ok(Value::Object(
                json!({"a":"b","c":1,"d":true,"e":null,"f":[1,"b",false,true,null],"g":{"a":"b","c":1,"d":false,"e":true,"f":null}})
                    .as_object()
                    .unwrap()
                    .clone()
            ))),
            (&arg1, "null", Ok(Value::Null)),
            (&arg2, "{a:b,c:1,d:true}", Ok(Value::Object(
                json!({"a":"b","c":"1","d":"true"}  )
                    .as_object()
                    .unwrap()
                    .clone()
            ))),
            (&arg2, "{e:'null'}", Ok(Value::Object(json!({"e":"null"}).as_object().unwrap().clone()))),
            (&arg2, "{f:'[1,2,3]'}", Ok(Value::Object(json!({"f":"[1,2,3]"}).as_object().unwrap().clone()))),
            (&arg2, "{g:'{a:b}'}", Ok(Value::Object(json!({"g":"{a:b}"}).as_object().unwrap().clone()))),
            (&arg2, "{h:'hello world'}", Ok(Value::Object(json!({"h":"hello world"}).as_object().unwrap().clone()))),
            (&arg2, "{i:a,j:b,}", Ok(Value::Object(json!({"i":"a","j":"b"}).as_object().unwrap().clone()))),
            // test error cases
            (&arg1, "{a:b,f:[1,g:{a:b,f:null}}", Err(ErrorData("Expect character ',' or ']'", "{a:b,f:[1,g:{a:b,f:null}}", 11, 1))),
            (&arg1, "{a:b,g:{a:b,f:[1,2,3]}", Err(ErrorData("Expect character ',' or '}'", "{a:b,g:{a:b,f:[1,2,3]}", 22, 1))),
            (&arg2, "null", Err(ErrorData("Invalid argument value: 'dict2' argument is not allowed to be null", "null", 0, 4))),
            (&arg2, "{a:b,c:1,d:true,e:null}", Err(ErrorData("Invalid argument value: 'dict2.e' argument is not allowed to be null", "{a:b,c:1,d:true,e:null}", 18, 5))),
            (&arg2, "{f:[1,2,3]}", Err(ErrorData("Failed to parse string, please wrap '[' character in single quotes", "{f:[1,2,3]}", 3, 1))),
            (&arg2, "{g:{a:b}}", Err(ErrorData("Failed to parse string, please wrap '{' character in single quotes", "{g:{a:b}}", 3, 1))),
            (&arg2, "{h:hello world}", Err(ErrorData("Failed to parse string, please wrap whitespace character in single quotes", "{h:hello world}", 8, 1))),
        ] {
            match expected {
                Ok(expected) => {
                    let result = parser.parse(value, arg.to_owned()).unwrap();
                    assert_eq!(result, SHResult::Value(expected));
                }
                Err(ErrorData(msg_target, data_target, at_target, range_target)) => {
                    let result = parser.parse(value, arg.to_owned());
                    match result {
                        Err(AzError::UserUsage(
                            AzUserUsageError::InvalidShorthandSyntaxError {
                                msg,
                                data,
                                at,
                                range,
                            },
                        )) => {
                            assert_eq!(
                                (msg, data, at, range),
                                (
                                    msg_target.to_string(),
                                    data_target.to_string(),
                                    at_target,
                                    range_target
                                )
                            );
                        }
                        _ => {
                            panic!("Unexpected result: {:?}", result);
                        }
                    }
                }
            }
        }
    }

    #[test]
    fn test_parse_array_arg() {
        let parser = ShorthandSyntaxParser::default();

        let mut cmd = CmdCommand::default();
        let mut arr_arg = CmdArg::default();
        arr_arg.var_name = "$Instance.arr".to_string();
        arr_arg.name = "arr".to_string();
        arr_arg.options = vec!["arr".to_string()];
        arr_arg.nullable = true;
        let mut arr_type = CmdArrayArg::default();
        let mut arr_item_arg = CmdArg::default();
        arr_item_arg.nullable = true;
        arr_item_arg.arg_type = Some(cmd_arg::ArgType::AnyType(CmdAnyTypeArg::default()));
        arr_type.item = Some(Box::new(arr_item_arg));
        arr_arg.arg_type = Some(cmd_arg::ArgType::Array(Box::new(arr_type)));
        cmd.args.push(arr_arg);

        let mut arr_arg = CmdArg::default();
        arr_arg.var_name = "$Instance.arr2".to_string();
        arr_arg.name = "arr2".to_string();
        arr_arg.options = vec!["arr2".to_string()];
        let mut arr_type = CmdArrayArg::default();
        let mut arr_item_arg = CmdArg::default();
        arr_item_arg.arg_type = Some(cmd_arg::ArgType::Str(CmdStringArg::default()));
        arr_type.item = Some(Box::new(arr_item_arg));
        arr_arg.arg_type = Some(cmd_arg::ArgType::Array(Box::new(arr_type)));
        cmd.args.push(arr_arg);

        let arg1 = Some(CmdArgWrapper::new(Some(&cmd), &cmd.args[0]).unwrap());
        let arg2 = Some(CmdArgWrapper::new(Some(&cmd), &cmd.args[1]).unwrap());

        for (arg, value, expected) in [
            (&arg1, "[1,2,3]", Ok(Value::Array(json!([1,2,3]).as_array().unwrap().clone()))),
            (&arg1, "null", Ok(Value::Null)),
            (&arg1, "['a','b','c']", Ok(Value::Array(json!(["a","b","c"]).as_array().unwrap().clone()))),
            (&arg1, "['a',1,true]", Ok(Value::Array(json!(["a",1,true]).as_array().unwrap().clone()))),
            (&arg1, "['a',1,true,]", Ok(Value::Array(json!(["a",1,true]).as_array().unwrap().clone()))),
            (&arg1, "[1,true,false,null,abc,[1,2,3],{a:b,c:1,d:false,e:true,f:null}]", Ok(Value::Array(json!([1,true,false,null,"abc",[1,2,3],{"a":"b","c":1,"d":false,"e":true,"f":null}]).as_array().unwrap().clone()))),
            (&arg2, "[a,1,false,true,1.2,'null',''/','[1, 2, 3]','{\"a\":\"b\"}']", Ok(Value::Array(json!(["a","1","false","true","1.2","null","'","[1, 2, 3]","{\"a\":\"b\"}"]).as_array().unwrap().clone()))),
            (&arg2, "[a,1,false,true,1.2,]", Ok(Value::Array(json!(["a","1","false","true","1.2"]).as_array().unwrap().clone()))),
            // test error cases
            (&arg1, "[{a:b]", Err(ErrorData("Expect character ',' or '}'", "[{a:b]", 5, 1))),
            (&arg1, "[[1,2,3]", Err(ErrorData("Expect character ',' or ']'", "[[1,2,3]", 8, 1))),
            (&arg2, "null", Err(ErrorData("Invalid argument value: 'arr2' argument is not allowed to be null", "null", 0, 4))),
            (&arg2, "{a:b}", Err(ErrorData("Invalid argument value: 'arr2' argument has invalid value type, expect [list] type value", "{a:b}", 0, 1))),
            (&arg2, "[a,null]", Err(ErrorData("Invalid argument value: 'arr2[1]' argument is not allowed to be null", "[a,null]", 3, 5))),
        ] {
            match expected {
                Ok(expected) => {
                    let result = parser.parse(value, arg.to_owned()).unwrap();
                    assert_eq!(result, SHResult::Value(expected));
                }
                Err(ErrorData(msg_target, data_target, at_target, range_target)) => {
                    let result = parser.parse(value, arg.to_owned());
                    match result {
                        Err(AzError::UserUsage(
                            AzUserUsageError::InvalidShorthandSyntaxError {
                                msg,
                                data,
                                at,
                                range,
                            },
                        )) => {
                            assert_eq!(
                                (msg, data, at, range),
                                (
                                    msg_target.to_string(),
                                    data_target.to_string(),
                                    at_target,
                                    range_target
                                )
                            );
                        }
                        _ => {
                            panic!("Unexpected result: {:?}", result);
                        }
                    }
                }
            }
        }
    }

    #[test]
    fn test_parse_array_arg_from_file() {
        let parser = ShorthandSyntaxParser::default();

        let mut cmd = CmdCommand::default();
        let mut arr_arg = CmdArg::default();
        arr_arg.var_name = "$Instance.arr".to_string();
        arr_arg.name = "arr".to_string();
        arr_arg.options = vec!["arr".to_string()];
        arr_arg.nullable = true;
        let mut arr_type = CmdArrayArg::default();
        let mut arr_item_arg = CmdArg::default();
        arr_item_arg.nullable = true;
        arr_item_arg.arg_type = Some(cmd_arg::ArgType::AnyType(CmdAnyTypeArg::default()));
        arr_type.item = Some(Box::new(arr_item_arg));
        arr_arg.arg_type = Some(cmd_arg::ArgType::Array(Box::new(arr_type)));
        cmd.args.push(arr_arg);

        let mut arr_arg = CmdArg::default();
        arr_arg.var_name = "$Instance.arr2".to_string();
        arr_arg.name = "arr2".to_string();
        arr_arg.options = vec!["arr2".to_string()];
        let mut arr_type = CmdArrayArg::default();
        let mut arr_item_arg = CmdArg::default();
        arr_item_arg.arg_type = Some(cmd_arg::ArgType::Str(CmdStringArg::default()));
        arr_type.item = Some(Box::new(arr_item_arg));
        arr_arg.arg_type = Some(cmd_arg::ArgType::Array(Box::new(arr_type)));
        cmd.args.push(arr_arg);

        let arg1 = Some(CmdArgWrapper::new(Some(&cmd), &cmd.args[0]).unwrap());
        let arg2 = Some(CmdArgWrapper::new(Some(&cmd), &cmd.args[1]).unwrap());

        for (arg, value, expected) in [
            (&arg1, json!([1,2,3]), Ok(Value::Array(json!([1,2,3]).as_array().unwrap().clone()))),
            (&arg1, json!(null), Ok(Value::Null)),
            (&arg1, json!(["a","b","c"]), Ok(Value::Array(json!(["a","b","c"]).as_array().unwrap().clone()))),
            (&arg1, json!(["a",1,true]), Ok(Value::Array(json!(["a",1,true]).as_array().unwrap().clone()))),
            (&arg1, json!([1,true,false,null,"abc",[1,2,3],{"a":"b","c":1,"d":false,"e":true,"f":null}]), Ok(Value::Array(json!([1,true,false,null,"abc",[1,2,3],{"a":"b","c":1,"d":false,"e":true,"f":null}]).as_array().unwrap().clone()))),
            (&arg2, json!(["a","1","false","true","1.2","null","'","[1, 2, 3]","{\"a\":\"b\"}"]), Ok(Value::Array(json!(["a","1","false","true","1.2","null","'","[1, 2, 3]","{\"a\":\"b\"}"]).as_array().unwrap().clone()))),
            (&arg2, json!(["a","1","false","true","1.2"]), Ok(Value::Array(json!(["a","1","false","true","1.2"]).as_array().unwrap().clone()))),
            // test error cases
            (&arg2, json!(null), Err(ErrorData("Invalid argument value: 'arr2' argument is not allowed to be null", "null", 0, 4))),
            (&arg2, json!({"a":"b"}), Err(ErrorData("Invalid argument value: 'arr2' argument has invalid value type, expect [list] type value", "{\"a\":\"b\"}", 0, 1))),
            (&arg2, json!(["a",null]), Err(ErrorData("Invalid argument value: 'arr2[1]' argument is not allowed to be null", "[a,null]", 0, 5))),
            (&arg2, json!(["a",[1, 2, 3]]), Err(ErrorData("Invalid argument value: 'arr2[1]' argument has invalid value type, expect [string] type value", "[a,[1,2,3]]", 0, 5))),
            (&arg2, json!(["a",{"b":"c"}]), Err(ErrorData("Invalid argument value: 'arr2[1]' argument has invalid value type, expect [string] type value", "[a,{b:c}]", 0, 5))),
        ] {
            let mut file = NamedTempFile::with_suffix(".json").expect("Failed to create temp file");
            writeln!(file, "{}", value).expect("Failed to write to temp file");
            file.flush().expect("Failed to flush temp file");
            let file_value = format!("file://{}", file.path().to_str().unwrap());

            match expected {
                Ok(expected) => {
                    let result = parser.parse(file_value.as_str(), arg.to_owned()).unwrap();
                    assert_eq!(result, SHResult::Value(expected));
                }
                Err(ErrorData(msg_target, _, at_target, _)) => {
                    let result = parser.parse(file_value.as_str(), arg.clone());
                    match result {
                        Err(AzError::UserUsage(
                            AzUserUsageError::InvalidShorthandSyntaxError {
                                msg,
                                data,
                                at,
                                range,
                            },
                        )) => {
                            assert_eq!(
                                (msg, data, at, range),
                                (
                                    msg_target.to_string(),
                                    file_value.to_string(),
                                    at_target,
                                    file_value.len()
                                )
                            );
                        }
                        _ => {
                            panic!("Unexpected result: {:?}", result);
                        }
                    }
                }
            }
        }

        // test invalid json file
        let mut file = NamedTempFile::with_suffix(".json").expect("Failed to create temp file");
        writeln!(file, "{}", "[\"a\", \"b").expect("Failed to write to temp file");
        file.flush().expect("Failed to flush temp file");
        let file_value = format!("file://{}", file.path().to_str().unwrap());
        let result = parser.parse(file_value.as_str(), arg1.to_owned());
        match result {
            Err(AzError::UserUsage(AzUserUsageError::InvalidShorthandSyntaxError {
                msg,
                data,
                at,
                range,
            })) => {
                assert!(msg.starts_with("Failed to parse json file:"));
                assert_eq!(
                    (data, at, range),
                    (file_value.to_string(), 0, file_value.len())
                );
            }
            _ => {
                panic!("Unexpected result: {:?}", result);
            }
        }
    }

    #[test]
    fn test_parse_single_quote_string() {
        let parser = ShorthandSyntaxParser::default();

        for (value, expected) in [
            ("'hello world'", Ok(("hello world".to_string(), 13))),
            ("'hello world'abc", Ok(("hello world".to_string(), 13))),
            (
                "'hello world'/ab/c//'",
                Ok(("hello world'ab/c//".to_string(), 21)),
            ),
            (
                "'hello world",
                Err(ErrorData(
                    "Miss the end single quote character: '",
                    "'hello world",
                    12,
                    1,
                )),
            ),
            (
                "'hello world'/abc",
                Err(ErrorData(
                    "Miss the end single quote character: '",
                    "'hello world'/abc",
                    17,
                    1,
                )),
            ),
        ] {
            match expected {
                Ok((v, length)) => {
                    let mut remain = value.chars();
                    let result = parser.parse_single_quotes_string(&mut remain).unwrap();
                    assert_eq!(result, (v, length));
                }
                Err(ErrorData(msg_target, data_target, at_target, range_target)) => {
                    let mut remain = value.chars();
                    let result = parser.parse_single_quotes_string(&mut remain);
                    match result {
                        Err(AzUserUsageError::InvalidShorthandSyntaxError {
                            msg,
                            data,
                            at,
                            range,
                        }) => {
                            assert_eq!(
                                (msg, data, at, range),
                                (
                                    msg_target.to_string(),
                                    data_target.to_string(),
                                    at_target,
                                    range_target
                                )
                            );
                        }
                        _ => {
                            panic!("Unexpected result: {:?}", result);
                        }
                    }
                }
            }
        }
    }

    #[test]
    fn test_parse_string_convert_simple_type() {
        let parser = ShorthandSyntaxParser::default();

        for (value, expected) in [
            ("abc", Ok((Value::String("abc".to_string()), 3))),
            ("'abc'", Ok((Value::String("abc".to_string()), 5))),
            ("123", Ok((Value::Number(serde_json::Number::from(123)), 3))),
            ("'123'", Ok((Value::String("123".to_string()), 5))),
            ("true", Ok((Value::Bool(true), 4))),
            ("'true'", Ok((Value::String("true".to_string()), 6))),
            ("false", Ok((Value::Bool(false), 5))),
            ("'false'", Ok((Value::String("false".to_string()), 7))),
            ("null", Ok((Value::Null, 4))),
            ("'null'", Ok((Value::String("null".to_string()), 6))),
            (
                "'{\"a\":\"b\"}'",
                Ok((Value::String("{\"a\":\"b\"}".to_string()), 11)),
            ),
            ("'[1,2,3]'", Ok((Value::String("[1,2,3]".to_string()), 9))),
        ] {
            match expected {
                Ok((v, length)) => {
                    let mut remain = value.chars();
                    let result = parser.parse_string(&mut remain, true, true).unwrap();
                    assert_eq!(result, (Some(v), length));
                }
                Err(ErrorData(msg_target, data_target, at_target, range_target)) => {
                    let mut remain = value.chars();
                    let result = parser.parse_string(&mut remain, true, true);
                    match result {
                        Err(AzUserUsageError::InvalidShorthandSyntaxError {
                            msg,
                            data,
                            at,
                            range,
                        }) => {
                            assert_eq!(
                                (msg, data, at, range),
                                (
                                    msg_target.to_string(),
                                    data_target.to_string(),
                                    at_target,
                                    range_target
                                )
                            );
                        }
                        _ => {
                            panic!("Unexpected result: {:?}", result);
                        }
                    }
                }
            }
        }
    }

    #[test]
    fn test_parse_string_not_convert_simple_type() {
        let parser = ShorthandSyntaxParser::default();

        for (value, expected) in [
            ("abc", Ok((Value::String("abc".to_string()), 3))),
            ("'abc'", Ok((Value::String("abc".to_string()), 5))),
            ("123", Ok((Value::String("123".to_string()), 3))),
            ("'123'", Ok((Value::String("123".to_string()), 5))),
            ("true", Ok((Value::String("true".to_string()), 4))),
            ("'true'", Ok((Value::String("true".to_string()), 6))),
            ("false", Ok((Value::String("false".to_string()), 5))),
            ("'false'", Ok((Value::String("false".to_string()), 7))),
            ("null", Ok((Value::Null, 4))),
            ("'null'", Ok((Value::String("null".to_string()), 6))),
            (
                "'{\"a\":\"b\"}'",
                Ok((Value::String("{\"a\":\"b\"}".to_string()), 11)),
            ),
            ("'[1,2,3]'", Ok((Value::String("[1,2,3]".to_string()), 9))),
        ] {
            match expected {
                Ok((v, length)) => {
                    let mut remain = value.chars();
                    let result = parser.parse_string(&mut remain, false, true).unwrap();
                    assert_eq!(result, (Some(v), length));
                }
                Err(ErrorData(msg_target, data_target, at_target, range_target)) => {
                    let mut remain = value.chars();
                    let result = parser.parse_string(&mut remain, false, true);
                    match result {
                        Err(AzUserUsageError::InvalidShorthandSyntaxError {
                            msg,
                            data,
                            at,
                            range,
                        }) => {
                            assert_eq!(
                                (msg, data, at, range),
                                (
                                    msg_target.to_string(),
                                    data_target.to_string(),
                                    at_target,
                                    range_target
                                )
                            );
                        }
                        _ => {
                            panic!("Unexpected result: {:?}", result);
                        }
                    }
                }
            }
        }
    }

    #[test]
    fn test_parse_string_part() {
        let parser = ShorthandSyntaxParser::default();

        for (value, expected) in [
            ("abc,def", Ok((Value::String("abc".to_string()), 3, ",def"))),
            (
                "'a b c',def",
                Ok((Value::String("a b c".to_string()), 7, ",def")),
            ),
            ("abc:def", Ok((Value::String("abc".to_string()), 3, ":def"))),
            (
                "abc],def",
                Ok((Value::String("abc".to_string()), 3, "],def")),
            ),
            (
                "abc},def",
                Ok((Value::String("abc".to_string()), 3, "},def")),
            ),
            (
                "'',def,ghi",
                Ok((Value::String("".to_string()), 2, ",def,ghi")),
            ),
            // error cases
            (
                ",def",
                Err(ErrorData(
                    "Failed to parse string, please use '' wrapped empty string",
                    ",def",
                    0,
                    1,
                )),
            ),
            (
                "",
                Err(ErrorData(
                    "Failed to parse string, please use '' wrapped empty string",
                    "",
                    0,
                    1,
                )),
            ),
            (
                "a'bc,def,ghi",
                Err(ErrorData(
                    "Failed to parse string, please wrap ''' character in single quotes",
                    "a'bc,def,ghi",
                    1,
                    1,
                )),
            ),
            (
                "a\"bc,def,ghi",
                Err(ErrorData(
                    "Failed to parse string, please wrap '\"' character in single quotes",
                    "a\"bc,def,ghi",
                    1,
                    1,
                )),
            ),
            (
                "a bc,def,ghi",
                Err(ErrorData(
                    "Failed to parse string, please wrap whitespace character in single quotes",
                    "a bc,def,ghi",
                    1,
                    1,
                )),
            ),
            (
                "a\tbc,def,ghi",
                Err(ErrorData(
                    "Failed to parse string, please wrap whitespace character in single quotes",
                    "a\tbc,def,ghi",
                    1,
                    1,
                )),
            ),
            (
                "a\nbc,def,ghi",
                Err(ErrorData(
                    "Failed to parse string, please wrap whitespace character in single quotes",
                    "a\nbc,def,ghi",
                    1,
                    1,
                )),
            ),
            (
                "a\rbc,def,ghi",
                Err(ErrorData(
                    "Failed to parse string, please wrap whitespace character in single quotes",
                    "a\rbc,def,ghi",
                    1,
                    1,
                )),
            ),
        ] {
            match expected {
                Ok((v, length, tail)) => {
                    let mut remain = value.chars();
                    let result = parser.parse_string(&mut remain, false, true).unwrap();
                    assert_eq!(result, (Some(v), length));
                    assert_eq!(remain.as_str(), tail);
                }
                Err(ErrorData(msg_target, data_target, at_target, range_target)) => {
                    let mut remain = value.chars();
                    let result = parser.parse_string(&mut remain, false, true);
                    match result {
                        Err(AzUserUsageError::InvalidShorthandSyntaxError {
                            msg,
                            data,
                            at,
                            range,
                        }) => {
                            assert_eq!(
                                (msg, data, at, range),
                                (
                                    msg_target.to_string(),
                                    data_target.to_string(),
                                    at_target,
                                    range_target
                                )
                            );
                        }
                        _ => {
                            panic!("Unexpected result: {:?}", result);
                        }
                    }
                }
            }
        }
    }

    #[test]
    fn test_split_partial_value_key() {
        let parser = ShorthandSyntaxParser::default();

        for (value, expected) in [
            ("a=", Some(("a", None))),
            ("a=''", Some(("a", Some("''")))),
            ("'a'/=b'[1].b=", Some(("'a'/=b'[1].b", None))),
            ("a[]={c:d}", Some(("a[]", Some("{c:d}")))),
            ("[0].b[0]='{c:d}'", Some(("[0].b[0]", Some("'{c:d}'")))),
            ("+=[1,2,3]", Some(("+", Some("[1,2,3]")))),
            (
                "+=[{a:b},{b:c},{c:d}]",
                Some(("+", Some("[{a:b},{b:c},{c:d}]"))),
            ),
            (
                "arr+=[{a:b},{b:c},{c:d}]",
                Some(("arr+", Some("[{a:b},{b:c},{c:d}]"))),
            ),
            ("b[0].c+=[a,b,c]", Some(("b[0].c+", Some("[a,b,c]")))),
            ("matrix[0]+=[1,2,3]", Some(("matrix[0]+", Some("[1,2,3]")))),
            ("a={c:d}", Some(("a", Some("{c:d}")))),
            ("a.b='{c:d}'", Some(("a.b", Some("'{c:d}'")))),
            ("a[1]={c:d}", Some(("a[1]", Some("{c:d}")))),
            ("a[1].b={c:d}", Some(("a[1].b", Some("{c:d}")))),
            ("a[1][2]={c:d}", Some(("a[1][2]", Some("{c:d}")))),
            ("'a b'={c:d}", Some(("'a b'", Some("{c:d}")))),
            ("'a '/'={c:d}", Some(("'a '/'", Some("{c:d}")))),
            ("'a '/'.b={c:d}", Some(("'a '/'.b", Some("{c:d}")))),
            ("'a '/'[1]={c:d}", Some(("'a '/'[1]", Some("{c:d}")))),
            ("'a //'[1]={c:d}", Some(("'a //'[1]", Some("{c:d}")))),
            ("'a=b'[1].b={c:d}", Some(("'a=b'[1].b", Some("{c:d}")))),
            ("'a'/=b'[1].b=", Some(("'a'/=b'[1].b", None))),
            ("a.b[1].d={c:d}", Some(("a.b[1].d", Some("{c:d}")))),
            ("a.b[1][2].d={c:d}", Some(("a.b[1][2].d", Some("{c:d}")))),
            ("a.'b cd'.d={c:d}", Some(("a.'b cd'.d", Some("{c:d}")))),
            ("a.'b c'/d'.d={c:d}", Some(("a.'b c'/d'.d", Some("{c:d}")))),
            ("[1]={c:d}", Some(("[1]", Some("{c:d}")))),
            ("[1].b={c:d}", Some(("[1].b", Some("{c:d}")))),
            ("[1][2]={c:d}", Some(("[1][2]", Some("{c:d}")))),
            ("[1][2].b={c:d}", Some(("[1][2].b", Some("{c:d}")))),
            // test invalid cases which will not blocked by regex, but will raise error when parse partial key parts
            ("a[]={c:d}", Some(("a[]", Some("{c:d}")))),
            ("[1]..b={c:d}", Some(("[1]..b", Some("{c:d}")))),
            ("[1].b.={c:d}", Some(("[1].b.", Some("{c:d}")))),
            ("[1].[2]={c:d}", Some(("[1].[2]", Some("{c:d}")))),
            // test invalid cases which should be blocked by regex
            ("[a]={c:d}", None),
            ("a.'b cd'e={c:d}", None),
            ("a[1]b={c:d}", None),
            (".a={c:d}", None),
        ] {
            let result = parser.split_partial_value(&value);
            assert_eq!(result, expected);
        }
    }

    #[test]
    fn test_parse_partial_value_key_parts() {
        let parser = ShorthandSyntaxParser::default();

        for (value, expected) in [
            (
                "a",
                Ok((vec![SHPartialKeyPart::parse_name("a").unwrap()], false)),
            ),
            (
                "a.b",
                Ok((
                    vec![
                        SHPartialKeyPart::parse_name("a").unwrap(),
                        SHPartialKeyPart::parse_name("b").unwrap(),
                    ],
                    false,
                )),
            ),
            (
                "a[1]",
                Ok((
                    vec![
                        SHPartialKeyPart::parse_name("a").unwrap(),
                        SHPartialKeyPart::parse_index("1").unwrap(),
                    ],
                    false,
                )),
            ),
            ("+", Ok((vec![], true))),
            (
                "a[1].b+",
                Ok((
                    vec![
                        SHPartialKeyPart::parse_name("a").unwrap(),
                        SHPartialKeyPart::Index(1),
                        SHPartialKeyPart::parse_name("b").unwrap(),
                    ],
                    true,
                )),
            ),
            (
                "[2].b[0]+",
                Ok((
                    vec![
                        SHPartialKeyPart::Index(2),
                        SHPartialKeyPart::parse_name("b").unwrap(),
                        SHPartialKeyPart::Index(0),
                    ],
                    true,
                )),
            ),
            (
                "[1][2].'a[1]+'",
                Ok((
                    vec![
                        SHPartialKeyPart::Index(1),
                        SHPartialKeyPart::Index(2),
                        SHPartialKeyPart::parse_name("a[1]+").unwrap(),
                    ],
                    false,
                )),
            ),
            (
                "[1][2].'a[1]+'+",
                Ok((
                    vec![
                        SHPartialKeyPart::Index(1),
                        SHPartialKeyPart::Index(2),
                        SHPartialKeyPart::parse_name("a[1]+").unwrap(),
                    ],
                    true,
                )),
            ),
            (
                "a[1].b",
                Ok((
                    vec![
                        SHPartialKeyPart::parse_name("a").unwrap(),
                        SHPartialKeyPart::parse_index("1").unwrap(),
                        SHPartialKeyPart::parse_name("b").unwrap(),
                    ],
                    false,
                )),
            ),
            (
                "a[1][2]",
                Ok((
                    vec![
                        SHPartialKeyPart::parse_name("a").unwrap(),
                        SHPartialKeyPart::parse_index("1").unwrap(),
                        SHPartialKeyPart::parse_index("2").unwrap(),
                    ],
                    false,
                )),
            ),
            (
                "'a b'",
                Ok((vec![SHPartialKeyPart::parse_name("a b").unwrap()], false)),
            ),
            (
                "'a '/'",
                Ok((vec![SHPartialKeyPart::parse_name("a '").unwrap()], false)),
            ),
            (
                "'a '/'.b",
                Ok((
                    vec![
                        SHPartialKeyPart::parse_name("a '").unwrap(),
                        SHPartialKeyPart::parse_name("b").unwrap(),
                    ],
                    false,
                )),
            ),
            (
                "'a '/'[1]",
                Ok((
                    vec![
                        SHPartialKeyPart::parse_name("a '").unwrap(),
                        SHPartialKeyPart::parse_index("1").unwrap(),
                    ],
                    false,
                )),
            ),
            (
                "'a //'[1]",
                Ok((
                    vec![
                        SHPartialKeyPart::parse_name("a //").unwrap(),
                        SHPartialKeyPart::parse_index("1").unwrap(),
                    ],
                    false,
                )),
            ),
            (
                "'a=b'[1].b",
                Ok((
                    vec![
                        SHPartialKeyPart::parse_name("a=b").unwrap(),
                        SHPartialKeyPart::parse_index("1").unwrap(),
                        SHPartialKeyPart::parse_name("b").unwrap(),
                    ],
                    false,
                )),
            ),
            (
                "'a'/=b'[1].b",
                Ok((
                    vec![
                        SHPartialKeyPart::parse_name("a'=b").unwrap(),
                        SHPartialKeyPart::parse_index("1").unwrap(),
                        SHPartialKeyPart::parse_name("b").unwrap(),
                    ],
                    false,
                )),
            ),
            (
                "a.b[1].d",
                Ok((
                    vec![
                        SHPartialKeyPart::parse_name("a").unwrap(),
                        SHPartialKeyPart::parse_name("b").unwrap(),
                        SHPartialKeyPart::parse_index("1").unwrap(),
                        SHPartialKeyPart::parse_name("d").unwrap(),
                    ],
                    false,
                )),
            ),
            (
                "a[1].b+",
                Ok((
                    vec![
                        SHPartialKeyPart::parse_name("a").unwrap(),
                        SHPartialKeyPart::Index(1),
                        SHPartialKeyPart::parse_name("b").unwrap(),
                    ],
                    true,
                )),
            ),
            (
                "a.b[1][2].d",
                Ok((
                    vec![
                        SHPartialKeyPart::parse_name("a").unwrap(),
                        SHPartialKeyPart::parse_name("b").unwrap(),
                        SHPartialKeyPart::parse_index("1").unwrap(),
                        SHPartialKeyPart::parse_index("2").unwrap(),
                        SHPartialKeyPart::parse_name("d").unwrap(),
                    ],
                    false,
                )),
            ),
            (
                "a.'b cd'.d",
                Ok((
                    vec![
                        SHPartialKeyPart::parse_name("a").unwrap(),
                        SHPartialKeyPart::parse_name("b cd").unwrap(),
                        SHPartialKeyPart::parse_name("d").unwrap(),
                    ],
                    false,
                )),
            ),
            (
                "a.'b c'/d'.d",
                Ok((
                    vec![
                        SHPartialKeyPart::parse_name("a").unwrap(),
                        SHPartialKeyPart::parse_name("b c'd").unwrap(),
                        SHPartialKeyPart::parse_name("d").unwrap(),
                    ],
                    false,
                )),
            ),
            (
                "[1]",
                Ok((vec![SHPartialKeyPart::parse_index("1").unwrap()], false)),
            ),
            (
                "[1].b",
                Ok((
                    vec![
                        SHPartialKeyPart::parse_index("1").unwrap(),
                        SHPartialKeyPart::parse_name("b").unwrap(),
                    ],
                    false,
                )),
            ),
            (
                "[1][2]",
                Ok((
                    vec![
                        SHPartialKeyPart::parse_index("1").unwrap(),
                        SHPartialKeyPart::parse_index("2").unwrap(),
                    ],
                    false,
                )),
            ),
            (
                "[1][2].b",
                Ok((
                    vec![
                        SHPartialKeyPart::parse_index("1").unwrap(),
                        SHPartialKeyPart::parse_index("2").unwrap(),
                        SHPartialKeyPart::parse_name("b").unwrap(),
                    ],
                    false,
                )),
            ),
            (
                "1.2.3",
                Ok((
                    vec![
                        SHPartialKeyPart::parse_name("1").unwrap(),
                        SHPartialKeyPart::parse_name("2").unwrap(),
                        SHPartialKeyPart::parse_name("3").unwrap(),
                    ],
                    false,
                )),
            ),
            // error cases
            (
                "a[1.b",
                Err(ErrorData("Expect character ']'", "a[1.b", 3, 1)),
            ),
            (
                "a.b[1.b",
                Err(ErrorData("Expect character ']'", "a.b[1.b", 5, 1)),
            ),
            ("a[].b", Err(ErrorData("Miss prop name", "a[].b", 1, 2))),
            ("a.b[].c", Err(ErrorData("Miss prop name", "a.b[].c", 3, 2))),
            ("a.''.c", Err(ErrorData("Miss prop name", "a.''.c", 1, 3))),
            ("[1]..b", Err(ErrorData("Miss prop name", "[1]..b", 3, 1))),
            ("[1].b.", Err(ErrorData("Miss prop name", "[1].b.", 5, 1))),
            ("[1].[2]", Err(ErrorData("Miss prop name", "[1].[2]", 3, 1))),
            (
                "b[a]",
                Err(ErrorData(
                    "Invalid index, support integer for direct index.",
                    "b[a]",
                    1,
                    3,
                )),
            ),
        ] {
            match expected {
                Ok((parts, is_append)) => {
                    let result = parser.parse_partial_value_key_parts(&value).unwrap();
                    assert_eq!(result, (SHPartialKeys::new(parts), is_append));
                }
                Err(ErrorData(msg_target, data_target, at_target, range_target)) => {
                    let result = parser.parse_partial_value_key_parts(&value);
                    match result {
                        Err(AzUserUsageError::InvalidShorthandSyntaxError {
                            msg,
                            data,
                            at,
                            range,
                        }) => {
                            assert_eq!(
                                (msg, data, at, range),
                                (
                                    msg_target.to_string(),
                                    data_target.to_string(),
                                    at_target,
                                    range_target
                                )
                            );
                        }
                        _ => {
                            panic!("Unexpected result: {:?}", result);
                        }
                    }
                }
            }
        }
    }
}
