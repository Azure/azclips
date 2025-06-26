use std::ffi::CString;
use std::os::raw::c_char;
use std::str::FromStr;
use std::sync::atomic::{AtomicU64, Ordering};

use az_core::{
    pb::{
        crs_arg, crs_number_arg, crs_string_arg, CrsAnyTypeArg, CrsArg, CrsArgEnum, CrsNumberArg,
        CrsStringArg,
    },
    uri::CrsUri,
};

// C-compatible argument array structure
#[repr(C)]
pub struct CArgumentArray {
    pub items: *mut CArgumentInfo,
    pub count: usize,
}

impl CArgumentArray {
    pub fn new() -> Self {
        Self {
            items: std::ptr::null_mut(),
            count: 0,
        }
    }
}

// C-compatible argument type enumeration
#[repr(C)]
#[derive(Debug, Clone, Copy, PartialEq)]
pub enum CArgType {
    String = 0,
    Number = 1,
    Boolean = 2,
    Object = 3,
    Array = 4,
    AnyType = 5,
    Class = 6,
}

#[repr(C)]
#[derive(Clone, Copy)]
pub struct CArgumentInfo {
    pub var_name: *mut c_char,
    pub name: *mut c_char,
    pub position: *mut i32,
    pub short_description: *mut c_char,
    pub long_description: *mut c_char,
    pub default_value: *mut c_char, // json string
    pub blank_value: *mut c_char,   // json string

    pub arg_type: CArgType,
    pub arg_type_details: CArgTypeDetails,

    pub internal: bool,
    pub required: bool,
    pub nullable: bool,
}

// C-compatible enum item structure
#[repr(C)]
pub struct CArgEnumItem {
    pub name: *mut c_char,
    pub value: *mut c_char, // json string
    pub internal: bool,
}

// C-compatible enum structure
#[repr(C)]
pub struct CArgEnum {
    pub items: *mut CArgEnumItem,
    pub item_count: usize,
    pub support_extension: bool,
}

#[repr(C)]
#[derive(Clone, Copy)]
pub struct CStringArg {
    pub format: CStringFormat,
    pub r#enum: *mut CArgEnum,
}

#[repr(C)]
#[derive(Clone, Copy)]
pub struct CNumberArg {
    pub format: CNumberFormat,
    pub r#enum: *mut CArgEnum,
}

#[repr(C)]
#[derive(Clone, Copy)]
pub struct CBooleanArg {
    pub _dummy: u8, // Ensure 1-byte size
}

#[repr(C)]
#[derive(Clone, Copy)]
pub struct CAnyTypeArg {
    pub _dummy: u8, // Ensure 1-byte size
}

#[repr(C)]
#[derive(Clone, Copy)]
pub struct CObjectArgAdditionalProperties {
    pub item: CArgumentInfo,
}

#[repr(C)]
#[derive(Clone, Copy)]
pub struct CObjectArg {
    pub cls_type: *mut c_char,
    pub additional_props: *mut CObjectArgAdditionalProperties,
    pub props: *mut CArgumentInfo,
    pub props_count: usize,
}

#[repr(C)]
#[derive(Clone, Copy)]
pub struct CArrayArg {
    pub item: *mut CArgumentInfo,
}

#[repr(C)]
#[derive(Clone, Copy)]
pub struct CClassArg {
    pub cls_type: *mut c_char,
}

#[repr(C)]
#[derive(Clone, Copy)]
pub union CArgTypeDetails {
    pub string_arg: CStringArg,
    pub number_arg: CNumberArg,
    pub boolean_arg: CBooleanArg,
    pub object_arg: CObjectArg,
    pub array_arg: CArrayArg,
    pub any_type_arg: CAnyTypeArg,
    pub class_arg: CClassArg,
}

// C-compatible string format enumeration
#[repr(C)]
#[derive(Debug, Clone, Copy, PartialEq)]
pub enum CStringFormatType {
    String = 0,
    Binary = 1,
    Byte = 2,
    Duration = 3,
    Date = 4,
    Time = 5,
    DateTime = 6,
    Uuid = 7,
    Password = 8,
    SubscriptionId = 9,
    ResourceGroupName = 10,
    ResourceId = 11,
    ResourceLocation = 12,
}

#[repr(C)]
#[derive(Clone, Copy)]
pub struct CStringFormat {
    pub format_data: CStringFormatData,
    pub format_type: CStringFormatType,
}

