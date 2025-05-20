use serde_json::Value;

use crate::{
    error::AzUserUsageError,
    pb::{
        CrsArrayFormat, CrsBinaryFormat, CrsByteFormat, CrsDateFormat, CrsDateTimeFormat,
        CrsDoubleFormat, CrsDurationFormat, CrsFloatFormat, CrsInt32Format, CrsInt64Format,
        CrsObjectFormat, CrsPasswordFormat, CrsResourceGroupNameFormat, CrsResourceIdFormat,
        CrsResourceLocationFormat, CrsStringFormat, CrsSubscriptionIdFormat, CrsTimeFormat,
        CrsUint32Format, CrsUint64Format, CrsUuidFormat,
    },
};

// TODO: implement all the format functions based on the format message

pub fn format_str_binary(value: Value, _: &CrsBinaryFormat) -> Result<Value, AzUserUsageError> {
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

pub fn format_str_byte(value: Value, _: &CrsByteFormat) -> Result<Value, AzUserUsageError> {
    Ok(value)
}

pub fn format_str_string(value: Value, _: &CrsStringFormat) -> Result<Value, AzUserUsageError> {
    Ok(value)
}

pub fn format_str_duration(value: Value, _: &CrsDurationFormat) -> Result<Value, AzUserUsageError> {
    Ok(value)
}

pub fn format_str_date(value: Value, _: &CrsDateFormat) -> Result<Value, AzUserUsageError> {
    Ok(value)
}

pub fn format_str_datetime(value: Value, _: &CrsDateTimeFormat) -> Result<Value, AzUserUsageError> {
    Ok(value)
}

pub fn format_str_time(value: Value, _: &CrsTimeFormat) -> Result<Value, AzUserUsageError> {
    Ok(value)
}

pub fn format_str_uuid(value: Value, _: &CrsUuidFormat) -> Result<Value, AzUserUsageError> {
    Ok(value)
}

pub fn format_str_password(value: Value, _: &CrsPasswordFormat) -> Result<Value, AzUserUsageError> {
    Ok(value)
}

pub fn format_str_subscription_id(
    value: Value,
    _: &CrsSubscriptionIdFormat,
) -> Result<Value, AzUserUsageError> {
    Ok(value)
}

pub fn format_str_resource_group_name(
    value: Value,
    _: &CrsResourceGroupNameFormat,
) -> Result<Value, AzUserUsageError> {
    Ok(value)
}

pub fn format_str_resource_id(
    value: Value,
    _: &CrsResourceIdFormat,
) -> Result<Value, AzUserUsageError> {
    Ok(value)
}

pub fn format_str_resource_location(
    value: Value,
    _: &CrsResourceLocationFormat,
) -> Result<Value, AzUserUsageError> {
    Ok(value)
}

pub fn format_number_int32(value: Value, _: &CrsInt32Format) -> Result<Value, AzUserUsageError> {
    Ok(value)
}

pub fn format_number_int64(value: Value, _: &CrsInt64Format) -> Result<Value, AzUserUsageError> {
    Ok(value)
}

pub fn format_number_uint32(value: Value, _: &CrsUint32Format) -> Result<Value, AzUserUsageError> {
    Ok(value)
}

pub fn format_number_uint64(value: Value, _: &CrsUint64Format) -> Result<Value, AzUserUsageError> {
    Ok(value)
}

pub fn format_number_float(value: Value, _: &CrsFloatFormat) -> Result<Value, AzUserUsageError> {
    Ok(value)
}

pub fn format_number_double(value: Value, _: &CrsDoubleFormat) -> Result<Value, AzUserUsageError> {
    Ok(value)
}

pub fn format_array(value: Value, _: &CrsArrayFormat) -> Result<Value, AzUserUsageError> {
    Ok(value)
}

pub fn format_object(value: Value, _: &CrsObjectFormat) -> Result<Value, AzUserUsageError> {
    Ok(value)
}
