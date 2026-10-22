# Developer-only: fetch unmodified, pinned open-source assets and their licenses.
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$fontRoot = Join-Path $root 'src\ClockWidget\Assets\Fonts'
$licenseRoot = Join-Path $root 'licenses'
$headers = @{ 'User-Agent' = 'ClockWidget-build' }
$commit = (Invoke-RestMethod 'https://api.github.com/repos/google/fonts/commits/main' -Headers $headers).sha
$inventory = @()
foreach ($font in @('nunito','inter')) {
    $folder = Join-Path $fontRoot $font
    New-Item -ItemType Directory -Force $folder | Out-Null
    $files = Invoke-RestMethod "https://api.github.com/repos/google/fonts/contents/ofl/$($font)?ref=$commit" -Headers $headers
    $ttf = $files | Where-Object { $_.name -like '*.ttf' -and $_.name -notlike '*Italic*' } | Select-Object -First 1
    if (!$ttf) { throw "No upright variable font found for $font" }
    $fontUrl = "https://raw.githubusercontent.com/google/fonts/$commit/ofl/$font/$([Uri]::EscapeDataString($ttf.name))"
    $licenseUrl = "https://raw.githubusercontent.com/google/fonts/$commit/ofl/$font/OFL.txt"
    Invoke-WebRequest $fontUrl -OutFile (Join-Path $folder 'font.ttf')
    Invoke-WebRequest $licenseUrl -OutFile (Join-Path $folder 'OFL.txt')
    $inventory += [ordered]@{ name=$font; source=$fontUrl; commit=$commit; license='OFL-1.1'; licenseSource=$licenseUrl; sha256=(Get-FileHash (Join-Path $folder 'font.ttf') -Algorithm SHA256).Hash.ToLower(); modifications='None'; obligations='Retain copyright and full OFL license; font remains OFL; do not sell font by itself.' }
}
New-Item -ItemType Directory -Force $licenseRoot | Out-Null
# Runtime version is pinned by release-runtime.json; preserve upstream notices for that exact release.
$runtimeVersion = (Get-Content (Join-Path $root 'release-runtime.json') | ConvertFrom-Json).version
foreach ($component in @('runtime','wpf')) {
    $destination = Join-Path $licenseRoot $component
    New-Item -ItemType Directory -Force $destination | Out-Null
    foreach ($name in @('LICENSE.TXT','THIRD-PARTY-NOTICES.TXT')) {
        $url = "https://raw.githubusercontent.com/dotnet/$component/v$runtimeVersion/$name"
        Invoke-WebRequest $url -OutFile (Join-Path $destination $name)
    }
}
$distribution = Join-Path $licenseRoot 'distribution'
New-Item -ItemType Directory -Force $distribution | Out-Null
$localSdk = Join-Path $root '.tools\dotnet'
if (!(Test-Path (Join-Path $localSdk 'LICENSE.txt'))) { throw 'Install the official SDK in .tools/dotnet to preserve its distribution notices.' }
Copy-Item (Join-Path $localSdk 'LICENSE.txt') (Join-Path $distribution 'LICENSE.txt')
Copy-Item (Join-Path $localSdk 'ThirdPartyNotices.txt') (Join-Path $distribution 'ThirdPartyNotices.txt')
$inventory | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $root 'licenses\fonts.json') -Encoding UTF8
