use serde_json::Value;

use crate::{
    error::AzUserUsageError,
    pb::command::{
        CmdArrayFormat, CmdBinaryFormat, CmdByteFormat, CmdDateFormat, CmdDateTimeFormat,
        CmdDoubleFormat, CmdDurationFormat, CmdFloatFormat, CmdInt32Format, CmdInt64Format,
        CmdObjectFormat, CmdPasswordFormat, CmdResourceGroupNameFormat, CmdResourceIdFormat,
        CmdResourceLocationFormat, CmdStringFormat, CmdSubscriptionIdFormat, CmdTimeFormat,
        CmdUint32Format, CmdUint64Format, CmdUuidFormat,
    },
};

// TODO: implement all the format functions based on the format message

pub(super) fn format_str_binary(
    value: Value,
    _: &CmdBinaryFormat,
) -> Result<Value, AzUserUsageError> {
    // if value.is_null() {
    //     return Ok(value);
    // }
    // if value.is_string() {
    //     let file_path = value.as_str().unwrap();
    //     // check the file is readable
    //     if let Err(e) = std::fs::File::open(file_path) {
    //         return Err(AzUserUsageError::InvalidArgumentValueError(format!(
    //             "file {} is not readable: {}",
    //             file_path, e
    //         )));
    //     }
    // } else {
    //     return Err(AzUserUsageError::InvalidArgumentValueError(format!(
    //         "invalid value type for binary argument, expected file path string",
    //     )));
    // }
    Ok(value)
}

pub(super) fn format_str_byte(value: Value, _: &CmdByteFormat) -> Result<Value, AzUserUsageError> {
    Ok(value)
}

pub(super) fn format_str_string(
    value: Value,
    _: &CmdStringFormat,
) -> Result<Value, AzUserUsageError> {
    Ok(value)
}

pub(super) fn format_str_duration(
    value: Value,
    _: &CmdDurationFormat,
) -> Result<Value, AzUserUsageError> {
    Ok(value)
}

pub(super) fn format_str_date(value: Value, _: &CmdDateFormat) -> Result<Value, AzUserUsageError> {
    Ok(value)
}

pub(super) fn format_str_datetime(
    value: Value,
    _: &CmdDateTimeFormat,
) -> Result<Value, AzUserUsageError> {
    Ok(value)
}

pub(super) fn format_str_time(value: Value, _: &CmdTimeFormat) -> Result<Value, AzUserUsageError> {
    Ok(value)
}

pub(super) fn format_str_uuid(value: Value, _: &CmdUuidFormat) -> Result<Value, AzUserUsageError> {
    Ok(value)
}

pub(super) fn format_str_password(
    value: Value,
    _: &CmdPasswordFormat,
) -> Result<Value, AzUserUsageError> {
    Ok(value)
}

pub(super) fn format_str_subscription_id(
    value: Value,
    _: &CmdSubscriptionIdFormat,
) -> Result<Value, AzUserUsageError> {
    Ok(value)
}

pub(super) fn format_str_resource_group_name(
    value: Value,
    _: &CmdResourceGroupNameFormat,
) -> Result<Value, AzUserUsageError> {
    Ok(value)
}

pub(super) fn format_str_resource_id(
    value: Value,
    _: &CmdResourceIdFormat,
) -> Result<Value, AzUserUsageError> {
    Ok(value)
}

pub(super) fn format_str_resource_location(
    value: Value,
    _: &CmdResourceLocationFormat,
) -> Result<Value, AzUserUsageError> {
    Ok(value)
}

pub(super) fn format_number_int32(
    value: Value,
    _: &CmdInt32Format,
) -> Result<Value, AzUserUsageError> {
    Ok(value)
}

pub(super) fn format_number_int64(
    value: Value,
    _: &CmdInt64Format,
) -> Result<Value, AzUserUsageError> {
    Ok(value)
}

pub(super) fn format_number_uint32(
    value: Value,
    _: &CmdUint32Format,
) -> Result<Value, AzUserUsageError> {
    Ok(value)
}

pub(super) fn format_number_uint64(
    value: Value,
    _: &CmdUint64Format,
) -> Result<Value, AzUserUsageError> {
    Ok(value)
}

pub(super) fn format_number_float(
    value: Value,
    _: &CmdFloatFormat,
) -> Result<Value, AzUserUsageError> {
    Ok(value)
}

pub(super) fn format_number_double(
    value: Value,
    _: &CmdDoubleFormat,
) -> Result<Value, AzUserUsageError> {
    Ok(value)
}

pub(super) fn format_array(value: Value, _: &CmdArrayFormat) -> Result<Value, AzUserUsageError> {
    Ok(value)
}

pub(super) fn format_object(value: Value, _: &CmdObjectFormat) -> Result<Value, AzUserUsageError> {
    Ok(value)
}
