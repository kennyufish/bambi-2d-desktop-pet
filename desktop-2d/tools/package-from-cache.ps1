$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent $PSScriptRoot
$cacheZip = Join-Path $env:LOCALAPPDATA 'electron\Cache\3978a3c4a2965533dc07f99112894e7e7f80c9ea0f13e2a48cd5a29593568fb2\electron-v40.10.2-win32-x64.zip'
$output = Join-Path $projectRoot 'dist\win-unpacked'
$staging = Join-Path $projectRoot ("dist\win-unpacked-staging-" + [guid]::NewGuid().ToString('N'))
$previousBase = Join-Path $projectRoot 'dist\win-unpacked-previous'
$previous = $previousBase
$backupIndex = 1
while (Test-Path -LiteralPath $previous) {
    $previous = "$previousBase-$backupIndex"
    $backupIndex += 1
}

if (-not (Test-Path -LiteralPath $cacheZip)) {
    throw "Cached Electron runtime not found: $cacheZip"
}

$resolvedProject = [System.IO.Path]::GetFullPath($projectRoot)
$resolvedOutput = [System.IO.Path]::GetFullPath($output)
$resolvedStaging = [System.IO.Path]::GetFullPath($staging)
$resolvedPrevious = [System.IO.Path]::GetFullPath($previous)
foreach ($candidate in @($resolvedOutput, $resolvedStaging, $resolvedPrevious)) {
    if (-not $candidate.StartsWith($resolvedProject + [System.IO.Path]::DirectorySeparatorChar)) {
        throw "Refusing to package outside project: $candidate"
    }
}

New-Item -ItemType Directory -Path $resolvedStaging -Force | Out-Null
Expand-Archive -LiteralPath $cacheZip -DestinationPath $resolvedStaging -Force

$electronExe = Join-Path $resolvedStaging 'electron.exe'
$productExe = Join-Path $resolvedStaging 'YourCatDesktopPet.exe'
Move-Item -LiteralPath $electronExe -Destination $productExe

$appOutput = Join-Path $resolvedStaging 'resources\app'
New-Item -ItemType Directory -Path $appOutput -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $projectRoot 'main.mjs') -Destination $appOutput
Copy-Item -LiteralPath (Join-Path $projectRoot 'preload.cjs') -Destination $appOutput
Copy-Item -LiteralPath (Join-Path $projectRoot 'package.json') -Destination $appOutput
Copy-Item -LiteralPath (Join-Path $projectRoot 'src') -Destination $appOutput -Recurse
Copy-Item -LiteralPath (Join-Path $projectRoot 'renderer') -Destination $appOutput -Recurse
Copy-Item -LiteralPath (Join-Path $projectRoot 'sprite-packs') -Destination $appOutput -Recurse

if (Test-Path -LiteralPath $resolvedOutput) {
    Move-Item -LiteralPath $resolvedOutput -Destination $resolvedPrevious
}
Move-Item -LiteralPath $resolvedStaging -Destination $resolvedOutput

Write-Output "PACKAGED=$(Join-Path $resolvedOutput 'YourCatDesktopPet.exe')"
