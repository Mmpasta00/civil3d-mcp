<#
.SYNOPSIS
  Installs the Gradon Civil 3D add-in as an AutoCAD ApplicationPlugin bundle.

.DESCRIPTION
  Builds Civil3dMcpPlugin (Release) unless -SkipBuild is passed, copies the
  bundle into %APPDATA%\Autodesk\ApplicationPlugins\Gradon.bundle\Contents,
  and writes a default %APPDATA%\Gradon\civil3d.json config if one doesn't
  already exist (an existing config is never overwritten).

  Run on the Windows machine running Civil 3D 2026. Requires the .NET SDK to
  build from source unless -SkipBuild is passed with a prior build already
  in bin\Release\net10.0-windows.

.PARAMETER SkipBuild
  Skip `dotnet build` and copy whatever is already in bin\Release\net10.0-windows.
#>
param(
  [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"

$scriptDir    = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectDir   = Join-Path $scriptDir "Civil3dMcpPlugin"
$buildOutput  = Join-Path $projectDir "bin\Release\net10.0-windows"
$bundleSrc    = Join-Path $scriptDir "Gradon.bundle"
$bundleDest   = Join-Path $env:APPDATA "Autodesk\ApplicationPlugins\Gradon.bundle"
$contentsDest = Join-Path $bundleDest "Contents"

if (-not $SkipBuild) {
  Write-Host "Building Civil3dMcpPlugin (Release)..."
  dotnet build $projectDir -c Release
  if ($LASTEXITCODE -ne 0) { throw "Build failed." }
}

if (-not (Test-Path $buildOutput)) {
  throw "Build output not found at $buildOutput. Build the project first, or omit -SkipBuild."
}

Write-Host "Installing bundle to $bundleDest ..."
New-Item -ItemType Directory -Force -Path $contentsDest | Out-Null

Copy-Item -Path (Join-Path $bundleSrc "PackageContents.xml") -Destination $bundleDest -Force
Copy-Item -Path (Join-Path $buildOutput "*") -Destination $contentsDest -Recurse -Force

$configDir  = Join-Path $env:APPDATA "Gradon"
$configPath = Join-Path $configDir "civil3d.json"

if (-not (Test-Path $configPath)) {
  Write-Host "Writing default config to $configPath ..."
  New-Item -ItemType Directory -Force -Path $configDir | Out-Null
  $defaultConfig = @{
    serviceUrl = "http://localhost:8799"
    apiKey     = ""
    userId     = ""
    firmId     = ""
  } | ConvertTo-Json
  Set-Content -Path $configPath -Value $defaultConfig -Encoding UTF8
} else {
  Write-Host "Config already exists at $configPath — leaving it alone."
}

Write-Host ""
Write-Host "Done. Start Civil 3D 2026 and run GRADON to open the chat palette."
