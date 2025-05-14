use core::fmt;
use percent_encoding::{percent_decode_str, utf8_percent_encode, AsciiSet, CONTROLS};
use serde::{Deserialize, Deserializer, Serialize, Serializer};
use std::{collections::HashMap, str::FromStr};

use crate::{error::AzInternalError, APPLICATION_NAME};

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct CmdUri {
    /// the version of the cmds, it should be the same as the version of the command component
    version: Option<String>,
    /// the command name parts
    segments: Vec<String>,
    /// whether the command is executable
    executable: bool,
    /// the key is case sensitive, the value is stored as a list of strings if multiple values are assigned then the query part will has duplicate keys
    params: HashMap<String, Vec<String>>,
}

const CMD_URI_SCHEMA: &str = "cmd://";

const VERSION_KEY: &str = "VERSION";

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

impl fmt::Display for CmdUri {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        write!(
            f,
            "{}{}{}",
            CMD_URI_SCHEMA,
            self.segments
                .iter()
                // if a cmd with positional arguments the cli wrapper will put the positional argument as tailing segments before check with the components
                // so we need to percent encode the segments as the value may include special characters
                .map(|s| utf8_percent_encode(s, COMPONENT_ENCODE_SET).to_string())
                .collect::<Vec<_>>()
                .join("/"),
            if self.executable { "" } else { "/" }
        )?;
        let mut query_parts = Vec::new();
        if let Some(version) = &self.version {
            query_parts.push(format!(
                "VERSION={}",
                utf8_percent_encode(version, COMPONENT_ENCODE_SET)
            ));
        }

        query_parts.extend(
            self.params
                .iter()
                .map(|(k, values)| {
                    values
                        .iter()
                        .map(|v| {
                            format!(
                                "{}={}",
                                utf8_percent_encode(k, COMPONENT_ENCODE_SET),
                                utf8_percent_encode(v, COMPONENT_ENCODE_SET)
                            )
                        })
                        .collect::<Vec<_>>()
                        .join("&") // join the parts with the same key before sort to keep the order of the values
                })
                .filter(|s| !s.is_empty()),
        );
        if !query_parts.is_empty() {
            query_parts.sort();
            write!(f, "?{}", query_parts.join("&"))?;
        }
        Ok(())
    }
}

impl FromStr for CmdUri {
    type Err = AzInternalError;

    /// Parse a string to a CmdUri
    ///
    /// When convert cmd_uri to string, the query part keys will be sorted. So the output string may be different from the input string.
    ///
    /// # Examples
    ///
    /// ```
    /// use az_core::cmd_uri::CmdUri;
    /// use std::str::FromStr;
    /// let cmd_uri = CmdUri::from_str("cmd://azure/network/vnet/create?VERSION=1.0.0&v=1.2&bool=true&num=1&specialvalue=%3F%23%3D%2F%3A").unwrap();
    /// assert_eq!(cmd_uri.to_string(), "cmd://azure/network/vnet/create?VERSION=1.0.0&bool=true&num=1&specialvalue=%3F%23%3D%2F%3A&v=1.2");
    /// assert_eq!(cmd_uri.executable(), true);
    /// assert_eq!(cmd_uri.version(), Some("1.0.0"));
    /// assert_eq!(cmd_uri.param_values("num").unwrap().next().unwrap(), "1");
    /// assert_eq!(cmd_uri.param_values("bool").unwrap().next().unwrap(), "true");
    /// assert_eq!(cmd_uri.param_values("specialvalue").unwrap().next().unwrap(), "?#=/:");
    /// assert_eq!(cmd_uri.param_values("v").unwrap().next().unwrap(), "1.2");
    /// ```
    ///
    /// ```
    /// use az_core::cmd_uri::CmdUri;
    /// use std::str::FromStr;
    /// let cmd_uri = CmdUri::from_str("cmd://azure/network/vnet/").unwrap();
    /// assert_eq!(cmd_uri.to_string(), "cmd://azure/network/vnet/");
    /// assert_eq!(cmd_uri.executable(), false);
    /// assert_eq!(cmd_uri.version(), None);
    /// assert_eq!(cmd_uri.param_values("num").is_none(), true);
    /// ```
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

