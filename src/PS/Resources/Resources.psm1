# Load the agent helper first
. (Join-Path $PSScriptRoot 'AgentHelper.ps1')

# Load all exported functions
$exportsPath = Join-Path $PSScriptRoot './exports'
$defaultFunctions = Get-ChildItem function:
Get-ChildItem -Path $exportsPath -Recurse -Include '*.ps1' -File 
    | Where-Object { $defaultFunctions -notcontains $_.BaseName } 
    | ForEach-Object { . $_.FullName }

# Export all functions and explicitly export the AI agent aliases
Export-ModuleMember -Function * -Alias @('ask', 'please', 'pls')