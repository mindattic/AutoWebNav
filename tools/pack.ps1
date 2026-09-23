#Requires -Version 5.1
<#
.SYNOPSIS
    Packs AutoWebNav and AutoWebNav.WebView2 (Release) into C:\LocalNuGet, the local source every
    MindAttic consumer's NuGet.config lists. Optionally also copies them into a consumer repo's
    checked-in package folder (Prose keeps lib/local-packages so CI can restore without C:\LocalNuGet).

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File tools\pack.ps1
    powershell -ExecutionPolicy Bypass -File tools\pack.ps1 -CopyTo D:\Projects\MindAttic\Prose\lib\local-packages
#>
param([string]$Output = 'C:\LocalNuGet', [string[]]$CopyTo = @())

$ErrorActionPreference = 'Stop'
$repoDir = Split-Path $PSScriptRoot

dotnet test (Join-Path $repoDir 'AutoWebNav.Tests') --configuration Release | Out-Host
if ($LASTEXITCODE -ne 0) { Write-Host 'Tests failed - not packing.' -ForegroundColor Red; exit $LASTEXITCODE }

New-Item -ItemType Directory -Force $Output | Out-Null
foreach ($proj in @('AutoWebNav\AutoWebNav.csproj', 'AutoWebNav.WebView2\AutoWebNav.WebView2.csproj')) {
    dotnet pack (Join-Path $repoDir $proj) --configuration Release --output $Output | Out-Host
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

[xml]$props = Get-Content (Join-Path $repoDir 'Directory.Build.props')
$version = $props.Project.PropertyGroup.Version
foreach ($dest in $CopyTo) {
    New-Item -ItemType Directory -Force $dest | Out-Null
    Copy-Item (Join-Path $Output "AutoWebNav.$version.nupkg"), (Join-Path $Output "AutoWebNav.WebView2.$version.nupkg") $dest -Force
    Write-Host "  Copied $version packages to $dest" -ForegroundColor Gray
}
Write-Host "  Packed AutoWebNav $version -> $Output" -ForegroundColor Green