        let path_parts = path.split('/');
        let mut segments = Vec::new();
        for s in path_parts {
            let segment = percent_decode_str(s.trim())
                .decode_utf8()
                .map_err(|_| AzInternalError::InvalidCmdUriError(value.to_string()))?
                .to_string();
            segments.push(segment);
        }

        if segments.iter().any(|s| s.is_empty()) {
            return Err(AzInternalError::InvalidCmdUriError(value.to_string()));
        }

        if segments.is_empty() {
            return Err(AzInternalError::InvalidCmdUriError(value.to_string()));
        }

        if segments[0] != APPLICATION_NAME {
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

                // the version must be the Upper case
                if decoded_k.eq(VERSION_KEY) {
                    version = Some(decoded_v.into_owned());
                } else {
                    params
                        .entry(decoded_k.into_owned())
                        .or_insert_with(Vec::new)
                        .push(decoded_v.into_owned());
                }
            }
        }

        Ok(CmdUri {
            version,
            segments,
            executable,
            params,
        })
    }
}

impl CmdUri {
    /// Create a new command uri
    ///
    /// # Examples
    ///
    /// ```
    /// use az_core::cmd_uri::CmdUri;
    /// let cmd_uri = CmdUri::new(["azure", "network", "vnet", "create"], true);
    /// ```
    ///
    /// ```
    /// use az_core::cmd_uri::CmdUri;
    /// let cmd_uri = CmdUri::new(["azure", "network", "vnet"], false);
    /// ```
    pub fn new(cmds: impl IntoIterator<Item = impl Into<String>>, executable: bool) -> Self {
        Self {
            version: None,
            segments: cmds.into_iter().map(Into::into).collect(),
            executable,
            params: HashMap::new(),
        }
    }

    /// Set the version of the command uri
    ///
    /// # Examples
    ///
    /// ```
    /// use az_core::cmd_uri::CmdUri;
    /// let cmd_uri = CmdUri::new(["azure", "network", "vnet", "create"], true).with_version("1.0.0");
    /// assert_eq!(cmd_uri.to_string(), "cmd://azure/network/vnet/create?VERSION=1.0.0");
    /// ```
    pub fn with_version(mut self, version: impl Into<String>) -> Self {
        self.version = Some(version.into());
        self
    }

    /// Set the param of the command uri, the value will be appended to the end of existing values
    ///
    /// # Examples
    ///
    /// ```
    /// use az_core::cmd_uri::CmdUri;
    /// let cmd_uri = CmdUri::new(["azure", "network", "vnet", "create"], true).with_param("num", "1");
    /// assert_eq!(cmd_uri.to_string(), "cmd://azure/network/vnet/create?num=1");
    /// ```
    ///
    pub fn with_param(mut self, key: impl Into<String>, value: impl Into<String>) -> Self {
        let key = key.into();
        if key.eq(VERSION_KEY) {
            panic!("please use with_version to set the version");
        }
        let value = value.into();
        self.params.entry(key).or_default().push(value);
        self
    }

    /// Set the param values of the command uri
    /// It will replace the existing values of the key
    ///
    /// # Examples
    ///
    /// ```
    /// use az_core::cmd_uri::CmdUri;
    /// let cmd_uri = CmdUri::new(["azure", "network", "vnet", "create"], true).with_param_values("num", vec!["1", "2"]);
    /// assert_eq!(cmd_uri.to_string(), "cmd://azure/network/vnet/create?num=1&num=2");
    /// ```
    pub fn with_param_values(
        mut self,
        key: impl Into<String>,
        values: Vec<impl Into<String>>,
    ) -> Self {
        let key = key.into();
        if key.eq(VERSION_KEY) {
            panic!("please use with_version to set the version");
        }
        self.params
            .insert(key, values.into_iter().map(Into::into).collect());
        self
    }
}

impl CmdUri {
    pub fn executable(&self) -> bool {
        self.executable
    }

    pub fn version(&self) -> Option<&str> {
        self.version.as_deref()
    }

    pub fn segments(&self) -> impl Iterator<Item = &str> {
        self.segments.iter().map(String::as_str)
    }

