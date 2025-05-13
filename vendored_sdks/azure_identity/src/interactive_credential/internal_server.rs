use std::io::{BufRead, BufReader, Write};
use std::net::{Shutdown, TcpListener, TcpStream};
use std::process::Command;
use std::time::Duration;
use webbrowser;
use azure_core::Error;
use azure_core::error::ErrorKind;
use urlencoding;

///The port where the local server is listening on the auth_code
#[allow(dead_code)]
pub const LOCAL_SERVER_PORT: u16 = 47828;

/// Opens the given URL in the default system browser and starts a local web server
/// to receive the authorization code.
#[allow(dead_code)]
pub async fn open_url(url: &str, success_template: Option<&str>, error_template: Option<&str>) -> Result<String, Error> {
    if !try_open_browser(url) {
        tracing::error!("Failed to open browser for URL: {}", url);
        return Err(Error::new(
            ErrorKind::Credential,
            format!("Failed to open web browser for authentication URL: {}", url)
        ));
    }
    
    start_webserver(success_template, error_template)
}

fn try_open_browser(url: &str) -> bool {
    match webbrowser::open(url) {
        Ok(_) => return true,
        Err(e) => {
            tracing::warn!("Failed to open browser: {}", e);
            false
        }
    }
}

/// Starts the webserver on `http://localhost`. Returns the authorization code or an error if the server fails to start or times out.
#[allow(dead_code)]
fn start_webserver(success_template: Option<&str>, error_template: Option<&str>) -> Result<String, Error> {
    println!("Waiting for authentication response...");
    
    let listener = TcpListener::bind(("127.0.0.1", LOCAL_SERVER_PORT))
        .map_err(|e| Error::new(
            ErrorKind::Credential,
            format!("Failed to start local authentication server: {}", e)
        ))?;

    listener.set_nonblocking(true)
        .map_err(|e| Error::new(
            ErrorKind::Credential, 
            format!("Failed to set server to non-blocking mode: {}", e)
        ))?;
    
    let start_time = std::time::Instant::now();
    let timeout = std::time::Duration::from_secs(300); // 5-minute timeout
    
    // Progress indicator (optional)
    let mut last_indicator_time = start_time;
    let indicator_interval = std::time::Duration::from_secs(30);
    
    loop {
        if start_time.elapsed() > timeout {
            return Err(Error::new(
                ErrorKind::Credential,
                "Authentication timed out after waiting 5 minutes"
            ));
        }
        
        // Print periodic waiting message
        if last_indicator_time.elapsed() >= indicator_interval {
            println!("Still waiting for authentication response... (press Ctrl+C to cancel)");
            last_indicator_time = std::time::Instant::now();
        }
        
        match listener.accept() {
            Ok((stream, _)) => return handle_client(stream, success_template, error_template),
            Err(ref e) if e.kind() == std::io::ErrorKind::WouldBlock => {
                std::thread::sleep(std::time::Duration::from_millis(100));
                continue;
            }
            Err(e) => {
                tracing::error!("Error accepting connection: {}", e);
                return Err(Error::new(
                    ErrorKind::Credential,
                    format!("Error accepting connection: {}", e)
                ));
            }
        }
    }
}

/// Main method to handle the incomming traffic.
/// After a 10s timeout the stream will be closed
/// if the stream could be opened, we read the whole request and try to extract the auth_code
/// Returns also the html code to show if it worked
#[allow(dead_code)]
fn handle_client(mut stream: TcpStream, success_template: Option<&str>, error_template: Option<&str>) -> Result<String, Error> {
    stream
        .set_read_timeout(Some(Duration::from_secs(10)))
        .map_err(|e| Error::new(
            ErrorKind::Credential,
            format!("Failed to set read timeout: {}", e)
        ))?;

    let buf_reader = BufReader::new(&stream);
    let mut request_lines = vec![];
    for line in buf_reader.lines().map_while(Result::ok) {
        if line.is_empty() {
            break;
        }
        request_lines.push(line);
    }

    let request = request_lines.join("\n");

    
    // Check for errors in the OAuth response
    if let Some((error_code, error_description, error_uri)) = extract_auth_error(&request) {
        let formatted_description = format_error_description(&error_description);
        send_error_response(&mut stream, &error_code, &formatted_description, &error_uri, error_template)?;
        
        return Err(Error::new(
            ErrorKind::Credential, 
            format!("Error response: {} - {}", error_code, formatted_description)
        ));
    }

    let auth_code = extract_auth_code(&request).ok_or_else(|| Error::new(
        ErrorKind::Credential,
        "Failed to extract authorization code from response"
    ))?;
    
    send_success_response(&mut stream, success_template)?;

    Ok(auth_code)
}

/// Sends a success response to the client using the provided HTML template
/// If no template is provided, uses a default template
fn send_success_response(
    stream: &mut TcpStream, 
    template: Option<&str>
) -> Result<(), Error> {
    let response_body = template.unwrap_or(
        r#"<!DOCTYPE html>
<html>
<head>
    <title>Authentication Success</title>
</head>
<body>
    <h2>Authentication Complete</h2>
    <p>You have successfully authenticated with Azure. You may close this browser tab.</p>
</body>
</html>"#
    );

    send_response(stream, "200 OK", response_body)
}

