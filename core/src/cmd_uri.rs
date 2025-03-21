use core::fmt;
use percent_encoding::{percent_decode_str, utf8_percent_encode, AsciiSet, CONTROLS};
use std::{collections::HashMap, str::FromStr};

use crate::error::AzInternalError;

const CMD_URI_SCHEMA: &str = "cmd://";

/// Reference the [component percent-encode set](https://url.spec.whatwg.org/#query-percent-encode-set) for the allowed characters in the query part of the command uri
const COMPONENT_ENCODE_SET: &AsciiSet = &CONTROLS
    .add(b' ')
    .add(b'"')
    .add(b'#')
    .add(b'<')
    .add(b'>') // query
    .add(b'?')
    .add(b'^')
    .add(b'`')
    .add(b'{')
    .add(b'}') // path
    .add(b'/')
    .add(b':')
    .add(b';')
    .add(b'=')
    .add(b'@')
    .add(b'[')
    .add(b']')
    .add(b'|') // userinfo
    .add(b'$')
    .add(b'&')
    .add(b'+')
    .add(b',');

#[derive(Debug)]
pub struct CmdUri {
    // the version of the cmds, it should be the save as the version of the command service
    version: Option<String>,
    // the command name parts
    cmds: Vec<String>,
    // whether the command is executable
    executable: bool,
    // the key should be in lower case
    params: HashMap<String, String>,
}

impl fmt::Display for CmdUri {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        write!(
            f,
            "{}{}{}",
            CMD_URI_SCHEMA,
            self.cmds.join("/"),
            if self.executable { "" } else { "/" }
        )?;
        let mut query_parts = Vec::new();
        if let Some(version) = &self.version {
            query_parts.push(format!(
                "version={}",
                utf8_percent_encode(version, COMPONENT_ENCODE_SET)
            ));
        }
        query_parts.extend(self.params.iter().map(|(k, v)| {
            format!(
                "{}={}",
                utf8_percent_encode(k, COMPONENT_ENCODE_SET),
                utf8_percent_encode(v, COMPONENT_ENCODE_SET)
            )
        }));
        if !query_parts.is_empty() {
            query_parts.sort();
            write!(f, "?{}", query_parts.join("&"))?;
        }
        Ok(())
    }
}

impl FromStr for CmdUri {
    type Err = AzInternalError;

    fn from_str(value: &str) -> Result<Self, Self::Err> {
        if !value.starts_with(CMD_URI_SCHEMA) {
            return Err(AzInternalError::InvalidCmdUriError(value.to_string()));
        }
        // split value into parts and query
        let mut parts = value[CMD_URI_SCHEMA.len()..].splitn(2, '?');
        let mut path = parts.next().unwrap();
        let executable = !path.ends_with("/");
        if !executable {
            // for command group expression, remove the trailing slash
            path = &path[..path.len() - 1];
        }
        let cmds = path
            .split('/')
            .map(|s| s.trim().to_string())
            .collect::<Vec<_>>();

        if cmds.iter().any(|s| s.is_empty()) {
            return Err(AzInternalError::InvalidCmdUriError(value.to_string()));
        }

        if cmds.is_empty() {
            return Err(AzInternalError::InvalidCmdUriError(value.to_string()));
        }

        let mut params = HashMap::new();
        let mut version = None;
        if let Some(query) = parts.next() {
            for part in query.split('&') {
                let mut kv = part.splitn(2, '=');
                let k = kv
                    .next()
                    .ok_or(AzInternalError::InvalidCmdUriError(value.to_string()))?;
                let v = kv
                    .next()
                    .ok_or(AzInternalError::InvalidCmdUriError(value.to_string()))?;

                // URL decode the key and value
                let decoded_k = percent_decode_str(k)
                    .decode_utf8()
                    .map_err(|_| AzInternalError::InvalidCmdUriError(value.to_string()))?;
                let decoded_v = percent_decode_str(v)
                    .decode_utf8()
                    .map_err(|_| AzInternalError::InvalidCmdUriError(value.to_string()))?;

                if decoded_k.eq_ignore_ascii_case("version") {
                    version = Some(decoded_v.into_owned());
                } else {
                    params.insert(decoded_k.into_owned(), decoded_v.into_owned());
                }
            }
        }

        Ok(CmdUri {
            version,
            cmds,
            executable,
            params,
        })
    }
}

