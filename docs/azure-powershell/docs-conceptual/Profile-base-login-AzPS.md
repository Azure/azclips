---
title: Manage sign-in profiles in Azure PowerShell (Private preview)
description: Learn how to use named profiles in Azure PowerShell to save sign-in context, switch between accounts and subscriptions, and re-authenticate a specific profile.
ms.topic: conceptual
---

# Manage sign-in profiles in Azure PowerShell (Private preview)

A *profile* is a named sign-in context that Azure PowerShell saves when you authenticate. Each
profile remembers who you signed in as and which tenant and subscription to use, so you can switch
between different accounts, tenants, and subscriptions without signing in again every time. Because
each profile has a memorable name, it's easy to recall and tell environments apart at a glance—for
example a development versus a production environment—instead of memorizing tenant and subscription
IDs. Profiles also conveniently store the basic information needed to sign in, so you don't have to
re-enter that context on every command.

Profiles are useful when you regularly work across more than one context, for example a personal
subscription and a corporate subscription, or a development tenant and a production tenant.

## What a profile stores

When you sign in and save a profile, Azure PowerShell records the connection context for that
profile, including:

- The profile name
- The Azure tenant (directory)
- The Azure subscription
- The cloud environment
- The sign-in identity (the signed-in user, when available)
- The authentication method used to sign in

Profiles are stored locally on your machine. Switching profiles changes which of these saved
contexts Azure PowerShell uses for subsequent commands.

## Sign in and save a profile

Use `Connect-AzAccount` with the `-OutProfile` parameter to sign in and save the result as a named
profile. Specify the tenant and subscription you want the profile to use:

```powershell
Connect-AzAccount -TenantId <tenant-id> -SubscriptionId <subscription-id> -OutProfile personal
```

To create a second profile for a different account, tenant, or subscription, sign in again with a
different profile name:

```powershell
Connect-AzAccount -TenantId <tenant-id> -SubscriptionId <subscription-id> -OutProfile corp
```

The profile that you sign in to most recently becomes the active profile.

If a browser isn't available (for example, over SSH or in a container), add `-UseDeviceCode` to
complete sign-in with the device code flow:

```powershell
Connect-AzAccount -TenantId <tenant-id> -SubscriptionId <subscription-id> -OutProfile corp -UseDeviceCode
```

> [!NOTE]
> If you run `Connect-AzAccount` without `-OutProfile`, Azure PowerShell signs you in without
> creating a named profile. A temporary profile is saved so that behavior stays compatible with the
> existing Azure PowerShell. To keep a reusable, named context, always pass `-OutProfile`.

### Profile naming rules

A profile name must:

- Be 1–100 characters long.
- Contain only letters, digits, hyphens (`-`), underscores (`_`), or periods (`.`).

The name `temp` is reserved for sign-in without a named profile and can't be used as a profile name.

## List your profiles

To see all saved profiles, use `Get-AzProfile`:

```powershell
Get-AzProfile
```

## Show a profile

To view the details of a specific profile, use `Get-AzProfile` with the `-Name` parameter:

```powershell
Get-AzProfile -Name personal
```

To view the currently active profile, use the `-Active` parameter:

```powershell
Get-AzProfile -Active
```

## Switch the active profile

Use `Set-AzProfile` to change which profile is active. After you switch, commands that rely on the
active context use the newly activated profile:

```powershell
Set-AzProfile -Name personal
Get-AzResourceGroup
```

## Re-authenticate an existing profile

If a profile's sign-in has expired, sign in again and pass the existing profile name to `-Profile`
to refresh it in place:

```powershell
Connect-AzAccount -Profile personal
```

The `-Profile` and `-OutProfile` parameters are mutually exclusive: use `-OutProfile` to create or
overwrite a profile, and `-Profile` to re-authenticate an existing one.

## Remove a profile

To delete a profile you no longer need, use `Remove-AzProfile`:

```powershell
Remove-AzProfile -Name corp
```

If you remove the active profile, no profile is active until you set one with `Set-AzProfile`.

## Sign out

To sign out of the active profile and clear its account from the token cache, use
`Disconnect-AzAccount`:

```powershell
Disconnect-AzAccount
```

## Customizing Prompt to Display Profile Name

Configure your shell prompt to display the active profile name. Azure PowerShell writes the active
profile name to an `active-profile` file in its local application data directory
(`{LocalApplicationData}/.azclips/active-profile`).

### PowerShell

Run `code $PROFILE` to open your PowerShell profile and add the following code snippet:

```powershell
function prompt {
    $activeProfileFile = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) '.azclips/active-profile'
    $activeProfile = if (Test-Path $activeProfileFile) { (Get-Content $activeProfileFile -Raw).Trim() } else { '' }
    $suffix = if ($activeProfile) { " @$activeProfile" } else { '' }
    "PS $($ExecutionContext.SessionState.Path.CurrentLocation)$suffix$('>' * ($NestedPromptLevel + 1)) "
}
```

### zsh + starship

Add this to your `~/.config/starship.toml`

```toml
[custom.profile]
command = 'cat ~/Library/Application\ Support/.azclips/active-profile'
symbol = '@'
when = true
description = 'Name of the active profile of Azure Client Tools'
```

> [!NOTE]
> The location of the `active-profile` file depends on your operating system's local application
> data directory. If the paths above don't match your environment, run `Get-AzProfile -Active` to
> confirm the active profile.
