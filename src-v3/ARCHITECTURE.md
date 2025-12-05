# src-v3 Architecture: Clean Separation of Concerns

## Overview

This implementation follows a clean architecture pattern with **zero duplication** between CLI and PowerShell. Business logic is shared, and only the "translation layers" differ.

## Architecture Diagram

```
┌─────────────────────────────────────────────────────────────┐
│                         CLI Entry                            │
│              azv2 group create --name test                   │
└────────────────────┬────────────────────────────────────────┘
                     │
                     ↓
┌─────────────────────────────────────────────────────────────┐
│                   CLI Command Adapter                        │
│           GroupCreateCommand (Commands/)                     │
│   • Parses options dict: {"name": "test"}                   │
│   • Creates CreateGroupRequest                              │
│   • Formats Console output                                  │
└────────────────────┬────────────────────────────────────────┘
                     │
                     ↓
┌─────────────────────────────────────────────────────────────┐
│                  BUSINESS LOGIC LAYER                        │
│              GroupManager.CreateGroupAsync()                 │
│   • Pure business logic - no CLI/PS knowledge               │
│   • Azure SDK calls                                         │
│   • Returns GroupInfo DTO                                   │
└────────────────────┬────────────────────────────────────────┘
                     ↑
                     │
┌─────────────────────────────────────────────────────────────┐
│              PowerShell Cmdlet Adapter                       │
│        NewAzV2ResourceGroupCmdlet (Cmdlets/)                │
│   • Maps PS parameters to CreateGroupRequest                │
│   • Formats PowerShell output (WriteObject)                 │
└────────────────────┬────────────────────────────────────────┘
                     ↑
                     │
┌─────────────────────────────────────────────────────────────┐
│                    PowerShell Entry                          │
│         New-AzV2ResourceGroup -Name test                    │
└─────────────────────────────────────────────────────────────┘
```

## Folder Structure

### Group Module

```
Group/
├── Business/                           ← Pure business logic
│   ├── GroupManager.cs                ← CreateGroupAsync(), DeleteGroupAsync(), etc.
│   └── Models/                        ← DTOs
│       ├── CreateGroupRequest.cs      ← Input parameters
│       ├── DeleteGroupRequest.cs
│       ├── ListGroupsRequest.cs
│       ├── ShowGroupRequest.cs
│       └── GroupInfo.cs               ← Return data
├── Commands/                          ← CLI adapters (thin wrappers)
│   ├── GroupCreateCommand.cs          ← ICommand: options dict → GroupManager
│   ├── GroupListCommand.cs
│   ├── GroupShowCommand.cs
│   └── GroupDeleteCommand.cs
├── Cmdlets/                           ← PowerShell adapters (thin wrappers)
│   ├── NewAzV2ResourceGroupCmdlet.cs  ← PSCmdlet: PS params → GroupManager
│   ├── GetAzV2ResourceGroupCmdlet.cs
│   ├── SetAzV2ResourceGroupCmdlet.cs
│   └── RemoveAzV2ResourceGroupCmdlet.cs
└── Group/                             ← Original location (module registration)
    └── GroupModule.cs                 ← Returns Commands for CLI discovery
```

### Login Module

```
Login/
├── Business/                           ← Pure authentication logic
│   ├── AuthenticationManager.cs        ← LoginAsync(), Logout(), GetCurrentContext()
│   └── Models/
│       ├── LoginRequest.cs
│       └── AuthenticationInfo.cs
├── Commands/
│   └── LoginCommand.cs                 ← Handles login/logout/show actions
├── Cmdlets/
│   ├── ConnectAzV2AccountCmdlet.cs
│   ├── DisconnectAzV2AccountCmdlet.cs
│   └── GetAzV2ContextCmdlet.cs
└── Login/
    ├── LoginModule.cs
    └── AzureAuthenticationContext.cs   ← Global auth state
```

## Example: Creating a Resource Group

### CLI Path

```bash
azv2 group create --name mygroup --location eastus
```

1. **DefaultCommandEngine** → `CommandBinder` → loads `Group` module
2. **GroupModule.GetCommands()** → returns `GroupCreateCommand`
3. **GroupCreateCommand.ExecuteAsync()**:
   ```csharp
   var request = new CreateGroupRequest {
       Name = options["name"],
       Location = options["location"]
   };
   var result = await GroupManager.CreateGroupAsync(request);
   Console.WriteLine($"Created: {result.Name}");
   ```

