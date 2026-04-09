// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Core.Abstractions
{
    public enum ClientType
    {
        PowerShell,
        CLI
    }

    public static class ClientTypeExtensions
    {
        public static string ToServiceName(this ClientType clientType) => clientType switch
        {
            ClientType.CLI => "AzureCLI",
            ClientType.PowerShell => "AzurePS",
            _ => "AzureCLI"
        };
    }
}
