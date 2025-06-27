# Azure Cirrus PowerShell

This library is the adapter for the commands of services run in PowerShell framework. It provides a modular approach to Azure service management through PowerShell cmdlets.

## Architecture

The Azure Cirrus PowerShell system consists of:

- **Cirrus Core Module**: The foundational module containing core functionality
- **Plugin Modules**: Individual service-specific modules (e.g., Databricks, Storage, etc.)
- **Azure Aggregator Module**: A convenience module that imports all available service modules

## Prerequisites

Before building the PowerShell modules, ensure you have the following installed:

- **Rust toolchain** (latest stable version)
- **.NET SDK** (version 9.0 or later)
- **PowerShell 7.2+**
- **PlatyPS module** for help generation:
  ```powershell
  Install-Module -Name platyPS -Force
  ```

## Building Modules

### Building the Cirrus Core Module

The Cirrus core module contains the fundamental functionality required by all plugin modules.

```powershell
# Basic build (Debug configuration)
.\modules\Cirrus\Build-CirrusModule.ps1 -OutputPSModules "C:\PSModules"

# Release build
.\modules\Cirrus\Build-CirrusModule.ps1 -Release -OutputPSModules "C:\PSModules"

# Clean build with tests
.\modules\Cirrus\Build-CirrusModule.ps1 -Clean -Release -Test -OutputPSModules "C:\PSModules"

# Keep help directory for inspection
.\modules\Cirrus\Build-CirrusModule.ps1 -OutputPSModules "C:\PSModules" -KeepHelpFolder $true
```

### Building Plugin Modules

Plugin modules are built from `.plugin` component files and provide service-specific functionality.

#### Building a Single Plugin Module

```powershell
# Build a single plugin module
.\modules\Cirrus\Build-PluginModule.ps1 -ComponentPath "C:\path\to\databricks.plugin" -OutputPSModules "C:\PSModules"

# Build with help directory preserved
.\modules\Cirrus\Build-PluginModule.ps1 -ComponentPath "C:\path\to\databricks.plugin" -OutputPSModules "C:\PSModules" -KeepHelpFolder $true
```

#### Building Multiple Plugin Modules

```powershell
# Build all plugin modules from a directory
.\modules\Cirrus\Build-PluginModule.ps1 -ComponentsFolder "C:\Components" -OutputPSModules "C:\PSModules"
```

#### Creating Azure Aggregator Module Only

```powershell
# Create or update only the Azure aggregator module (without building plugins)
.\modules\Cirrus\Build-PluginModule.ps1 -CreateAzureModuleOnly -OutputPSModules "C:\PSModules"
```

When building plugins, the script will:

1. Automatically build the Cirrus core module if it doesn't exist
2. Build each `.plugin` file found in the specified directory
3. Create an **Azure aggregator module** that imports all successfully built modules
4. Provide a comprehensive build summary

### Azure Aggregator Module

The Azure aggregator module is automatically created when building multiple plugins. It provides a convenient way to import all Azure service modules with a single import statement.

**Features:**

- **Automatic Creation**: Generated when building multiple plugins
- **RequiredModules**: Automatically includes all successfully built plugin modules and the Cirrus core module
- **Convenience Import**: Import all Azure modules with one command
- **Version**: Currently set to 0.1.0

**Usage:**

```powershell
# Import all Azure modules at once
Import-Module "C:\PSModules\Azure\Azure.psd1"
```

## Module Structure

After building, your PSModules directory will contain:

```
PSModules/
├── Cirrus/                 # Core module
│   ├── Cirrus.psd1
│   ├── Cirrus.psm1
│   ├── Cirrus.dll
│   ├── cirrus_ps.dll
│   └── *.xml              # Help files
├── [PluginName]/          # Individual plugin modules
│   ├── [PluginName].psd1
│   ├── [PluginName].psm1
│   ├── component.plugin
│   └── *.xml              # Help files
└── Azure/                 # Aggregator module
    └── Azure.psd1
```

## Using the Modules

### Import Individual Modules

```powershell
# Import core module
Import-Module "C:\PSModules\Cirrus\Cirrus.psd1"

# Import specific service module
Import-Module "C:\PSModules\Databricks\Databricks.psd1"
```

### Import All Modules

```powershell
# Import all Azure modules at once
Import-Module "C:\PSModules\Azure\Azure.psd1"
```

### Add to PowerShell Module Path

```powershell
# Add to PSModulePath for automatic discovery
$env:PSModulePath += ";C:\PSModules"

# Now you can import by name
Import-Module Azure
Import-Module Cirrus
Import-Module Databricks
```

## Build Script Parameters

### Build-CirrusModule.ps1 Parameters

- `-Release`: Build in Release configuration (default: Debug)
- `-Clean`: Clean previous build artifacts
- `-Test`: Run module tests after building
- `-OutputPSModules`: Target directory for module output (required)
- `-KeepHelpFolder`: Keep help directory after generation (default: false)

### Build-PluginModule.ps1 Parameters

- `-ComponentPath`: Path to single .plugin file (single mode)
- `-ComponentsFolder`: Path to folder containing .plugin files (batch mode)
- `-OutputPSModules`: Target directory for module output (required)
- `-KeepHelpFolder`: Keep help directory after generation (default: false)

## Troubleshooting

### Common Issues

1. **Missing Prerequisites**: Ensure Rust, .NET SDK, and PlatyPS are installed
2. **Build Failures**: Check that all dependencies are available and paths are correct
3. **Help Generation Issues**: Ensure PlatyPS module is installed and up to date
4. **Module Import Issues**: Verify that the OutputPSModules path is correct and accessible

### Debug Mode

Use the `-KeepHelpFolder $true` parameter to preserve help directories for troubleshooting help generation issues.
