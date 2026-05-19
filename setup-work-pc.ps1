# Civil 3D MCP — One-shot setup script for Windows / Civil 3D 2026
# Run from any PowerShell window. Assumes Node.js + .NET 10 SDK are not yet installed.
# Usage: Right-click → "Run with PowerShell"  OR  in PowerShell: powershell -ExecutionPolicy Bypass -File .\setup-work-pc.ps1

$ErrorActionPreference = "Stop"

Write-Host "=== Civil 3D MCP — work-PC setup ===" -ForegroundColor Cyan

# ---------- 1. Check / install Git ----------
if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
    Write-Host "Git not found. Install from https://git-scm.com/download/win and re-run." -ForegroundColor Yellow
    exit 1
}
Write-Host "Git: $(git --version)" -ForegroundColor Green

# ---------- 2. Check / install Node.js ----------
if (-not (Get-Command node -ErrorAction SilentlyContinue)) {
    Write-Host "Node.js not found. Install LTS from https://nodejs.org/ and re-run." -ForegroundColor Yellow
    exit 1
}
Write-Host "Node: $(node --version)" -ForegroundColor Green

# ---------- 3. Check / install .NET 10 SDK ----------
$dotnetOk = $false
if (Get-Command dotnet -ErrorAction SilentlyContinue) {
    $sdks = dotnet --list-sdks 2>$null
    if ($sdks -match "^10\.") { $dotnetOk = $true }
}
if (-not $dotnetOk) {
    Write-Host ".NET 10 SDK not found. Install from https://dotnet.microsoft.com/download/dotnet/10.0 and re-run." -ForegroundColor Yellow
    exit 1
}
Write-Host ".NET SDK: $(dotnet --version)" -ForegroundColor Green

# ---------- 4. Clone / update repo ----------
$repoDir = Join-Path $HOME "Documents\civil3d-mcp"
if (Test-Path $repoDir) {
    Write-Host "Repo exists — pulling latest…" -ForegroundColor Cyan
    Push-Location $repoDir
    git pull
    Pop-Location
} else {
    Write-Host "Cloning repo to $repoDir…" -ForegroundColor Cyan
    git clone https://github.com/Mmpasta00/civil3d-mcp.git $repoDir
}

# ---------- 5. Build TypeScript MCP server ----------
Write-Host "Building MCP server (TypeScript)…" -ForegroundColor Cyan
Push-Location $repoDir
npm install
npm run build
Pop-Location

# ---------- 6. Build C# Civil 3D plugin ----------
Write-Host "Building Civil 3D plugin (C# .NET 10)…" -ForegroundColor Cyan
Push-Location (Join-Path $repoDir "plugin\Civil3dMcpPlugin")
dotnet build -c Release
Pop-Location

# ---------- 7. Locate built DLL ----------
$dll = Join-Path $repoDir "plugin\Civil3dMcpPlugin\bin\Release\net10.0-windows\Civil3dMcpPlugin.dll"
if (-not (Test-Path $dll)) {
    # Fallback to Debug if Release wasn't built
    $dll = Join-Path $repoDir "plugin\Civil3dMcpPlugin\bin\Debug\net10.0-windows\Civil3dMcpPlugin.dll"
}

Write-Host ""
Write-Host "=== Setup complete ===" -ForegroundColor Green
Write-Host ""
Write-Host "NEXT STEPS:" -ForegroundColor Yellow
Write-Host ""
Write-Host "1. In Civil 3D 2026, type NETLOAD and select:" -ForegroundColor White
Write-Host "   $dll" -ForegroundColor Cyan
Write-Host ""
Write-Host "2. In the Civil 3D command line, type C3DMCPSTATUS to verify the server is listening on port 8080." -ForegroundColor White
Write-Host ""
Write-Host "3. Edit your Claude Desktop config at:" -ForegroundColor White
Write-Host "   $env:APPDATA\Claude\claude_desktop_config.json" -ForegroundColor Cyan
Write-Host "   Use claude-desktop-config.example.json in this repo as a template." -ForegroundColor White
Write-Host "   Set the 'args' path to: $repoDir\build\index.js" -ForegroundColor White
Write-Host ""
Write-Host "4. Restart Claude Desktop, open a Civil 3D drawing, and ask Claude:" -ForegroundColor White
Write-Host "   ""What surfaces are in my drawing?""" -ForegroundColor Cyan
Write-Host ""