#[repr(C)]
#[derive(Clone, Copy)]
pub union CStringFormatData {
    pub string_impl: CStringFormatImpl,
    pub binary_impl: CBinaryFormatImpl,
    pub byte_impl: CByteFormatImpl,
    pub duration_impl: CDurationFormatImpl,
    pub date_impl: CDateFormatImpl,
    pub time_impl: CTimeFormatImpl,
    pub date_time_impl: CDateTimeFormatImpl,
    pub uuid_impl: CUuidFormatImpl,
    pub password_impl: CPasswordFormatImpl,
    pub subscription_id_impl: CSubscriptionIdFormatImpl,
    pub resource_group_name_impl: CResourceGroupNameFormatImpl,
    pub resource_id_impl: CResourceIdFormatImpl,
    pub resource_location_impl: CResourceLocationFormatImpl,
}

#[repr(C)]
#[derive(Clone, Copy)]
pub struct CStringFormatImpl {
    pub pattern: *mut c_char,
    pub min_length: *mut u32,
    pub max_length: *mut u32,
}

#[repr(C)]
#[derive(Clone, Copy)]
pub struct CBinaryFormatImpl {
    pub _dummy: u8, // Ensure 1-byte size
}

#[repr(C)]
#[derive(Clone, Copy)]
pub struct CByteFormatImpl {
    pub _dummy: u8, // Ensure 1-byte size
}

// C-compatible protocol enumeration
#[repr(C)]
#[derive(Debug, Clone, Copy, PartialEq)]
pub enum CDurationProtocol {
    ISO8601 = 0,
}

#[repr(C)]
#[derive(Clone, Copy)]
pub struct CDurationFormatImpl {
    pub protocol: CDurationProtocol,
}

// C-compatible protocol enumeration
#[repr(C)]
#[derive(Debug, Clone, Copy, PartialEq)]
pub enum CDateProtocol {
    ISO8601 = 0,
}

#[repr(C)]
#[derive(Clone, Copy)]
pub struct CDateFormatImpl {
    pub protocol: CDateProtocol,
}

// C-compatible protocol enumeration
#[repr(C)]
#[derive(Debug, Clone, Copy, PartialEq)]
pub enum CTimeProtocol {
    ISO8601 = 0,
}

#[repr(C)]
#[derive(Clone, Copy)]
pub struct CTimeFormatImpl {
    pub protocol: CTimeProtocol,
    pub ms_length: *mut u32,
}

// C-compatible protocol enumeration
#[repr(C)]
#[derive(Debug, Clone, Copy, PartialEq)]
pub enum CDateTimeProtocol {
    ISO8601 = 0,
    RFC1123 = 1,
}

#[repr(C)]
#[derive(Clone, Copy)]
pub struct CDateTimeFormatImpl {
    pub protocol: CDateTimeProtocol,
}

#[repr(C)]
#[derive(Clone, Copy)]
pub struct CUuidFormatImpl {
    pub _dummy: u8, // Ensure 1-byte size
}

#[repr(C)]
#[derive(Clone, Copy)]
pub struct CPasswordFormatImpl {
    pub _dummy: u8, // Ensure 1-byte size
}

#[repr(C)]
#[derive(Clone, Copy)]
pub struct CSubscriptionIdFormatImpl {
    pub _dummy: u8, // Ensure 1-byte size
}

#[repr(C)]
#[derive(Clone, Copy)]
pub struct CResourceGroupNameFormatImpl {
    pub _dummy: u8, // Ensure 1-byte size
}

#[repr(C)]
#[derive(Clone, Copy)]
pub struct CResourceIdFormatImpl {
    pub templates: *mut *mut c_char,
    pub template_count: usize,
}

#[repr(C)]
#[derive(Clone, Copy)]
pub struct CResourceLocationFormatImpl {
    pub _dummy: u8, // Ensure 1-byte size
}

// C-compatible number format enumeration
#[repr(C)]
#[derive(Debug, Clone, Copy, PartialEq)]
pub enum CNumberFormatType {
    Int32 = 0,
    Int64 = 1,
    UInt32 = 2,
    UInt64 = 3,
    Float = 4,
    Double = 5,
}

#[repr(C)]
#[derive(Clone, Copy)]
pub struct CNumberFormat {
    pub format_data: CNumberFormatData,
    pub format_type: CNumberFormatType,
}

#[repr(C)]
#[derive(Clone, Copy)]
pub union CNumberFormatData {
    pub int32_impl: CInt32FormatImpl,
    pub int64_impl: CInt64FormatImpl,
    pub uint32_impl: CUInt32FormatImpl,
    pub uint64_impl: CUInt64FormatImpl,
    pub float_impl: CFloatFormatImpl,
    pub double_impl: CDoubleFormatImpl,
}

#[repr(C)]
#[derive(Clone, Copy)]
pub struct CInt32FormatImpl {
    pub min_value: *mut i32,
    pub max_value: *mut i32,
    pub multiple_of: *mut i32,
}

