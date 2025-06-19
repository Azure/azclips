mod credential;
mod mgmt;
mod poller;

pub mod options;
pub mod policies;

pub use mgmt::MgmtClient;

use typespec::error::{Error as ClientError, ErrorKind, Result as ClientResult};
use typespec_client_core::error::http_response_from_body;
use typespec_client_core::http::Response as RawResponse;
use typespec_client_core::http;

pub use credential::ClientCredential;
pub(crate) use typespec_client_core::http::{headers, Method, StatusCode, headers::Headers, request};

use bytes::Bytes;

#[derive(Debug, Clone)]
pub struct Response {
    pub status_code: StatusCode,
    pub headers: Headers,
    pub body: Bytes,
}

impl Response {
    pub async fn from_raw_response(resp: RawResponse) -> ClientResult<Self> {
        let (status_code, headers, body) = resp.deconstruct();
        let body = body.collect().await?;
        Ok(Self {
            status_code,
            headers,
            body,
        })
    }
}

impl From<Response> for ErrorKind {
    fn from(val: Response) -> Self {
        http_response_from_body(val.status_code, &val.body)
    }
}

impl From<Response> for ClientError {
    fn from(val: Response) -> Self {
        let error_kind: ErrorKind = val.into();
        error_kind.into_error()
    }
}

pub enum ClientType {
    /// Management client for control plane operations
    Mgmt,
}

impl ClientType {
    // fn build_client(self, credential: Arc<dyn TokenCredential>) -> Box<dyn HttpClient> {
    //     match self {
    //         ClientType::Mgmt => Box::new(MgmtClient::default()),
    //     }
    // }
}
