[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$PublishExe
)

try {
    Write-Host "::notice::verify-publish-output: start ($PublishExe)"
    if (-not (Test-Path $PublishExe)) {
        Write-Host "::error::verify-publish-output: NEREA.exe missing: $PublishExe"
        exit 1
    }
    $PublishDir = Split-Path -Parent $PublishExe

    # Files that must sit next to NEREA.exe or the process dies before any
    # managed diagnostics can run (the silent "installed but won't run").
    $required = @(
        'NEREA.dll',
        'Microsoft.ui.xaml.dll',
        'Microsoft.WindowsAppRuntime.Bootstrap.dll',
        'Microsoft.WindowsAppRuntime.dll',
        'coreclr.dll',
        'clrjit.dll',
        'hostfxr.dll'
    )
    $missing = @($required | Where-Object { -not (Test-Path (Join-Path $PublishDir $_)) })
    if ($missing.Count -gt 0) {
        Write-Host ("::error::verify-publish-output: missing required runtime files: " + ($missing -join '%2C '))
        exit 1
    }
    Write-Host "::notice::verify-publish-output: all required runtime files present"

    Write-Host "Publish output top-level files:"
    Get-ChildItem -LiteralPath $PublishDir -File | Sort-Object Name |
        ForEach-Object { Write-Host ("  {0}  {1:N0} bytes" -f $_.Name, $_.Length) }

    $pri = @(Get-ChildItem -LiteralPath $PublishDir -Filter '*.pri' -Recurse)
    if ($pri.Count -gt 0) {
        Write-Host ("::notice::resource indexes: " + (($pri | ForEach-Object { $_.Name }) -join '%2C '))
    }
    else {
        Write-Host "::warning::no .pri resource index found in publish output - XAML resources may be missing"
    }

    Copy-Item (Join-Path $PSScriptRoot '..\NEREA-portable-readme.txt') (Join-Path $PublishDir 'README.txt') -Force
    Write-Host "::notice::verify-publish-output: copied Persian README.txt%2C done"
    exit 0
}
catch {
    Write-Host ("::error::verify-publish-output crashed at line {0}: {1}" -f $_.InvocationInfo.ScriptLineNumber, $_.Exception.Message)
    exit 1
}
