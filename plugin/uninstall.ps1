<#
.SYNOPSIS
  Removes the Gradon Civil 3D add-in bundle.

.DESCRIPTION
  Deletes %APPDATA%\Autodesk\ApplicationPlugins\Gradon.bundle. Leaves the
  user's %APPDATA%\Gradon\civil3d.json config in place so reinstalling
  doesn't lose their service URL / API key.
#>

$ErrorActionPreference = "Stop"

$bundleDest = Join-Path $env:APPDATA "Autodesk\ApplicationPlugins\Gradon.bundle"

if (Test-Path $bundleDest) {
  Write-Host "Removing $bundleDest ..."
  Remove-Item -Path $bundleDest -Recurse -Force
  Write-Host "Done. Restart Civil 3D if it's currently running."
} else {
  Write-Host "Nothing to remove — $bundleDest does not exist."
}