#[repr(C)]
#[derive(Clone, Copy)]
pub struct CInt64FormatImpl {
    pub min_value: *mut i64,
    pub max_value: *mut i64,
    pub multiple_of: *mut i64,
}

#[repr(C)]
#[derive(Clone, Copy)]
pub struct CUInt32FormatImpl {
    pub min_value: *mut u32,
    pub max_value: *mut u32,
    pub multiple_of: *mut u32,
}

#[repr(C)]
#[derive(Clone, Copy)]
pub struct CUInt64FormatImpl {
    pub min_value: *mut u64,
    pub max_value: *mut u64,
    pub multiple_of: *mut u64,
}

#[repr(C)]
#[derive(Clone, Copy)]
pub struct CFloatFormatImpl {
    pub min_value: *mut f32,
    pub max_value: *mut f32,
    pub multiple_of: *mut f32,
    pub exclusive_minimum: bool,
    pub exclusive_maximum: bool,
}

#[repr(C)]
#[derive(Clone, Copy)]
pub struct CDoubleFormatImpl {
    pub min_value: *mut f64,
    pub max_value: *mut f64,
    pub multiple_of: *mut f64,
    pub exclusive_minimum: bool,
    pub exclusive_maximum: bool,
}

// Helper function to get command arguments asynchronously
pub async fn get_command_arguments_async(
    uri_str: &str,
) -> Result<Vec<CrsArg>, az_core::error::AzError> {
    let core = az_core::AzCore::instance().await;
    let uri = CrsUri::from_str(uri_str)?;
    let invocation = core.resolve_invocation(uri).await?;

    let crs_interface = core.get_interface(&invocation).await?;

    match crs_interface.level {
        Some(az_core::pb::crs_interface::Level::Command(command)) => Ok(command.args),
        Some(az_core::pb::crs_interface::Level::CommandGroup(_)) => {
            // Command groups don't have arguments
            Ok(Vec::new())
        }
        None => Ok(Vec::new()),
    }
}

// Helper function to convert an Option<String> to a C string pointer
fn option_string_to_c_ptr(opt_str: Option<String>) -> *mut c_char {
    match opt_str {
        Some(s) => match CString::new(s) {
            Ok(cstr) => cstr.into_raw(),
            Err(_) => std::ptr::null_mut(),
        },
        None => std::ptr::null_mut(),
    }
}

// Helper function to convert a String to a C string pointer
fn string_to_c_ptr(s: String) -> *mut c_char {
    match CString::new(s) {
        Ok(cstr) => cstr.into_raw(),
        Err(_) => std::ptr::null_mut(),
    }
}

// Helper function to convert an Option<T> to a boxed pointer
fn option_to_boxed_ptr<T>(opt_val: Option<T>) -> *mut T {
    match opt_val {
        Some(val) => Box::into_raw(Box::new(val)),
        None => std::ptr::null_mut(),
    }
}

// Convert CrsArgEnum to CArgEnum
fn convert_crs_arg_enum_to_c_arg_enum(crs_enum: &CrsArgEnum) -> *mut CArgEnum {
    let items: Vec<CArgEnumItem> = crs_enum
        .items
        .iter()
        .map(|item| CArgEnumItem {
            name: string_to_c_ptr(item.name.clone()),
            value: string_to_c_ptr(item.value.clone()),
            internal: item.internal,
        })
        .collect();

    let items_ptr = if items.is_empty() {
        std::ptr::null_mut()
    } else {
        let boxed_items = items.into_boxed_slice();
        Box::into_raw(boxed_items) as *mut CArgEnumItem
    };

    Box::into_raw(Box::new(CArgEnum {
        items: items_ptr,
        item_count: crs_enum.items.len(),
        support_extension: crs_enum.support_extension,
    }))
}

