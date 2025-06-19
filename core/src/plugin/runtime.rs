use async_trait::async_trait;
use prost::Message;
use std::io::Read;
use std::path::PathBuf;
use std::str::FromStr;

use crate::component::{AzComponent, AzComponentCtx};
use crate::error::{AzError, AzInternalError};
use crate::pb::{
    crs_interface, ComponentMetadata, CrsCommand, CrsCommandGroup, CrsInterface,
    CrsPluginComponent, ExecuteRequest, ExecuteResponse, InterfaceRequest, InterfaceResponse,
    MetadataRequest, MetadataResponse, ValidateRequest, ValidateResponse,
};
use crate::uri::CrsUri;

pub struct PluginRuntime {
    inner: CrsPluginComponent,
}

impl PluginRuntime {
    pub(crate) async fn load_from_path(path: &PathBuf) -> Result<Self, AzInternalError> {
        let contents = tokio::fs::read(path).await?;
        let mut decoder = flate2::read::ZlibDecoder::new(&contents[..]);
        let mut buffer = Vec::new();
        decoder.read_to_end(&mut buffer)?;
        let component = CrsPluginComponent::decode(&buffer[..])
            .map_err(|e| AzInternalError::PluginLoadError(e))?;
        Ok(Self { inner: component })
    }

    pub(crate) fn get_metadata(&self) -> Result<ComponentMetadata, AzInternalError> {
        let metadata = self.inner.metadata.clone();
        metadata.ok_or(AzInternalError::InvalidPluginError(format!(
            "Invalid plugin: missing metadata"
        )))
    }
}

#[async_trait]
impl<T: AzComponentCtx> AzComponent<T> for PluginRuntime {
    async fn metadata(&self, _: &T, _: MetadataRequest) -> Result<MetadataResponse, AzError> {
        let metadata = self.get_metadata()?;
        Ok(MetadataResponse {
            metadata: Some(metadata),
        })
    }

    async fn validate(
        &self,
        _: &T,
        request: ValidateRequest,
    ) -> Result<ValidateResponse, AzError> {
        let component_uri = self.inner.uri();
        let uri = &request.uri;
        if !uri.starts_with(component_uri.to_string().as_str()) {
            return Ok(ValidateResponse { uri: None });
        }

        let uri = CrsUri::from_str(&request.uri)
            .map_err(|e| AzInternalError::InvalidCrsUriError(e.to_string()))?;
        let mut segments = uri
            .segments()
            .skip(component_uri.segments().count())
            .map(|s| s.to_string());

        match self.inner.find_command(&mut segments) {
            Some(command) => {
                let mut validated_uri = CrsUri::from_str(&command.uri).unwrap();
                for (idx, positional_value) in uri
                    .segments()
                    .skip(validated_uri.segments().count())
                    .enumerate()
                {
                    let arg = command
                        .positional_args
                        .get(idx)
                        .or(command.positional_args.last());
                    match arg {
                        Some(arg) => {
                            validated_uri = validated_uri.with_param(arg, positional_value);
                        }
                        None => {
                            return Ok(ValidateResponse { uri: None });
                        }
                    }
                }
                if validated_uri.executable() {
                    // the command uri should be executable
                    Ok(ValidateResponse {
                        uri: Some(validated_uri.to_string()),
                    })
                } else {
                    Err(AzInternalError::InvalidPluginError(
                        format!("Invalid command uri which is not executable: {}", command.uri),
                    ).into())
                }
            }
            None => match self.inner.find_group(&mut segments) {
                Some(group) => {
                    let validated_uri = CrsUri::from_str(&group.uri).unwrap();
                    if !validated_uri.executable() {
                        // the command group uri should not be executable
                        Ok(ValidateResponse {
                            uri: Some(validated_uri.to_string()),
                        })
                    } else {
                        Err(AzInternalError::InvalidPluginError(
                            format!("Invalid group uri which is executable: {}", group.uri),
                        ).into())
                    }
                }
                None => Ok(ValidateResponse { uri: None }),
            },
        }
    }

