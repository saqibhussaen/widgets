param([switch]$Package, [switch]$NoRestore, [switch]$SkipTests)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Push-Location $root
try {
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    $env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
    $env:MSBuildEnableWorkloadResolver = 'false'
    $local = Join-Path $root '.tools\dotnet\dotnet.exe'
    $dotnet = if (Test-Path $local) { $local } else { 'dotnet' }
    if (Test-Path $local) {
        $env:DOTNET_ROOT = Join-Path $root '.tools\dotnet'
        $env:DOTNET_CLI_HOME = Join-Path $root '.tools\home'
        $env:NUGET_PACKAGES = Join-Path $root '.tools\packages'
    }
    $arguments = @('build','tests\ClockWidget.Tests\ClockWidget.Tests.csproj','-c','Release','-m:1','-nr:false')
    if ($NoRestore) { $arguments += '--no-restore' }
    & $dotnet @arguments
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
    if (!$SkipTests) {
        & '.\tests\ClockWidget.Tests\bin\Release\net10.0-windows10.0.19041.0\win-x64\ClockWidget.Tests.exe'
        if ($LASTEXITCODE -ne 0) { throw 'Tests failed or execution was blocked.' }
    } else { Write-Warning 'Automated tests explicitly skipped. This is not a passing test result; record the reason in docs/VALIDATION.md. CI never skips tests.' }
    & (Join-Path $PSScriptRoot 'verify-licenses.ps1')
    if ($Package) {
        # Unique staging prevents stale files being included in a newer release.
        $stage = Join-Path $root ('artifacts\stage-' + [Guid]::NewGuid().ToString('N'))
        & $dotnet publish src\ClockWidget\ClockWidget.csproj -c Release -r win-x64 --self-contained true -p:TargetLatestRuntimePatch=true -p:PublishReadyToRun=false -p:DebugType=None -p:DebugSymbols=false -o $stage -m:1 -nr:false
        if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
        Copy-Item README.md,decisions.md,Flow.md,MEDIA_CONTROLS.md -Destination $stage
        Copy-Item docs -Destination $stage -Recurse
        & (Join-Path $PSScriptRoot 'verify-licenses.ps1') -PublishDirectory $stage
        $zip = Join-Path $root 'artifacts\Widgets-win-x64.zip'
        Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip -Force
        (Get-FileHash $zip -Algorithm SHA256).Hash.ToLower() + '  Widgets-win-x64.zip' | Set-Content (Join-Path $root 'artifacts\Widgets-win-x64.sha256') -Encoding ASCII
        Write-Host "Portable package: $zip"
    }
} finally { Pop-Location }
