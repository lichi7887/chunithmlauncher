param(
  [string]$Configuration = "Release",
  [string]$Runtime = "win-x64",
  [string]$OutputRoot = "路径",
  [string]$VersionPrefix = "1.0.0",
  [bool]$EnableReadyToRun = $false
)

$ErrorActionPreference = "Stop"

$project = "路径"
$stamp = Get-Date -Format "yyyyMMdd_HHmmss"
$output = Join-Path $OutputRoot $stamp
$versionSuffix = Get-Date -Format "yyyyMMdd.HHmm"
$version = "$VersionPrefix+$versionSuffix"

Write-Host "Publishing $project" -ForegroundColor Cyan
Write-Host "Version: $version" -ForegroundColor Cyan
Write-Host "Output: $output" -ForegroundColor Cyan
Write-Host "ReadyToRun: $EnableReadyToRun" -ForegroundColor Cyan

$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"

# Use installed runtime (framework-dependent), single-file for easier distribution.
dotnet publish $project `
  -c $Configuration `
  -r $Runtime `
  --self-contained false `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:PublishReadyToRun=$EnableReadyToRun `
  -p:UseAppHost=true `
  -p:Version=$version `
  -p:FileVersion=$VersionPrefix `
  -p:AssemblyVersion=$VersionPrefix `
  -o $output

Write-Host "Done. Output: $output" -ForegroundColor Green