    async fn interface(
        &self,
        _: &T,
        request: InterfaceRequest,
    ) -> Result<InterfaceResponse, AzError> {
        let component_uri = self.inner.uri();
        let uri = &request.uri;
        if !uri.starts_with(component_uri.to_string().as_str()) {
            return Ok(InterfaceResponse { interface: None });
        }
        let depth = if request.depth == 0 {
            None
        } else {
            Some(request.depth)
        };

        let uri = CrsUri::from_str(&request.uri)
            .map_err(|e| AzInternalError::InvalidCrsUriError(e.to_string()))?;
        let mut segments = uri
            .segments()
            .skip(component_uri.segments().count())
            .map(|s| s.to_string());

        let interface = if uri.executable() {
            let command = self.inner.find_command(&mut segments);
            if segments.next().is_some() {
                // the command should be fully matched
                return Ok(InterfaceResponse { interface: None });
            }
            command.map(|c| CrsInterface {
                level: Some(crs_interface::Level::Command(c.partial_clone(depth))),
            })
        } else {
            let group = self.inner.find_group(&mut segments);
            if segments.next().is_some() {
                // the group should be fully matched
                return Ok(InterfaceResponse { interface: None });
            }
            group.map(|g| CrsInterface {
                level: Some(crs_interface::Level::CommandGroup(g.partial_clone(depth))),
            })
        };
        Ok(InterfaceResponse { interface })
    }

    async fn execute(&self, ctx: &T, request: ExecuteRequest) -> Result<ExecuteResponse, AzError> {
        
        // let params = request.params.as_ref().map(|p| {
        //     let mut params = HashMap::new();
        //     for (key, value) in p.params.iter() {
        //         params.insert(key.clone(), serde_json::);
        //     }
        //     params
        // });

        todo!()
    }
}

impl CrsPluginComponent {
    fn uri(&self) -> CrsUri {
        CrsUri::from_str(&self.metadata.as_ref().unwrap().uri).expect("Invalid plugin: missing uri")
    }

    fn find_group<'a>(
        &'a self,
        segments: &mut impl Iterator<Item = String>,
    ) -> Option<&'a CrsCommandGroup> {
        let interface = self
            .interface
            .as_ref()
            .expect("Invalid plugin: missing interface");
        match interface.level.as_ref() {
            Some(crs_interface::Level::CommandGroup(command_group)) => {
                command_group.find_group(segments)
            }
            Some(crs_interface::Level::Command(_)) => None,
            None => None,
        }
    }

    fn find_command<'a>(
        &'a self,
        segments: &mut impl Iterator<Item = String>,
    ) -> Option<&'a CrsCommand> {
        let interface = self
            .interface
            .as_ref()
            .expect("Invalid plugin: missing interface");
        match interface.level.as_ref() {
            Some(crs_interface::Level::CommandGroup(command_group)) => {
                command_group.find_command(segments)
            }
            Some(crs_interface::Level::Command(command)) => Some(command),
            None => None,
        }
    }
}

impl CrsCommandGroup {
    fn find_group<'a>(
        &'a self,
        segments: &mut impl Iterator<Item = String>,
    ) -> Option<&'a CrsCommandGroup> {
        match segments.next().as_deref() {
            None => Some(self),
            Some(segment) => {
                let group = self.groups.iter().find(|g| g.name == segment);
                match group {
                    Some(group) => group.find_group(segments),
                    None => None,
                }
            }
        }
    }

    fn find_command<'a>(
        &'a self,
        segments: &mut impl Iterator<Item = String>,
    ) -> Option<&'a CrsCommand> {
        match segments.next().as_deref() {
            None => None,
            Some(segment) => {
                let command = self.commands.iter().find(|c| c.name == segment);
                match command {
                    Some(command) => Some(command),
                    None => {
                        let group = self.groups.iter().find(|g| g.name == segment);
                        match group {
                            Some(group) => group.find_command(segments),
                            None => None,
                        }
                    }
                }
            }
        }
    }

    // Clone the command group and its sub-groups and commands in the given depth
    fn partial_clone(&self, depth: Option<u32>) -> CrsCommandGroup {
        if depth.is_none_or(|d| d > 0) {
            let depth = depth.map(|d| d - 1);
            CrsCommandGroup {
                name: self.name.clone(),
                uri: self.uri.clone(),
                help: self.help.clone(),
                groups: self.groups.iter().map(|g| g.partial_clone(depth)).collect(),
                commands: self
                    .commands
                    .iter()
                    .map(|c| c.partial_clone(depth))
                    .collect(),
            }
        } else {
            CrsCommandGroup {
                name: self.name.clone(),
                uri: self.uri.clone(),
                help: self.help.clone(),
                groups: vec![],
                commands: vec![],
            }
        }
    }
}

impl CrsCommand {

    // The plugin data is not cloned
    fn partial_clone(&self, _: Option<u32>) -> CrsCommand {
        CrsCommand {
            name: self.name.clone(),
            uri: self.uri.clone(),
            help: self.help.clone(),
            version: self.version.clone(),
            confirmation: self.confirmation.clone(),
            args: self.args.clone(),
            positional_args: self.positional_args.clone(),
            plugin: None,
        }
    }
}
