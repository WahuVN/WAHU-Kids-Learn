param(
    [Parameter(Mandatory = $true)]
    [string]$AppVersion,
    [string]$PortableZip,
    [string]$ReleaseManifestPath,
    [string]$OutputDirectory
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
Set-Location $root

function Assert-True([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}
function Sha256([string]$Path) { return (Get-FileHash -Algorithm SHA256 -LiteralPath $Path).Hash.ToUpperInvariant() }

if ([string]::IsNullOrWhiteSpace($ReleaseManifestPath)) { $ReleaseManifestPath = Join-Path $root 'build\win7_x86\release_manifest_dev.json' }
Assert-True (Test-Path -LiteralPath $ReleaseManifestPath -PathType Leaf) "Release manifest not found: $ReleaseManifestPath"
$manifest = Get-Content -Raw -LiteralPath $ReleaseManifestPath | ConvertFrom-Json
Assert-True ([int]$manifest.schema_version -eq 6) 'Win7 validation pack requires release manifest schema V6.'
Assert-True ([string]$manifest.app_version -eq $AppVersion) "Release manifest version mismatch: $($manifest.app_version)"
Assert-True (-not [string]::IsNullOrWhiteSpace([string]$manifest.git_commit)) 'Release manifest git_commit is missing.'
Assert-True (-not [string]::IsNullOrWhiteSpace([string]$manifest.production_art.manifest_sha256)) 'Release manifest production-art SHA is missing.'

if ([string]::IsNullOrWhiteSpace($PortableZip)) { $PortableZip = Join-Path $root ([string]$manifest.portable.zip) }
Assert-True (Test-Path -LiteralPath $PortableZip -PathType Leaf) "Portable ZIP not found: $PortableZip"
$portableSha = Sha256 $PortableZip
Assert-True ($portableSha -eq ([string]$manifest.portable.zip_sha256).ToUpperInvariant()) 'Portable ZIP SHA does not match release manifest.'

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) { $OutputDirectory = Join-Path $root "build\win7-target\WAHU-Win7-Validation-$AppVersion" }
if (Test-Path -LiteralPath $OutputDirectory) { Remove-Item -LiteralPath $OutputDirectory -Recurse -Force }
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null

$targetScript = Join-Path $root 'tools\build\Test-Win7Target.ps1'
Assert-True (Test-Path -LiteralPath $targetScript -PathType Leaf) 'Test-Win7Target.ps1 is missing.'
$zipName = Split-Path $PortableZip -Leaf
Copy-Item -LiteralPath $PortableZip -Destination (Join-Path $OutputDirectory $zipName) -Force
Copy-Item -LiteralPath $targetScript -Destination (Join-Path $OutputDirectory 'Test-Win7Target.ps1') -Force

$metadata = [ordered]@{
    schema_version = 1
    app_version = $AppVersion
    git_commit = [string]$manifest.git_commit
    portable_file = $zipName
    portable_sha256 = $portableSha
    production_art_manifest_sha256 = ([string]$manifest.production_art.manifest_sha256).ToUpperInvariant()
    release_manifest_schema = [int]$manifest.schema_version
    generated_at_utc = [DateTime]::UtcNow.ToString('o')
    target_policy = 'Windows 7 SP1 + .NET Framework 4.8 + x86 runtime + schema V6'
}
$metadataPath = Join-Path $OutputDirectory 'VALIDATION_METADATA.json'
$metadata | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $metadataPath -Encoding UTF8

$cmd = @"
@echo off
setlocal
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Test-Win7Target.ps1" -PortableZip "%~dp0$zipName" -AppVersion "$AppVersion" -ExpectedPortableSha256 "$portableSha" -ExpectedGitCommit "$([string]$manifest.git_commit)" -ExpectedProductionArtManifestSha256 "$(([string]$manifest.production_art.manifest_sha256).ToUpperInvariant())" -ReportPath "%~dp0win7-target-report.json"
set RC=%ERRORLEVEL%
echo.
if "%RC%"=="0" (
  echo WIN7 TARGET TEST PASS
  echo Gui file win7-target-report.json ve may build de import evidence.
) else (
  echo WIN7 TARGET TEST FAIL - code %RC%
  echo Gui file win7-target-report.json de xem loi.
)
pause
exit /b %RC%
"@
$cmdPath = Join-Path $OutputDirectory 'RUN-WIN7-VALIDATION.cmd'
[IO.File]::WriteAllText($cmdPath, $cmd, [Text.Encoding]::ASCII)

$readme = @"
WAHU KIDS LEARN - WINDOWS 7 TARGET VALIDATION

1. Copy/extract this whole folder onto the real Windows 7 SP1 machine.
2. Double-click RUN-WIN7-VALIDATION.cmd.
3. The test must open the real WAHU child UI briefly and will then close it.
4. Copy win7-target-report.json back to the build machine.
5. On the build machine run:
   powershell -File tools\build\Import-Win7TargetEvidence.ps1 -ReportPath <path-to-report> -Apply

The target test is portable and must not install/uninstall WAHU or mutate installed learner data.
It will refuse non-Windows-7-SP1 targets, missing .NET Framework 4.8, wrong artifact SHA,
wrong schema/content evidence, non-x86 app runtime, failed bootstrap, or missing child UI window.

AppVersion: $AppVersion
Git commit: $([string]$manifest.git_commit)
Portable SHA256: $portableSha
Production-art manifest SHA256: $(([string]$manifest.production_art.manifest_sha256).ToUpperInvariant())
"@
[IO.File]::WriteAllText((Join-Path $OutputDirectory 'README-WIN7-VALIDATION.txt'), $readme, (New-Object Text.UTF8Encoding($false)))

$packRoot = Split-Path $OutputDirectory -Parent
New-Item -ItemType Directory -Force -Path $packRoot | Out-Null
$packZip = $OutputDirectory + '.zip'
if (Test-Path -LiteralPath $packZip) { Remove-Item -LiteralPath $packZip -Force }
Compress-Archive -Path (Join-Path $OutputDirectory '*') -DestinationPath $packZip -CompressionLevel Optimal
$packSha = Sha256 $packZip
Set-Content -LiteralPath ($packZip + '.sha256') -Value "$packSha  $(Split-Path $packZip -Leaf)" -Encoding ASCII

Write-Host "WIN7_VALIDATION_PACK_PASS folder=$OutputDirectory"
Write-Host "WIN7_VALIDATION_PACK_ZIP=$packZip"
Write-Host "WIN7_VALIDATION_PACK_SHA256=$packSha"
