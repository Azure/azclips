using System.Collections.ObjectModel;
using System.Management.Automation;
using System.Text;
using System.IO;

namespace Cirrus.Core;

/// <summary>
/// Provides functionality for generating PowerShell function scripts from cmdlet information
/// </summary>
public static class PSCmdlet
{
    /// <summary>
    /// Template for generating PowerShell function script with external help
    /// </summary>
    private const string FunctionTemplateWithExternalHelp = @"function {0} {{
    <#
    .EXTERNALHELP {1}
    #>
    [CmdletBinding()]
    param()

    DynamicParam {{
        try {{
            return [Cirrus.Core.PSCmdlet]::GetDynamicParameters(""{2}"")
        }}
        catch {{
            Write-Warning ""Failed to get dynamic parameters: $_""
            return New-Object System.Management.Automation.RuntimeDefinedParameterDictionary
        }}
    }}
    
    Process {{
        Write-Host ""=== {0} Execution ==="" -ForegroundColor Cyan
        Write-Host ""URI: {2}"" -ForegroundColor Yellow
        
        if ($PSBoundParameters.Count -gt 0) {{
            Write-Host ""Parameters received:"" -ForegroundColor Green
            $PSBoundParameters.GetEnumerator() | ForEach-Object {{
                Write-Host ""  $($_.Key) = $($_.Value)"" -ForegroundColor White
            }}
        }}
        else {{
            Write-Host ""No parameters provided"" -ForegroundColor Gray
        }}
        
        Write-Host ""=== End Execution ==="" -ForegroundColor Cyan
    }}
}}";

    /// <summary>
    /// Template for generating PowerShell function script with inline help
    /// </summary>
    private const string FunctionTemplateWithInlineHelp = @"function {0} {{
    <#
    .SYNOPSIS
    {1}
    
    .DESCRIPTION
    {2}
    
    .EXAMPLE
    {0} -ResourceName ""MyResource"" -Location ""centralus""
    
    .NOTES
    Auto-generated cmdlet from Azure Cirrus
    URI: {3}
    Dynamic parameters are generated based on the command context.
    #>
    [CmdletBinding()]
    param()

    DynamicParam {{
        try {{
            return [Cirrus.Core.PSCmdlet]::GetDynamicParameters(""{3}"")
        }}
        catch {{
            Write-Warning ""Failed to get dynamic parameters: $_""
            return New-Object System.Management.Automation.RuntimeDefinedParameterDictionary
        }}
    }}
    
    Process {{
        Write-Host ""=== {0} Execution ==="" -ForegroundColor Cyan
        Write-Host ""URI: {3}"" -ForegroundColor Yellow
        
        if ($PSBoundParameters.Count -gt 0) {{
            Write-Host ""Parameters received:"" -ForegroundColor Green
            $PSBoundParameters.GetEnumerator() | ForEach-Object {{
                Write-Host ""  $($_.Key) = $($_.Value)"" -ForegroundColor White
            }}
        }}
        else {{
            Write-Host ""No parameters provided"" -ForegroundColor Gray
        }}
        
        Write-Host ""=== End Execution ==="" -ForegroundColor Cyan
    }}
}}";

    /// <summary>
    /// Generates a PowerShell function script for the given cmdlet information
    /// </summary>
    /// <param name="cmdletInfo">The cmdlet information to generate script for</param>
    /// <param name="externalHelpPath">Path to external help file, or null if not available</param>
    /// <returns>PowerShell function script as string</returns>
    public static string GenerateFunctionScript(CmdletInfo cmdletInfo, string? externalHelpPath = null)
    {
        if (cmdletInfo == null)
            throw new ArgumentNullException(nameof(cmdletInfo));

        if (string.IsNullOrWhiteSpace(cmdletInfo.Name))
            throw new ArgumentException("Cmdlet name cannot be null or empty", nameof(cmdletInfo));

        if (string.IsNullOrWhiteSpace(cmdletInfo.Uri))
            throw new ArgumentException("Cmdlet URI cannot be null or empty", nameof(cmdletInfo));

        // Sanitize the cmdlet name to ensure it's a valid PowerShell function name
        string sanitizedName = SanitizeFunctionName(cmdletInfo.Name);

        string functionScript;

        if (!string.IsNullOrWhiteSpace(externalHelpPath))
        {
            // Use external help template
            functionScript = string.Format(
                FunctionTemplateWithExternalHelp,
                sanitizedName,          // {0} - Function name
                externalHelpPath,       // {1} - External help file path
                cmdletInfo.Uri          // {2} - URI
            );
        }
        else
        {
            // Use inline help template
            string synopsis = !string.IsNullOrWhiteSpace(cmdletInfo.Synopsis)
                ? cmdletInfo.Synopsis
                : GenerateSynopsis(sanitizedName);
            string description = !string.IsNullOrWhiteSpace(cmdletInfo.Description)
                ? cmdletInfo.Description
                : GenerateDescription(sanitizedName, cmdletInfo.Uri);

            functionScript = string.Format(
                FunctionTemplateWithInlineHelp,
                sanitizedName,      // {0} - Function name
                synopsis,           // {1} - Synopsis
                description,        // {2} - Description
                cmdletInfo.Uri      // {3} - URI
            );
        }

        return functionScript;
    }

    /// <summary>
    /// Generates multiple function scripts for an array of cmdlet information
    /// </summary>
    /// <param name="cmdlets">Array of cmdlet information</param>
    /// <param name="moduleName">The module name to check for help file</param>
    /// <param name="moduleDirectory">The module directory path</param>
    /// <returns>Combined PowerShell function scripts as string</returns>
    public static string GenerateFunctionScripts(CmdletInfo[] cmdlets, string? moduleName = null, string? moduleDirectory = null)
    {
        if (cmdlets == null || cmdlets.Length == 0)
            return string.Empty;

        // Check for external help file
        string? externalHelpPath = null;
        if (!string.IsNullOrWhiteSpace(moduleName) && !string.IsNullOrWhiteSpace(moduleDirectory))
        {
            string helpFileName = $"{moduleName}-help.xml";
            string helpFilePath = Path.Combine(moduleDirectory, helpFileName);

            if (File.Exists(helpFilePath))
            {
                externalHelpPath = helpFileName; // Use relative path for .EXTERNALHELP
            }
        }

        var scriptBuilder = new StringBuilder();

        foreach (var cmdlet in cmdlets)
        {
            if (cmdlet != null && !string.IsNullOrWhiteSpace(cmdlet.Name) && !string.IsNullOrWhiteSpace(cmdlet.Uri))
            {
                try
                {
                    string functionScript = GenerateFunctionScript(cmdlet, externalHelpPath);
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

    public static RuntimeDefinedParameterDictionary GetDynamicParameters(string uri)
    {
        var paramDict = new RuntimeDefinedParameterDictionary();

        try
        {
            // Fake Parameter 1: ResourceName (String, Mandatory)
            var resourceNameAttribute = new ParameterAttribute
            {
                Mandatory = true,
                HelpMessage = "The name of the resource to operate on",
                Position = 0
            };

            var resourceNameParam = new RuntimeDefinedParameter(
                "ResourceName",
                typeof(string),
                new Collection<Attribute> { resourceNameAttribute }
            );
            paramDict.Add("ResourceName", resourceNameParam);

            // Fake Parameter 2: Location (String, Optional with ValidateSet)
            var locationAttribute = new ParameterAttribute
            {
                Mandatory = false,
                HelpMessage = "The Azure region where the resource should be created"
            };

            var locationValidateSet = new ValidateSetAttribute(
                "eastus", "westus", "centralus", "northeurope", "westeurope", "japaneast"
            );

            var locationParam = new RuntimeDefinedParameter(
                "Location",
                typeof(string),
                new Collection<Attribute> { locationAttribute, locationValidateSet }
            );
            paramDict.Add("Location", locationParam);

            // Optional: Add URI-specific parameters based on the URI
            if (!string.IsNullOrEmpty(uri))
            {
                // You can add URI-specific logic here later
                // For example, if uri contains "vm", add VM-specific parameters
                if (uri.Contains("vm", StringComparison.OrdinalIgnoreCase))
                {
                    // VM Size parameter
                    var vmSizeAttribute = new ParameterAttribute
                    {
                        Mandatory = false,
                        HelpMessage = "The size of the virtual machine"
                    };

                    var vmSizeValidateSet = new ValidateSetAttribute(
                        "Standard_B1s", "Standard_B2s", "Standard_D2s_v3", "Standard_D4s_v3"
                    );

                    var vmSizeParam = new RuntimeDefinedParameter(
                        "VMSize",
                        typeof(string),
                        new Collection<Attribute> { vmSizeAttribute, vmSizeValidateSet }
                    );
                    paramDict.Add("VMSize", vmSizeParam);
                }
            }
        }
        catch (Exception ex)
        {
            // Log the error but don't throw - return empty dictionary instead
            System.Diagnostics.Debug.WriteLine($"Error creating dynamic parameters: {ex.Message}");
        }

        return paramDict;
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