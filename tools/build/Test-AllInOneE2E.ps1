param(
    [Parameter(Mandatory = $true)]
    [string]$AppVersion,
    [string]$AllInOneExe
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
Set-Location $root

if ([string]::IsNullOrWhiteSpace($AllInOneExe)) {
    $AllInOneExe = Join-Path $root "build\allinone\WAHU-Kids-Learn-Portable-AllInOne-win7-x86-$AppVersion.exe"
}
if (-not (Test-Path -LiteralPath $AllInOneExe -PathType Leaf)) {
    throw "Không tìm thấy All-in-One EXE: $AllInOneExe"
}

$reportPath = Join-Path $root 'build\allinone_e2e_dev.json'
$extractRoot = Join-Path $env:TEMP ('wahu-allinone-e2e-' + [Guid]::NewGuid().ToString('N'))
$bootstrapReport = Join-Path $env:TEMP ('wahu-allinone-bootstrap-' + [Guid]::NewGuid().ToString('N') + '.txt')
$installedRoot = Join-Path $env:LOCALAPPDATA 'WAHU Kids Learn'
$installedDb = Join-Path $installedRoot 'data\learning.db'

function Assert([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}

function Optional-Hash([string]$Path) {
    if (Test-Path -LiteralPath $Path -PathType Leaf) {
        return (Get-FileHash -Algorithm SHA256 -LiteralPath $Path).Hash
    }
    return $null
}

function Assert-ProductionArtPayload([string]$PayloadRoot) {
    $validator = Join-Path $root 'tools\build\Test-ProductionAssetPayload.ps1'
    Assert (Test-Path -LiteralPath $validator -PathType Leaf) 'production-art validator missing'
    $sourceManifestPath = Join-Path $root 'src\App\Assets\Generated\Ready\ASSET_SELECTION_MANIFEST.json'
    Assert (Test-Path -LiteralPath $sourceManifestPath -PathType Leaf) 'production-art source manifest missing'
    $sourceManifest = Get-Content -Raw -LiteralPath $sourceManifestPath | ConvertFrom-Json
    $expectedCount = [int]$sourceManifest.summary.totalSelected
    Assert ($expectedCount -gt 0) 'production-art source manifest has invalid totalSelected'
    $expectedManifestSha = (Get-FileHash -Algorithm SHA256 -LiteralPath $sourceManifestPath).Hash.ToUpperInvariant()
    $assetRoot = Join-Path $PayloadRoot 'Assets\Generated\Ready'
    & $validator -Mode Tree -Path $assetRoot -ExpectedManifestSha256 $expectedManifestSha
    return [ordered]@{ png_count = $expectedCount; manifest_sha256 = $expectedManifestSha }
}

$result = [ordered]@{}
$installedDbHashBefore = Optional-Hash $installedDb

try {
    New-Item -ItemType Directory -Force -Path $extractRoot | Out-Null
    $args = @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/SP-',('/DIR="' + $extractRoot + '"'))
    $setup = Start-Process -FilePath $AllInOneExe -ArgumentList $args -PassThru -Wait
    $result.setup_exit = $setup.ExitCode
    Assert ($setup.ExitCode -eq 0) "All-in-One setup exit $($setup.ExitCode)"

    foreach ($rel in @(
        'portable.mode',
        'WAHUKidsLearn.exe',
        'WAHUKidsLearn.exe.config',
        'WAHU.Data.dll',
        'System.Data.SQLite.dll',
        'e_sqlite3.dll',
        'config\install_manifest_v1.json',
        'data\schema\006_attempt_commit_key_immutability.sql',
        'content_packs\math_grade2_v1\question_bank_v1.json',
        'content_packs\math_quick_rescue_v1\manifest.json',
        'content_packs\math_quick_rescue_v1\learning_content_v1.json',
        'Assets\Generated\Ready\ASSET_SELECTION_MANIFEST.json'
    )) {
        Assert (Test-Path -LiteralPath (Join-Path $extractRoot $rel) -PathType Leaf) "All-in-One payload missing: $rel"
    }
    $productionArtEvidence = Assert-ProductionArtPayload $extractRoot
    $result.production_art_png_count = [int]$productionArtEvidence.png_count
    $result.production_art_manifest_sha256 = [string]$productionArtEvidence.manifest_sha256

    Assert (-not (Get-ChildItem -LiteralPath $extractRoot -Filter 'unins*.exe' -File -ErrorAction SilentlyContinue)) 'Portable All-in-One unexpectedly created an uninstaller.'
    Assert (-not (Test-Path -LiteralPath (Join-Path $extractRoot 'UserData'))) 'All-in-One payload unexpectedly contains learner UserData before first boot.'

    $runtimeManifest = Get-Content -Raw -LiteralPath (Join-Path $extractRoot 'config\install_manifest_v1.json') | ConvertFrom-Json
    Assert ($runtimeManifest.app_version -eq $AppVersion) "All-in-One runtime app_version mismatch: $($runtimeManifest.app_version)"

    $exe = Join-Path $extractRoot 'WAHUKidsLearn.exe'
    $boot = Start-Process -FilePath $exe -ArgumentList @('--bootstrap-smoke','--out',$bootstrapReport,'--portable') -PassThru -Wait
    $result.bootstrap_exit = $boot.ExitCode
    Assert ($boot.ExitCode -eq 0) "All-in-One portable bootstrap exit $($boot.ExitCode)"
    Assert (Test-Path -LiteralPath $bootstrapReport -PathType Leaf) 'All-in-One bootstrap report missing.'

    $text = Get-Content -Raw -LiteralPath $bootstrapReport
    foreach ($needle in @(
        'result=PASS',
        'storage_mode=PORTABLE',
        ('app_version=' + $AppVersion),
        'schema_version=6',
        'migration_version=6',
        'integrity=ok',
        'foreign_key_issues=0'
    )) {
        Assert ($text.Contains($needle)) "All-in-One bootstrap report missing: $needle"
    }

    $portableDb = Join-Path $extractRoot 'UserData\data\learning.db'
    Assert (Test-Path -LiteralPath $portableDb -PathType Leaf) 'All-in-One did not create portable learner DB under UserData.'
    $result.portable_db_sha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $portableDb).Hash

    $installedDbHashAfter = Optional-Hash $installedDb
    if ($installedDbHashBefore) {
        Assert ($installedDbHashBefore -eq $installedDbHashAfter) 'All-in-One portable E2E modified installed learner DB.'
    } else {
        Assert (-not $installedDbHashAfter) 'All-in-One portable E2E created installed learner DB.'
    }

    $result.installed_db_hash_before = $installedDbHashBefore
    $result.installed_db_hash_after = $installedDbHashAfter
    $result.all_in_one_bytes = (Get-Item -LiteralPath $AllInOneExe).Length
    $result.all_in_one_sha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $AllInOneExe).Hash
    $result.test_result = 'PASS'
}
catch {
    $result.test_result = 'FAIL'
    $result.error = $_.Exception.Message
    throw
}
finally {
    $result.tested_at_utc = [DateTime]::UtcNow.ToString('o')
    $result | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $reportPath -Encoding UTF8
    Remove-Item -LiteralPath $bootstrapReport -Force -ErrorAction SilentlyContinue
    if (Test-Path -LiteralPath $extractRoot) {
        Remove-Item -LiteralPath $extractRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}

$result | Format-List
Write-Host "ALL_IN_ONE_E2E_PASS report=$reportPath"
