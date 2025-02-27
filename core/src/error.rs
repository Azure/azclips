use thiserror::Error;


pub type AzResult<T> = Result<T, AzError>;

#[derive(Error, Debug)]
pub enum AzError {
    #[error("An error occurred: {0}")]
    GeneralError(String),
    
    #[error("IO error: {0}")]
    Io(#[from] std::io::Error),

    #[error("Parse error: {0}")]
    Parse(#[from] std::num::ParseIntError),

    #[error("Config error: {0}")]
    Config(String),

    #[error("Auth error: {0}")]
    Auth(String),
}

// You can add more error variants as needed.
