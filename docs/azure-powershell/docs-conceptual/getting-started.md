---
title: Get started with Next-Generation Azure PowerShell (Public preview)
description: Install and sign in to Next-Generation Azure PowerShell, verify your active profile, and learn about supported environments, coexistence, and feedback during public preview.
---

# Get started with Next-Generation Azure PowerShell (Public preview)

Next-Generation Azure CLI & Azure PowerShell provides command-line and PowerShell experiences
for managing Azure, with shared authentication, profiles, and configuration. This article helps
you start using the PowerShell public preview. The preview doesn't yet cover every classic cmdlet
or workflow.

## Install and verify the preview

Follow the [installation overview](./install-overview.md) to install the preview modules.
The documented download flow uses an authenticated GitHub CLI (`gh`) session and requires access
to the repository and release assets. GitHub sign-in is separate from Azure sign-in.

Start a fresh PowerShell 7.6 or later session. Check the installed versions, then explicitly import
the preview version reported by the bundled installer. Replace `<preview-module-version>` with
that version:

```powershell
$PSVersionTable.PSVersion
Get-Module Az -ListAvailable | Select-Object Name, Version, ModuleBase
Import-Module Az -RequiredVersion <preview-module-version>
Get-Module Az, Az.Accounts | Select-Object Name, Version, ModuleBase
Get-Command Connect-AzAccount | Select-Object Name, Source, Version
```

> [!IMPORTANT]
> Classic and preview modules use the same `Az` and `Az.*` names. An unversioned
> `Import-Module Az` can load classic modules instead. Choose the preview before importing
> any Az module or running a cmdlet that auto-loads one; start another PowerShell process to
> switch versions. `Remove-Module` doesn't unload binary assemblies.

## Authentication

For an initial interactive sign-in, specify an Azure subscription that you can access.
Replace the placeholders with your tenant and subscription IDs:

```powershell
Connect-AzAccount -TenantId <tenant-id> -SubscriptionId <subscription-id>
Get-AzProfile -Active
Get-AzResourceGroup
```

`Get-AzResourceGroup` is a read-only check; an empty result is valid if the subscription has no
resource groups. If you installed individual modules instead of the roll-up, install the preview
`Az.Resources` module from the same bundle to use this command.

If a browser isn't available, use device code sign-in:

```powershell
Connect-AzAccount -TenantId <tenant-id> -SubscriptionId <subscription-id> -UseDeviceCode
```

The preview implements the following authentication methods:

| Method | PowerShell parameters |
| --- | --- |
| Interactive user | `Connect-AzAccount` with `-SubscriptionId`; optionally specify `-TenantId`. |
| Device code | Add `-UseDeviceCode` to user sign-in. |
| Managed identity | `-Identity`; select a user-assigned identity with `-ClientId`, `-ObjectId`, or `-ResourceId`. Requires an environment that exposes the identity. |
| Service principal with a client secret | `-ClientId`, `-ClientSecret` (a `SecureString`), `-TenantId`, and `-SubscriptionId`; alternatively use `-ServicePrincipal -Credential` with a `PSCredential` whose username is the application ID. |
| Service principal with workload identity federation (OIDC) | `-ServicePrincipal`, `-FederatedToken`, `-ClientId`, `-TenantId`, and `-SubscriptionId`. Requires a configured federated credential. |

Use `Get-Command Connect-AzAccount -Syntax` for the parameters in your installed build. Don't assume
every classic authentication option or combination is available. For client-secret examples, see
[manage sign-in profiles](./profile-base-login.md#sign-in-with-a-service-principal-client-secret).

### Shared profiles and configuration

Sign-in saves and activates a profile. Without `-OutProfile`, sign-in uses the reserved
`default` profile. To keep multiple contexts, see [manage sign-in profiles](./profile-base-login.md).

The two preview surfaces share profiles and authentication state when run as the same operating
system user with the same state directory. This isn't shared sign-in with classic Azure PowerShell
or classic Azure CLI; sign in to the preview separately.

By default, configuration (`config.json`), profiles (`profiles.json`), the active profile pointer,
and authentication cache files use `~/.azclips` (`$HOME\.azclips` on Windows). The environment
variable `AZCLIPS_HOME` overrides this root. See [configuration management](./config.md) for
settings and precedence.

## Coexistence with classic tools

Preview and classic modules can be installed side by side at different versions, but you must
choose which version loads in each PowerShell process. Existing scripts that import modules
without a version constraint can select a different version than intended.

See [switch between classic and preview modules](./switch-azps-versions.md) for version selection
and clean-session guidance. Installing the modules doesn't replace your CLI executable.

## Supported environments

The preview modules require PowerShell 7.6 or later, matching the module manifests and bundled
installer.

### Supported operating systems

- Any operating system supported by PowerShell 7.6 or later

### Supported shells

- PowerShell 7.6 or later

Check the active shell version with `$PSVersionTable.PSVersion`.

### Unsupported environments

The following environments aren't supported:

- PowerShell versions earlier than 7.6
- Windows PowerShell 5.1
- Windows PowerShell Integrated Scripting Environment (ISE)

## Automation support

Service principal and managed identity sign-in can be used for non-interactive scenarios when
their credentials or identity environment are available. Pin the module version and confirm
that the preview supports the cmdlets, parameters, and output objects your script needs.
Federated tokens are short-lived and must be refreshed by the issuing identity provider.

## Logging issues and providing feedback

Your feedback helps improve the public preview.

If you encounter an issue:

1. Verify that you're using a supported operating system and PowerShell 7.6 or later.
1. Reproduce the issue, if possible.
1. Collect any relevant error messages or command output.
1. Search [existing issues][01], then use the appropriate bug report, feature request, or feedback
   template if you have repository access.

When reporting an issue, include:

- Operating system
- PowerShell version
- Next-Generation Azure PowerShell module versions and paths
  (`Get-Module Az, Az.Accounts | Select-Object Name, Version, ModuleBase`)
- Classic Azure PowerShell or Azure CLI version, if relevant to a coexistence problem
- Steps to reproduce the problem
- Expected behavior
- Actual behavior
- Any relevant error messages

Remove secrets, tokens, usernames, subscription IDs, tenant IDs, and other sensitive information
from commands, output, logs, and screenshots before submitting. If you can't access the repository
or issue templates, the GitHub feedback route isn't available to your account.

## Known preview limitations

During the public preview, you should expect some limitations:

- Not all Azure PowerShell and Azure CLI scenarios are available.
- Classic and preview binary modules can't be switched within a running PowerShell process.
- Feature availability may change between preview releases.

<!-- link references -->

[01]: https://github.com/Azure/azclips/issues
