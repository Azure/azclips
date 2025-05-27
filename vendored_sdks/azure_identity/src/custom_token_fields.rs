// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

//! Custom implementation of extra token fields for the OAuth2 crate.

use oauth2::ExtraTokenFields;
use serde::{Deserialize, Serialize};

/// Custom extra token fields implementation.
#[derive(Clone, Debug, Deserialize, PartialEq, Serialize)]
pub struct CustomTokenFields {
    /// The ID token returned by the OAuth2 server.
    #[serde(default, rename = "id_token", skip_serializing_if = "Option::is_none")]
    pub id_token: Option<String>,
}

// The ExtraTokenFields trait is just a marker trait with bounds
// DeserializeOwned + Debug + Serialize, no methods to implement
impl ExtraTokenFields for CustomTokenFields {}
