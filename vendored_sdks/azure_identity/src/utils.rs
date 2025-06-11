use azure_core::{error::ErrorKind, Error};
use base64::engine::general_purpose::URL_SAFE;
use base64::Engine as _;

// Reference: https://github.com/AzureAD/microsoft-authentication-library-for-python/blob/db1c3848456696920fb5fb21c1a25a5e9323e443/msal/oauth2cli/oidc.py#L15-L36
pub fn decode_part_id_token(raw: &str, encoding: Option<&str>) -> azure_core::Result<String> {
    // Handle empty input
    if raw.is_empty() {
        return Err(Error::new(
            ErrorKind::Credential,
            "Empty ID token part provided".to_string(),
        ));
    }

    // Add padding if necessary
    let rem = raw.len() % 4;
    let mut b64 = raw.to_string();
    if rem != 0 {
        b64.push_str(&"=".repeat(4 - rem));
    }

    // Decode base64url-encoded string
    let bytes = URL_SAFE.decode(&b64).map_err(|e| {
        Error::new(
            ErrorKind::Credential,
            format!("Failed to decode base64 content: {}", e),
        )
    })?;

    match encoding {
        Some(enc) => {
            match enc.to_lowercase().as_str() {
                "utf-8" | "utf8" => {
                    // Most JWT payload parts are UTF-8
                    std::str::from_utf8(&bytes)
                        .map_err(|e| {
                            Error::new(
                                ErrorKind::Credential,
                                format!("Invalid UTF-8 sequence in token: {}", e)
                            )
                        })
                        .map(|s| s.to_owned())
                }
                "ascii" => {
                    // Handle ASCII encoding
                    Ok(String::from_utf8_lossy(&bytes).into_owned())
                }
                _ => {
                    Err(Error::new(
                        ErrorKind::Credential,
                        format!("Unsupported encoding '{}' for ID token payload. Supported encodings: utf-8, ascii", enc)
                    ))
                }
            }
        }
        // Default to UTF-8 when no encoding specified (safer than hex dump)
        None => std::str::from_utf8(&bytes)
            .map(|s| s.to_owned())
            .or_else(|_| Ok(String::from_utf8_lossy(&bytes).into_owned())),
    }
}
