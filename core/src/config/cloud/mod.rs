mod cfg;
mod metadata;

use std::{
    fmt::{self, Display},
    str::FromStr,
};

use serde::{Deserialize, Deserializer, Serialize, Serializer};

pub use cfg::AzCloudCfg;
pub use metadata::{endpoint, resource_id, AzCloudMetadata};

#[derive(Debug, PartialEq, Eq, Clone, Hash)]
pub(crate) enum AzCloud {
    AzureCloud,
    AzureChinaCloud,
    AzureUSGovernment,
    AzureGermanCloud,
    Custom(String),
}

impl AzCloud {
    pub fn list_builtin() -> Vec<Self> {
        vec![
            Self::AzureCloud,
            Self::AzureChinaCloud,
            Self::AzureUSGovernment,
            Self::AzureGermanCloud,
        ]
    }
}

impl Default for AzCloud {
    fn default() -> Self {
        Self::AzureCloud
    }
}

impl Display for AzCloud {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            AzCloud::AzureCloud => write!(f, "AzureCloud"),
            AzCloud::AzureChinaCloud => write!(f, "AzureChinaCloud"),
            AzCloud::AzureUSGovernment => write!(f, "AzureUSGovernment"),
            AzCloud::AzureGermanCloud => write!(f, "AzureGermanCloud"),
            AzCloud::Custom(name) => write!(f, "{}", name),
        }
    }
}

impl FromStr for AzCloud {
    type Err = String;

    fn from_str(s: &str) -> Result<Self, Self::Err> {
        // ignore case
        match s.to_lowercase().as_str() {
            "azurecloud" => Ok(AzCloud::AzureCloud),
            "azurechinacloud" => Ok(AzCloud::AzureChinaCloud),
            "azureusgovernment" => Ok(AzCloud::AzureUSGovernment),
            "azuregermancloud" => Ok(AzCloud::AzureGermanCloud),
            _ => Ok(AzCloud::Custom(s.to_string())),
        }
    }
}

impl Serialize for AzCloud {
    fn serialize<S>(&self, serializer: S) -> Result<S::Ok, S::Error>
    where
        S: Serializer,
    {
        serializer.serialize_str(&self.to_string())
    }
}

impl<'de> Deserialize<'de> for AzCloud {
    fn deserialize<D>(deserializer: D) -> Result<Self, D::Error>
    where
        D: Deserializer<'de>,
    {
        let s = String::deserialize(deserializer)?;
        Ok(Self::from_str(&s).unwrap())
    }
}

#[cfg(test)]
mod test {
    use std::str::FromStr;

    use super::AzCloud;

    #[test]
    fn test_az_cloud() {
        let cloud = AzCloud::AzureCloud;
        assert_eq!(cloud.to_string(), "AzureCloud");
        let cloud = AzCloud::AzureChinaCloud;
        assert_eq!(cloud.to_string(), "AzureChinaCloud");
        let cloud = AzCloud::AzureUSGovernment;
        assert_eq!(cloud.to_string(), "AzureUSGovernment");
        let cloud = AzCloud::AzureGermanCloud;
        assert_eq!(cloud.to_string(), "AzureGermanCloud");
        let cloud = AzCloud::Custom("CustomCloud".to_string());
        assert_eq!(cloud.to_string(), "CustomCloud");
    }

    #[test]
    fn test_az_cloud_from_str() {
        let cloud = AzCloud::from_str("AzureCloud").unwrap();
        assert_eq!(cloud, AzCloud::AzureCloud);
        let cloud = AzCloud::from_str("azurecloud").unwrap();
        assert_eq!(cloud, AzCloud::AzureCloud);
        let cloud = AzCloud::from_str("AzureChinaCloud").unwrap();
        assert_eq!(cloud, AzCloud::AzureChinaCloud);
        let cloud = AzCloud::from_str("azurechinacloud").unwrap();
        assert_eq!(cloud, AzCloud::AzureChinaCloud);
        let cloud = AzCloud::from_str("AzureUSGovernment").unwrap();
        assert_eq!(cloud, AzCloud::AzureUSGovernment);
        let cloud = AzCloud::from_str("azureusgovernment").unwrap();
        assert_eq!(cloud, AzCloud::AzureUSGovernment);
        let cloud = AzCloud::from_str("AzureGermanCloud").unwrap();
        assert_eq!(cloud, AzCloud::AzureGermanCloud);
        let cloud = AzCloud::from_str("azuregermancloud").unwrap();
        assert_eq!(cloud, AzCloud::AzureGermanCloud);
        let cloud = AzCloud::from_str("CustomCloud").unwrap();
        assert_eq!(cloud, AzCloud::Custom("CustomCloud".to_string()));
        let cloud2 = AzCloud::from_str("CustomCloud2").unwrap();
        assert_ne!(cloud, cloud2);
    }
}
