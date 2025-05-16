// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

//! Custom token response types that include id_token field.

use oauth2::{EmptyExtraTokenFields, TokenType};
use serde::{Deserialize, Serialize};

/// A token response with an id_token field, extending the standard OAuth2 token response.
#[derive(Debug, Clone, Deserialize, Serialize)]
pub struct EntraIdTokenResponse<TT> 
where
    TT: TokenType,
{
    #[serde(bound = "TT: TokenType")]
    #[serde(flatten)]
    standard_response: oauth2::StandardTokenResponse<EmptyExtraTokenFields, TT>,
    id_token: Option<String>,
}

impl<TT> EntraIdTokenResponse<TT> 
where
    TT: TokenType,
{
    pub fn id_token(&self) -> Option<&str> {
        self.id_token.as_deref()
    }
    
    pub fn into_parts(self) -> (oauth2::StandardTokenResponse<EmptyExtraTokenFields, TT>, Option<String>) {
        (self.standard_response, self.id_token)
    }
}

impl<TT> oauth2::TokenResponse for EntraIdTokenResponse<TT>
where
    TT: TokenType,
{
    type TokenType = TT;
    
    /// Get the access token.
    fn access_token(&self) -> &oauth2::AccessToken {
        self.standard_response.access_token()
    }

    /// Get the refresh token, if one was provided.
    fn refresh_token(&self) -> Option<&oauth2::RefreshToken> {
        self.standard_response.refresh_token()
    }

    /// Get the token type.
    fn token_type(&self) -> &TT {
        self.standard_response.token_type()
    }

    /// Get the token expiration time, if one was provided.
    fn expires_in(&self) -> Option<std::time::Duration> {
        self.standard_response.expires_in()
    }

    /// Get the scopes for which the token is valid, if any were provided.
    fn scopes(&self) -> Option<&Vec<oauth2::Scope>> {
        self.standard_response.scopes()
    }
}

/// From JSON implementation for our custom token response
impl<'de, TT> EntraIdTokenResponse<TT>
where
    TT: TokenType,
{
    /// Parse the token response from JSON
    pub fn from_json(json: &[u8]) -> Result<Self, oauth2::RequestTokenError<oauth2::reqwest::Error, oauth2::StandardErrorResponse<oauth2::basic::BasicErrorResponseType>>> {
        serde_json::from_slice(json).map_err(|err| {
            oauth2::RequestTokenError::Other(format!("failed to parse JSON: {}", err))
        })
    }
}
