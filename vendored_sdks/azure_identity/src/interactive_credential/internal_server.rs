use std::io::{BufRead, BufReader, Write};
use std::net::{Shutdown, TcpListener, TcpStream};
use std::process::Command;
use std::time::Duration;
use std::borrow::Cow;
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
pub async fn open_url(url: &str) -> Result<String, Error> {
    if !try_open_browser(url) {
        tracing::error!("Failed to open browser for URL: {}", url);
        return Err(Error::new(
            ErrorKind::Credential,
            format!("Failed to open web browser for authentication URL: {}", url)
        ));
    }
    
    start_webserver()
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
fn start_webserver() -> Result<String, Error> {
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
            Ok((stream, _)) => return handle_client(stream),
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
fn handle_client(mut stream: TcpStream) -> Result<String, Error> {
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
    
    // check if the request contains an error response
    if let Some((error_code, error_description)) = extract_auth_error(&request) {
        // Send a response to the browser showing the error
        send_error_response(&mut stream, &error_code, &error_description)?;
        
        return Err(Error::new(
            ErrorKind::Credential, 
            format!("Authentication error: {} - {}", error_code, error_description)
        ));
    }

    let auth_code = extract_auth_code(&request).ok_or_else(|| Error::new(
        ErrorKind::Credential,
        "Failed to extract authorization code from response"
    ))?;
    
    send_success_response(&mut stream)?;

    Ok(auth_code)
}

fn send_success_response(stream: &mut TcpStream) -> Result<(), Error> {
    let response_body = r#"<!DOCTYPE html>
<html><head><title>Auth Complete</title></head>
<body><p>Authentication complete. You may close this tab.</p></body>
</html>"#;

    send_response(stream, "200 OK", response_body)
}

fn send_error_response(stream: &mut TcpStream, error_code: &str, error_description: &str) -> Result<(), Error> {
    let response_body = format!(r#"<!DOCTYPE html>
<html>
<head><title>Login Failed</title></head>
<body>
    <h2>Login Failed</h2>
    <p>Error: {}</p>
    <p>Details: {}</p>
    <p>You may close this tab and try again.</p>
</body>
</html>"#, error_code, error_description);

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
fn extract_auth_error(request: &str) -> Option<(String, String)> {
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
            .unwrap_or_else(|_| Cow::from("Failed to decode error description"))
            .to_string();
    }
    
    Some((error_code.to_string(), error_description))
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