/// Sends an error response to the client using the provided HTML template
/// If no template is provided, uses a default template
/// 
/// # Arguments
/// * `stream` - The TCP stream to write the response to
/// * `error_code` - The OAuth error
/// * `error_description` - The detailed error description
/// * `template` - Optional HTML template
fn send_error_response(
    stream: &mut TcpStream, 
    error_code: &str, 
    error_description: &str,
    error_url: &str,
    template: Option<&str>
) -> Result<(), Error> {
    let response_body = if let Some(template) = template {
        template
            .replace("$error_code", error_code)
            .replace("$error_description", error_description)
            .replace("$error_url", error_url)
    } else {
        format!(r#"<!DOCTYPE html>
<html>
<head>
    <title>Authentication Failed</title>
</head>
<body>
    <h2>Authentication Failed</h2>
    <p><span class="error">Error:</span>{}</p>
    <p><strong>Details:</strong></p>
    <div>{}</div>
    <div>Error url: <a href="{}" target="_blank">{}</a></div>
    <p>You may close this tab and try again.</p>
</body>
</html>"#, error_code, error_description, error_url, error_url)
    };

    send_response(stream, "400 Bad Request", &response_body)
}

fn send_response(stream: &mut TcpStream, status: &str, body: &str) -> Result<(), Error> {
    let response = format!(
        "HTTP/1.1 {}\r\nContent-Type: text/html\r\nContent-Length: {}\r\n\r\n{}",
        status,
        body.len(),
        body
    );

    stream.write_all(response.as_bytes()).map_err(|e| Error::new(
        ErrorKind::Credential, 
        format!("Failed to write response: {}", e)
    ))?;
    
    stream.flush().map_err(|e| Error::new(
        ErrorKind::Credential,
        format!("Failed to flush response: {}", e)
    ))?;
    
    stream.shutdown(Shutdown::Both).map_err(|e| Error::new(
        ErrorKind::Credential,
        format!("Failed to shutdown connection: {}", e)
    ))?;
    
    Ok(())
}

/// Extracts the `code` query parameter from the request.
#[allow(dead_code)]
fn extract_auth_code(request: &str) -> Option<String> {
    let code_start = request.rfind("code=")? + 5;
    let rest = &request[code_start..];
    let end = rest.find('&').unwrap_or(rest.len());
    Some(rest[..end].to_string())
}

/// Extracts error information from an OAuth error response.
fn extract_auth_error(request: &str) -> Option<(String, String, String)> {
    let error_start = request.find("error=")?;
    let error_code_start = error_start + 6; // "error=" is 6 chars
    let error_code_end = request[error_code_start..].find('&')
        .map(|pos| error_code_start + pos)
        .unwrap_or(request.len());
    let error_code = &request[error_code_start..error_code_end];
    
    let mut error_description = String::new();
    if let Some(desc_start) = request.find("error_description=") {
        let desc_value_start = desc_start + 18; // "error_description=" is 18 chars
        let desc_value_end = request[desc_value_start..].find('&')
            .map(|pos| desc_value_start + pos)
            .unwrap_or(request.len());
        error_description = urlencoding::decode(&request[desc_value_start..desc_value_end])  
            .map(|s| s.into_owned())  
            .unwrap_or("Failed to decode error description".to_string());  
    }
    
    let mut error_uri = String::new();
    if let Some(uri_start) = request.find("error_uri=") {
        let uri_value_start = uri_start + 10; // "error_uri=" is 10 chars
        let uri_value_end = request[uri_value_start..].find('&')
            .map(|pos| uri_value_start + pos)
            .unwrap_or(request.len());
        error_uri = urlencoding::decode(&request[uri_value_start..uri_value_end])
            .map(|s| s.into_owned())
            .unwrap_or("Failed to decode error url".to_string());
    }
    
    Some((error_code.to_string(), error_description, error_uri))
}

fn format_error_description(error_description: &str) -> String {    
    let with_spaces = error_description.replace('+', " ");
    
    let formatted = if with_spaces.contains("Trace ID:") {
        let parts: Vec<&str> = with_spaces.split('+').collect();
        let mut result = String::new();
        
        for part in parts {
            if part.starts_with("Trace ID:") || 
               part.starts_with("Correlation ID:") || 
               part.starts_with("Timestamp:") {
                  result.push_str("\n");
               }
            result.push_str(part);
            result.push_str(" ");
        }
        result
    } else {
        with_spaces
    };
    
    formatted
}

// Add this new function for tests
#[allow(dead_code)]
async fn is_command_available(cmd: &str) -> bool {
    #[cfg(windows)]
    let result = Command::new("where").arg(cmd).output();
    #[cfg(not(windows))]
    let result = Command::new("which").arg(cmd).output();

    match result {
        Ok(output) => output.status.success(),
        Err(_) => false,
    }
}

#[cfg(test)]
mod test_internal_server {
    use super::*;
    use tracing::Level;
    use tracing_subscriber::FmtSubscriber;
    fn init_logger() {
        let subscriber = FmtSubscriber::builder()
            .with_max_level(Level::DEBUG)
            .finish();
        let _ = tracing::subscriber::set_global_default(subscriber);
    }

    #[tokio::test]
    async fn test_valid_command() {
        init_logger();
        #[cfg(windows)]
        assert!(is_command_available("cmd").await);
        #[cfg(not(windows))]
        assert!(is_command_available("ls").await);
    }

    #[tokio::test]
    async fn test_invalid_command() {
        init_logger();
        assert!(!is_command_available("non_existing_command_foo").await);
    }

    #[test]
    fn test_extract_code_param() {
        let url = "GET /?code=abc123&state=xyz";
        assert_eq!(extract_auth_code(url).unwrap(), "abc123");
    }

    #[test]
    fn test_extract_code_at_end() {
        let url = "GET /?state=xyz&code=abc123";
        assert_eq!(extract_auth_code(url).unwrap(), "abc123");
    }

    #[test]
    fn test_extract_code_missing() {
        let url = "GET /?state=only";
        assert!(extract_auth_code(url).is_none());
    }
}