    pub fn last_segment(&self) -> &str {
        self.segments.last().unwrap()
    }

    /// Get the keys of the params
    pub fn params(&self) -> impl Iterator<Item = (&str, impl Iterator<Item = &str>)> {
        self.params
            .iter()
            .map(|(k, values)| (k.as_str(), values.iter().map(String::as_str)))
    }

    /// The key is case sensitive
    pub fn param_values<'a>(&'a self, key: &str) -> Option<impl Iterator<Item = &'a str>> {
        self.params
            .get(key)
            .map(|values| values.iter().map(String::as_str))
    }
}

impl CmdUri {
    /// Return the nodes cmd uri from the tail to the head, if the cmd uri is not executable, the first node is itself.
    ///
    /// The nodes are not executable, and all the params and version are ignored.
    ///
    /// # Examples
    ///
    /// ```
    /// use az_core::cmd_uri::CmdUri;
    /// let cmd_uri = CmdUri::new(["azure", "network", "vnet", "create"], true);
    /// let nodes = cmd_uri.ascending_nodes().collect::<Vec<_>>();
    /// assert_eq!(nodes.len(), 3);
    /// assert_eq!(nodes[0].to_string(), "cmd://azure/network/vnet/");
    /// assert_eq!(nodes[0].executable(), false);
    /// assert_eq!(nodes[1].to_string(), "cmd://azure/network/");
    /// assert_eq!(nodes[1].executable(), false);
    /// assert_eq!(nodes[2].to_string(), "cmd://azure/");
    /// assert_eq!(nodes[2].executable(), false);
    /// ```
    ///
    /// ```
    /// use az_core::cmd_uri::CmdUri;
    /// let cmd_uri = CmdUri::new(["azure", "network", "vnet"], false);
    /// let nodes = cmd_uri.ascending_nodes().collect::<Vec<_>>();
    /// assert_eq!(nodes.len(), 3);
    /// assert_eq!(nodes[0].to_string(), "cmd://azure/network/vnet/");
    /// assert_eq!(nodes[0].executable(), false);
    /// assert_eq!(nodes[1].to_string(), "cmd://azure/network/");
    /// assert_eq!(nodes[1].executable(), false);
    /// assert_eq!(nodes[2].to_string(), "cmd://azure/");
    /// assert_eq!(nodes[2].executable(), false);
    /// ```
    ///
    pub fn ascending_nodes(&self) -> impl Iterator<Item = CmdUri> + '_ {
        let tail = if self.executable {
            self.segments.len() - 1
        } else {
            self.segments.len()
        };
        (0..tail).rev().map(|i| {
            let segments = self.segments[..=i].to_vec();
            
            CmdUri::new(segments, false)
        })
    }

    /// Check if the current command URI is a descendant of the given ancestor URI
    /// at the specified hierarchical level
    pub fn is_child_of(&self, parent: &CmdUri, level_depth: usize) -> bool {
        assert!(level_depth > 0, "level_depth must be greater than 0");
        if self.segments.len() != parent.segments.len() + level_depth {
            return false;
        }
        self.segments[..parent.segments.len()] == parent.segments
    }
}

impl Serialize for CmdUri {
    fn serialize<S>(&self, serializer: S) -> Result<S::Ok, S::Error>
    where
        S: Serializer,
    {
        serializer.serialize_str(&self.to_string())
    }
}