impl CmdUri {
    pub fn new(cmds: Vec<impl Into<String>>, executable: bool) -> Self {
        Self {
            version: None,
            cmds: cmds.into_iter().map(Into::into).collect(),
            executable,
            params: HashMap::new(),
        }
    }

    pub fn with_version(mut self, version: impl Into<String>) -> Self {
        self.version = Some(version.into());
        self
    }

    /// The key should be in lower case, otherwise it will convert to lower case
    pub fn with_param(mut self, key: impl Into<String>, value: impl Into<String>) -> Self {
        let key = key.into().to_lowercase();
        let value = value.into();
        if key.eq("version") {
            panic!("please use with_version to set the version");
        }
        self.params.insert(key, value);
        self
    }

    /// The key should be in lower case, otherwise it will convert to lower case
    pub fn with_params<K, V>(mut self, params: HashMap<K, V>) -> Self
    where
        K: Into<String>,
        V: Into<String>,
    {
        self.params.extend(params.into_iter().map(|(k, v)| {
            let key = k.into().to_lowercase();
            let value = v.into();
            if key.eq("version") {
                panic!("please use with_version to set the version");
            }
            (key, value)
        }));
        self
    }

    pub fn executable(&self) -> bool {
        self.executable
    }

    pub fn version(&self) -> Option<&str> {
        self.version.as_ref().map(|s| s.as_str())
    }

    /// The key should be in lower case, otherwise it will convert to lower case
    pub fn get_param_str(&self, key: &str) -> Option<&str> {
        self.params
            .get(key.to_lowercase().as_str())
            .map(|s| s.as_str())
    }

