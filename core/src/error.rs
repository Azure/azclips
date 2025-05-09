use thiserror::Error;

pub(crate) type AzResult<T, E = AzError> = Result<T, E>;

#[derive(Error, Debug)]
pub enum AzUserUsageError {
    #[error("Command not found: {0}")]
    CommandNotFoundError(String),

    #[error("Command group is not executable")]
    CommandGroupNotExecutableError,

    #[error("Unrecognized arguments: {0}")]
    UnrecognizedArgumentError(String),

    #[error("Required argument missing: {0}")]
    RequiredArgumentMissingError(String),

    #[error("Invalid argument value: {0}")]
    InvalidArgumentValueError(String),

    #[error("Invalid input: {0}")]
    InvalidInputError(#[from] std::io::Error),

    // #[error("Unsupported feature: {0}")]
    // UnsupportedFeatureError(String),

    // #[error("Failed to parse file '{0}' with exception:\n{1}")]
    // ParseFileError(String, String),

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

    #[error("Invalid partial keys: {0}")]
    InvalidPartialKeysError(String),

    #[error("Invalid command URI: {0}")]
    InvalidCmdUriError(String),

    #[error("Invalid path: {0}")]
    InvalidPathError(String),

    #[error("IO error: {0}")]
    Io(#[from] std::io::Error),

    #[error("JSON error: {0}")]
    Json(#[from] serde_json::Error),

    #[error("Invalid param value: {0}")]
    InvalidParamValueError(String),
}

#[derive(Error, Debug)]
pub enum AzError {
    /// The errors which are related to user's usage
    #[error("Usage error: {0}")]
    UserUsage(#[from] AzUserUsageError),

    #[error("Internal error: {0}")]
    Internal(#[from] AzInternalError),

    #[error("An error occurred: {0}")]
    GeneralError(String),

    // #[error("Parse error: {0}")]
    // Parse(#[from] std::num::ParseIntError),
    #[error("Config error: {0}")]
    Config(String),

    #[error("Authentication error: {0}")]
    Authentication(#[from] AuthenticationError),
}

#[derive(Error, Debug)]
pub enum AuthenticationError {
    #[error("Failed to authenticate: {0}")]
    AuthenticationFailed(String),

    #[error("Token expired")]
    TokenExpired,

    #[error("Invalid token: {0}")]
    InvalidToken(String),
    
    #[error("Invalid tenant")]
    InvalidTenant,
}

// You can add more error variants as needed.