impl<'de> Deserialize<'de> for CmdUri {
    fn deserialize<D>(deserializer: D) -> Result<Self, D::Error>
    where
        D: Deserializer<'de>,
    {
        let s = String::deserialize(deserializer)?;
        CmdUri::from_str(&s).map_err(serde::de::Error::custom)
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_cmd_uri_build() {
        let cmd_uri = CmdUri::new(["azure", "network", "vnet", "create"], true);
        assert_eq!(cmd_uri.to_string(), "cmd://azure/network/vnet/create");
        assert!(cmd_uri.executable());

        let cmd_uri = cmd_uri.with_version("1.0.0");
        assert_eq!(
            cmd_uri.to_string(),
            "cmd://azure/network/vnet/create?VERSION=1.0.0"
        );
        let nodes = cmd_uri.ascending_nodes().collect::<Vec<_>>();
        assert_eq!(nodes.len(), 3);
        assert_eq!(nodes[0].to_string(), "cmd://azure/network/vnet/");
        assert!(!nodes[0].executable());
        assert_eq!(nodes[1].to_string(), "cmd://azure/network/");
        assert!(!nodes[1].executable());
        assert_eq!(nodes[2].to_string(), "cmd://azure/");
        assert!(!nodes[2].executable());
        assert_eq!(cmd_uri.version(), Some("1.0.0"));

        let cmd_uri = cmd_uri
            .with_param("num", 1.to_string())
            .with_param("num", 2.to_string());
        assert_eq!(
            cmd_uri.to_string(),
            "cmd://azure/network/vnet/create?VERSION=1.0.0&num=1&num=2"
        );
        assert_eq!(
            cmd_uri.param_values("num").unwrap().collect::<Vec<_>>(),
            vec!["1", "2"]
        );

        let cmd_uri = cmd_uri.with_param("bool", true.to_string());
        assert_eq!(
            cmd_uri.to_string(),
            "cmd://azure/network/vnet/create?VERSION=1.0.0&bool=true&num=1&num=2"
        );
        assert_eq!(
            cmd_uri.param_values("bool").unwrap().collect::<Vec<_>>(),
            vec!["true"]
        );

        let cmd_uri = cmd_uri.with_param("specialvalue", "?#=/:");
        assert_eq!(
            cmd_uri.to_string(),
            "cmd://azure/network/vnet/create?VERSION=1.0.0&bool=true&num=1&num=2&specialvalue=%3F%23%3D%2F%3A"
        );
        assert_eq!(
            cmd_uri
                .param_values("specialvalue")
                .unwrap()
                .collect::<Vec<_>>(),
            vec!["?#=/:"]
        );

        let cmd_uri = cmd_uri.with_param_values("num", vec!["3"]);
        assert_eq!(
            cmd_uri.to_string(),
            "cmd://azure/network/vnet/create?VERSION=1.0.0&bool=true&num=3&specialvalue=%3F%23%3D%2F%3A"
        );

        let cmd_uri = CmdUri::new(["azure", "network"], false);
        assert_eq!(cmd_uri.to_string(), "cmd://azure/network/");
        assert!(!cmd_uri.executable());

        let cmd_uri = cmd_uri.with_version("1.0.0");
        assert_eq!(cmd_uri.to_string(), "cmd://azure/network/?VERSION=1.0.0");

        // make sure the key is case sensitive
        let cmd_uri = cmd_uri.with_param("NUM", 1.to_string());
        assert_eq!(
            cmd_uri.to_string(),
            "cmd://azure/network/?NUM=1&VERSION=1.0.0"
        );
        let nodes = cmd_uri.ascending_nodes().collect::<Vec<_>>();
        assert_eq!(nodes.len(), 2);
        assert_eq!(nodes[0].to_string(), "cmd://azure/network/");
        assert!(!nodes[0].executable());
        assert_eq!(nodes[1].to_string(), "cmd://azure/");
        assert!(!nodes[1].executable());

        let cmd_url = CmdUri::new(["azure", "network", "vnet", "create"], true);
        let cmd_url = cmd_url.with_param_values(
            "Key",
            vec![
                "Value1".to_string(),
                "Value2".to_string(),
                "Value3".to_string(),
            ],
        );
        assert_eq!(
            cmd_url.to_string(),
            "cmd://azure/network/vnet/create?Key=Value1&Key=Value2&Key=Value3"
        );
        let cmd_url = cmd_url
            .with_param_values("Key", vec!["Value".to_string()])
            .with_param_values("key", vec!["value".to_string()]);
        assert_eq!(
            cmd_url.to_string(),
            "cmd://azure/network/vnet/create?Key=Value&key=value"
        );
        let nodes = cmd_url.ascending_nodes().collect::<Vec<_>>();
        assert_eq!(nodes.len(), 3);
        assert_eq!(nodes[0].to_string(), "cmd://azure/network/vnet/");
        assert!(!nodes[0].executable());
        assert_eq!(nodes[1].to_string(), "cmd://azure/network/");
        assert!(!nodes[1].executable());
        assert_eq!(nodes[2].to_string(), "cmd://azure/");
        assert!(!nodes[2].executable());

        // test cmd uri with positional arguments
        // this format is used only when executable command is not checked
        // for component response command, the positional arguments will be converted to params in the query part
        let cmd_uri = CmdUri::new(
            [
                "azure",
                "config",
                "set",
                "logging.log_dir=~/az-logs",
                "defaults.location=westus2",
                "defaults.group=MyResourceGroup",
            ],
            true,
        );

        assert_eq!(cmd_uri.to_string(), "cmd://azure/config/set/logging.log_dir%3D~%2Faz-logs/defaults.location%3Dwestus2/defaults.group%3DMyResourceGroup");

        // test panic case
        let result = std::panic::catch_unwind(|| {
            let cmd_uri = CmdUri::new(["azure", "network", "vnet", "create"], true);
            cmd_uri.with_param(VERSION_KEY, "1.0.0");
        });
        assert!(result.is_err());

        let result = std::panic::catch_unwind(|| {
            let cmd_uri = CmdUri::new(["azure", "network", "vnet", "create"], true);
            cmd_uri
                .with_param_values("key", vec!["value".to_string()])
                .with_param_values(VERSION_KEY, vec!["1.0.0".to_string()]);
        });
        assert!(result.is_err());
    }

    #[test]
    fn test_cmd_uri_parse() {
        let cmd_uri_str = "cmd://azure/network/vnet/create?Num=0&VERSION=1.0.0&bool=true&num=3&num=1&num=2&specialvalue=%3F%23%3D%2F%3A";
        let cmd_uri = CmdUri::from_str(cmd_uri_str).unwrap();
        assert_eq!(cmd_uri.to_string(), cmd_uri_str);
        assert_eq!(cmd_uri.segments, vec!["azure", "network", "vnet", "create"]);
        assert!(cmd_uri.executable());
        assert_eq!(cmd_uri.version(), Some("1.0.0"));
        assert_eq!(
            cmd_uri.param_values("num").unwrap().collect::<Vec<_>>(),
            vec!["3", "1", "2"]
        );
        assert_eq!(
            cmd_uri.param_values("bool").unwrap().collect::<Vec<_>>(),
            vec!["true"]
        );
        assert_eq!(
            cmd_uri
                .param_values("specialvalue")
                .unwrap()
                .collect::<Vec<_>>(),
            vec!["?#=/:"]
        );
        // make sure the key is case insensitive
        assert!(cmd_uri.param_values("NUM").is_none());
        assert_eq!(
            cmd_uri.param_values("Num").unwrap().collect::<Vec<_>>(),
            vec!["0"]
        );

        let cmd_uri_str = "cmd://azure/network/?VERSION=1.0.0";
        let cmd_uri = CmdUri::from_str(cmd_uri_str).unwrap();
        assert_eq!(cmd_uri.to_string(), cmd_uri_str);
        assert_eq!(cmd_uri.segments, vec!["azure", "network"]);
        assert!(!cmd_uri.executable());
        assert_eq!(cmd_uri.version(), Some("1.0.0"));
        assert!(cmd_uri.param_values("num").is_none());

        let cmd_uri_str = "cmd://azure/config/set/logging.log_dir%3D~%2Faz-logs/defaults.location%3Dwestus2/defaults.group%3DMyResourceGroup";
        let cmd_uri = CmdUri::from_str(cmd_uri_str).unwrap();
        assert_eq!(cmd_uri.to_string(), cmd_uri_str);
        assert_eq!(
            cmd_uri.segments,
            vec![
                "azure",
                "config",
                "set",
                "logging.log_dir=~/az-logs",
                "defaults.location=westus2",
                "defaults.group=MyResourceGroup"
            ]
        );
        assert!(cmd_uri.executable());
        assert_eq!(cmd_uri.version(), None);

        // test error cases
        let cmd_uri_str = "http://azure/network/vnet/create?VERSION=1.0.0";
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

        let cmd_uri_str = "cmd://az/network/vnet/create";
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
