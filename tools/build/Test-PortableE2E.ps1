param(
    [string]$AppVersion = '0.1.5-dev',
    [string]$PortableZip
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
Set-Location $root

if ([string]::IsNullOrWhiteSpace($PortableZip)) {
    $PortableZip = Join-Path $root "build\portable\WAHU-Kids-Learn-Portable-win7-x86-$AppVersion.zip"
}
if (-not (Test-Path -LiteralPath $PortableZip -PathType Leaf)) { throw "Không tìm thấy portable ZIP: $PortableZip" }

$reportPath = Join-Path $root 'build\portable_e2e_dev.json'
$extractRoot = Join-Path $env:TEMP ('wahu-portable-e2e-' + [Guid]::NewGuid().ToString('N'))
$bootstrapReport = Join-Path $env:TEMP ('wahu-portable-bootstrap-' + [Guid]::NewGuid().ToString('N') + '.txt')
$bootstrapReport2 = Join-Path $env:TEMP ('wahu-portable-bootstrap2-' + [Guid]::NewGuid().ToString('N') + '.txt')
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
    $assetRoot = Join-Path $PayloadRoot 'Assets\Generated\Ready'
    $manifestPath = Join-Path $assetRoot 'ASSET_SELECTION_MANIFEST.json'
    Assert (Test-Path -LiteralPath $manifestPath -PathType Leaf) 'portable production-art manifest missing'
    $manifest = Get-Content -Raw -LiteralPath $manifestPath | ConvertFrom-Json
    $assets = @($manifest.assets)
    Assert ($assets.Count -eq 74) "portable production-art manifest count $($assets.Count)"
    $pngFiles = @(Get-ChildItem -LiteralPath $assetRoot -Recurse -Filter *.png -File)
    Assert ($pngFiles.Count -eq 74) "portable production-art PNG count $($pngFiles.Count)"
    foreach ($asset in $assets) {
        $rel = ([string]$asset.finalPath).Replace('\\','\').Replace('/','\')
        $path = Join-Path $assetRoot $rel
        Assert (Test-Path -LiteralPath $path -PathType Leaf) "portable production-art file missing: $rel"
        $actualSha = (Get-FileHash -Algorithm SHA256 -LiteralPath $path).Hash.ToUpperInvariant()
        Assert ($actualSha -eq ([string]$asset.sha256).ToUpperInvariant()) "portable production-art SHA mismatch: $rel"
    }
    return [ordered]@{
        png_count = $pngFiles.Count
        manifest_sha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $manifestPath).Hash.ToUpperInvariant()
    }
}

$result = [ordered]@{}
$installedRootExistedBefore = Test-Path -LiteralPath $installedRoot
$installedDbHashBefore = Optional-Hash $installedDb

