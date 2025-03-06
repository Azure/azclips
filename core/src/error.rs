use thiserror::Error;

pub type AzResult<T, E = AzError> = Result<T, E>;

#[derive(Error, Debug, PartialEq)]
pub enum AzUserUsageError {
    #[error("Command not found: {0}")]
    CommandNotFoundError(String),

    #[error("Unrecognized arguments: {0}")]
    UnrecognizedArgumentError(String),

    #[error("Required argument missing: {0}")]
    RequiredArgumentMissingError(String),

    #[error("Invalid argument value: {0}")]
    InvalidArgumentValueError(String),

    #[error("Unsupported feature: {0}")]
    UnsupportedFeatureError(String),

    #[error("Failed to parse file '{0}' with exception:\n{1}")]
    ParseFileError(String, String),

    // TODO: use the data, at, range to show the error location
    #[error("Invalid shorthand syntax: {msg}")]
    InvalidShorthandSyntaxError {
        msg: String,
        data: String,
        at: usize,
        range: usize,
    },

    // #[error("Invalid index key")]

    /// Arguments can not be specified together such as polymorphic arguments
    #[error("Mutually exclusive arguments: {0}")]
    MutuallyExclusiveArgumentError(String),
    // #[error("Invalid argument format: {0}")]
    // InvalidArgumentFormatError(String),
}

#[derive(Error, Debug)]
pub enum AzInternalError {
    #[error("Invalid command definition: {0}")]
    InvalidCommandDefinitionError(String),

    #[error("Invalid argument definition: {0}")]
    InvalidArgumentDefinitionError(String),
}

#[derive(Error, Debug)]
pub enum AzError {
    /// The errors which are related to user's usage
    #[error("Usage error: {0}")]
    UserUsage(#[from] AzUserUsageError),

    #[error("Internal error: {0}")]
    Internal(#[from] AzInternalError),
    // #[error("An error occurred: {0}")]
    // GeneralError(String),

    // #[error("IO error: {0}")]
    // Io(#[from] std::io::Error),

    // #[error("Parse error: {0}")]
    // Parse(#[from] std::num::ParseIntError),
    #[error("Config error: {0}")]
    Config(String),
    // #[error("Auth error: {0}")]
    // Auth(String),
}

// You can add more error variants as needed.
