[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ExePath,
    [int]$WaitSeconds = 15
)

$ErrorActionPreference = 'Stop'

function Show-Diagnostics {
    $logPath = Join-Path $env:LOCALAPPDATA 'NEREA\logs\nerea-crash.log'
    if (Test-Path $logPath) {
        Write-Host "=== NEREA crash log ($logPath) ==="
        Get-Content $logPath | Select-Object -Last 60 | ForEach-Object { Write-Host $_ }
    }
    else {
        Write-Host "(no crash log at $logPath - the failure happened below the managed layer)"
    }
    try {
        Write-Host "=== Application event log entries mentioning NEREA (last 5 minutes) ==="
        Get-WinEvent -FilterHashtable @{ LogName = 'Application'; StartTime = (Get-Date).AddMinutes(-5) } -ErrorAction SilentlyContinue |
            Where-Object { $_.Message -match 'NEREA' } |
            Select-Object -First 8 |
            ForEach-Object { Write-Host ("[{0}] {1}: {2}" -f $_.TimeCreated, $_.ProviderName, $_.Message) }
    }
    catch {
        Write-Host "(event log query unavailable)"
    }
}

if (-not (Test-Path $ExePath)) {
    Write-Host "::error::Executable not found: $ExePath"
    exit 1
}

$logPath = Join-Path $env:LOCALAPPDATA 'NEREA\logs\nerea-crash.log'
$sizeBefore = 0
if (Test-Path $logPath) { $sizeBefore = (Get-Item $logPath).Length }

Write-Host "Launching $ExePath and waiting $WaitSeconds seconds..."
$proc = Start-Process -FilePath $ExePath -PassThru
Start-Sleep -Seconds $WaitSeconds

if ($proc.HasExited) {
    Write-Host "::error::NEREA exited during startup (exit code: $($proc.ExitCode))"
    Show-Diagnostics
    exit 1
}

# The process is alive, but NEREA may still have failed internally and be
# sitting on the last-resort fatal dialog (MessageBoxW blocks the process).
# Detect that case through the crash log it writes before showing the dialog.
$crashContent = ''
if (Test-Path $logPath) {
    $raw = Get-Content $logPath -Raw
    if ($raw.Length -gt $sizeBefore) { $crashContent = $raw.Substring($sizeBefore) }
}

if ($crashContent -match '\[Main\]|\[OnLaunched\]|could not start|AppDomain\.UnhandledException|DllNotFoundException') {
    Write-Host "::error::NEREA is alive but wrote fatal diagnostics - startup actually failed:"
    Write-Host $crashContent
    Show-Diagnostics
    Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
    exit 1
}

Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
Write-Host "SMOKE TEST PASSED: NEREA.exe ran for $WaitSeconds seconds without fatal diagnostics."
exit 0