// Convert CrsStringArg to CStringArg
fn convert_crs_string_arg_to_c_string_arg(crs_string_arg: &CrsStringArg) -> CStringArg {
    let enum_ptr = crs_string_arg
        .r#enum
        .as_ref()
        .map(convert_crs_arg_enum_to_c_arg_enum)
        .unwrap_or(std::ptr::null_mut());

    let format = match &crs_string_arg.format {
        Some(crs_string_arg::Format::String(string_format)) => CStringFormat {
            format_type: CStringFormatType::String,
            format_data: CStringFormatData {
                string_impl: CStringFormatImpl {
                    pattern: option_string_to_c_ptr(string_format.pattern.clone()),
                    min_length: option_to_boxed_ptr(string_format.min_length),
                    max_length: option_to_boxed_ptr(string_format.max_length),
                },
            },
        },
        Some(crs_string_arg::Format::Binary(_)) => CStringFormat {
            format_type: CStringFormatType::Binary,
            format_data: CStringFormatData {
                binary_impl: CBinaryFormatImpl { _dummy: 0 },
            },
        },
        Some(crs_string_arg::Format::Byte(_)) => CStringFormat {
            format_type: CStringFormatType::Byte,
            format_data: CStringFormatData {
                byte_impl: CByteFormatImpl { _dummy: 0 },
            },
        },
        Some(crs_string_arg::Format::Duration(duration_format)) => CStringFormat {
            format_type: CStringFormatType::Duration,
            format_data: CStringFormatData {
                duration_impl: CDurationFormatImpl {
                    protocol: match duration_format.protocol {
                        0 => CDurationProtocol::ISO8601,
                        _ => CDurationProtocol::ISO8601,
                    },
                },
            },
        },
        Some(crs_string_arg::Format::Date(date_format)) => CStringFormat {
            format_type: CStringFormatType::Date,
            format_data: CStringFormatData {
                date_impl: CDateFormatImpl {
                    protocol: match date_format.protocol {
                        0 => CDateProtocol::ISO8601,
                        _ => CDateProtocol::ISO8601,
                    },
                },
            },
        },
        Some(crs_string_arg::Format::Time(time_format)) => CStringFormat {
            format_type: CStringFormatType::Time,
            format_data: CStringFormatData {
                time_impl: CTimeFormatImpl {
                    protocol: match time_format.protocol {
                        0 => CTimeProtocol::ISO8601,
                        _ => CTimeProtocol::ISO8601,
                    },
                    ms_length: option_to_boxed_ptr(time_format.ms_length),
                },
            },
        },
        Some(crs_string_arg::Format::DateTime(datetime_format)) => CStringFormat {
            format_type: CStringFormatType::DateTime,
            format_data: CStringFormatData {
                date_time_impl: CDateTimeFormatImpl {
                    protocol: match datetime_format.protocol {
                        0 => CDateTimeProtocol::ISO8601,
                        1 => CDateTimeProtocol::RFC1123,
                        _ => CDateTimeProtocol::ISO8601,
                    },
                },
            },
        },
        Some(crs_string_arg::Format::Uuid(_)) => CStringFormat {
            format_type: CStringFormatType::Uuid,
            format_data: CStringFormatData {
                uuid_impl: CUuidFormatImpl { _dummy: 0 },
            },
        },
        Some(crs_string_arg::Format::Password(_)) => CStringFormat {
            format_type: CStringFormatType::Password,
            format_data: CStringFormatData {
                password_impl: CPasswordFormatImpl { _dummy: 0 },
            },
        },
        Some(crs_string_arg::Format::SubscriptionId(_)) => CStringFormat {
            format_type: CStringFormatType::SubscriptionId,
            format_data: CStringFormatData {
                subscription_id_impl: CSubscriptionIdFormatImpl { _dummy: 0 },
            },
        },
        Some(crs_string_arg::Format::ResourceGroupName(_)) => CStringFormat {
            format_type: CStringFormatType::ResourceGroupName,
            format_data: CStringFormatData {
                resource_group_name_impl: CResourceGroupNameFormatImpl { _dummy: 0 },
            },
        },
        Some(crs_string_arg::Format::ResourceId(resource_id_format)) => CStringFormat {
            format_type: CStringFormatType::ResourceId,
            format_data: CStringFormatData {
                resource_id_impl: CResourceIdFormatImpl {
                    templates: if resource_id_format.templates.is_empty() {
                        std::ptr::null_mut()
                    } else {
                        let templates: Vec<*mut c_char> = resource_id_format
                            .templates
                            .iter()
                            .map(|t| string_to_c_ptr(t.clone()))
                            .collect();
                        let boxed_templates = templates.into_boxed_slice();
                        Box::into_raw(boxed_templates) as *mut *mut c_char
                    },
                    template_count: resource_id_format.templates.len(),
                },
            },
        },
        Some(crs_string_arg::Format::ResourceLocation(_)) => CStringFormat {
            format_type: CStringFormatType::ResourceLocation,
            format_data: CStringFormatData {
                resource_location_impl: CResourceLocationFormatImpl { _dummy: 0 },
            },
        },
        None => CStringFormat {
            format_type: CStringFormatType::String,
            format_data: CStringFormatData {
                string_impl: CStringFormatImpl {
                    pattern: std::ptr::null_mut(),
                    min_length: std::ptr::null_mut(),
                    max_length: std::ptr::null_mut(),
                },
            },
        },
    };

    CStringArg {
        format,
        r#enum: enum_ptr,
    }
}

