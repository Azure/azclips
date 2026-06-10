$binPath = (Resolve-Path (Join-Path $PSScriptRoot '..\..\bin')).Path
$env:PATH = $binPath + [IO.Path]::PathSeparator + $env:PATH

. (Join-Path $binPath 'azv2.completions.ps1')

# Show loaded command tree
Write-Host "Loaded command tree keys: $($global:_Azv2CommandTree.Keys -join ', ')" -ForegroundColor Cyan

# Test the core _Azv2GetCompletions function directly

Write-Host "`n--- 'azv2 ' → all groups (empty word) ---"
_Azv2GetCompletions 'azv2 ' 5 | Format-Table CompletionText, ResultType

Write-Host "--- 'azv2 g' → group ---"
_Azv2GetCompletions 'azv2 g' 6 | Format-Table CompletionText, ResultType

Write-Host "--- 'azv2 group ' → subcommands (empty word) ---"
_Azv2GetCompletions 'azv2 group ' 11 | Format-Table CompletionText, ResultType

Write-Host "--- 'azv2 group c' → create ---"
_Azv2GetCompletions 'azv2 group c' 12 | Format-Table CompletionText, ResultType

Write-Host "--- 'azv2 group create ' → parameters (empty word) ---"
_Azv2GetCompletions 'azv2 group create ' 18 | Format-Table CompletionText, ResultType

Write-Host "--- 'azv2 group create --' → parameters ---"
_Azv2GetCompletions 'azv2 group create --' 20 | Format-Table CompletionText, ResultType

Write-Host "--- 'azv2 vm c' → create ---"
_Azv2GetCompletions 'azv2 vm c' 9 | Format-Table CompletionText, ResultType

Write-Host "--- 'azv2 login ' → login params (empty word) ---"
_Azv2GetCompletions 'azv2 login ' 11 | Format-Table CompletionText, ResultType

Write-Host "--- 'azv2 login --' → login params ---"
_Azv2GetCompletions 'azv2 login --' 13 | Format-Table CompletionText, ResultType

Write-Host "--- 'azv2 config l' → config subcommands ---"
_Azv2GetCompletions 'azv2 config l' 13 | Format-Table CompletionText, ResultType
