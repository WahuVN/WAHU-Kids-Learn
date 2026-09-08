param(
    [string]$ReportPath,
    [string]$ReleaseManifestPath,
    [string]$PortableZip,
    [switch]$Apply,
    [switch]$SelfTest
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
Set-Location $root

function Assert-True([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}

function Sha256([string]$Path) {
    return (Get-FileHash -Algorithm SHA256 -LiteralPath $Path).Hash.ToUpperInvariant()
}

function Read-Json([string]$Path) {
    Assert-True (Test-Path -LiteralPath $Path -PathType Leaf) "Missing JSON file: $Path"
    return Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json
}

function Validate-Win7Evidence([object]$Report, [object]$Manifest, [string]$LocalPortableZip) {
    Assert-True ($null -ne $Report) 'Win7 report is null.'
    Assert-True ($null -ne $Manifest) 'Release manifest is null.'
    Assert-True ([int]$Report.report_schema_version -eq 1) 'Unsupported Win7 target report schema.'
    Assert-True ([string]$Report.test_result -eq 'PASS') 'Win7 target report is not PASS.'
    Assert-True ([string]$Report.target_status -eq 'TARGET_SMOKE_VERIFIED') 'Win7 target status is not TARGET_SMOKE_VERIFIED.'
    Assert-True ([bool]$Report.is_target_windows7) 'Report does not identify the target as Windows 7.'
    Assert-True ([int]$Report.os_major -eq 6 -and [int]$Report.os_minor -eq 1) 'Target OS is not Windows 7 (6.1).'
    Assert-True ([int]$Report.os_build -ge 7601) 'Windows 7 build is older than SP1.'
    Assert-True ([int]$Report.service_pack_major -ge 1) 'Windows 7 Service Pack 1 evidence is missing.'
    Assert-True ([bool]$Report.preflight_is_target_windows7) 'Preflight did not identify Windows 7.'
    Assert-True ([bool]$Report.preflight_os_supported) 'Preflight reports unsupported OS.'
    Assert-True ([bool]$Report.preflight_net48_or_later) 'Preflight reports .NET 4.8 unavailable.'
    Assert-True ([int]$Report.preflight_net_release -ge 528040) '.NET 4.8 Release evidence is below 528040.'
    Assert-True ([string]$Report.preflight_process_arch -eq 'x86') 'Target app process architecture is not x86.'
    Assert-True ([string]$Report.preflight_compatibility -eq 'WIN7_SP1_RUNTIME_COMPATIBLE') 'Target compatibility is not WIN7_SP1_RUNTIME_COMPATIBLE.'
    Assert-True ([int]$Report.preflight_exit -eq 0) 'Target preflight exit is not zero.'
    Assert-True ([int]$Report.bootstrap_first_exit -eq 0 -and [int]$Report.bootstrap_second_exit -eq 0) 'Target bootstrap exit is not zero.'
    Assert-True ([int]$Report.bootstrap_schema_version -eq 6 -and [int]$Report.bootstrap_migration_version -eq 6) 'Target database schema/migration is not V6.'
    Assert-True ([string]$Report.bootstrap_integrity -eq 'ok') 'Target SQLite integrity is not ok.'
    Assert-True ([int]$Report.bootstrap_foreign_key_issues -eq 0) 'Target SQLite foreign-key issues detected.'
    Assert-True ([int]$Report.bootstrap_verified_content_packs -eq 2) 'Target did not verify both bundled content packs.'
    Assert-True ([bool]$Report.ui_process_started -and [bool]$Report.ui_window_seen) 'Target child UI did not open a real window.'

    Assert-True ([int]$Manifest.schema_version -eq 6) 'Release manifest schema is not V6.'
    Assert-True ([string]$Report.app_version -eq [string]$Manifest.app_version) 'Win7 report app_version does not match release manifest.'
    Assert-True ([string]$Report.git_commit -eq [string]$Manifest.git_commit) 'Win7 report git_commit does not match release manifest.'
    Assert-True (-not [string]::IsNullOrWhiteSpace([string]$Manifest.portable.zip_sha256)) 'Release manifest portable SHA is missing.'
    Assert-True ([string]$Report.portable_sha256 -eq [string]$Manifest.portable.zip_sha256) 'Win7 report portable SHA does not match release manifest.'
    Assert-True ([string]$Report.expected_portable_sha256 -eq [string]$Manifest.portable.zip_sha256) 'Win7 report expected portable SHA does not match release manifest.'
    Assert-True ([string]$Report.production_art_manifest_sha256 -eq [string]$Manifest.production_art.manifest_sha256) 'Win7 report production-art manifest SHA does not match release manifest.'
    Assert-True ([string]$Report.expected_production_art_manifest_sha256 -eq [string]$Manifest.production_art.manifest_sha256) 'Win7 report expected production-art SHA does not match release manifest.'

    if (-not [string]::IsNullOrWhiteSpace($LocalPortableZip)) {
        Assert-True (Test-Path -LiteralPath $LocalPortableZip -PathType Leaf) "Local portable ZIP not found: $LocalPortableZip"
        $localSha = Sha256 $LocalPortableZip
        Assert-True ($localSha -eq ([string]$Report.portable_sha256).ToUpperInvariant()) 'Local portable ZIP SHA does not match target-tested artifact.'
    }

    if ($null -ne $Report.installed_db_hash_before -and -not [string]::IsNullOrWhiteSpace([string]$Report.installed_db_hash_before)) {
        Assert-True ([string]$Report.installed_db_hash_before -eq [string]$Report.installed_db_hash_after) 'Target report shows installed learner DB mutation.'
    }

    return $true
}

function Invoke-SelfTest {
    $manifest = [pscustomobject]@{
        schema_version = 6
        app_version = '9.9.9-dev'
        git_commit = ('a' * 40)
        production_art = [pscustomobject]@{ manifest_sha256 = ('C' * 64) }
        portable = [pscustomobject]@{ zip_sha256 = ('B' * 64) }
    }
    $baseReport = [ordered]@{
        report_schema_version = 1
        test_result = 'PASS'
        target_status = 'TARGET_SMOKE_VERIFIED'
        is_target_windows7 = $true
        os_major = 6
        os_minor = 1
        os_build = 7601
        service_pack_major = 1
        preflight_is_target_windows7 = $true
        preflight_os_supported = $true
        preflight_net48_or_later = $true
        preflight_net_release = 528040
        preflight_process_arch = 'x86'
        preflight_compatibility = 'WIN7_SP1_RUNTIME_COMPATIBLE'
        preflight_exit = 0
        bootstrap_first_exit = 0
        bootstrap_second_exit = 0
        bootstrap_schema_version = 6
        bootstrap_migration_version = 6
        bootstrap_integrity = 'ok'
        bootstrap_foreign_key_issues = 0
        bootstrap_verified_content_packs = 2
        ui_process_started = $true
        ui_window_seen = $true
        app_version = '9.9.9-dev'
        git_commit = ('a' * 40)
        portable_sha256 = ('B' * 64)
        expected_portable_sha256 = ('B' * 64)
        production_art_manifest_sha256 = ('C' * 64)
        expected_production_art_manifest_sha256 = ('C' * 64)
        installed_db_hash_before = ('D' * 64)
        installed_db_hash_after = ('D' * 64)
    }
    $valid = [pscustomobject]$baseReport
    $null = Validate-Win7Evidence $valid $manifest $null

    $badShaMap = [ordered]@{} + $baseReport
    $badShaMap.portable_sha256 = ('E' * 64)
    $badShaRejected = $false
    try { $null = Validate-Win7Evidence ([pscustomobject]$badShaMap) $manifest $null } catch { $badShaRejected = $true }
    Assert-True $badShaRejected 'Self-test failed: wrong artifact SHA was accepted.'

    $badOsMap = [ordered]@{} + $baseReport
    $badOsMap.os_major = 10
    $badOsMap.os_minor = 0
    $badOsMap.is_target_windows7 = $false
    $badOsRejected = $false
    try { $null = Validate-Win7Evidence ([pscustomobject]$badOsMap) $manifest $null } catch { $badOsRejected = $true }
    Assert-True $badOsRejected 'Self-test failed: non-Win7 report was accepted.'

    $badCommitMap = [ordered]@{} + $baseReport
    $badCommitMap.git_commit = ('f' * 40)
    $badCommitRejected = $false
    try { $null = Validate-Win7Evidence ([pscustomobject]$badCommitMap) $manifest $null } catch { $badCommitRejected = $true }
    Assert-True $badCommitRejected 'Self-test failed: wrong Git commit was accepted.'

    Write-Host 'WIN7_TARGET_EVIDENCE_SELFTEST_PASS valid=1 reject_sha=1 reject_os=1 reject_commit=1'
}

if ($SelfTest) {
    Invoke-SelfTest
    exit 0
}

if ([string]::IsNullOrWhiteSpace($ReleaseManifestPath)) { $ReleaseManifestPath = Join-Path $root 'build\win7_x86\release_manifest_dev.json' }
Assert-True (-not [string]::IsNullOrWhiteSpace($ReportPath)) 'ReportPath is required unless -SelfTest is used.'

$report = Read-Json $ReportPath
$manifest = Read-Json $ReleaseManifestPath
if ([string]::IsNullOrWhiteSpace($PortableZip) -and $null -ne $manifest.portable -and -not [string]::IsNullOrWhiteSpace([string]$manifest.portable.zip)) {
    $PortableZip = Join-Path $root ([string]$manifest.portable.zip)
}
$null = Validate-Win7Evidence $report $manifest $PortableZip

if ($Apply) {
    $manifest.gates.win7_target_smoke = 'PASS'
    $manifest | Add-Member -Force NoteProperty win7_target ([pscustomobject]@{
        status = 'TARGET_SMOKE_VERIFIED'
        evidence_report = [IO.Path]::GetFullPath($ReportPath)
        tested_at_utc = [string]$report.tested_at_utc
        machine_name = [string]$report.machine_name
        os_caption = [string]$report.os_caption
        os_version = [string]$report.os_version
        service_pack_major = [int]$report.service_pack_major
        os_architecture = [string]$report.os_architecture
        powershell_version = [string]$report.powershell_version
        net_framework_release = [int]$report.preflight_net_release
        sha2_readiness = [string]$report.preflight_sha2_readiness
        compatibility = [string]$report.preflight_compatibility
        process_arch = [string]$report.preflight_process_arch
        portable_sha256 = [string]$report.portable_sha256
        production_art_manifest_sha256 = [string]$report.production_art_manifest_sha256
        bootstrap_schema_version = [int]$report.bootstrap_schema_version
        bootstrap_migration_version = [int]$report.bootstrap_migration_version
        bootstrap_sqlite_version = [string]$report.bootstrap_sqlite_version
        ui_window_seen = [bool]$report.ui_window_seen
    })
    $manifest | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $ReleaseManifestPath -Encoding UTF8
    $roundTrip = Read-Json $ReleaseManifestPath
    Assert-True ([string]$roundTrip.gates.win7_target_smoke -eq 'PASS') 'Failed to persist win7_target_smoke=PASS.'
    Assert-True ([string]$roundTrip.win7_target.status -eq 'TARGET_SMOKE_VERIFIED') 'Failed to persist Win7 target evidence.'
}

Write-Host "WIN7_TARGET_EVIDENCE_PASS report=$ReportPath apply=$([bool]$Apply)"
