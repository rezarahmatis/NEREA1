[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ExePath,
    [int]$WaitSeconds = 15
)

function Sanitize-AnnotationLine {
    param([string]$Line)
    $flat = $Line.Replace('%', '%25').Replace("`r", '').Replace("`n", '')
    $flat = $flat.Replace(':', '%3A').Replace(',', '%2C')
    if ($flat.Length -gt 400) { $flat = $flat.Substring(0, 400) }
    return $flat
}

function Write-Notice {
    param([string]$Message)
    Write-Host ("::notice::smoke-test: " + (Sanitize-AnnotationLine -Line $Message))
}

function Emit-TextAsAnnotations {
    param([string]$Text, [int]$MaxLines = 10, [string]$Level = 'error')
    $lines = @($Text -split "`n" | Where-Object { $_.Trim().Length -gt 0 })
    $count = [Math]::Min($MaxLines, $lines.Count)
    for ($i = 0; $i -lt $count; $i++) {
        Write-Host ("::${Level}::" + (Sanitize-AnnotationLine -Line $lines[$i]))
    }
}

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

try {
    Write-Notice "starting for $ExePath"
    if (-not (Test-Path $ExePath)) {
        Write-Host "::error::smoke-test: executable not found: $ExePath"
        exit 1
    }

    $logPath = Join-Path $env:LOCALAPPDATA 'NEREA\logs\nerea-crash.log'
    $sizeBefore = 0
    if (Test-Path $logPath) { $sizeBefore = (Get-Item $logPath).Length }

    # The app must NOT inherit this script's stdout/stderr: MSBuild Exec
    # captures our output through a pipe, and a process that outlives the
    # script (or writes to / closes the inherited handles) corrupts the CI
    # log stream and breaks every later annotation. Redirect the app's
    # output to files instead - this also captures what the app prints.
    $outFile = Join-Path $env:TEMP 'nerea-smoke-stdout.txt'
    $errFile = Join-Path $env:TEMP 'nerea-smoke-stderr.txt'
    Remove-Item $outFile -Force -ErrorAction SilentlyContinue
    Remove-Item $errFile -Force -ErrorAction SilentlyContinue

    Write-Notice "launching app%2C waiting $WaitSeconds seconds (log size before: $sizeBefore)"
    $proc = Start-Process -FilePath $ExePath -PassThru -RedirectStandardOutput $outFile -RedirectStandardError $errFile
    Start-Sleep -Seconds $WaitSeconds
    Write-Notice "wait finished%2C HasExited=$($proc.HasExited)"

    if ($proc.HasExited) {
        Write-Host "::error::smoke-test: NEREA exited during startup (exit code: $($proc.ExitCode))"
        Show-Diagnostics
        exit 1
    }

    $crashContent = ''
    if (Test-Path $logPath) {
        $raw = Get-Content $logPath -Raw
        if ($raw -and $raw.Length -gt $sizeBefore) { $crashContent = $raw.Substring($sizeBefore) }
    }
    Write-Notice "log captured%2C new bytes=$($crashContent.Length)"

    if ($crashContent -match '\[Main\]|\[OnLaunched\]|could not start|AppDomain\.UnhandledException|DllNotFoundException') {
        Write-Host "::error::smoke-test: NEREA is alive but logged a fatal startup error"
        Emit-TextAsAnnotations -Text $crashContent
        Show-Diagnostics
        Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
        exit 1
    }

    # Surface whatever the app printed (may be empty) for diagnosis.
    foreach ($capture in @(@('stdout', $outFile), @('stderr', $errFile))) {
        $label = $capture[0]
        $file = $capture[1]
        if (Test-Path $file) {
            $content = Get-Content $file -Raw
            if ($content) {
                $tail = $content
                if ($tail.Length -gt 800) { $tail = $tail.Substring($tail.Length - 800) }
                Write-Host "=== app $label ($file) ==="
                Write-Host $tail
                Emit-TextAsAnnotations -Text $tail -MaxLines 5 -Level 'notice'
            }
        }
    }

    Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
    Write-Host "SMOKE TEST PASSED: NEREA.exe ran for $WaitSeconds seconds without fatal diagnostics."
    exit 0
}
catch {
    Write-Host ("::error::smoke-test crashed at line {0}: {1}" -f $_.InvocationInfo.ScriptLineNumber, $_.Exception.Message)
    Show-Diagnostics
    exit 1
}
