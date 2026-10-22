param([string]$PublishDirectory)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$required = @('LICENSE','THIRD-PARTY-NOTICES.md','licenses\INVENTORY.md','licenses\fonts.json',
    'licenses\runtime\LICENSE.TXT','licenses\runtime\THIRD-PARTY-NOTICES.TXT',
    'licenses\wpf\LICENSE.TXT','licenses\wpf\THIRD-PARTY-NOTICES.TXT',
    'licenses\distribution\LICENSE.txt','licenses\distribution\ThirdPartyNotices.txt',
    'licenses\windows-sdk\LICENSE.txt','licenses\windows-sdk\SDK-LICENSE.rtf')
foreach ($relative in $required) {
    if (!(Test-Path (Join-Path $root $relative))) { throw "Missing notice: $relative" }
}
$inventory = Get-Content (Join-Path $root 'licenses\fonts.json') -Raw | ConvertFrom-Json
foreach ($entry in $inventory) {
    if ($entry.license -ne 'OFL-1.1' -or $entry.commit -notmatch '^[a-f0-9]{40}$') { throw 'Invalid font provenance' }
    $folder = Join-Path $root "src\ClockWidget\Assets\Fonts\$($entry.name)"
    if ((Get-FileHash (Join-Path $folder 'font.ttf') -Algorithm SHA256).Hash.ToLower() -ne $entry.sha256) { throw "Font hash mismatch: $($entry.name)" }
    if ((Get-Content (Join-Path $folder 'OFL.txt') -Raw) -notmatch 'SIL OPEN FONT LICENSE Version 1.1') { throw 'Missing font license text' }
}
$allowed = @('Microsoft.Windows.SDK.NET.Ref/10.0.19041.57')
$assets = Get-Content (Join-Path $root 'src\ClockWidget\obj\project.assets.json') -Raw | ConvertFrom-Json
$runtimeVersion = (Get-Content (Join-Path $root 'release-runtime.json') | ConvertFrom-Json).version
foreach ($entry in $assets.libraries.PSObject.Properties) {
    if ($entry.Value.type -eq 'package' -and $entry.Name -notin $allowed) { throw "Unreviewed dependency: $($entry.Name). Review license/provenance and update inventory/allowlist." }
}
foreach ($framework in $assets.project.frameworks.PSObject.Properties) {
    foreach ($download in $framework.Value.downloadDependencies) {
        $expected = if ($download.name -eq 'Microsoft.Windows.SDK.NET.Ref') { '10.0.19041.57' } elseif ($download.name -in @('Microsoft.NETCore.App.Runtime.win-x64','Microsoft.WindowsDesktop.App.Runtime.win-x64','Microsoft.AspNetCore.App.Runtime.win-x64')) { $runtimeVersion } else { throw "Unreviewed SDK/runtime download: $($download.name)" }
        if ($download.version -ne "[$expected, $expected]") { throw "Unreviewed SDK/runtime version: $($download.name) $($download.version)" }
    }
}
if ($PublishDirectory) {
    foreach ($relative in $required) { if (!(Test-Path (Join-Path $PublishDirectory $relative))) { throw "Missing release notice: $relative" } }
    foreach ($entry in $inventory) {
        $folder = Join-Path $PublishDirectory "Assets\Fonts\$($entry.name)"
        if (!(Test-Path (Join-Path $folder 'OFL.txt'))) { throw 'Release font license missing' }
        if ((Get-FileHash (Join-Path $folder 'font.ttf')).Hash.ToLower() -ne $entry.sha256) { throw 'Release font integrity mismatch' }
    }
    $config = Get-Content (Join-Path $PublishDirectory 'Widgets.runtimeconfig.json') -Raw | ConvertFrom-Json
    $version = (Get-Content (Join-Path $root 'release-runtime.json') | ConvertFrom-Json).version
    if (!$config.runtimeOptions.includedFrameworks) { throw 'Expected self-contained runtime.' }
    foreach ($framework in $config.runtimeOptions.includedFrameworks) { if ($framework.version -ne $version) { throw "Unreviewed runtime version $($framework.version)" } }
    $deps = Get-Content (Join-Path $PublishDirectory 'Widgets.deps.json') -Raw | ConvertFrom-Json
    $allowedPacks = @("runtimepack.Microsoft.NETCore.App.Runtime.win-x64/$version", "runtimepack.Microsoft.WindowsDesktop.App.Runtime.win-x64/$version", 'runtimepack.Microsoft.Windows.SDK.NET.Ref/10.0.19041.57')
    foreach ($library in $deps.libraries.PSObject.Properties) {
        if ($library.Value.type -eq 'runtimepack' -and $library.Name -notin $allowedPacks) { throw "Unreviewed shipped runtime pack: $($library.Name)" }
    }
}
Write-Host 'License texts, font hashes, and dependency allowlist verified.'
