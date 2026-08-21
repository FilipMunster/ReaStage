<#
.SYNOPSIS
    Publishes ReaStage and packs each build into a zip in the repository root.

.DESCRIPTION
    Runs dotnet publish through the publish profiles in
    ReaStage/Properties/PublishProfiles, so the self-contained and single-file
    settings stay defined in one place. The version in the archive name comes from
    <Version> in ReaStage.csproj.

.PARAMETER Platform
    Which package to build: win-x64, linux-x64 or all (default).

.EXAMPLE
    .\publish.ps1
    .\publish.ps1 -Platform linux-x64
#>
[CmdletBinding()]
param(
    [ValidateSet('win-x64', 'linux-x64', 'all')]
    [string]$Platform = 'all'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# Windows PowerShell needs this loaded; on PowerShell 7 it is already in place
Add-Type -AssemblyName System.IO.Compression.FileSystem -ErrorAction SilentlyContinue

$projectPath = Join-Path $PSScriptRoot 'ReaStage\ReaStage.csproj'
if (-not (Test-Path -LiteralPath $projectPath))
{
    throw "Project not found: $projectPath"
}

# Publish profile per platform; the profile decides self-contained and single-file
$profiles = @{
    'win-x64'   = 'Win_x64'
    'linux-x64' = 'Linux_x64'
}

$targets = if ($Platform -eq 'all') { $profiles.Keys | Sort-Object } else { @($Platform) }

function Get-ProjectVersion
{
    param([string]$Path)

    # XPath rather than property access: only one PropertyGroup carries Version and
    # Set-StrictMode rejects reading a property the others do not have
    $xml = [xml](Get-Content -Raw -LiteralPath $Path)
    $node = $xml.SelectSingleNode('/Project/PropertyGroup/Version')

    if (-not $node)
    {
        throw "No <Version> found in $Path"
    }

    return $node.InnerText.Trim()
}

# A zip written on Windows records "made by FAT", so Unix permission bits in the
# entries are ignored by unzip - setting them only clobbered the DOS attributes and
# made things worse. The Linux install steps therefore invoke the scripts through
# bash (no execute bit needed) and install-ReaStage.sh chmods the app itself.
function New-Package
{
    param(
        [string]$SourceDir,
        [string]$ZipPath,
        [string]$RootName
    )

    if (Test-Path -LiteralPath $ZipPath)
    {
        Remove-Item -LiteralPath $ZipPath -Force
    }

    # Built entry by entry rather than with CreateFromDirectory, so every file sits
    # under one folder in the archive and can carry a Unix mode
    $archive = [System.IO.Compression.ZipFile]::Open($ZipPath, 'Create')
    try
    {
        $prefixLength = $SourceDir.TrimEnd('\', '/').Length + 1

        foreach ($file in Get-ChildItem -LiteralPath $SourceDir -Recurse -File)
        {
            $relative = $file.FullName.Substring($prefixLength).Replace('\', '/')
            [void][System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
                $archive, $file.FullName, "$RootName/$relative", 'Optimal')
        }
    }
    finally
    {
        $archive.Dispose()
    }
}

$version = Get-ProjectVersion -Path $projectPath
Write-Host "ReaStage $version" -ForegroundColor Cyan

$results = @()

foreach ($rid in $targets)
{
    $publishProfile = $profiles[$rid]
    $publishDir = Join-Path $PSScriptRoot "ReaStage\bin\Release\net10.0\publish\$rid"

    Write-Host ""
    Write-Host "=== Publishing $rid ($publishProfile) ===" -ForegroundColor Cyan

    # Old files are not removed by publish and would end up in the archive
    if (Test-Path -LiteralPath $publishDir)
    {
        Remove-Item -LiteralPath $publishDir -Recurse -Force
    }

    dotnet publish $projectPath "-p:PublishProfile=$publishProfile" -o $publishDir
    if ($LASTEXITCODE -ne 0)
    {
        throw "dotnet publish failed for $rid (exit code $LASTEXITCODE)"
    }

    $name = "ReaStage_${version}_${rid}"
    $zipPath = Join-Path $PSScriptRoot "$name.zip"

    Write-Host "Packing $name.zip" -ForegroundColor Cyan
    New-Package -SourceDir $publishDir -ZipPath $zipPath -RootName $name

    $results += [pscustomobject]@{
        Platform = $rid
        Zip      = Split-Path -Leaf $zipPath
        SizeMB   = [math]::Round((Get-Item -LiteralPath $zipPath).Length / 1MB, 1)
    }
}

Write-Host ""
Write-Host "=== Done ===" -ForegroundColor Green
$results | Format-Table -AutoSize
