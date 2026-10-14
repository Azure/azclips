# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.
#
# Creates a resource group through the Azclips PowerShell modules, for use as a setup
# step by other test scripts. Idempotent: New-AzResourceGroup on an existing group is a
# no-op update, so re-running is safe.
#
# Prereqs: build the solution, import src/PS/Resources/Resources.psd1, then sign in.
#
# Usage: ./tests/Scripts/PS/CreateResourceGroup.ps1 <resource-group> [-Location eastus]

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string] $ResourceGroupName,

    [string] $Location = 'eastus',

    [string] $SubscriptionId = 'f758ac53-3e63-4317-a956-0997793808d7'
)

# The official Az.Resources module exports a New-AzResourceGroup with no -SubscriptionId.
# If it is installed, PowerShell auto-loads it and the calls below fail with a confusing
# "parameter cannot be found" error, so import the azclips manifest explicitly and verify
# the cmdlet actually resolves to it.
$manifest = Join-Path $PSScriptRoot '..' '..' '..' 'src' 'PS' 'Resources' 'Resources.psd1'
if (-not (Test-Path $manifest)) {
    throw "Module manifest '$manifest' not found. Build the solution first with dotnet build."
}
Import-Module $manifest -Force -ErrorAction Stop

$resolved = Get-Command New-AzResourceGroup -ErrorAction Stop
if ($resolved.Module.Name -ne 'Resources') {
    throw "New-AzResourceGroup resolves to module '$($resolved.Module.Name)', not the azclips 'Resources' module. Remove or hide the conflicting module and retry."
}

Write-Host "--- Create resource group '$ResourceGroupName' in '$Location' ---"
New-AzResourceGroup `
    -SubscriptionId $SubscriptionId `
    -Name $ResourceGroupName `
    -Location $Location
