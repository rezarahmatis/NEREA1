[CmdletBinding()]
param(
    [string]$DiagDir = ''
)

# Runs via MSBuild <OnError> after the verify/smoke targets fail. This is a
# FRESH pwsh process (a new MSBuild Exec pipe) launched AFTER the app process
# was killed, which is why its output reliably reaches the CI log where the
# smoke script's own output vanished mid-script (runs 26 and 27: a notice
# was emitted, the very next ::error:: never appeared, only the exit code
# survived). It exits 0 so it never masks the original failure.

function San([string]$s) {
    $f = $s.Replace('%', '%25').Replace("`r", '').Replace("`n", '')
    $f = $f.Replace(':', '%3A').Replace(',', '%2C')
    if ($f.Length -gt 380) { $f = $f.Substring(0, 380) }
    return $f
}

try {
    Stop-Process -Name NEREA -Force -ErrorAction SilentlyContinue

    $result = $null
    if ($DiagDir) {
        $p = Join-Path $DiagDir 'nerea-smoke-result.txt'
        if (Test-Path $p) { $result = $p }
    }

    if ($result) {
        Write-Host "=== NEREA smoke-test verdict file ($result) ==="
        Get-Content $result | ForEach-Object { Write-Host $_ }
        $lines = @(Get-Content $result | Where-Object { $_.Trim().Length -gt 0 })
        $n = [Math]::Min(12, $lines.Count)
        for ($i = 0; $i -lt $n; $i++) {
            Write-Host ("::notice::diag: " + (San $lines[$i]))
        }
    }
    else {
        Write-Host "::notice::diag: no nerea-smoke-result.txt found - failure happened before the smoke test"
    }

    $crash = Join-Path $env:LOCALAPPDATA 'NEREA\logs\nerea-crash.log'
    if (Test-Path $crash) {
        Write-Host "=== NEREA crash log ($crash) tail 80 ==="
        $tail = @(Get-Content $crash -Tail 80)
        foreach ($line in $tail) { Write-Host $line }
        # Compiler-style diagnostic lines get parsed into check-run
        # annotations by the runner (the CS0169 warnings did), giving a
        # second, independent channel for the crash-log content.
        $n2 = [Math]::Min(12, $tail.Count)
        for ($i = 0; $i -lt $n2; $i++) {
            $ln = $i + 1
            Write-Host ("$crash($ln): error NEREA0001: " + (San $tail[$i]))
        }
    }
    else {
        Write-Host "::notice::diag: no crash log - the failure happened below the managed layer"
        Write-Host "::warning::diag: no crash log - the failure happened below the managed layer"
    }

    exit 0
}
catch {
    Write-Host ("::error::diagnose crashed at line {0}: {1}" -f $_.InvocationInfo.ScriptLineNumber, $_.Exception.Message)
    exit 0
}
