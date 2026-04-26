# Auth Context — Baseline Specification

> **Status**: Baseline (current-state documentation)  
> **Scope**: Documents the existing auth context behavior as of this writing — no proposed changes.  
> **RFC 2119**: Key words MUST, SHALL, SHOULD, MAY are used per [RFC 2119](https://www.rfc-editor.org/rfc/rfc2119).

---

## 1. Overview

The authentication context system manages a single persisted identity (tenant + subscription) that all CLI and PowerShell commands use for authorization. There is exactly one active context at a time, stored as a JSON file on disk.

### 1.1 Components

| Component | Type | Location |
|---|---|---|
| `AuthContext` | Record | `Core.Abstractions.AuthContext` |
| `IAuthContextService` | Interface | `Core.Abstractions.IAuthContextService` |
| `AuthContextService` | Singleton service | `Core.Authentication.AuthContextService` |
| `LoginCommand` | IProcessor | `Microsoft.Azure.Cirrus.Extensions.Login.Commands.LoginCommand` |
| `LogoutCommand` | IProcessor | `Microsoft.Azure.Cirrus.Extensions.Login.Commands.LogoutCommand` |
| `GetContextCommand` | IProcessor | `Microsoft.Azure.Cirrus.Extensions.Login.Commands.GetContextCommand` |
| `CommandAuthHandler` | HTTP pipeline handler | `Core.Runtime.Handler.CommandAuthHandler` |
| `Engine` | PowerShell/CLI engine | `Core.Engine.Engine` |
| `ExtensionIndex` (Login) | Command registration | `Microsoft.Azure.Cirrus.Extensions.Login.ExtensionIndex` |

### 1.2 Data Model

```csharp
public record AuthContext(string TenantId, string SubscriptionId);
```

The `AuthContext` record holds exactly two fields:
- **TenantId** — Microsoft Entra (Azure AD) tenant identifier
- **SubscriptionId** — Azure subscription identifier

There is no concept of profiles, named contexts, environment, resource group defaults, or authenticated user metadata.

### 1.3 Persistence

- **Directory**: `{Environment.SpecialFolder.LocalApplicationData}/.cirrus/`
- **File**: `context.json`
- **Platform paths**:
  - macOS: `~/Library/Application Support/.cirrus/context.json`
  - Linux: `~/.local/share/.cirrus/context.json`
  - Windows: `%LOCALAPPDATA%\.cirrus\context.json`
- **Format**: Indented JSON, case-insensitive deserialization

Example `context.json`:
```json
{
  "TenantId": "72f988bf-86f1-41af-91ab-2d7cd011db47",
  "SubscriptionId": "12345678-1234-1234-1234-123456789abc"
}
```

### 1.4 DI Registration

`AuthContextService` is registered as a **Singleton** via `RegisterAuthenticationServices()` in `Core.Authentication.DIRegisterExtensions`. Both CLI and PowerShell share the same singleton instance for the lifetime of the process (CLI) or module session (PowerShell).

---

## 2. Command Surface

### 2.1 CLI Commands

| CLI Syntax | Extension | Command Key | Maps To |
|---|---|---|---|
| `azv2 login --subscription-id <id> [--tenant-id <id>] [--use-device-code]` | Login | `""` (empty) | `LoginCommand` |
| `azv2 login logout` | Login | `"logout"` | `LogoutCommand` |
| `azv2 login show` | Login | `"show"` | `GetContextCommand` |

CLI parameters use kebab-case (`--subscription-id`, `--tenant-id`, `--use-device-code`).

### 2.2 PowerShell Cmdlets

| PowerShell Syntax | Extension | Command Key | Maps To |
|---|---|---|---|
| `Connect-CirrusAccount -SubscriptionId <id> [-TenantId <id>] [-UseDeviceCode]` | Login | `Connect-CirrusAccount` | `LoginCommand` |
| `Disconnect-CirrusAccount` | Login | `Disconnect-CirrusAccount` | `LogoutCommand` |
| `Get-CirrusAccount` | Login | `Get-CirrusAccount` | `GetContextCommand` |

PowerShell parameters use PascalCase and are defined in the `.ps1` wrapper functions.

---

## 3. Requirements (Current Behavior)

### REQ-CTX-01: Single Context Model
**Priority**: P0  
**Rationale**: The system was designed for a single active identity. All commands share one context.

The system MUST maintain at most one `AuthContext` at any time. There is no support for multiple named contexts, profiles, or context stacking.

### REQ-CTX-02: Context Persistence
**Priority**: P0  
**Rationale**: Context must survive across CLI invocations and PowerShell sessions.

The `AuthContextService` MUST persist `AuthContext` to `{LocalApplicationData}/.cirrus/context.json` as indented JSON. The service MUST create the directory if it does not exist.

### REQ-CTX-03: Context Loading
**Priority**: P0  
**Rationale**: Every command needs auth context to acquire tokens.

On every call to `GetCurrentContextAsync()`, the service MUST reload the context from disk. There is no durable in-memory cache across calls (the `_currentContext` field is overwritten each time).

### REQ-CTX-04: Context Clearing
**Priority**: P0  
**Rationale**: Logout must fully remove persisted state.

`ClearContextAsync()` MUST set the in-memory context to `null` AND delete `context.json` from disk if it exists.

### REQ-CTX-05: Empty Context Default
**Priority**: P0  
**Rationale**: Commands must not fail during context construction when no login has occurred.

When no persisted context exists, `Engine.CreateCommandContext()` MUST default to `new AuthContext(TenantId: "", SubscriptionId: "")`. Commands will proceed with empty strings; token acquisition may fail downstream.

### REQ-CTX-06: Context Isolation per Invocation
**Priority**: P1  
**Rationale**: Defensive copy prevents mutation across commands.

`Engine.CreateCommandContext()` MUST create a defensive copy of the loaded `AuthContext` using `authContext with { }` before assigning it to the `CommandAuthHandler`.

### REQ-CTX-07: Service Locator Access in Commands
**Priority**: P1  
**Rationale**: Commands use `CirrusServiceProvider` (service locator) to resolve services at execution time rather than constructor injection.

Login, Logout, and GetContext commands MUST resolve `IAuthContextService` via `CirrusServiceProvider.GetRequiredService<IAuthContextService>()` during `ProcessAsync()`.

---

## 4. Scenarios (Given/When/Then)

### 4.1 Login

#### Scenario: CTX-LOGIN-01 — Successful interactive login
**Requirement**: REQ-CTX-02  
**Test mapping**: `LoginCommand_ShouldPersistContext_WhenInteractiveLoginSucceeds`

```gherkin
Given no context.json exists on disk
  And the user has valid Azure credentials
When the user runs `azv2 login --subscription-id <sub-id> --tenant-id <tenant-id>`
Then LoginCommand MUST call IAuthService.LoginAsync with the provided tenant and subscription
  And LoginCommand MUST create an AuthContext with TenantId from the AuthResult and SubscriptionId from input
  And LoginCommand MUST call IAuthContextService.SaveContextAsync with the new AuthContext
  And context.json MUST be written to {LocalApplicationData}/.cirrus/context.json
  And the AuthContext MUST be written to the command output
```

#### Scenario: CTX-LOGIN-02 — Device code login
**Requirement**: REQ-CTX-02  
**Test mapping**: `LoginCommand_ShouldPersistContext_WhenDeviceCodeLoginSucceeds`

```gherkin
Given no context.json exists on disk
  And the user has valid Azure credentials
When the user runs `azv2 login --subscription-id <sub-id> --use-device-code true`
Then LoginCommand MUST call IAuthService.LoginAsync with UseDeviceCodeFlow = true
  And the MSAL device code message MUST be displayed to the user
  And upon successful authentication, context.json MUST be written
```

#### Scenario: CTX-LOGIN-03 — Login overwrites existing context
**Requirement**: REQ-CTX-02  
**Test mapping**: `LoginCommand_ShouldOverwriteContext_WhenContextAlreadyExists`

```gherkin
Given context.json exists with TenantId "old-tenant" and SubscriptionId "old-sub"
When the user runs `azv2 login --subscription-id new-sub --tenant-id new-tenant`
Then context.json MUST be overwritten with the new TenantId and SubscriptionId
  And the old context MUST NOT be preserved anywhere
```

#### Scenario: CTX-LOGIN-04 — Login with TenantId omitted
**Requirement**: REQ-CTX-02  
**Test mapping**: `LoginCommand_ShouldUseMsalTenant_WhenTenantIdOmitted`

```gherkin
Given the user does not provide --tenant-id
When the user runs `azv2 login --subscription-id <sub-id>`
Then LoginCommand MUST pass an empty string as TenantId to LoginRequest
  And MsalAuthService MUST fall back to the hardcoded default tenant "72f988bf-86f1-41af-91ab-2d7cd011db47"
  And the AuthContext TenantId MUST be set from AuthResult.TenantId (resolved by MSAL)
```

#### Scenario: CTX-LOGIN-05 — PowerShell Connect-CirrusAccount
**Requirement**: REQ-CTX-02  
**Test mapping**: `ConnectCirrusAccount_ShouldPersistContext_WhenLoginSucceeds`

```gherkin
Given the Cirrus.Login PowerShell module is imported
When the user runs `Connect-CirrusAccount -SubscriptionId <sub-id> -TenantId <tenant-id>`
Then the PS wrapper MUST construct a CommandInvocation for extension "Login", command "Connect-CirrusAccount"
  And Engine.Process MUST execute synchronously on the PowerShell runspace thread
  And the LoginCommand MUST persist context identically to the CLI path
  And output MUST be written via $PSCmdlet.WriteObject on the runspace thread
```

### 4.2 Logout

#### Scenario: CTX-LOGOUT-01 — Successful logout
**Requirement**: REQ-CTX-04  
**Test mapping**: `LogoutCommand_ShouldClearContext_WhenLoggedIn`

```gherkin
Given context.json exists with a valid TenantId and SubscriptionId
  And the MSAL token cache contains cached tokens
When the user runs `azv2 login logout`
Then LogoutCommand MUST call IAuthService.LogoutAsync to clear the MSAL token cache
  And LogoutCommand MUST call IAuthContextService.ClearContextAsync
  And context.json MUST be deleted from disk
  And the in-memory _currentContext MUST be set to null
  And the command output MUST be boolean true
```

#### Scenario: CTX-LOGOUT-02 — Logout when not logged in
**Requirement**: REQ-CTX-04  
**Test mapping**: `LogoutCommand_ShouldSucceed_WhenNoContextExists`

```gherkin
Given no context.json exists on disk
When the user runs `azv2 login logout`
Then LogoutCommand MUST call IAuthService.LogoutAsync (clears any stale MSAL cache)
  And ClearContextAsync MUST succeed without error (file deletion is conditional)
  And the command output MUST be boolean true
```

#### Scenario: CTX-LOGOUT-03 — PowerShell Disconnect-CirrusAccount
**Requirement**: REQ-CTX-04  
**Test mapping**: `DisconnectCirrusAccount_ShouldClearContext_WhenLoggedIn`

```gherkin
Given the Cirrus.Login PowerShell module is imported
  And context.json exists with valid context
When the user runs `Disconnect-CirrusAccount`
Then the PS wrapper MUST call Engine.Process with a "Disconnect-CirrusAccount" invocation
  And ShouldProcess confirmation MUST be evaluated (ConfirmImpact = Medium)
  And the LogoutCommand MUST execute identically to the CLI path
```

### 4.3 Get Context

#### Scenario: CTX-GET-01 — Retrieve existing context
**Requirement**: REQ-CTX-03  
**Test mapping**: `GetContextCommand_ShouldReturnContext_WhenContextExists`

```gherkin
Given context.json exists with TenantId "t1" and SubscriptionId "s1"
When the user runs `azv2 login show`
Then GetContextCommand MUST call IAuthContextService.GetCurrentContextAsync
  And the service MUST read context.json from disk (not from cache)
  And the command output MUST contain TenantId "t1" and SubscriptionId "s1"
```

#### Scenario: CTX-GET-02 — Retrieve context when none exists
**Requirement**: REQ-CTX-05  
**Test mapping**: `GetContextCommand_ShouldThrowOrReturnNull_WhenNoContextExists`

```gherkin
Given no context.json exists on disk
When the user runs `azv2 login show`
Then GetCurrentContextAsync MUST return null
  And GetContextCommand uses null-forgiving operator (!) — behavior is undefined
  And a NullReferenceException MAY be thrown when calling WriteOutput
```

#### Scenario: CTX-GET-03 — PowerShell Get-CirrusAccount
**Requirement**: REQ-CTX-03  
**Test mapping**: `GetCirrusAccount_ShouldReturnContext_WhenContextExists`

```gherkin
Given the Cirrus.Login PowerShell module is imported
  And context.json exists with valid context
When the user runs `Get-CirrusAccount`
Then the PS wrapper MUST call Engine.Process with a "Get-CirrusAccount" invocation
  And the GetContextCommand MUST execute identically to the CLI path
  And the AuthContext MUST be written to the pipeline via $PSCmdlet.WriteObject
```

### 4.4 Command Execution (Auth Context Injection)

#### Scenario: CTX-EXEC-01 — Context injected into command pipeline
**Requirement**: REQ-CTX-03, REQ-CTX-06  
**Test mapping**: `Engine_ShouldInjectAuthContext_WhenProcessingCommand`

```gherkin
Given context.json exists with TenantId "t1" and SubscriptionId "s1"
When any command is executed via Engine.Process
Then Engine.CreateCommandContext MUST call authContextService.GetCurrentContextAsync()
  And the loaded AuthContext MUST be defensively copied (authContext with { })
  And the copy MUST be assigned to CommandAuthHandler.AuthContext
  And CommandAuthHandler.AcquireTokenAsyncDefault MUST be bound to IAuthService.AcquireTokenSilentlyAsync
```

#### Scenario: CTX-EXEC-02 — Bearer token uses context TenantId
**Requirement**: REQ-CTX-03  
**Test mapping**: `CommandAuthHandler_ShouldUseContextTenantId_WhenAcquiringToken`

```gherkin
Given a CommandContext has been created with AuthContext.TenantId = "t1"
When CommandAuthHandler.SendAsync processes an HTTP request
Then it MUST call AcquireTokenAsyncDelegate with tenantId = "t1"
  And it MUST set the Authorization header to "Bearer <access-token>"
  And it MUST pass the request to the next handler in the pipeline
```

#### Scenario: CTX-EXEC-03 — No context on disk defaults to empty
**Requirement**: REQ-CTX-05  
**Test mapping**: `Engine_ShouldUseEmptyDefaults_WhenNoContextExists`

```gherkin
Given no context.json exists on disk
When any command is executed via Engine.Process
Then GetCurrentContextAsync MUST return null
  And Engine MUST substitute AuthContext(TenantId: "", SubscriptionId: "")
  And the command MUST proceed with empty auth context
  And token acquisition MAY fail with MsalUiRequiredException downstream
```

#### Scenario: CTX-EXEC-04 — PowerShell thread-affinity preserved
**Requirement**: REQ-CTX-06  
**Test mapping**: `Engine_ShouldPumpOutputOnCallerThread_WhenProcessingPSCommand`

```gherkin
Given a PowerShell cmdlet invokes Engine.Process on the runspace thread
When the engine creates the CommandContext and executes the command
Then CreateCommandContext MUST execute synchronously (sync-over-async) on the caller's thread
  And command output MUST be pumped via Channel<T> back to the caller's thread
  And the outputDelegate ($PSCmdlet.WriteObject) MUST execute on the original runspace thread
```

### 4.5 Persistence Edge Cases

#### Scenario: CTX-PERSIST-01 — Concurrent CLI invocations
**Requirement**: REQ-CTX-02, REQ-CTX-03  
**Test mapping**: `AuthContextService_ShouldHandleConcurrentAccess_WhenMultipleProcesses`

```gherkin
Given two CLI processes execute simultaneously
When both call SaveContextAsync with different AuthContext values
Then the last write wins (no file locking or conflict resolution)
  And the resulting context.json MAY contain either context
  And no exception SHOULD be thrown (OS file write is atomic for small files)
```

#### Scenario: CTX-PERSIST-02 — Corrupt context.json
**Requirement**: REQ-CTX-03  
**Test mapping**: `AuthContextService_ShouldThrow_WhenContextFileCorrupted`

```gherkin
Given context.json contains malformed JSON
When GetCurrentContextAsync is called
Then JsonSerializer.Deserialize MUST throw a JsonException
  And AuthContextService MUST wrap it in a generic Exception("Failed to load authentication context from file.")
  And the exception MUST propagate to the caller
```

#### Scenario: CTX-PERSIST-03 — Context directory does not exist
**Requirement**: REQ-CTX-02  
**Test mapping**: `AuthContextService_ShouldCreateDirectory_WhenSaving`

```gherkin
Given the .cirrus directory does not exist under LocalApplicationData
When SaveContextAsync is called
Then the service MUST call Directory.CreateDirectory to create the path
  And context.json MUST be written successfully
```

### 4.6 Service Registration

#### Scenario: CTX-DI-01 — Singleton registration
**Requirement**: REQ-CTX-07  
**Test mapping**: `Bootstrapper_ShouldRegisterAuthContextServiceAsSingleton`

```gherkin
Given the application starts (CLI or PowerShell)
When the DI container is built
Then IAuthContextService MUST be registered as a Singleton
  And the same instance MUST be returned for all resolutions within the process lifetime
```

#### Scenario: CTX-DI-02 — CirrusServiceProvider initialization
**Requirement**: REQ-CTX-07  
**Test mapping**: `CirrusServiceProvider_ShouldResolveAuthContextService_WhenInitialized`

```gherkin
Given CirrusServiceProvider.Initialize has been called with a ClientType
When a command calls CirrusServiceProvider.GetRequiredService<IAuthContextService>()
Then the same AuthContextService singleton MUST be returned
  And it MUST have ILoggingService and ITelemetryService injected via primary constructor
```

---

## 5. Known Limitations & TODOs

These are documented observations from the current codebase, not proposed changes.

| ID | Description | Source |
|---|---|---|
| TODO-01 | `AuthContextService` reloads from disk on every `GetCurrentContextAsync` call — no proper cache management, synchronization, or multi-account support. | `AuthContextService.cs` inline comment |
| TODO-02 | `AuthContextService` has no file system abstraction — direct `File.*` calls make unit testing difficult. | `AuthContextService.cs` inline comment |
| TODO-03 | `GetContextCommand` uses null-forgiving operator (`!`) on the result of `GetCurrentContextAsync()` — will throw `NullReferenceException` if no context exists. | `GetContext.cs` |
| TODO-04 | `MsalAuthService.BuildApp` falls back to a hardcoded Microsoft corporate tenant ID when no tenant is provided. | `MsalAuthService.cs` inline comment |
| TODO-05 | Token cache uses `plaintextUnsafe: true` in all code paths — encrypted keyring/keychain storage is defined but not active. | `MsalAuthService.cs` |
| TODO-06 | No support for multiple accounts, profiles, or named contexts. The system is strictly single-context. | Architecture constraint |
| TODO-07 | No file locking or conflict resolution for concurrent access to `context.json`. | `AuthContextService.cs` |
| TODO-08 | `ClearContextAsync` uses synchronous `File.Delete` despite the async method signature. | `AuthContextService.cs` |

---

## 6. Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│                        User Invocation                          │
│  CLI: azv2 login --subscription-id ...                          │
│  PS:  Connect-CirrusAccount -SubscriptionId ...                 │
└────────────────────────┬────────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────────┐
│                     Engine.Process()                             │
│  1. CreateCommandContext()                                       │
│     ├── authContextService.GetCurrentContextAsync()              │
│     │   └── Reads {LocalAppData}/.cirrus/context.json            │
│     ├── Defaults to AuthContext("", "") if null                  │
│     ├── Defensive copy: authContext with { }                     │
│     └── Binds to CommandAuthHandler.AuthContext                  │
│  2. GetCommand(invocation) → IProcessor instance                 │
│  3. instance.Init(commandContext)                                │
│  4. instance.ProcessAsync()                                      │
│  5. Channel<T> pump → outputDelegate (CLI: Console / PS: Write)  │
└─────────────────────────────────────────────────────────────────┘
                         │
          ┌──────────────┼──────────────┐
          ▼              ▼              ▼
   ┌────────────┐ ┌────────────┐ ┌────────────┐
   │ LoginCmd   │ │ LogoutCmd  │ │ GetCtxCmd  │
   │            │ │            │ │            │
   │ Resolves:  │ │ Resolves:  │ │ Resolves:  │
   │ IAuthSvc   │ │ IAuthSvc   │ │ IAuthCtxSvc│
   │ IAuthCtxSvc│ │ IAuthCtxSvc│ │            │
   │            │ │            │ │ Reads ctx  │
   │ Login→Save │ │ Logout→Clr │ │ from disk  │
   │ context    │ │ context    │ │            │
   └──────┬─────┘ └──────┬─────┘ └──────┬─────┘
          │              │              │
          ▼              ▼              ▼
   ┌─────────────────────────────────────────┐
   │         context.json on disk            │
   │  {LocalAppData}/.cirrus/context.json    │
   │  { "TenantId": "...",                   │
   │    "SubscriptionId": "..." }            │
   └─────────────────────────────────────────┘
```

---

## 7. File Inventory

All source files that participate in the current auth context system:

| File | Role |
|---|---|
| `src/Core/Core.Abstractions/Models/AuthContext.cs` | Data model (record) |
| `src/Core/Core.Abstractions/IAuthContextService.cs` | Service contract |
| `src/Core/Core.Abstractions/IAuthService.cs` | Auth service contract |
| `src/Core/Core.Abstractions/Runtime/ICommandContext.cs` | Per-invocation context interface |
| `src/Core/Core.Abstractions/Runtime/Handler/ICommandAuthHandler.cs` | Auth handler interface |
| `src/Core/Core.Authentication/AuthContextService.cs` | Persistence implementation |
| `src/Core/Core.Authentication/MsalAuthService.cs` | MSAL authentication |
| `src/Core/Core.Authentication/DIRegisterExtensions.cs` | DI registration |
| `src/Core/Core.Runtime/CommandContext.cs` | Per-invocation context impl |
| `src/Core/Core.Runtime/Handler/CommandAuthHandler.cs` | Bearer token injection |
| `src/Core/Core.Engine/Engine.cs` | Engine with CreateCommandContext |
| `src/Core/Core.Engine/Bootstrapper.cs` | Service registration |
| `src/Extensions/Login/Commands/LoginCommandBase.cs` | Base class for login commands |
| `src/Extensions/Login/Commands/Login.cs` | Login command |
| `src/Extensions/Login/Commands/Logout.cs` | Logout command |
| `src/Extensions/Login/Commands/GetContext.cs` | Get context command |
| `src/Extensions/Login/ExtensionIndex.cs` | CLI/PS command registration |
| `src/PS/Login/Login.psm1` | PowerShell module loader |
| `src/PS/Login/Login.psd1` | PowerShell module manifest |
| `src/PS/Login/exports/Connect-CirrusAccount.ps1` | PS login wrapper |
| `src/PS/Login/exports/Disconnect-CirrusAccount.ps1` | PS logout wrapper |
| `src/PS/Login/exports/Get-CirrusAccount.ps1` | PS get-context wrapper |
| `tests/Common/Stubs/StubAuthContextService.cs` | Test stub |
| `docs/spec/auth-context.md` | Design specification |
| `wiki/02-deep-dive/authentication.md` | Wiki documentation |