try {
    New-Item -ItemType Directory -Force -Path $extractRoot | Out-Null
    Expand-Archive -LiteralPath $PortableZip -DestinationPath $extractRoot -Force

    $required = @(
        'portable.mode',
        'WAHUKidsLearn.exe',
        'WAHUKidsLearn.exe.config',
        'WAHU.Platform.dll',
        'WAHU.Learning.dll',
        'WAHU.Motion.dll',
        'WAHU.Content.dll',
        'WAHU.Audio.dll',
        'WAHU.Security.dll',
        'WAHU.Performance.dll',
        'WAHU.Session.dll',
        'WAHU.Data.dll',
        'WAHU.Updater.exe',
        'System.Data.SQLite.dll',
        'e_sqlite3.dll',
        'data\schema\001_initial.sql',
        'data\schema\002_attempt_immutability.sql',
        'data\schema\003_math_attempt_idempotency_runtime.sql',
        'data\schema\004_math_lesson_progress.sql',
        'data\schema\005_math_runtime_pack_identity.sql',
        'content_packs\math_grade2_v1\manifest.json',
        'content_packs\math_grade2_v1\verified_templates_v1.json',
        'content_packs\math_grade2_v1\lesson_catalog_v1.json',
        'content_packs\math_grade2_v1\question_bank_v1.json',
        'content_packs\math_grade2_v1\game_events_v1.json',
        'content_packs\english_grade2_v1\manifest.json',
        'config\runtime_defaults_v1.json',
        'config\database_runtime_v1.json',
        'config\paths_v1.json'
    )
    foreach ($rel in $required) {
        Assert (Test-Path -LiteralPath (Join-Path $extractRoot $rel)) "portable payload missing: $rel"
    }
    $productionArtEvidence = Assert-ProductionArtPayload $extractRoot
    $result.production_art_png_count = [int]$productionArtEvidence.png_count
    $result.production_art_manifest_sha256 = [string]$productionArtEvidence.manifest_sha256
    Assert (-not (Test-Path -LiteralPath (Join-Path $extractRoot 'UserData'))) 'portable ZIP unexpectedly contains UserData'

    $exe = Join-Path $extractRoot 'WAHUKidsLearn.exe'
    $first = Start-Process -FilePath $exe -ArgumentList @('--bootstrap-smoke','--out',$bootstrapReport) -PassThru -Wait
    $result.first_bootstrap_exit = $first.ExitCode
    Assert ($first.ExitCode -eq 0) "portable first bootstrap exit $($first.ExitCode)"
    Assert (Test-Path -LiteralPath $bootstrapReport) 'portable first bootstrap report missing'

    $text = Get-Content -Raw -LiteralPath $bootstrapReport
    foreach ($needle in @(
        'result=PASS',
        'storage_mode=PORTABLE',
        'config_files=10',
        'config_journal=DELETE',
        'update_enabled=True',
        'update_channel=dev',
        'update_installed_mode_only=False',
        'startup_default=True',
        'config_low_fps_cap=18',
        'config_normal_fps_cap=30',
        'provider_version=2.0.4.0',
        'sqlite_version=3.53.4',
        'schema_version=5',
        'migration_version=5',
        'pre_migration_backup=none',
        'integrity=ok',
        'foreign_key_issues=0','verified_content_packs=2','performance_profile=','performance_motion_fps_cap=','performance_max_animated_regions=','performance_image_cache_mb=','performance_audio_cache_mb=','performance_evidence=','parent_pin_configured=False'
    )) {
        Assert ($text.Contains($needle)) "portable report missing: $needle"
    }

    Assert ($text.Contains('app_version=' + $AppVersion)) 'portable runtime app_version mismatch'

    $portableUserRoot = Join-Path $extractRoot 'UserData'
    $portableDb = Join-Path $portableUserRoot 'data\learning.db'
    Assert (-not (Test-Path -LiteralPath (Join-Path $portableUserRoot 'security\parent_pin.json'))) 'portable bootstrap unexpectedly created Parent PIN record'
    $portableRecoveryMarker = Join-Path $portableUserRoot 'recovery\runtime.running'
    Assert ($text.Contains('config_user_root=' + $portableUserRoot)) 'portable config_user_root mismatch'
    Assert ($text.Contains('config_database_path=' + $portableDb)) 'portable config_database_path mismatch'
    Assert (Test-Path -LiteralPath $portableDb) 'portable learner DB not created under UserData'
    Assert (-not (Test-Path -LiteralPath $portableRecoveryMarker)) 'portable clean boot left runtime marker'
    $result.portable_db_bytes = (Get-Item -LiteralPath $portableDb).Length
    $result.portable_db_sha256_first = (Get-FileHash -Algorithm SHA256 -LiteralPath $portableDb).Hash

    $second = Start-Process -FilePath $exe -ArgumentList @('--bootstrap-smoke','--out',$bootstrapReport2) -PassThru -Wait
    $result.second_bootstrap_exit = $second.ExitCode
    Assert ($second.ExitCode -eq 0) "portable second bootstrap exit $($second.ExitCode)"
    $text2 = Get-Content -Raw -LiteralPath $bootstrapReport2
    Assert ($text2.Contains('storage_mode=PORTABLE')) 'portable second boot lost portable mode'
    Assert ($text2.Contains('db_created=False')) 'portable second boot unexpectedly recreated schema'
    Assert (-not (Test-Path -LiteralPath $portableRecoveryMarker)) 'portable second clean boot left runtime marker'

    # The portable run must not create or mutate the installed learner database.
    $installedRootExistsAfter = Test-Path -LiteralPath $installedRoot
    $installedDbHashAfter = Optional-Hash $installedDb
    if (-not $installedRootExistedBefore) {
        Assert (-not $installedRootExistsAfter) 'portable run created installed %LOCALAPPDATA% user root'
    }
    if ($installedDbHashBefore) {
        Assert ($installedDbHashBefore -eq $installedDbHashAfter) 'portable run modified installed learner DB'
    } else {
        Assert (-not $installedDbHashAfter) 'portable run created installed learner DB'
    }

    $result.installed_root_existed_before = $installedRootExistedBefore
    $result.installed_db_hash_before = $installedDbHashBefore
    $result.installed_db_hash_after = $installedDbHashAfter
    $result.zip_sha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $PortableZip).Hash
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
    Remove-Item -LiteralPath $bootstrapReport,$bootstrapReport2 -Force -ErrorAction SilentlyContinue
    if (Test-Path -LiteralPath $extractRoot) { Remove-Item -LiteralPath $extractRoot -Recurse -Force -ErrorAction SilentlyContinue }
}

$result | Format-List
Write-Host "PORTABLE_E2E_PASS report=$reportPath"
