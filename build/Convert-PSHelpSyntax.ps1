function Convert-PSHelpSyntax
{
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][AllowEmptyString()][string]$Content,
        [switch]$ForPlatyPS
    )

    $output = [System.Text.StringBuilder]::new()
    $inSyntax = $false
    $fence = ''
    $hasSyntaxHeading = $false
    foreach ($match in [regex]::Matches($Content, '[^\r\n]*(?:\r\n|\n|\r|$)'))
    {
        $line = $match.Value.TrimEnd("`r", "`n")
        if ($line -match '^[ \t]*(`{3,}|~{3,})')
        {
            $marker = $Matches[1]
            if (-not $fence)
            {
                if ($ForPlatyPS -and $inSyntax -and -not $hasSyntaxHeading)
                {
                    # PlatyPS 1.0.1 misparses heading-free syntax on refresh; restore only for tool input.
                    $newline = if ($match.Value.EndsWith("`r`n")) { "`r`n" } else { "`n" }
                    [void]$output.Append("### __AllParameterSets$newline$newline")
                    $hasSyntaxHeading = $true
                }
                $fence = $marker
            }
            elseif ($marker[0] -eq $fence[0] -and $marker.Length -ge $fence.Length -and
                $line.Trim() -eq $marker)
            {
                $fence = ''
            }
        }
        elseif (-not $fence)
        {
            if ($line -cmatch '^##[ \t]+SYNTAX[ \t]*$')
            {
                $inSyntax = $true
                $hasSyntaxHeading = $false
            }
            elseif ($line -match '^#{1,2}[ \t]+')
            {
                $inSyntax = $false
            }
            elseif ($inSyntax -and $line -match '^###[ \t]+')
            {
                $hasSyntaxHeading = $true
                if (-not $ForPlatyPS -and $line -cmatch '^###[ \t]+__AllParameterSets[ \t]*$')
                {
                    continue
                }
            }
        }
        [void]$output.Append($match.Value)
    }
    return $output.ToString()
}
