# Builds both assemblies (1.6 / 1.7) and installs the mod into the RimWorld Mods folder.
# Usage: powershell -ExecutionPolicy Bypass -File build.ps1 [-GameDir "D:\Games\RimWorld"] [-NoDeploy]
param(
    [string]$GameDir = "C:\Program Files (x86)\Steam\steamapps\common\RimWorld",
    [switch]$NoDeploy
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot

dotnet build "$root\Source\TimeToGraze\TimeToGraze.csproj" -c Release --nologo
if ($LASTEXITCODE -ne 0) { throw "Build failed" }

if ($NoDeploy) { return }

$target = Join-Path $GameDir "Mods\TimeToGraze"
New-Item -ItemType Directory -Force $target | Out-Null

foreach ($item in @("About", "Common", "1.6", "1.7", "LoadFolders.xml")) {
    $src = Join-Path $root $item
    $dst = Join-Path $target $item
    if (Test-Path $src -PathType Container) {
        # About is copied without purging: the game writes PublishedFileId.txt there on the first
        # Workshop upload, and deleting it turns "Update" into a second, separate upload.
        $mode = if ($item -eq "About") { "/E" } else { "/MIR" }
        robocopy $src $dst $mode /NJH /NJS /NFL /NDL /NP | Out-Null
        if ($LASTEXITCODE -ge 8) { throw "Copy of $item failed" }
    } else {
        Copy-Item $src $dst -Force
    }
}
$global:LASTEXITCODE = 0
Write-Host "Installed to $target"