### PowerShell Path

```powershell
New-AzV2ResourceGroup -Name mygroup -Location eastus
```

1. **PowerShell** → `NewAzV2ResourceGroupCmdlet.ProcessRecord()`
2. **NewAzV2ResourceGroupCmdlet**:
   ```csharp
   var request = new CreateGroupRequest {
       Name = Name,  // PS parameter
       Location = Location
   };
   var result = await GroupManager.CreateGroupAsync(request);  // SAME CALL
   WriteObject(result);
   ```

## Key Benefits

### ✅ Zero Duplication
- Business logic in `GroupManager` is written **once**
- CLI and PowerShell both call the **exact same methods**
- No code copying or synchronization issues

### ✅ Clear Separation of Concerns
- **Business/**: What to do (Azure SDK operations)
- **Commands/**: How CLI invokes it (options dict → request)
- **Cmdlets/**: How PowerShell invokes it (PS params → request)

### ✅ Easy to Test
- Test `GroupManager` methods directly with unit tests
- Test CLI adapters with mocked GroupManager
- Test PowerShell cmdlets with mocked GroupManager

### ✅ Easy to Extend
Adding a new operation requires:
1. Add method to `GroupManager.cs` (business logic)
2. Create `XxxCommand.cs` (CLI adapter)
3. Create `XxxCmdlet.cs` (PowerShell adapter)

### ✅ Consistent Behavior
Since both paths call the same business logic, CLI and PowerShell behavior is **guaranteed to be identical** (except for output formatting).

## Implementation Details

### Business Layer (GroupManager)

**Responsibilities:**
- Execute Azure SDK operations
- Validate authentication state
- Return strongly-typed DTOs
- **No** console output (returns data only)
- **No** knowledge of CLI or PowerShell

**Example:**
```csharp
public static async Task<GroupInfo> CreateGroupAsync(
    CreateGroupRequest request, 
    CancellationToken cancellationToken)
{
    ValidateAuthentication();
    var armClient = new ArmClient(AzureAuthenticationContext.Credential);
    // ... Azure SDK calls ...
    return MapToGroupInfo(resourceGroup);
}
```

### CLI Adapter (Commands/)

**Responsibilities:**
- Parse CLI options dictionary
- Build business request DTOs
- Call `GroupManager` methods
- Format output for console (Console.WriteLine)
- Handle CLI-specific errors

**Example:**
```csharp
public async Task ExecuteAsync(IDictionary<string, string> options, ...)
{
    var request = new CreateGroupRequest {
        Name = options["name"],
        Location = options["location"]
    };
    var result = await GroupManager.CreateGroupAsync(request);
    Console.WriteLine($"Created: {result.Name}");  // CLI formatting
}
```

### PowerShell Adapter (Cmdlets/)

**Responsibilities:**
- Map PowerShell parameters to request DTOs
- Call `GroupManager` methods
- Format output for PowerShell (WriteObject)
- Handle PowerShell-specific features (ShouldContinue, etc.)

**Example:**
```csharp
protected override void ProcessRecord()
{
    var request = new CreateGroupRequest {
        Name = Name,        // PS parameter
        Location = Location
    };
    var result = GroupManager.CreateGroupAsync(request).GetAwaiter().GetResult();
    WriteObject(result);  // PowerShell formatting
}
```

## Authentication Context

**AzureAuthenticationContext** (in Login module) provides global authentication state:
- `Credential`: TokenCredential for Azure SDK
- `SubscriptionId`: Current subscription
- `TenantId`: Current tenant
- `IsAuthenticated`: Auth status

Both CLI and PowerShell share this context, ensuring consistent authentication state.

## Migration from src-v2

Key changes from `src-v2`:
1. ❌ **Removed**: `CliRunner` process spawning
2. ✅ **Added**: `Business/` folder with pure logic
3. ✅ **Moved**: Commands to `Commands/` folder
4. ✅ **Moved**: Cmdlets to `Cmdlets/` folder
5. ✅ **Refactored**: All adapters now call `GroupManager`/`AuthenticationManager`

## Next Steps

1. Update `.csproj` files to include new folder structure
2. Test CLI path: `azv2 group create`
3. Test PowerShell path: `New-AzV2ResourceGroup`
4. Verify both paths produce identical results
5. Add unit tests for `GroupManager` methods
6. Add integration tests for end-to-end flows
