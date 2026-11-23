[CmdletBinding()]
param(
    [string]$TestFilter = '',
    [switch]$SkipTests
)

$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$artifacts = 'D:\FF14\Mods'
$current = Join-Path $artifacts 'AnoMesh'
$next = Join-Path $artifacts '.AnoMesh.next'
$previous = Join-Path $artifacts '.AnoMesh.previous'
$nextZip = Join-Path $artifacts '.AnoMesh.next.zip'
$zip = Join-Path $current 'AnoMech-custom-Yoruha.zip'

function Assert-OwnedPath([string]$Path) {
    $absolute = [IO.Path]::GetFullPath($Path)
    $allowed = @($current, $next, $previous, $nextZip, $zip)
    if ($absolute -notin $allowed -or !$absolute.StartsWith($artifacts + '\', [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to clean an unowned path: $absolute"
    }
    foreach ($parent in @($repo, $artifacts)) {
        if ((Test-Path -LiteralPath $parent) -and ((Get-Item -LiteralPath $parent -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
            throw "Refusing artifact operations through a reparse point: $parent"
        }
    }
    if (Test-Path -LiteralPath $absolute) {
        $items = @((Get-Item -LiteralPath $absolute -Force))
        if (Test-Path -LiteralPath $absolute -PathType Container) {
            $items += @(Get-ChildItem -LiteralPath $absolute -Force -Recurse)
        }
        if ($items | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint }) {
            throw "Refusing to clean artifacts containing a reparse point: $absolute"
        }
    }
}

function Remove-OwnedPath([string]$Path) {
    Assert-OwnedPath $Path
    if (Test-Path -LiteralPath $Path) { Remove-Item -LiteralPath $Path -Recurse -Force }
}

$portable = Join-Path (Split-Path $repo -Parent) '.dotnet\dotnet.exe'
if (Test-Path -LiteralPath $portable) {
    $dotnet = $portable
    $env:DOTNET_ROOT = Split-Path $portable -Parent
} else {
    $dotnet = (Get-Command dotnet -ErrorAction Stop).Source
}

Push-Location $repo
try {
    if (!$SkipTests) {
        $testArgs = @('test', 'tests/AnoMech.Tests/AnoMech.Tests.csproj', '-c', 'Release', '--logger', 'trx;LogFileName=latest-custom.trx')
        if ($TestFilter) { $testArgs += @('--filter', $TestFilter) }
        & $dotnet @testArgs
        if ($LASTEXITCODE -ne 0) { throw 'Tests failed; the last published package was retained.' }
    }
    & $dotnet build 'AnoMech/AnoMech.csproj' '-c' 'Release' '-t:Rebuild'
    if ($LASTEXITCODE -ne 0) { throw 'Rebuild failed; the last published package was retained.' }

    foreach ($path in @($current, $next, $previous, $nextZip, $zip)) { Assert-OwnedPath $path }
    New-Item -ItemType Directory -Path $artifacts -Force | Out-Null
    if (!(Test-Path -LiteralPath $current) -and (Test-Path -LiteralPath $previous)) {
        Move-Item -LiteralPath $previous -Destination $current
    }
    Remove-OwnedPath $next
    Remove-OwnedPath $nextZip
    New-Item -ItemType Directory -Path $next | Out-Null
    $build = Join-Path $repo 'AnoMech\bin\Release'
    Get-ChildItem -LiteralPath $build -File |
        Where-Object Extension -in '.dll', '.json', '.pdb' |
        Copy-Item -Destination $next
    $dll = Join-Path $next 'AnoMech.dll'
    $hash = (Get-FileHash -LiteralPath $dll -Algorithm SHA256).Hash
    if ($hash -ne (Get-FileHash -LiteralPath (Join-Path $build 'AnoMech.dll')).Hash) { throw 'DLL copy verification failed.' }
    $info = [ordered]@{
        project = $repo
        builtUtc = [DateTime]::UtcNow.ToString('o')
        revision = (& git rev-parse HEAD | Out-String).Trim()
        dllSha256 = $hash
        tests = $(if ($SkipTests) { 'Skipped; see prior verification' } elseif ($TestFilter) { $TestFilter } else { 'Full plugin test suite' })
    }
    $info | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $next 'build-info.json') -Encoding utf8
    Compress-Archive -Path (Join-Path $next '*') -DestinationPath $nextZip -Force
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [IO.Compression.ZipFile]::OpenRead($nextZip)
    try {
        $stream = $archive.GetEntry('AnoMech.dll').Open()
        $sha = [Security.Cryptography.SHA256]::Create()
        try { $zipHash = [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-', '') }
        finally { $sha.Dispose(); $stream.Dispose() }
    } finally { $archive.Dispose() }
    if ($zipHash -ne $hash) { throw 'ZIP verification failed; the last published package was retained.' }

    Move-Item -LiteralPath $nextZip -Destination (Join-Path $next 'AnoMech-custom-Yoruha.zip') -Force
    Remove-OwnedPath $previous
    if (Test-Path -LiteralPath $current) { Move-Item -LiteralPath $current -Destination $previous }
    try {
        Move-Item -LiteralPath $next -Destination $current
    } catch {
        Remove-OwnedPath $current
        if (Test-Path -LiteralPath $previous) { Move-Item -LiteralPath $previous -Destination $current }
        throw
    }
    Remove-OwnedPath $previous
    Write-Host "DLL: $(Join-Path $current 'AnoMech.dll')"
    Write-Host "ZIP: $zip"
    Write-Host "SHA256: $hash"
} finally {
    Pop-Location
}
