[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ExePath,
    [int]$WaitSeconds = 15
)

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

function Sanitize-AnnotationLine {
    param([string]$Line)
    $flat = $Line.Replace('%', '%25').Replace("`r", '').Replace("`n", '')
    $flat = $flat.Replace(':', '%3A').Replace(',', '%2C')
    if ($flat.Length -gt 400) { $flat = $flat.Substring(0, 400) }
    return $flat
}

function Emit-TextAsAnnotations {
    param([string]$Text, [int]$MaxLines = 10)
    $lines = @($Text -split "`n" | Where-Object { $_.Trim().Length -gt 0 })
    $count = [Math]::Min($MaxLines, $lines.Count)
    for ($i = 0; $i -lt $count; $i++) {
        Write-Host ("::error::" + (Sanitize-AnnotationLine -Line $lines[$i]))
    }
}

try {
    Write-Host "::notice::smoke-test: starting for $ExePath"
    if (-not (Test-Path $ExePath)) {
        Write-Host "::error::smoke-test: executable not found: $ExePath"
        exit 1
    }

    $logPath = Join-Path $env:LOCALAPPDATA 'NEREA\logs\nerea-crash.log'
    $sizeBefore = 0
    if (Test-Path $logPath) { $sizeBefore = (Get-Item $logPath).Length }

    Write-Host "::notice::smoke-test: launching app%2C waiting $WaitSeconds seconds (log size before: $sizeBefore)"
    $proc = Start-Process -FilePath $ExePath -PassThru
    Start-Sleep -Seconds $WaitSeconds
    Write-Host "::notice::smoke-test: wait finished%2C HasExited=$($proc.HasExited)"

    if ($proc.HasExited) {
        Write-Host "::error::smoke-test: NEREA exited during startup (exit code: $($proc.ExitCode))"
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
        Write-Host "::error::smoke-test: NEREA is alive but logged a fatal startup error"
        Emit-TextAsAnnotations -Text $crashContent
        Show-Diagnostics
        Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
        exit 1
    }

    Write-Host "::notice::smoke-test: no fatal diagnostics%2C stopping process"
    Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
    Write-Host "SMOKE TEST PASSED: NEREA.exe ran for $WaitSeconds seconds without fatal diagnostics."
    exit 0
}
catch {
    Write-Host ("::error::smoke-test crashed at line {0}: {1}" -f $_.InvocationInfo.ScriptLineNumber, $_.Exception.Message)
    Show-Diagnostics
    exit 1
}
