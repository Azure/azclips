# Check for orphan cirrus-agent daemon processes left over after Core.Tui exit.
# Run after closing all TUI windows to verify DisposeAsync did its job.

param(
    [switch]$KillOrphans
)

$daemons = Get-Process -Name 'cirrus-agent','copilot' -ErrorAction SilentlyContinue

if ($null -eq $daemons -or $daemons.Count -eq 0) {
    Write-Host "[OK] No cirrus-agent / copilot daemon processes running." -ForegroundColor Green
    exit 0
}

Write-Host "[WARN] Found $($daemons.Count) daemon process(es):" -ForegroundColor Yellow
$daemons | Select-Object Id, ProcessName, StartTime, @{N='RSSMB';E={[math]::Round($_.WorkingSet64/1MB,1)}} | Format-Table -AutoSize

$ownTui = Get-Process -Name 'Core.Tui' -ErrorAction SilentlyContinue
if ($ownTui) {
    Write-Host "[INFO] Core.Tui is still running (PID $($ownTui.Id -join ',')); daemons may belong to it." -ForegroundColor Cyan
    exit 0
}

if ($KillOrphans) {
    foreach ($p in $daemons) {
        Write-Host "  killing PID $($p.Id) ($($p.ProcessName))" -ForegroundColor Yellow
        Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue
    }
    Write-Host "[DONE] orphan daemons killed. Re-run without -KillOrphans to verify." -ForegroundColor Green
    exit 0
}

Write-Host "[FAIL] Orphan daemons detected. Re-run with -KillOrphans to clean up." -ForegroundColor Red
exit 1