// Convert CrsNumberArg to CNumberArg
fn convert_crs_number_arg_to_c_number_arg(crs_number_arg: &CrsNumberArg) -> CNumberArg {
    let enum_ptr = crs_number_arg
        .r#enum
        .as_ref()
        .map(convert_crs_arg_enum_to_c_arg_enum)
        .unwrap_or(std::ptr::null_mut());

    let format = match &crs_number_arg.format {
        Some(crs_number_arg::Format::Int32(int32_format)) => CNumberFormat {
            format_type: CNumberFormatType::Int32,
            format_data: CNumberFormatData {
                int32_impl: CInt32FormatImpl {
                    min_value: option_to_boxed_ptr(int32_format.minimum),
                    max_value: option_to_boxed_ptr(int32_format.maximum),
                    multiple_of: option_to_boxed_ptr(int32_format.multiple_of),
                },
            },
        },
        Some(crs_number_arg::Format::Int64(int64_format)) => CNumberFormat {
            format_type: CNumberFormatType::Int64,
            format_data: CNumberFormatData {
                int64_impl: CInt64FormatImpl {
                    min_value: option_to_boxed_ptr(int64_format.minimum),
                    max_value: option_to_boxed_ptr(int64_format.maximum),
                    multiple_of: option_to_boxed_ptr(int64_format.multiple_of),
                },
            },
        },
        Some(crs_number_arg::Format::Uint32(uint32_format)) => CNumberFormat {
            format_type: CNumberFormatType::UInt32,
            format_data: CNumberFormatData {
                uint32_impl: CUInt32FormatImpl {
                    min_value: option_to_boxed_ptr(uint32_format.minimum),
                    max_value: option_to_boxed_ptr(uint32_format.maximum),
                    multiple_of: option_to_boxed_ptr(uint32_format.multiple_of),
                },
            },
        },
        Some(crs_number_arg::Format::Uint64(uint64_format)) => CNumberFormat {
            format_type: CNumberFormatType::UInt64,
            format_data: CNumberFormatData {
                uint64_impl: CUInt64FormatImpl {
                    min_value: option_to_boxed_ptr(uint64_format.minimum),
                    max_value: option_to_boxed_ptr(uint64_format.maximum),
                    multiple_of: option_to_boxed_ptr(uint64_format.multiple_of),
                },
            },
        },
        Some(crs_number_arg::Format::Float(float_format)) => CNumberFormat {
            format_type: CNumberFormatType::Float,
            format_data: CNumberFormatData {
                float_impl: CFloatFormatImpl {
                    min_value: option_to_boxed_ptr(float_format.minimum),
                    max_value: option_to_boxed_ptr(float_format.maximum),
                    exclusive_minimum: float_format.exclusive_minimum,
                    exclusive_maximum: float_format.exclusive_maximum,
                    multiple_of: option_to_boxed_ptr(float_format.multiple_of),
                },
            },
        },
        Some(crs_number_arg::Format::Double(double_format)) => CNumberFormat {
            format_type: CNumberFormatType::Double,
            format_data: CNumberFormatData {
                double_impl: CDoubleFormatImpl {
                    min_value: option_to_boxed_ptr(double_format.minimum),
                    max_value: option_to_boxed_ptr(double_format.maximum),
                    exclusive_minimum: double_format.exclusive_minimum,
                    exclusive_maximum: double_format.exclusive_maximum,
                    multiple_of: option_to_boxed_ptr(double_format.multiple_of),
                },
            },
        },
        None => CNumberFormat {
            format_type: CNumberFormatType::Int32,
            format_data: CNumberFormatData {
                int32_impl: CInt32FormatImpl {
                    min_value: std::ptr::null_mut(),
                    max_value: std::ptr::null_mut(),
                    multiple_of: std::ptr::null_mut(),
                },
            },
        },
    };

    CNumberArg {
        format,
        r#enum: enum_ptr,
    }
}

// Global counter for fake class name generation
static FAKE_CLASS_NAME_COUNTER: AtomicU64 = AtomicU64::new(1);

// Helper function to generate fake class names with incrementing index
fn generate_fake_class_name() -> String {
    let index = FAKE_CLASS_NAME_COUNTER.fetch_add(1, Ordering::SeqCst);
    format!("FakeClassName{}", index)
}

// Helper function to ensure proper CamelCase formatting
fn ensure_camel_case(input: &str) -> String {
    if input.is_empty() {
        return input.to_string();
    }

    let mut result = String::new();
    let mut capitalize_next = true;

    for ch in input.chars() {
        if ch.is_alphabetic() {
            if capitalize_next {
                result.push(ch.to_uppercase().next().unwrap_or(ch));
                capitalize_next = false;
            } else {
                result.push(ch.to_lowercase().next().unwrap_or(ch));
            }
        } else if ch == '_' || ch == '-' || ch.is_whitespace() {
            capitalize_next = true;
        } else {
            result.push(ch);
        }
    }

    result
}

