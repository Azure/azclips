using System;
using System.Text;

namespace Cirrus.Core;

/// <summary>
/// Provides functionality for generating PowerShell function scripts from cmdlet information
/// </summary>
public static class PSCmdlet
{
    /// <summary>
    /// Template for generating PowerShell function script
    /// </summary>
    private const string FunctionTemplate = @"function {0} {{
    <#
    .SYNOPSIS
    {1}
    
    .DESCRIPTION
    {2}
    
    .PARAMETER Parameters
    Parameters to pass to the cmdlet (as hashtable)
    
    .EXAMPLE
    {0} -Parameters @{{key=""value""}}
    
    .NOTES
    Auto-generated cmdlet from Azure Cirrus
    URI: {3}
    #>
    [CmdletBinding()]
    param(
        [Parameter()]
        [hashtable]$Parameters = @{{}}
    )
    
    Process {{
                 try {{
             Write-Verbose ""Executing cmdlet: {0}""
             Write-Verbose ""URI: {3}""
             
             # Convert hashtable to JSON for Rust backend
             $parametersJson = $Parameters | ConvertTo-Json -Depth 10 -Compress
             
             # Call the Rust library through PSWrapper
             $resultJson = [Cirrus.Core.PSWrapper]::RunCommand(""{3}"", $parametersJson)
             
             # Parse and return the result
             if ([string]::IsNullOrEmpty($resultJson) -or $resultJson -eq ""{{}}"") {{
                 Write-Host ""Command executed successfully"" -ForegroundColor Green
                 return $null
             }}
             
             $result = $resultJson | ConvertFrom-Json
             return $result
         }}
         catch {{
             Write-Error ""Failed to execute {0}: $_""
             throw
         }}
     }}
}}";

    /// <summary>
    /// Generates a PowerShell function script for the given cmdlet information
    /// </summary>
    /// <param name="cmdletInfo">The cmdlet information to generate script for</param>
    /// <returns>PowerShell function script as string</returns>
    public static string GenerateFunctionScript(CmdletInfo cmdletInfo)
    {
        if (cmdletInfo == null)
            throw new ArgumentNullException(nameof(cmdletInfo));

        if (string.IsNullOrWhiteSpace(cmdletInfo.Name))
            throw new ArgumentException("Cmdlet name cannot be null or empty", nameof(cmdletInfo));

        if (string.IsNullOrWhiteSpace(cmdletInfo.Uri))
            throw new ArgumentException("Cmdlet URI cannot be null or empty", nameof(cmdletInfo));

        // Sanitize the cmdlet name to ensure it's a valid PowerShell function name
        string sanitizedName = SanitizeFunctionName(cmdletInfo.Name);

        // Generate synopsis and description
        string synopsis = GenerateSynopsis(sanitizedName);
        string description = GenerateDescription(sanitizedName, cmdletInfo.Uri);

        // Format the function script using the template
        string functionScript = string.Format(
            FunctionTemplate,
            sanitizedName,      // {0} - Function name
            synopsis,           // {1} - Synopsis
            description,        // {2} - Description
            cmdletInfo.Uri      // {3} - URI
        );

        return functionScript;
    }

    /// <summary>
    /// Generates multiple function scripts for an array of cmdlet information
    /// </summary>
    /// <param name="cmdlets">Array of cmdlet information</param>
    /// <returns>Combined PowerShell function scripts as string</returns>
    public static string GenerateFunctionScripts(CmdletInfo[] cmdlets)
    {
        if (cmdlets == null || cmdlets.Length == 0)
            return string.Empty;

        var scriptBuilder = new StringBuilder();

        foreach (var cmdlet in cmdlets)
        {
            if (cmdlet != null && !string.IsNullOrWhiteSpace(cmdlet.Name) && !string.IsNullOrWhiteSpace(cmdlet.Uri))
            {
                try
                {
                    string functionScript = GenerateFunctionScript(cmdlet);
                    scriptBuilder.AppendLine(functionScript);
                    scriptBuilder.AppendLine(); // Add blank line between functions
                }
                catch (Exception ex)
                {
                    // Log warning but continue with other cmdlets
                    System.Diagnostics.Debug.WriteLine($"Warning: Failed to generate function script for cmdlet '{cmdlet.Name}': {ex.Message}");
                }
            }
        }

        return scriptBuilder.ToString();
    }

    /// <summary>
    /// Sanitizes a function name to ensure it's valid for PowerShell
    /// </summary>
    /// <param name="name">The original name</param>
    /// <returns>Sanitized function name</returns>
    private static string SanitizeFunctionName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "Unknown-Function";

        // Remove invalid characters and ensure it starts with a letter or underscore
        var sanitized = new StringBuilder();
        bool firstChar = true;

        foreach (char c in name)
        {
            if (firstChar)
            {
                if (char.IsLetter(c) || c == '_')
                {
                    sanitized.Append(c);
                    firstChar = false;
                }
                else if (char.IsDigit(c))
                {
                    sanitized.Append('_');
                    sanitized.Append(c);
                    firstChar = false;
                }
                // Skip invalid first characters
            }
            else
            {
                if (char.IsLetterOrDigit(c) || c == '_' || c == '-')
                {
                    sanitized.Append(c);
                }
                // Skip other invalid characters
            }
        }

        string result = sanitized.ToString();
        return string.IsNullOrEmpty(result) ? "Unknown-Function" : result;
    }

    /// <summary>
    /// Generates a synopsis for the cmdlet
    /// </summary>
    /// <param name="functionName">The function name</param>
    /// <returns>Synopsis text</returns>
    private static string GenerateSynopsis(string functionName)
    {
        return $"Executes the {functionName} cmdlet";
    }

    /// <summary>
    /// Generates a description for the cmdlet
    /// </summary>
    /// <param name="functionName">The function name</param>
    /// <param name="uri">The cmdlet URI</param>
    /// <returns>Description text</returns>
    private static string GenerateDescription(string functionName, string uri)
    {
        return $"This cmdlet was automatically generated from the Azure Cirrus Rust backend.\n" +
               $"It executes the {functionName} operation using the URI: {uri}\n" +
               $"Parameters should be passed as a hashtable to the -Parameters parameter.";
    }
}