    /// The key should be in lower case, otherwise it will convert to lower case
    pub fn get_param_from_str<T>(&self, key: &str) -> Result<Option<T>, T::Err>
    where
        T: std::str::FromStr,
    {
        self.params
            .get(key.to_lowercase().as_str())
            .map(|s| s.as_str())
            .map(T::from_str)
            .transpose()
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_cmd_uri_build() {
        let cmd_uri = CmdUri::new(vec!["azure", "network", "vnet", "create"], true);
        assert_eq!(cmd_uri.to_string(), "cmd://azure/network/vnet/create");
        assert_eq!(cmd_uri.executable(), true);

        let cmd_uri = cmd_uri.with_version("1.0.0");
        assert_eq!(
            cmd_uri.to_string(),
            "cmd://azure/network/vnet/create?version=1.0.0"
        );

        assert_eq!(cmd_uri.version(), Some("1.0.0"));

        let cmd_uri = cmd_uri.with_param("num", 1.to_string());
        assert_eq!(
            cmd_uri.to_string(),
            "cmd://azure/network/vnet/create?num=1&version=1.0.0"
        );
        assert_eq!(cmd_uri.get_param_from_str("num").unwrap(), Some(1));

        let cmd_uri = cmd_uri.with_param("bool", true.to_string());
        assert_eq!(
            cmd_uri.to_string(),
            "cmd://azure/network/vnet/create?bool=true&num=1&version=1.0.0"
        );
        assert_eq!(cmd_uri.get_param_from_str("bool").unwrap(), Some(true));

        let cmd_uri = cmd_uri.with_param("specialvalue", "?#=/:");
        assert_eq!(
            cmd_uri.to_string(),
            "cmd://azure/network/vnet/create?bool=true&num=1&specialvalue=%3F%23%3D%2F%3A&version=1.0.0"
        );
        assert_eq!(cmd_uri.get_param_str("specialvalue"), Some("?#=/:"));

        let cmd_uri = CmdUri::new(vec!["azure", "network"], false);
        assert_eq!(cmd_uri.to_string(), "cmd://azure/network/");
        assert_eq!(cmd_uri.executable(), false);

        let cmd_uri = cmd_uri.with_version("1.0.0");
        assert_eq!(cmd_uri.to_string(), "cmd://azure/network/?version=1.0.0");

        // make sure the key is case insensitive
        let cmd_uri = cmd_uri.with_param("NUM", 1.to_string());
        assert_eq!(
            cmd_uri.to_string(),
            "cmd://azure/network/?num=1&version=1.0.0"
        );

        let cmd_url = CmdUri::new(vec!["azure", "network", "vnet", "create"], true);
        let params = HashMap::from([("KEY".to_string(), "Value".to_string())]);
        let cmd_url = cmd_url.with_params(params);
        assert_eq!(
            cmd_url.to_string(),
            "cmd://azure/network/vnet/create?key=Value"
        );

        // test panic case
        let result = std::panic::catch_unwind(|| {
            let cmd_uri = CmdUri::new(vec!["azure", "network", "vnet", "create"], true);
            cmd_uri.with_param("version", "1.0.0");
        });
        assert!(result.is_err());

        let result = std::panic::catch_unwind(|| {
            let cmd_uri = CmdUri::new(vec!["azure", "network", "vnet", "create"], true);
            let params = HashMap::from([
                ("key".to_string(), "value".to_string()),
                ("version".to_string(), "1.0.0".to_string()),
            ]);
            cmd_uri.with_params(params);
        });
        assert!(result.is_err());
    }

    #[test]
    fn test_cmd_uri_parse() {
        let cmd_uri_str = "cmd://azure/network/vnet/create?bool=true&num=1&specialvalue=%3F%23%3D%2F%3A&version=1.0.0";
        let cmd_uri = CmdUri::from_str(cmd_uri_str).unwrap();
        assert_eq!(cmd_uri.to_string(), cmd_uri_str);
        assert_eq!(cmd_uri.cmds, vec!["azure", "network", "vnet", "create"]);
        assert_eq!(cmd_uri.executable(), true);
        assert_eq!(cmd_uri.version(), Some("1.0.0"));
        assert_eq!(cmd_uri.get_param_str("num"), Some("1"));
        assert_eq!(cmd_uri.get_param_str("bool"), Some("true"));
        assert_eq!(cmd_uri.get_param_str("specialvalue"), Some("?#=/:"));
        // make sure the key is case insensitive
        assert_eq!(cmd_uri.get_param_str("NUM"), Some("1"));

        let cmd_uri_str = "cmd://azure/network/?version=1.0.0";
        let cmd_uri = CmdUri::from_str(cmd_uri_str).unwrap();
        assert_eq!(cmd_uri.to_string(), cmd_uri_str);
        assert_eq!(cmd_uri.cmds, vec!["azure", "network"]);
        assert_eq!(cmd_uri.executable(), false);
        assert_eq!(cmd_uri.version(), Some("1.0.0"));
        assert_eq!(cmd_uri.get_param_str("num"), None);

        // test error cases
        let cmd_uri_str = "http://azure/network/vnet/create?version=1.0.0";
        match CmdUri::from_str(cmd_uri_str) {
            Err(AzInternalError::InvalidCmdUriError(s)) => {
                assert_eq!(s, cmd_uri_str);
            }
            _ => panic!("should be InvalidCmdUriError"),
        }

        let cmd_uri_str = "cmd://";
        match CmdUri::from_str(cmd_uri_str) {
            Err(AzInternalError::InvalidCmdUriError(_)) => {}
            _ => panic!("should be InvalidCmdUriError"),
        }

        let cmd_uri_str = "cmd://azure/network/vnet//create";
        match CmdUri::from_str(cmd_uri_str) {
            Err(AzInternalError::InvalidCmdUriError(_)) => {}
            _ => panic!("should be InvalidCmdUriError"),
        }
    }
}
