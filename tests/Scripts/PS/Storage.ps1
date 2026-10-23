# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

[CmdletBinding()]
param(
    [Parameter(Mandatory)][guid]$TenantId,
    [Parameter(Mandatory)][guid]$SubscriptionId,
    [Parameter(Mandatory)][string]$Profile,
    [string]$Location = 'australiaeast',
    [string]$ProvisioningCli = 'az',
    [switch]$AllowLive
)

& (Join-Path $PSScriptRoot '..\StorageSmoke.ps1') -Surface PowerShell @PSBoundParameters
