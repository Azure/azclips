mod mgmt;
mod credential;
pub mod options;
pub mod policies;

// use mgmt::MgmtClient;


pub(crate) use typespec_client_core::http;

pub use typespec_client_core::http::{HttpClient, Request, Response};
pub use typespec::error::{Error as ClientError, Result as ClientResult};

pub use credential::ClientCredential;

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