// Main conversion function
pub fn convert_crs_arg_to_c_argument_info(crs_arg: &CrsArg) -> CArgumentInfo {
    let var_name = string_to_c_ptr(crs_arg.var_name.clone());
    let name = string_to_c_ptr(crs_arg.name.clone());
    let position = std::ptr::null_mut(); // Position is not directly available in CrsArg

    let short_description = crs_arg
        .help
        .as_ref()
        .map(|h| string_to_c_ptr(h.short.clone()))
        .unwrap_or(std::ptr::null_mut());

    let long_description = crs_arg
        .help
        .as_ref()
        .and_then(|h| h.long.as_ref())
        .map(|long| string_to_c_ptr(long.clone()))
        .unwrap_or(std::ptr::null_mut());

    let default_value = crs_arg
        .default
        .as_ref()
        .map(|d| string_to_c_ptr(d.value.clone()))
        .unwrap_or(std::ptr::null_mut());

    let blank_value = crs_arg
        .blank
        .as_ref()
        .map(|b| string_to_c_ptr(b.value.clone()))
        .unwrap_or(std::ptr::null_mut());

    let (arg_type, arg_type_details) =
        match crs_arg.arg_type.as_ref().expect("arg_type is required") {
            crs_arg::ArgType::String(string_arg) => {
                let c_string_arg = convert_crs_string_arg_to_c_string_arg(string_arg);
                (
                    CArgType::String,
                    CArgTypeDetails {
                        string_arg: c_string_arg,
                    },
                )
            }
            crs_arg::ArgType::Number(number_arg) => {
                let c_number_arg = convert_crs_number_arg_to_c_number_arg(number_arg);
                (
                    CArgType::Number,
                    CArgTypeDetails {
                        number_arg: c_number_arg,
                    },
                )
            }
            crs_arg::ArgType::Boolean(_) => (
                CArgType::Boolean,
                CArgTypeDetails {
                    boolean_arg: CBooleanArg { _dummy: 0 },
                },
            ),
            crs_arg::ArgType::Object(object_arg) => {
                // Generate or format cls_type based on whether it exists
                let cls_type = if let Some(existing_cls_type) = &object_arg.cls_type {
                    // Ensure existing cls_type is properly formatted as CamelCase
                    string_to_c_ptr(ensure_camel_case(existing_cls_type))
                } else {
                    // Generate fake cls_type with global incrementing index
                    string_to_c_ptr(generate_fake_class_name())
                };

                let props: Vec<CArgumentInfo> = object_arg
                    .props
                    .iter()
                    .map(convert_crs_arg_to_c_argument_info)
                    .collect();

                let props_ptr = if props.is_empty() {
                    std::ptr::null_mut()
                } else {
                    let boxed_props = props.into_boxed_slice();
                    Box::into_raw(boxed_props) as *mut CArgumentInfo
                };

                let additional_props = if let Some(additional) = &object_arg.additional_props {
                    if let Some(item) = &additional.item {
                        let item_info = convert_crs_arg_to_c_argument_info(item);
                        Box::into_raw(Box::new(CObjectArgAdditionalProperties { item: item_info }))
                    } else {
                        let item_info = convert_crs_arg_to_c_argument_info(&CrsArg {
                            arg_type: Some(crs_arg::ArgType::AnyType(CrsAnyTypeArg::default())),
                            ..Default::default()
                        });
                        Box::into_raw(Box::new(CObjectArgAdditionalProperties { item: item_info }))
                    }
                } else {
                    std::ptr::null_mut()
                };

                let c_object_arg = CObjectArg {
                    cls_type,
                    props: props_ptr,
                    props_count: object_arg.props.len(),
                    additional_props,
                };
                (
                    CArgType::Object,
                    CArgTypeDetails {
                        object_arg: c_object_arg,
                    },
                )
            }
            crs_arg::ArgType::Array(array_arg) => {
                let item_ptr = if let Some(item) = &array_arg.item {
                    let item_info = convert_crs_arg_to_c_argument_info(item);
                    Box::into_raw(Box::new(item_info))
                } else {
                    let item_info = convert_crs_arg_to_c_argument_info(&CrsArg {
                        arg_type: Some(crs_arg::ArgType::AnyType(CrsAnyTypeArg::default())),
                        ..Default::default()
                    });
                    Box::into_raw(Box::new(item_info))
                };

                let c_array_arg = CArrayArg { item: item_ptr };
                (
                    CArgType::Array,
                    CArgTypeDetails {
                        array_arg: c_array_arg,
                    },
                )
            }
            crs_arg::ArgType::AnyType(_) => (
                CArgType::AnyType,
                CArgTypeDetails {
                    any_type_arg: CAnyTypeArg { _dummy: 0 },
                },
            ),
            crs_arg::ArgType::Cls(cls_arg) => {
                let cls_type = string_to_c_ptr(cls_arg.cls_type.clone());
                let c_class_arg = CClassArg { cls_type };
                (
                    CArgType::Class,
                    CArgTypeDetails {
                        class_arg: c_class_arg,
                    },
                )
            }
        };

    CArgumentInfo {
        var_name,
        name,
        position,
        internal: crs_arg.internal,
        required: crs_arg.required,
        nullable: crs_arg.nullable,
        short_description,
        long_description,
        default_value,
        blank_value,
        arg_type,
        arg_type_details,
    }
}

