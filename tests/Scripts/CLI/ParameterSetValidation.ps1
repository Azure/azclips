# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

[CmdletBinding()]
param([string] $OutputDirectory = (Join-Path $PSScriptRoot '..\..\..\artifacts\parameter-set-validation'))

& (Join-Path $PSScriptRoot '..\ParameterSetValidation.ps1') -Client CLI -OutputDirectory $OutputDirectory
