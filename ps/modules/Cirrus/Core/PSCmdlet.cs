using System.Collections.ObjectModel;
using System.Management.Automation;
using System.Text;
using System.Collections;

namespace Cirrus.Core;

/// <summary>
/// Provides functionality for generating PowerShell function scripts from cmdlet information
/// </summary>
public static class PSCmdlet
{
    /// <summary>
    /// Base template for generating PowerShell function script
    /// </summary>
    private const string BaseFunctionTemplate = @"function {0} {{
    {1}
    [CmdletBinding()]
    param()

    DynamicParam {{
        try {{
            return [Cirrus.Core.PSCmdlet]::GetDynamicParameters(""{2}"", ""{3}"")
        }}
        catch {{
            Write-Warning ""Failed to get dynamic parameters: $_""
            return New-Object System.Management.Automation.RuntimeDefinedParameterDictionary
        }}
    }}
    
    Process {{
        $result = [Cirrus.Core.PSCmdlet]::ExecuteCommand(""{2}"", $PSBoundParameters)
        if ($result -ne $null) {{
            # Handle both single objects and arrays
            if ($result -is [Array]) {{
                foreach ($item in $result) {{
                    Write-Output $item
                }}
            }}
            else {{
                Write-Output $result
            }}
        }}
    }}
}}";

    /// <summary>
    /// Template for external help section
    /// </summary>
    private const string ExternalHelpTemplate = @"<#
    .EXTERNALHELP {0}
    #>";

    /// <summary>
    /// Template for inline help section
    /// </summary>
    private const string InlineHelpTemplate = @"<#
    .SYNOPSIS
    {0}
    
    .DESCRIPTION
    {1}
    
    .NOTES
    Auto-generated cmdlet from Azure Cirrus
    URI: {2}
    Dynamic parameters are generated based on the command context.
    #>";

    /// <summary>
    /// Generates a PowerShell function script for the given cmdlet information
    /// </summary>
    /// <param name="cmdletInfo">The cmdlet information to generate script for</param>
    /// <param name="moduleName">Module name for dynamic type creation</param>
    /// <param name="externalHelpPath">Path to external help file, or null if not available</param>
    /// <returns>PowerShell function script as string</returns>
    public static string GenerateFunctionScript(CmdletInfo cmdletInfo, string moduleName, string? externalHelpPath = null)
    {
        if (cmdletInfo == null)
            throw new ArgumentNullException(nameof(cmdletInfo));

        if (string.IsNullOrWhiteSpace(cmdletInfo.Name))
            throw new ArgumentException("Cmdlet name cannot be null or empty", nameof(cmdletInfo));

        if (string.IsNullOrWhiteSpace(cmdletInfo.Uri))
            throw new ArgumentException("Cmdlet URI cannot be null or empty", nameof(cmdletInfo));

        // Sanitize the cmdlet name to ensure it's a valid PowerShell function name
        string sanitizedName = SanitizeFunctionName(cmdletInfo.Name);

        string helpSection;

        if (!string.IsNullOrWhiteSpace(externalHelpPath))
        {
            // Use external help template
            helpSection = string.Format(
                ExternalHelpTemplate,
                externalHelpPath        // {0} - External help file path
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

            helpSection = string.Format(
                InlineHelpTemplate,
                synopsis,           // {0} - Synopsis
                description,        // {1} - Description
                cmdletInfo.Uri      // {2} - URI
            );
        }

        // Generate the complete function script
        string functionScript = string.Format(
            BaseFunctionTemplate,
            sanitizedName,          // {0} - Function name
            helpSection,            // {1} - Help section
            cmdletInfo.Uri,         // {2} - URI
            moduleName              // {3} - Module name
        );

        return functionScript;
    }

    /// <summary>
    /// Generate PowerShell function scripts for the given cmdlets
    /// </summary>
    /// <param name="cmdlets">Array of cmdlet information</param>
    /// <param name="moduleName">Module name (required)</param>
    /// <param name="moduleDirectory">Optional module directory path</param>
    /// <returns>Generated PowerShell script content</returns>
    public static string GenerateFunctionScripts(CmdletInfo[] cmdlets, string moduleName, string? moduleDirectory = null)
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
                    string functionScript = GenerateFunctionScript(cmdlet, moduleName, externalHelpPath);
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
    /// Get dynamic parameters for a command URI using the Cirrus argument system
    /// </summary>
    /// <param name="uri">Command URI</param>
    /// <param name="moduleName">Module name for type builder</param>
    /// <returns>Runtime defined parameter dictionary</returns>
    public static RuntimeDefinedParameterDictionary GetDynamicParameters(string uri, string moduleName)
    {
        var paramDict = new RuntimeDefinedParameterDictionary();

        try
        {
            // Get command arguments from the URI
            CrsArg[] args = PSWrapper.GetCommandArguments(uri);

            if (args.Length == 0)
            {
                return paramDict;
            }

            // Get or create the dynamic type builder for the module
            var builder = DynamicTypeBuilder.GetOrCreateBuilder(moduleName);

            // First pass: Create type definitions for all arguments
            // This allows for forward references and circular dependencies
            foreach (var arg in args)
            {
                try
                {
                    // This will create type definitions without building members yet
                    arg.GetOrCreateParameterType(builder);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error creating type for argument '{arg.Name}': {ex.Message}");
                }
            }

            // Complete all pending type creation to resolve dependencies
            builder.CreateAllPendingTypes();

            // Second pass: Build runtime parameters
            foreach (var arg in args)
            {
                try
                {
                    if (string.IsNullOrEmpty(arg.Name))
                        continue;

                    var runtimeParam = arg.Build(builder);
                    var parameterName = arg.GetParameterName();

                    paramDict.Add(parameterName, runtimeParam);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error building parameter for argument '{arg.Name}': {ex.Message}");
                    // Continue with other parameters even if one fails
                }
            }
        }
        catch (Exception ex)
        {
            // Log the error but don't throw - return empty dictionary instead
            System.Diagnostics.Debug.WriteLine($"Error creating dynamic parameters for URI '{uri}': {ex.Message}");
        }

        return paramDict;
    }

    /// <summary>
    /// Executes a command with the given URI and parameters
    /// </summary>
    /// <param name="uri">The URI of the command to execute</param>
    /// <param name="parameters">The PowerShell bound parameters</param>
    /// <returns>The result object to be output to PowerShell pipeline</returns>
    public static object ExecuteCommand(string uri, IDictionary parameters)
    {
        // Display execution information
        WriteColoredMessage($"URI: {uri}", "Yellow");

        if (parameters != null && parameters.Count > 0)
        {
            WriteColoredMessage("Parameters received:", "Green");
            foreach (DictionaryEntry param in parameters)
            {
                WriteColoredMessage($"  {param.Key} = {param.Value}", "White");
            }
        }
        else
        {
            WriteColoredMessage("No parameters provided", "Gray");
        }

        // Create and return a PSObject with proper TypeName
        var result = CreateResultObject(uri, parameters);

        WriteColoredMessage("=== Command Executed ===", "Cyan");

        return result;
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

    /// <summary>
    /// Creates a PSObject result with custom TypeName based on the URI and parameters
    /// </summary>
    /// <param name="uri">The command URI</param>
    /// <param name="parameters">The PowerShell bound parameters</param>
    /// <returns>A PSObject with custom TypeName</returns>
    private static PSObject CreateResultObject(string uri, IDictionary parameters)
    {
        // Create a PSObject with properties
        var result = new PSObject();
        string typeName = $"CirruResult";
        result.TypeNames.Insert(0, typeName);

        // Add basic properties
        result.Properties.Add(new PSNoteProperty("Uri", uri));
        result.Properties.Add(new PSNoteProperty("Timestamp", DateTime.UtcNow));
        result.Properties.Add(new PSNoteProperty("Success", true));
        result.Properties.Add(new PSNoteProperty("Id", Guid.NewGuid().ToString()));

        // Add parameters as a hashtable property
        if (parameters != null && parameters.Count > 0)
        {
            var paramHash = new Hashtable();
            foreach (DictionaryEntry param in parameters)
            {
                paramHash[param.Key] = param.Value;
            }
            result.Properties.Add(new PSNoteProperty("Parameters", paramHash));

            // Add Location if provided in parameters
            if (parameters.Contains("Location"))
            {
                result.Properties.Add(new PSNoteProperty("Location", parameters["Location"]));
            }

            // Add ResourceName if provided in parameters
            if (parameters.Contains("ResourceName"))
            {
                result.Properties.Add(new PSNoteProperty("Name", parameters["ResourceName"]));
            }
        }
        return result;
    }

    /// <summary>
    /// Writes a colored message to the PowerShell host
    /// </summary>
    /// <param name="message">The message to write</param>
    /// <param name="color">The color to use</param>
    private static void WriteColoredMessage(string message, string color)
    {
        try
        {
            // Create a script block to execute Write-Host with color
            var script = $"Write-Host \"{message}\" -ForegroundColor {color}";

            // Execute using PowerShell
            using (var ps = System.Management.Automation.PowerShell.Create())
            {
                ps.AddScript(script);
                ps.Invoke();
            }
        }
        catch
        {
            // Fallback to console output if PowerShell execution fails
            Console.WriteLine($"[{color}] {message}");
        }
    }
}