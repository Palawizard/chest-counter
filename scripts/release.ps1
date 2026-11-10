# Builds the installer and update packages, and optionally uploads them as a draft GitHub release.
# Usage: scripts\release.ps1 [-Upload]
param([switch]$Upload)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$repo = 'https://github.com/Palawizard/chest-counter'
$project = Join-Path $root 'src\ChestCounter\ChestCounter.csproj'
$publish = Join-Path $root 'dist\publish'
$releases = Join-Path $root 'dist\releases'

$version = ([xml](Get-Content $project)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
if (-not $version) { throw "No <Version> in $project" }

function Invoke-Checked {
    & $args[0] $args[1..($args.Count - 1)]
    if ($LASTEXITCODE -ne 0) { throw "$($args[0]) failed with exit code $LASTEXITCODE" }
}

if (Test-Path $publish) { Remove-Item $publish -Recurse -Force }
Invoke-Checked dotnet publish $project -c Release -r win-x64 --self-contained -o $publish --nologo -v q

# Previous release lets vpk build a delta package; the first release has none.
& vpk download github --repoUrl $repo -o $releases
if ($LASTEXITCODE -ne 0) { Write-Warning 'No previous release downloaded; building a full package only.' }

Invoke-Checked vpk pack `
    --packId Palawi.ChestCounter `
    --packVersion $version `
    --packDir $publish `
    --mainExe ChestCounter.exe `
    --packTitle 'Chest Counter' `
    --packAuthors Palawi `
    --runtime win-x64 `
    --icon (Join-Path $root 'src\ChestCounter\app.ico') `
    --splashImage (Join-Path $root 'assets\splash.png') `
    --shortcuts 'Desktop,StartMenuRoot' `
    -o $releases

if ($Upload) {
    $token = gh auth token
    Invoke-Checked vpk upload github --repoUrl $repo --token $token -o $releases --tag "v$version" --releaseName "Chest Counter $version"
    Write-Host "Draft release v$version uploaded. Review it on GitHub, then publish."
}

Write-Host "Built Chest Counter $version in $releases"