// Helper function to free C string pointer
unsafe fn free_c_str_ptr(ptr: *mut c_char) {
    if !ptr.is_null() {
        let _ = CString::from_raw(ptr);
    }
}

// Helper function to free boxed pointer
unsafe fn free_boxed_ptr<T>(ptr: *mut T) {
    if !ptr.is_null() {
        let _ = Box::from_raw(ptr);
    }
}

// Helper function to free CArgEnum
unsafe fn free_c_arg_enum(enum_ptr: *mut CArgEnum) {
    if !enum_ptr.is_null() {
        let enum_box = Box::from_raw(enum_ptr);
        if !enum_box.items.is_null() {
            let items_slice = std::slice::from_raw_parts_mut(enum_box.items, enum_box.item_count);
            for item in items_slice {
                free_c_str_ptr(item.name);
                free_c_str_ptr(item.value);
            }
            let _ = Box::from_raw(std::slice::from_raw_parts_mut(
                enum_box.items,
                enum_box.item_count,
            ));
        }
    }
}

// Helper function to free CArgumentInfo recursively
unsafe fn free_c_argument_info_recursive(arg_info: &CArgumentInfo) {
    // Free string fields
    free_c_str_ptr(arg_info.var_name);
    free_c_str_ptr(arg_info.name);
    free_boxed_ptr(arg_info.position);
    free_c_str_ptr(arg_info.short_description);
    free_c_str_ptr(arg_info.long_description);
    free_c_str_ptr(arg_info.default_value);
    free_c_str_ptr(arg_info.blank_value);

    // Free type-specific details
    match arg_info.arg_type {
        CArgType::String => {
            let string_arg = &arg_info.arg_type_details.string_arg;
            free_c_arg_enum(string_arg.r#enum);

            // Free string format details
            match string_arg.format.format_type {
                CStringFormatType::String => {
                    let string_impl = &string_arg.format.format_data.string_impl;
                    free_c_str_ptr(string_impl.pattern);
                    free_boxed_ptr(string_impl.min_length);
                    free_boxed_ptr(string_impl.max_length);
                }
                CStringFormatType::Time => {
                    let time_impl = &string_arg.format.format_data.time_impl;
                    free_boxed_ptr(time_impl.ms_length);
                }
                CStringFormatType::ResourceId => {
                    let resource_id_impl = &string_arg.format.format_data.resource_id_impl;
                    if !resource_id_impl.templates.is_null() {
                        let templates_slice = std::slice::from_raw_parts(
                            resource_id_impl.templates,
                            resource_id_impl.template_count,
                        );
                        for template in templates_slice {
                            free_c_str_ptr(*template);
                        }
                        let _ = Box::from_raw(std::slice::from_raw_parts_mut(
                            resource_id_impl.templates,
                            resource_id_impl.template_count,
                        ));
                    }
                }
                _ => {} // Other formats don't have additional heap allocations
            }
        }
        CArgType::Number => {
            let number_arg = &arg_info.arg_type_details.number_arg;
            free_c_arg_enum(number_arg.r#enum);

            // Free number format details
            match number_arg.format.format_type {
                CNumberFormatType::Int32 => {
                    let int32_impl = &number_arg.format.format_data.int32_impl;
                    free_boxed_ptr(int32_impl.min_value);
                    free_boxed_ptr(int32_impl.max_value);
                    free_boxed_ptr(int32_impl.multiple_of);
                }
                CNumberFormatType::Int64 => {
                    let int64_impl = &number_arg.format.format_data.int64_impl;
                    free_boxed_ptr(int64_impl.min_value);
                    free_boxed_ptr(int64_impl.max_value);
                    free_boxed_ptr(int64_impl.multiple_of);
                }
                CNumberFormatType::UInt32 => {
                    let uint32_impl = &number_arg.format.format_data.uint32_impl;
                    free_boxed_ptr(uint32_impl.min_value);
                    free_boxed_ptr(uint32_impl.max_value);
                    free_boxed_ptr(uint32_impl.multiple_of);
                }
                CNumberFormatType::UInt64 => {
                    let uint64_impl = &number_arg.format.format_data.uint64_impl;
                    free_boxed_ptr(uint64_impl.min_value);
                    free_boxed_ptr(uint64_impl.max_value);
                    free_boxed_ptr(uint64_impl.multiple_of);
                }
                CNumberFormatType::Float => {
                    let float_impl = &number_arg.format.format_data.float_impl;
                    free_boxed_ptr(float_impl.min_value);
                    free_boxed_ptr(float_impl.max_value);
                    free_boxed_ptr(float_impl.multiple_of);
                }
                CNumberFormatType::Double => {
                    let double_impl = &number_arg.format.format_data.double_impl;
                    free_boxed_ptr(double_impl.min_value);
                    free_boxed_ptr(double_impl.max_value);
                    free_boxed_ptr(double_impl.multiple_of);
                }
            }
        }
        CArgType::Object => {
            let object_arg = &arg_info.arg_type_details.object_arg;
            free_c_str_ptr(object_arg.cls_type);

            // Free properties array
            if !object_arg.props.is_null() {
                let props_slice =
                    std::slice::from_raw_parts(object_arg.props, object_arg.props_count);
                for prop in props_slice {
                    free_c_argument_info_recursive(prop);
                }
                let _ = Box::from_raw(std::slice::from_raw_parts_mut(
                    object_arg.props,
                    object_arg.props_count,
                ));
            }

            // Free additional properties
            if !object_arg.additional_props.is_null() {
                let additional_props = Box::from_raw(object_arg.additional_props);
                free_c_argument_info_recursive(&additional_props.item);
            }
        }
        CArgType::Array => {
            let array_arg = &arg_info.arg_type_details.array_arg;
            if !array_arg.item.is_null() {
                let item = Box::from_raw(array_arg.item);
                free_c_argument_info_recursive(&item);
            }
        }
        CArgType::Class => {
            let class_arg = &arg_info.arg_type_details.class_arg;
            free_c_str_ptr(class_arg.cls_type);
        }
        CArgType::Boolean | CArgType::AnyType => {
            // These types don't have additional heap allocations
        }
    }
}

// Main memory deallocation function
pub unsafe fn free_c_argument_info(arg_info: CArgumentInfo) {
    free_c_argument_info_recursive(&arg_info);
}

/// Print structure sizes for debugging alignment issues
pub fn print_structure_sizes() {
    use std::mem;

    println!("=== Rust Structure Sizes ===");
    println!(
        "CArgumentInfo size: {} bytes",
        mem::size_of::<CArgumentInfo>()
    );
    println!(
        "CArgTypeDetails size: {} bytes",
        mem::size_of::<CArgTypeDetails>()
    );
    println!("CObjectArg size: {} bytes", mem::size_of::<CObjectArg>());
    println!("CStringArg size: {} bytes", mem::size_of::<CStringArg>());
    println!("CNumberArg size: {} bytes", mem::size_of::<CNumberArg>());
    println!("CArrayArg size: {} bytes", mem::size_of::<CArrayArg>());
    println!("CClassArg size: {} bytes", mem::size_of::<CClassArg>());
    println!(
        "CFloatFormatImpl size: {} bytes",
        mem::size_of::<CFloatFormatImpl>()
    );
    println!(
        "CDoubleFormatImpl size: {} bytes",
        mem::size_of::<CDoubleFormatImpl>()
    );
    println!("CArgEnum size: {} bytes", mem::size_of::<CArgEnum>());
    println!(
        "CArgEnumItem size: {} bytes",
        mem::size_of::<CArgEnumItem>()
    );
    println!("CArgType size: {} bytes", mem::size_of::<CArgType>());
    println!(
        "*mut c_char size: {} bytes",
        mem::size_of::<*mut std::os::raw::c_char>()
    );
    println!("usize size: {} bytes", mem::size_of::<usize>());
    println!("bool size: {} bytes", mem::size_of::<bool>());
    println!("=============================");
}

/// Get the size of CArgumentInfo structure for alignment verification
/// This can be called from C# to compare structure sizes
#[no_mangle]
pub extern "C" fn cirrus_get_argument_info_size() -> usize {
    std::mem::size_of::<CArgumentInfo>()
}

/// Get the size of CArgTypeDetails union for alignment verification
#[no_mangle]
pub extern "C" fn cirrus_get_arg_type_details_size() -> usize {
    std::mem::size_of::<CArgTypeDetails>()
}
