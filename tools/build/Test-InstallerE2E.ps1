param(
    [string]$AppVersion = '0.1.2-dev',
    [string]$InstallerPath,
    [switch]$CleanOwnedTestData
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
Set-Location $root

if ([string]::IsNullOrWhiteSpace($InstallerPath)) {
    $InstallerPath = Join-Path $root "build\installer\WAHU-Kids-Learn-Setup-win7-x86-$AppVersion.exe"
}
if (-not (Test-Path -LiteralPath $InstallerPath -PathType Leaf)) { throw "Không tìm thấy installer: $InstallerPath" }

$appDir = Join-Path $env:LOCALAPPDATA 'Programs\WAHU Kids Learn'
$dataDir = Join-Path $env:LOCALAPPDATA 'WAHU Kids Learn'
$ownerMarker = Join-Path $dataDir '.wahu-e2e-owned'
$reportPath = Join-Path $root 'build\installer_e2e_dev.json'
$bootstrapReport = Join-Path $env:TEMP 'wahu-bootstrap-e2e.txt'

function Assert([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
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
    return [ordered]@{
        png_count = $expectedCount
        manifest_sha256 = $expectedManifestSha
    }
}

function Remove-TestArtifacts {
    if (Test-Path -LiteralPath $appDir) { Remove-Item -LiteralPath $appDir -Recurse -Force -ErrorAction SilentlyContinue }
    if (Test-Path -LiteralPath $dataDir) {
        if (Test-Path -LiteralPath $ownerMarker) { Remove-Item -LiteralPath $dataDir -Recurse -Force -ErrorAction SilentlyContinue }
    }
    Remove-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name 'WAHU Kids Learn' -ErrorAction SilentlyContinue
    $shortcut = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\WAHU Kids Learn.lnk'
    if (Test-Path -LiteralPath $shortcut) { Remove-Item -LiteralPath $shortcut -Force -ErrorAction SilentlyContinue }
    if (Test-Path -LiteralPath $bootstrapReport) { Remove-Item -LiteralPath $bootstrapReport -Force -ErrorAction SilentlyContinue }
}

# Safety: never touch an existing user data tree unless this script owns it.
if (Test-Path -LiteralPath $dataDir) {
    if (-not (Test-Path -LiteralPath $ownerMarker)) {
        throw "REFUSE: đã có dữ liệu tại $dataDir và không có marker E2E. Không chạy test để tránh đụng dữ liệu thật."
    }
    if ($CleanOwnedTestData) { Remove-TestArtifacts }
}
if (Test-Path -LiteralPath $appDir) {
    throw "REFUSE: app đã tồn tại tại $appDir. Hãy uninstall/clean test install trước."
}

$result = [ordered]@{}
try {
    New-Item -ItemType Directory -Force -Path $dataDir | Out-Null
    'owned-by-tools/build/Test-InstallerE2E.ps1' | Set-Content -LiteralPath $ownerMarker -Encoding ASCII

    $install = Start-Process -FilePath $InstallerPath -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/SP-') -PassThru -Wait
    $result.install_exit = $install.ExitCode
    Assert ($install.ExitCode -eq 0) "install exit $($install.ExitCode)"

    $required = @(
        'WAHUKidsLearn.exe',
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
        'WAHU.SetupPreflight.exe'
    )
    foreach ($rel in $required) { Assert (Test-Path -LiteralPath (Join-Path $appDir $rel)) "installed payload missing: $rel" }
    $productionArtEvidence = Assert-ProductionArtPayload $appDir
    $result.production_art_png_count = [int]$productionArtEvidence.png_count
    $result.production_art_manifest_sha256 = [string]$productionArtEvidence.manifest_sha256
    $result.installed_file_count = (Get-ChildItem -LiteralPath $appDir -Recurse -File).Count
    $runValue = (Get-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name 'WAHU Kids Learn' -ErrorAction Stop).'WAHU Kids Learn'
    Assert ($runValue -like '*WAHUKidsLearn.exe*--startup*') 'startup Run registry value missing/invalid'
    $result.startup_registry_present = $true

    & (Join-Path $appDir 'WAHU.SetupPreflight.exe') --out (Join-Path $env:TEMP 'wahu-preflight-installed.json') --signature-target (Join-Path $appDir 'WAHUKidsLearn.exe')
    $result.preflight_exit = $LASTEXITCODE
    Assert ($LASTEXITCODE -eq 0) "installed preflight exit $LASTEXITCODE"

    $app = Start-Process -FilePath (Join-Path $appDir 'WAHUKidsLearn.exe') -ArgumentList @('--bootstrap-smoke','--out',$bootstrapReport) -PassThru -Wait
    $result.bootstrap_exit = $app.ExitCode
    Assert ($app.ExitCode -eq 0) "app bootstrap smoke exit $($app.ExitCode)"
    Assert (Test-Path -LiteralPath $bootstrapReport) 'bootstrap report missing'
    $bootstrapText = Get-Content -Raw -LiteralPath $bootstrapReport
    foreach ($needle in @('result=PASS','previous_run_unclean=False','storage_mode=INSTALLED','config_files=10','config_journal=DELETE','update_enabled=True','update_channel=dev','update_manifest_url=https://github.com/WahuVN/WAHU-Kids-Learn/releases/download/update-dev/update-manifest.json','update_installed_mode_only=True','startup_default=True','config_low_fps_cap=18','config_normal_fps_cap=30','provider_version=2.0.4.0','sqlite_version=3.53.4','journal=DELETE','schema_version=5','migration_version=5','pre_migration_backup=none','integrity=ok','foreign_key_issues=0','verified_content_packs=2','performance_profile=','performance_motion_fps_cap=','performance_max_animated_regions=','performance_image_cache_mb=','performance_audio_cache_mb=','performance_evidence=','parent_pin_configured=False')) {
        Assert ($bootstrapText.Contains($needle)) "bootstrap report missing: $needle"
    }
    Assert ($bootstrapText.Contains('app_version=' + $AppVersion)) 'installed runtime app_version mismatch'
    Assert ($bootstrapText -match 'migration_sha256=[0-9A-F]{64}') 'bootstrap migration SHA-256 missing/invalid'
    Assert (-not (Test-Path -LiteralPath (Join-Path $dataDir 'security\parent_pin.json'))) 'bootstrap unexpectedly created Parent PIN record'
    $runtimeMarker = Join-Path $dataDir 'recovery\runtime.running'
    Assert (-not (Test-Path -LiteralPath $runtimeMarker)) 'clean bootstrap left stale runtime marker'

    # Simulate a previous hard crash by leaving the marker behind. The next clean
    # bootstrap must detect it as unclean and remove the marker on exit.
    New-Item -ItemType Directory -Force -Path (Split-Path $runtimeMarker) | Out-Null
    'simulated-crash-marker' | Set-Content -LiteralPath $runtimeMarker -Encoding ASCII
    $recoveryBootstrapReport = Join-Path $env:TEMP 'wahu-bootstrap-recovery-e2e.txt'
    $recoveryApp = Start-Process -FilePath (Join-Path $appDir 'WAHUKidsLearn.exe') -ArgumentList @('--bootstrap-smoke','--out',$recoveryBootstrapReport) -PassThru -Wait
    $result.recovery_bootstrap_exit = $recoveryApp.ExitCode
    Assert ($recoveryApp.ExitCode -eq 0) "recovery bootstrap exit $($recoveryApp.ExitCode)"
    $recoveryText = Get-Content -Raw -LiteralPath $recoveryBootstrapReport
    Assert ($recoveryText.Contains('result=PASS')) 'recovery bootstrap did not pass'
    Assert ($recoveryText.Contains('previous_run_unclean=True')) 'stale runtime marker was not detected'
    Assert ($recoveryText.Contains('integrity=ok')) 'recovery bootstrap integrity check did not pass'
    Assert (-not (Test-Path -LiteralPath $runtimeMarker)) 'recovery bootstrap did not clean runtime marker'
    Remove-Item -LiteralPath $recoveryBootstrapReport -Force -ErrorAction SilentlyContinue

    $dbPath = Join-Path $dataDir 'data\learning.db'
    Assert (Test-Path -LiteralPath $dbPath) 'learner DB missing after app bootstrap'
    $result.db_sha256_before_reinstall = (Get-FileHash -Algorithm SHA256 -LiteralPath $dbPath).Hash
    $result.db_bytes = (Get-Item -LiteralPath $dbPath).Length

    # Fail-closed installed config E2E: a safety-critical tamper must stop before DB work.
    $featureFlags = Join-Path $appDir 'config\feature_flags_v1.json'
    $featureBackup = Join-Path $env:TEMP ('wahu-feature-flags-' + [Guid]::NewGuid().ToString('N') + '.json')
    Copy-Item -LiteralPath $featureFlags -Destination $featureBackup -Force
    try {
        $flagsText = Get-Content -Raw -LiteralPath $featureFlags
        $tamperedFlags = $flagsText.Replace('"network": false','"network": true')
        Assert ($tamperedFlags -ne $flagsText) 'feature_flags network=false token not found for tamper test'
        Set-Content -LiteralPath $featureFlags -Value $tamperedFlags -Encoding UTF8
        $tamperReport = Join-Path $env:TEMP 'wahu-bootstrap-config-tamper-e2e.txt'
        $tamperApp = Start-Process -FilePath (Join-Path $appDir 'WAHUKidsLearn.exe') -ArgumentList @('--bootstrap-smoke','--out',$tamperReport) -PassThru -Wait
        $result.config_tamper_exit = $tamperApp.ExitCode
        Assert ($tamperApp.ExitCode -eq 42) "config tamper expected exit 42, got $($tamperApp.ExitCode)"
        Assert (Test-Path -LiteralPath $tamperReport) 'config tamper failure report missing'
        $tamperText = Get-Content -Raw -LiteralPath $tamperReport
        Assert ($tamperText.Contains('result=FAIL')) 'config tamper did not report FAIL'
        Assert ($tamperText.Contains('code=CONFIG_INVALID')) 'config tamper did not fail with CONFIG_INVALID'
        Assert (-not (Test-Path -LiteralPath $runtimeMarker)) 'invalid config created runtime marker before rejection'
        Assert ((Get-FileHash -Algorithm SHA256 -LiteralPath $dbPath).Hash -eq $result.db_sha256_before_reinstall) 'invalid config path modified learner DB'
        Remove-Item -LiteralPath $tamperReport -Force -ErrorAction SilentlyContinue
    }
    finally {
        Copy-Item -LiteralPath $featureBackup -Destination $featureFlags -Force
        Remove-Item -LiteralPath $featureBackup -Force -ErrorAction SilentlyContinue
    }

    $postRestoreConfigReport = Join-Path $env:TEMP 'wahu-bootstrap-config-restored-e2e.txt'
    $postRestoreConfigApp = Start-Process -FilePath (Join-Path $appDir 'WAHUKidsLearn.exe') -ArgumentList @('--bootstrap-smoke','--out',$postRestoreConfigReport) -PassThru -Wait
    $result.config_restored_bootstrap_exit = $postRestoreConfigApp.ExitCode
    Assert ($postRestoreConfigApp.ExitCode -eq 0) 'restored config did not bootstrap cleanly'
    Assert ((Get-Content -Raw -LiteralPath $postRestoreConfigReport).Contains('storage_mode=INSTALLED')) 'restored config lost installed mode'
    Remove-Item -LiteralPath $postRestoreConfigReport -Force -ErrorAction SilentlyContinue

    $sentinel = Join-Path $dataDir 'data\e2e-preserve-sentinel.txt'
    'do-not-delete-by-installer-or-uninstaller' | Set-Content -LiteralPath $sentinel -Encoding ASCII

    # Simulate Parent Mode turning startup off. An update/reinstall must preserve that preference.
    Remove-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name 'WAHU Kids Learn' -ErrorAction Stop
    $runDisabledBeforeReinstall = (Get-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name 'WAHU Kids Learn' -ErrorAction SilentlyContinue).'WAHU Kids Learn'
    Assert ([string]::IsNullOrWhiteSpace($runDisabledBeforeReinstall)) 'failed to simulate startup disabled before reinstall'

    $reinstall = Start-Process -FilePath $InstallerPath -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/SP-','/TASKS="!startup"') -PassThru -Wait
    $result.reinstall_exit = $reinstall.ExitCode
    Assert ($reinstall.ExitCode -eq 0) "reinstall exit $($reinstall.ExitCode)"
    Assert (Test-Path -LiteralPath $dbPath) 'learner DB lost on reinstall'
    Assert (Test-Path -LiteralPath $sentinel) 'sentinel lost on reinstall'
    $result.db_sha256_after_reinstall = (Get-FileHash -Algorithm SHA256 -LiteralPath $dbPath).Hash
    Assert ($result.db_sha256_before_reinstall -eq $result.db_sha256_after_reinstall) 'reinstall modified learner DB unexpectedly'
    $runAfterDisabledReinstall = (Get-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name 'WAHU Kids Learn' -ErrorAction SilentlyContinue).'WAHU Kids Learn'
    Assert ([string]::IsNullOrWhiteSpace($runAfterDisabledReinstall)) 'reinstall/update re-enabled startup against Parent preference'
    $result.startup_disabled_preserved_on_reinstall = $true

    $uninstaller = Join-Path $appDir 'unins000.exe'
    Assert (Test-Path -LiteralPath $uninstaller) 'uninstaller missing'
    $uninstall = Start-Process -FilePath $uninstaller -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART') -PassThru -Wait
    $result.uninstall_exit = $uninstall.ExitCode
    Assert ($uninstall.ExitCode -eq 0) "uninstall exit $($uninstall.ExitCode)"

    $result.app_removed_after_uninstall = -not (Test-Path -LiteralPath (Join-Path $appDir 'WAHUKidsLearn.exe'))
    $result.db_preserved_after_uninstall = Test-Path -LiteralPath $dbPath
    $result.sentinel_preserved_after_uninstall = Test-Path -LiteralPath $sentinel
    Assert $result.app_removed_after_uninstall 'app binary remained after uninstall'
    Assert $result.db_preserved_after_uninstall 'learner DB removed by uninstall'
    Assert $result.sentinel_preserved_after_uninstall 'learner sentinel removed by uninstall'
    $runAfterUninstall = (Get-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name 'WAHU Kids Learn' -ErrorAction SilentlyContinue).'WAHU Kids Learn'
    Assert ([string]::IsNullOrWhiteSpace($runAfterUninstall)) 'startup Run registry value remained after uninstall'
    $result.startup_registry_removed_after_uninstall = $true

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
    # Clean only data explicitly owned by this E2E run.
    Remove-TestArtifacts
}

$result | Format-List
Write-Host "INSTALLER_E2E_PASS report=$reportPath"
