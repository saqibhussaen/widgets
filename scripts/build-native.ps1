param([Parameter(Mandatory=$true)][string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$vsPath = & $vswhere -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
if (!$vsPath) { throw 'Install Visual Studio C++ build tools and the Windows 11 SDK.' }
$vcvars = Join-Path $vsPath 'VC\Auxiliary\Build\vcvars64.bat'
New-Item -ItemType Directory -Force $OutputDirectory | Out-Null
$output = (Resolve-Path $OutputDirectory).Path
$source = Join-Path $root 'native\ClockFont.cpp'
$dll = Join-Path $output 'ClockFont.dll'
$stagedDll = Join-Path $output 'ClockFont.build.dll'
$obj = Join-Path $output 'ClockFont.obj'
$lib = Join-Path $output 'ClockFont.lib'
$batch = Join-Path $output 'compile.cmd'
$imports = Join-Path $output 'imports.txt'
$version = [version]([xml](Get-Content (Join-Path $root 'Directory.Build.props') -Raw)).Project.PropertyGroup.Version
$versionText = $version.ToString()
$versionNumbers = "$($version.Major),$($version.Minor),$([Math]::Max(0,$version.Build)),0"
$resource = Join-Path $output 'version.rc'
$compiledResource = Join-Path $output 'version.res'
@"
#include <winver.h>
1 VERSIONINFO
FILEVERSION $versionNumbers
PRODUCTVERSION $versionNumbers
FILEOS VOS_NT_WINDOWS32
FILETYPE VFT_DLL
BEGIN
  BLOCK "StringFileInfo"
  BEGIN
    BLOCK "040904B0"
    BEGIN
      VALUE "CompanyName", "Saqib Hussain\0"
      VALUE "FileDescription", "Widgets variable font renderer\0"
      VALUE "FileVersion", "$versionText\0"
      VALUE "InternalName", "ClockFont\0"
      VALUE "LegalCopyright", "Copyright (c) 2026 Saqib Hussain\0"
      VALUE "OriginalFilename", "ClockFont.dll\0"
      VALUE "ProductName", "Widgets\0"
      VALUE "ProductVersion", "$versionText\0"
    END
  END
  BLOCK "VarFileInfo"
  BEGIN
    VALUE "Translation", 0x0409, 1200
  END
END
"@ | Set-Content -LiteralPath $resource -Encoding ASCII
@"
@echo off
call "$vcvars" >nul
if errorlevel 1 exit /b 1
rc /nologo /fo "$compiledResource" "$resource"
if errorlevel 1 exit /b 1
cl /nologo /std:c++17 /O2 /Oi /GS- /GR- /Zl /LD /W4 /WX "$source" /Fo"$obj" /link /NODEFAULTLIB /NOENTRY /OUT:"$stagedDll" /IMPLIB:"$lib" "$compiledResource" dwrite.lib
if errorlevel 1 exit /b 1
dumpbin /nologo /dependents "$stagedDll" > "$imports"
"@ | Set-Content -LiteralPath $batch -Encoding ASCII
& $env:ComSpec /d /c call $batch
if ($LASTEXITCODE -ne 0) { throw 'Native DirectWrite build failed.' }
$importedDlls = Select-String -Path $imports -Pattern '^\s+([\w.-]+\.dll)\s*$' | ForEach-Object { $_.Matches[0].Groups[1].Value }
if (!$importedDlls -or @($importedDlls | Where-Object { $_ -ine 'DWrite.dll' }).Count -gt 0) { throw 'Unreviewed native dependency; inspect imports.txt and update license inventory.' }
Copy-Item -LiteralPath $stagedDll -Destination $dll -Force
