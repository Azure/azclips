# Changes: Extension Framework & Login Refactor

## Overview
Refactored login functionality into an extension-based architecture with improved service integration.

## Key Changes

### Extension Framework
- **New extension structure**: Created `src/Extensions/Login/` with modular architecture
  - `ExtensionIndex.cs`: Command metadata registry for PowerShell and CLI clients
  - `Extensions.Resources.csproj`: Extension project configuration
  - `ResourcesClient.cs`: HTTP client for Azure Resource Manager API operations
  - `ResourcegroupModel.cs`: Resource group data model

### Login Command Migration
- Moved `LoginCommand.cs` from `src/Login/Commands/` to `src/Extensions/Login/`
- Integrated with DI container via `Bootstrapper.ServiceProviderAsync`
- Uses `IAuthService` for authentication operations
- Returns context information after login

### Core Improvements
- **CLI Integration** (`Program.cs`):
  - Bootstrapper initialization for DI container
  - `ILoggerService` usage for consistent output
  - `ExtensionDispatcher` instantiation moved outside try block for reuse

- **Authentication** (`MsalAuthService.cs`):
  - Changed token cache registration flag from `false` to `true` in `AcquireTokenInteractiveAsync`

## Files Changed
- 8 files: 323 insertions, 4 deletions
- New files: 5
- Modified files: 2
- Moved files: 1
