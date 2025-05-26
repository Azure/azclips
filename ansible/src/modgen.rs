mod docgen;

use anyhow::{self, bail, Result};
use az_core::pb::crs_interface::Level::{Command, CommandGroup};
use az_core::{uri::CrsUri, AzCore};

pub enum Module {
    Resource(ResourceModule),
    Action(ActionModule),
}

#[allow(dead_code)]
pub struct ResourceModule {
    uri: CrsUri,
    c: String,
    r: String,
    u: Option<String>,
    d: String,
}

impl ResourceModule {
    pub fn new(uri: CrsUri, c: String, r: String, u: Option<String>, d: String) -> Self {
        ResourceModule { uri, c, r, u, d }
    }

    pub async fn generate_module(&self) -> Result<String> {
        todo!();
    }
}

pub struct ActionModule {
    uri: CrsUri,
}

impl ActionModule {
    pub fn new(uri: CrsUri) -> Self {
        ActionModule { uri }
    }

    pub async fn generate_module(&self) -> Result<String> {
        let core = AzCore::instance().await;
        let invoke = core.resolve_invocation(self.uri.clone()).await?;
        if !invoke.uri.executable() {
            bail!("{} is not an executable URI", self.uri);
        }
        let itf = core.get_interface(&invoke).await?;

        if let Some(level) = itf.level {
            match level {
                Command(cmd) => {
                    let doc = docgen::Document::from(&cmd);
                    Ok(serde_yaml::to_string(&doc)?)
                }
                CommandGroup(_) => bail!("unexpected CommandGroup interface"),
            }
        } else {
            bail!("unexpected None interface level");
        }
    }
}
