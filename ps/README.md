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

The Azure aggregator module is automatically created when building plugins or can be created independently. It provides a convenient way to import all Azure service modules with a single import statement.

**Features:**

- **Automatic Creation**: Generated when building plugins or created on-demand
- **Smart Module Detection**: Automatically scans OutputPSModules directory for all valid PowerShell modules
- **Self-Exclusion**: Excludes the Azure module itself from RequiredModules to prevent circular dependencies
- **RequiredModules**: Automatically includes all discovered plugin modules (excluding Azure itself)
- **Convenience Import**: Import all Azure modules with one command
- **Deterministic GUID**: Uses URI-based GUID generation for consistency
- **Version**: Currently set to 0.1.0

**Usage:**

```powershell
# Import all Azure modules at once
Import-Module "C:\PSModules\Azure\Azure.psd1"

# Or if PSModules is in your module path
Import-Module Azure
```

**Standalone Creation:**

The Azure aggregator module can be created or updated independently without building any plugins:

```powershell
# Create/update Azure module by scanning existing modules
.\modules\Cirrus\Build-PluginModule.ps1 -CreateAzureModuleOnly -OutputPSModules "C:\PSModules"
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

**Parameter Sets:**

The script supports three different parameter sets:

1. **SingleComponent**: Build a single plugin module
2. **MultipleComponents**: Build multiple plugin modules from a folder
3. **AzureModuleOnly**: Create only the Azure aggregator module

**Parameters:**

- `-ComponentPath`: Path to single .plugin file (SingleComponent mode)
- `-ComponentsFolder`: Path to folder containing .plugin files (MultipleComponents mode)
- `-CreateAzureModuleOnly`: Create only the Azure aggregator module (AzureModuleOnly mode)
- `-OutputPSModules`: Target directory for module output (required for all modes)
- `-KeepHelpFolder`: Keep help directory after generation (default: false, available in SingleComponent and MultipleComponents modes)

## Troubleshooting

### Common Issues

1. **Missing Prerequisites**: Ensure Rust, .NET SDK, and PlatyPS are installed
2. **Build Failures**: Check that all dependencies are available and paths are correct
3. **Help Generation Issues**: Ensure PlatyPS module is installed and up to date
4. **Module Import Issues**: Verify that the OutputPSModules path is correct and accessible
5. **Azure Module Circular Dependencies**: The Azure aggregator module automatically excludes itself from RequiredModules to prevent circular dependencies

### Debug Mode

Use the `-KeepHelpFolder $true` parameter to preserve help directories for troubleshooting help generation issues.

### Parameter Set Validation

The Build-PluginModule.ps1 script uses PowerShell parameter sets to ensure correct usage:

- Use either `-ComponentPath` OR `-ComponentsFolder` OR `-CreateAzureModuleOnly`
- `-OutputPSModules` is required for all parameter sets
- `-KeepHelpFolder` is only available when building components (not for Azure-only mode)

## Advanced Usage

### Regenerating Azure Aggregator Module

If you add new plugin modules to your OutputPSModules directory, you can regenerate the Azure aggregator module without rebuilding all plugins:

```powershell
# Scan existing modules and update Azure aggregator
.\modules\Cirrus\Build-PluginModule.ps1 -CreateAzureModuleOnly -OutputPSModules "C:\PSModules"
```

### Module Discovery

The Azure aggregator module uses intelligent module discovery:

1. Scans all subdirectories in OutputPSModules
2. Validates that each directory contains a valid .psd1 manifest file
3. Excludes the Azure directory itself to prevent circular dependencies
4. Generates a RequiredModules list with all discovered valid modules

### Batch Processing with Error Handling

When building multiple plugins, the script provides robust error handling:

- Continues processing remaining plugins if one fails
- Provides detailed build summaries
- Still creates Azure aggregator module even if some plugins fail
- Reports success/failure counts for easy troubleshooting
