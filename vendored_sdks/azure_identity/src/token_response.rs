// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

//! Custom token response types that include additional fields like the ID token.

use oauth2::{
    basic::BasicTokenType,
    EmptyExtraTokenFields,
    StandardTokenResponse,
    TokenResponse,
};
use serde::{Deserialize, Serialize};
use std::time::{Duration, SystemTime};

/// Extended token response that includes the ID token alongside the OAuth2 standard fields.
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct ExtendedTokenResponse {
    /// The standard OAuth2 token response
    pub standard_response: StandardTokenResponse<EmptyExtraTokenFields, BasicTokenType>,
    /// The ID token returned by the OAuth2 server, if any
    pub id_token: Option<String>,
}

impl ExtendedTokenResponse {
    /// Create a new ExtendedTokenResponse from a standard response and an ID token
    pub fn new(
        standard_response: StandardTokenResponse<EmptyExtraTokenFields, BasicTokenType>,
        id_token: Option<String>,
    ) -> Self {
        Self {
            standard_response,
            id_token,
        }
    }

    /// Get the access token
    pub fn access_token(&self) -> &str {
        self.standard_response.access_token().secret()
    }

    /// Get the token type
    pub fn token_type(&self) -> &BasicTokenType {
        self.standard_response.token_type()
    }

    /// Get the scopes associated with this token
    pub fn scopes(&self) -> Option<&Vec<oauth2::Scope>> {
        self.standard_response.scopes()
    }

    /// Get the refresh token, if one was provided
    pub fn refresh_token(&self) -> Option<&oauth2::RefreshToken> {
        self.standard_response.refresh_token()
    }

    /// Get the ID token, if one was provided
    pub fn id_token(&self) -> Option<&str> {
        self.id_token.as_deref()
    }

    /// Get the expiration time for this token, if one was provided
    pub fn expires_in(&self) -> Option<Duration> {
        self.standard_response.expires_in()
    }

    /// Calculate the expiration time for this token as a SystemTime
    pub fn expires_at(&self) -> Option<SystemTime> {
        self.standard_response
            .expires_in()
            .map(|duration| SystemTime::now() + duration)
    }
}
