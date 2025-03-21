use crate::error::AzResult;

pub struct AzPlugin {
    // the server name for plugin
    name: String,
}

impl AzPlugin {
    pub async fn load(name: &str) -> AzResult<Self> {
        let plugin = Self {
            name: name.to_string(),
        };

        Ok(plugin)
    }
}
