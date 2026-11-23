[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ExePath,
    [int]$WaitSeconds = 15,
    [string]$DiagDir = ''
)

# Exit codes:
#   0  - app launched and reached its "main window activated" startup marker
#   10 - app process exited during the startup wait
#   20 - crash log gained fatal diagnostics (exception text) while alive
#   30 - app alive but the startup completion marker is missing after the wait
#
# Console output is kept MINIMAL on purpose. Inside MSBuild Exec this
# script's stdout pipe has proven unreliable once the app process has run:
# in runs 26/27 output vanished mid-script (a notice was emitted, the very
# next ::error:: was never seen, only the exit code survived). The full
# diagnosis therefore goes to $DiagDir\nerea-smoke-result.txt, which the
# workflow's "Diagnose app startup failure" step re-emits through its own
# fresh console. Exit codes still fail the build (MSB3073 shows the code).

function Sanitize-AnnotationLine {
    param([string]$Line)
    $flat = $Line.Replace('%', '%25').Replace("`r", '').Replace("`n", '')
    $flat = $flat.Replace(':', '%3A').Replace(',', '%2C')
    if ($flat.Length -gt 380) { $flat = $flat.Substring(0, 380) }
    return $flat
}

function Write-Notice {
    param([string]$Message)
    Write-Host ("::notice::smoke-test: " + (Sanitize-AnnotationLine -Line $Message))
}

function Add-Result {
    param([string]$Verdict, [string]$Detail)
    if (-not $DiagDir -or -not (Test-Path $DiagDir)) { return }
    try {
        $sb = New-Object System.Text.StringBuilder
        [void]$sb.AppendLine("=== NEREA smoke-test verdict: $Verdict ===")
        [void]$sb.AppendLine($Detail)
        $logPath = Join-Path $env:LOCALAPPDATA 'NEREA\logs\nerea-crash.log'
        if (Test-Path $logPath) {
            [void]$sb.AppendLine('=== nerea-crash.log (tail 100 lines) ===')
            foreach ($line in @(Get-Content $logPath -Tail 100)) { [void]$sb.AppendLine($line) }
            Copy-Item $logPath (Join-Path $DiagDir 'nerea-crash-after-smoke.log') -Force
        }
        else {
            [void]$sb.AppendLine('(no crash log at %LOCALAPPDATA%\NEREA\logs\nerea-crash.log)')
        }
        foreach ($name in @('nerea-smoke-stdout.txt', 'nerea-smoke-stderr.txt')) {
            $p = Join-Path $env:TEMP $name
            if (Test-Path $p) {
                [void]$sb.AppendLine("=== app $name (tail 40 lines) ===")
                foreach ($line in @(Get-Content $p -Tail 40)) { [void]$sb.AppendLine($line) }
            }
        }
        [System.IO.File]::WriteAllText((Join-Path $DiagDir 'nerea-smoke-result.txt'), $sb.ToString())
    }
    catch {
        # Diagnosis is best-effort; never mask the real verdict with it.
    }
}

try {
    if (-not (Test-Path $ExePath)) {
        Write-Host "::error::smoke-test: executable not found: $ExePath"
        Add-Result 'FAILED' "executable not found: $ExePath"
        exit 10
    }

    $logPath = Join-Path $env:LOCALAPPDATA 'NEREA\logs\nerea-crash.log'
    $sizeBefore = 0
    if (Test-Path $logPath) { $sizeBefore = (Get-Item $logPath).Length }

    $outFile = Join-Path $env:TEMP 'nerea-smoke-stdout.txt'
    $errFile = Join-Path $env:TEMP 'nerea-smoke-stderr.txt'
    Remove-Item $outFile -Force -ErrorAction SilentlyContinue
    Remove-Item $errFile -Force -ErrorAction SilentlyContinue

    Write-Notice "launching app, waiting $WaitSeconds seconds (log size before: $sizeBefore)"
    $proc = Start-Process -FilePath $ExePath -PassThru -RedirectStandardOutput $outFile -RedirectStandardError $errFile
    Start-Sleep -Seconds $WaitSeconds
    Write-Notice "wait finished, HasExited=$($proc.HasExited)"

    $increment = ''
    if (Test-Path $logPath) {
        $raw = Get-Content $logPath -Raw
        if ($raw -and $raw.Length -gt $sizeBefore) { $increment = $raw.Substring($sizeBefore) }
    }

    if ($proc.HasExited) {
        Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
        Add-Result 'FAILED' "NEREA exited during startup (exit code: $($proc.ExitCode)). The crash-log increment (if any) follows in this file."
        Write-Host "::error::smoke-test: NEREA exited during startup (exit code: $($proc.ExitCode))"
        exit 10
    }

    # [Main]/[OnLaunched]/[AppDomain...]/[TaskScheduler...] are the crash-entry
    # headers Program.WriteCrash writes. The bare "Exception" term catches the
    # XAML OnUnhandledException format, which logs e.Message + e.Exception
    # WITHOUT a bracket header (App.xaml.cs). A healthy startup log contains
    # only WriteLog breadcrumbs and none of these terms.
    $fatalPattern = '\[Main\]|\[OnLaunched\]|\[AppDomain\.UnhandledException\]|\[TaskScheduler\.UnobservedTaskException\]|could not start|DllNotFoundException|Exception'
    $completed = $increment -match 'OnLaunched: main window activated\. NEREA is running\.'

    if ($increment -match $fatalPattern) {
        Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
        Add-Result 'FAILED' "NEREA is alive but its crash log gained fatal diagnostics ($($increment.Length) bytes). Completion marker seen: $completed. The exception text follows in this file."
        Write-Host "::error::smoke-test: NEREA logged fatal startup diagnostics (crash log bytes: $($increment.Length); see the Diagnose step)"
        exit 20
    }

    if (-not $completed) {
        Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
        Add-Result 'FAILED' "NEREA is alive after $WaitSeconds seconds but the startup completion marker 'OnLaunched: main window activated' is missing (log bytes: $($increment.Length)). The window may never have appeared."
        Write-Host "::error::smoke-test: startup completion marker missing after $WaitSeconds seconds"
        exit 30
    }

    Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
    Add-Result 'PASSED' "NEREA started: main window activated, no fatal diagnostics (log bytes: $($increment.Length))."
    Write-Notice "result: PASSED - main window activated, no fatal diagnostics (log bytes: $($increment.Length))"
    Write-Host "SMOKE TEST PASSED: NEREA.exe reached its startup completion marker."
    exit 0
}
catch {
    Add-Result 'FAILED' ("smoke-test crashed at line {0}: {1}" -f $_.InvocationInfo.ScriptLineNumber, $_.Exception.Message)
    Write-Host ("::error::smoke-test crashed at line {0}: {1}" -f $_.InvocationInfo.ScriptLineNumber, $_.Exception.Message)
    exit 20
}
