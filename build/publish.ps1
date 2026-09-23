<#
.SYNOPSIS
    Builds a self-contained, single-file release of CalendarShot.

.DESCRIPTION
    Produces artifacts\CalendarShot-<version>-win-x64.zip containing CalendarShot.exe,
    plus a .sha256 file with the archive checksum. The executable includes the .NET
    runtime, so users do not need to install anything else.

.EXAMPLE
    ./build/publish.ps1
    ./build/publish.ps1 -Version 1.2.0
#>
[CmdletBinding()]
param(
    [string] $Configuration = 'Release',
    [string] $Runtime = 'win-x64',
    [string] $Version = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'src/CalendarShot/CalendarShot.csproj'
$artifacts = Join-Path $root 'artifacts'
$output = Join-Path $artifacts "publish-$Runtime"

if (Test-Path $output) {
    Remove-Item $output -Recurse -Force
}

$arguments = @(
    'publish', $project,
    '--configuration', $Configuration,
    '--runtime', $Runtime,
    '--self-contained', 'true',
    '--output', $output,
    '-p:PublishSingleFile=true',
    '-p:IncludeNativeLibrariesForSelfExtract=true',
    '-p:EnableCompressionInSingleFile=true',
    '-p:DebugType=none'
)

if ($Version) {
    $arguments += "-p:Version=$Version"
}
else {
    $Version = (Select-Xml -Path (Join-Path $root 'Directory.Build.props') -XPath '//Version' | Select-Object -First 1).Node.InnerText
}

& dotnet @arguments
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

Copy-Item (Join-Path $root 'LICENSE') $output
Copy-Item (Join-Path $root 'docs/USER_MANUAL.md') $output

$zip = Join-Path $artifacts "CalendarShot-$Version-$Runtime.zip"
if (Test-Path $zip) {
    Remove-Item $zip -Force
}

Compress-Archive -Path (Join-Path $output '*') -DestinationPath $zip
$hash = (Get-FileHash $zip -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content -Path "$zip.sha256" -Value "$hash  $(Split-Path -Leaf $zip)" -Encoding ascii

Write-Host "Created $zip"
Write-Host "SHA-256 $hash"
