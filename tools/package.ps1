# Build + package a CKAN-ready release zip.
# The zip intentionally contains only the GameData folder so NetKAN can resolve
# "find: ResupplyNetwork / install_to: GameData" without leftover files.
param([string]$Version = '1.0.0')

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$dotnet = Join-Path $env:USERPROFILE '.dotnet\dotnet.exe'
if (-not (Test-Path $dotnet)) { $dotnet = 'dotnet' }

$proj = Join-Path $repo 'src\ResupplyNetwork\ResupplyNetwork.csproj'
& $dotnet build $proj -c Release
if ($LASTEXITCODE -ne 0) { throw 'build failed' }

$build = Join-Path $repo 'build'
$stage = Join-Path $build ('stage-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Force -Path $stage | Out-Null

Copy-Item -Path (Join-Path $repo 'GameData') -Destination $stage -Recurse -Force

$pluginOut = Join-Path $stage 'GameData\ResupplyNetwork\Plugins'
New-Item -ItemType Directory -Force -Path $pluginOut | Out-Null
Copy-Item -Path (Join-Path $repo 'GameData\ResupplyNetwork\Plugins\ResupplyNetwork.dll') -Destination (Join-Path $pluginOut 'ResupplyNetwork.dll') -Force

$zip = Join-Path $build ("ResupplyNetwork-$Version.zip")
if (Test-Path $zip) { [System.IO.File]::Delete($zip) }

# Build the archive by hand so entry names use forward slashes (ZIP spec);
# ZipFile.CreateFromDirectory on .NET Framework emits backslashes, which
# CKAN/NetKAN validators may reject.
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zipStream = [System.IO.File]::Open($zip, [System.IO.FileMode]::Create)
$archive = New-Object System.IO.Compression.ZipArchive($zipStream, [System.IO.Compression.ZipArchiveMode]::Create)
Get-ChildItem -LiteralPath $stage -Recurse -File | ForEach-Object {
    $rel = $_.FullName.Substring($stage.Length + 1).Replace('\', '/')
    $entry = $archive.CreateEntry($rel, [System.IO.Compression.CompressionLevel]::Optimal)
    $out = $entry.Open()
    $in = [System.IO.File]::OpenRead($_.FullName)
    $in.CopyTo($out)
    $in.Dispose()
    $out.Dispose()
}
$archive.Dispose()
$zipStream.Dispose()

Write-Output ('zip: ' + $zip)
Get-ChildItem -LiteralPath $zip | ForEach-Object { Write-Output ('size: ' + $_.Length) }
