param(
    [string]$Version = "0.2.0"
)

$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent $PSScriptRoot
$sourceRoot = Join-Path $projectRoot 'dist\win-unpacked'
$distRoot = Join-Path $projectRoot 'dist'
$target = Join-Path $distRoot "YourCatDesktopPet-Portable-$Version.exe"
$sevenZip = Join-Path $env:ProgramFiles '7-Zip\7z.exe'
$sfxModule = Join-Path $env:ProgramFiles '7-Zip\7z.sfx'
$staging = Join-Path $distRoot ("portable-staging-" + [guid]::NewGuid().ToString('N'))

if (-not (Test-Path -LiteralPath $sourceRoot)) {
    throw "Missing unpacked app: $sourceRoot. Run npm run pack:cache first."
}
foreach ($dependency in @($sevenZip, $sfxModule)) {
    if (-not (Test-Path -LiteralPath $dependency)) {
        throw "Required 7-Zip component was not found: $dependency"
    }
}

$resolvedSource = [System.IO.Path]::GetFullPath($sourceRoot)
$resolvedDist = [System.IO.Path]::GetFullPath($distRoot)
$resolvedStaging = [System.IO.Path]::GetFullPath($staging)
if (-not $resolvedStaging.StartsWith($resolvedDist + [System.IO.Path]::DirectorySeparatorChar)) {
    throw "Refusing to stage outside dist: $resolvedStaging"
}

New-Item -ItemType Directory -Path $resolvedStaging | Out-Null
try {
    $files = Get-ChildItem -LiteralPath $resolvedSource -File -Recurse |
        Sort-Object FullName
    if ($files.Count -eq 0) {
        throw "No files found in unpacked app: $resolvedSource"
    }

    $archive = Join-Path $resolvedStaging 'payload.7z'
    $config = Join-Path $resolvedStaging 'sfx-config.txt'
    @(
        ';!@Install@!UTF-8!'
        'Title="Your Cat Desktop Pet"'
        'RunProgram="YourCatDesktopPet.exe"'
        'GUIMode="2"'
        ';!@InstallEnd@!'
    ) | Set-Content -LiteralPath $config -Encoding utf8

    & $sevenZip a -t7z -mx=9 $archive (Join-Path $resolvedSource '*')
    if ($LASTEXITCODE -ne 0) {
        throw "7-Zip could not create portable payload: $archive"
    }
    & $sevenZip t $archive
    if ($LASTEXITCODE -ne 0) {
        throw "7-Zip could not validate portable payload: $archive"
    }

    $output = [System.IO.File]::Open($target, [System.IO.FileMode]::Create, [System.IO.FileAccess]::Write)
    try {
        foreach ($part in @($sfxModule, $config, $archive)) {
            $input = [System.IO.File]::OpenRead($part)
            try { $input.CopyTo($output) }
            finally { $input.Dispose() }
        }
    } finally {
        $output.Dispose()
    }
    if (-not (Test-Path -LiteralPath $target) -or (Get-Item -LiteralPath $target).Length -lt 1MB) {
        throw "Portable executable was not created correctly: $target"
    }
    Write-Output "PORTABLE=$target"
} finally {
    if (Test-Path -LiteralPath $resolvedStaging) {
        Remove-Item -LiteralPath $resolvedStaging -Recurse -Force
    }
}
