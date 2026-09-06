param(
    [string]$OldVersion = '0.1.21-dev',
    [string]$NewVersion = '0.1.22-dev',
    [string]$OldInstallerPath,
    [string]$NewInstallerPath,
    [switch]$CleanOwnedTestData,
    [switch]$KeepStartupEnabled,
    [switch]$UseLiveFeed
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
Set-Location $root

if ([string]::IsNullOrWhiteSpace($OldInstallerPath)) {
    $OldInstallerPath = Join-Path $root "build\installer\WAHU-Kids-Learn-Setup-win7-x86-$OldVersion.exe"
}
if ([string]::IsNullOrWhiteSpace($NewInstallerPath)) {
    $NewInstallerPath = Join-Path $root "build\installer\WAHU-Kids-Learn-Setup-win7-x86-$NewVersion.exe"
}
foreach ($p in @($OldInstallerPath,$NewInstallerPath)) {
    if (-not (Test-Path -LiteralPath $p -PathType Leaf)) { throw "Không tìm thấy installer: $p" }
}
if ($OldVersion -eq $NewVersion) { throw 'OldVersion và NewVersion phải khác nhau.' }

$appDir = Join-Path $env:LOCALAPPDATA 'Programs\WAHU Kids Learn'
$dataDir = Join-Path $env:LOCALAPPDATA 'WAHU Kids Learn'
$ownerMarker = Join-Path $dataDir '.wahu-updater-e2e-owned'
$runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$reportPath = Join-Path $root 'build\updater_apply_e2e_dev.json'
$oldBootstrap = Join-Path $env:TEMP 'wahu-updater-old-bootstrap.txt'
$newBootstrap = Join-Path $env:TEMP 'wahu-updater-new-bootstrap.txt'
$probeExe = Join-Path $root 'tests\UpdateRuntimeSmoke\bin\Release\WAHU.UpdateRuntimeSmoke.exe'

function Assert([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}

function Read-AppVersion([string]$InstallRoot) {
    $manifest = Join-Path $InstallRoot 'config\install_manifest_v1.json'
    if (-not (Test-Path -LiteralPath $manifest)) { return $null }
    try { return (Get-Content -Raw -LiteralPath $manifest | ConvertFrom-Json).app_version } catch { return $null }
}

function Get-OwnedAppProcesses {
    $exe = [IO.Path]::GetFullPath((Join-Path $appDir 'WAHUKidsLearn.exe'))
    @(Get-Process -Name 'WAHUKidsLearn' -ErrorAction SilentlyContinue | Where-Object {
        try { [IO.Path]::GetFullPath($_.Path) -eq $exe } catch { $false }
    })
}

function Stop-OwnedAppProcesses {
    foreach ($p in @(Get-OwnedAppProcesses)) {
        try { $null = $p.CloseMainWindow() } catch { }
        try { if (-not $p.WaitForExit(5000)) { Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue } } catch { }
    }
}

function Remove-TestArtifacts {
    Stop-OwnedAppProcesses
    if (Test-Path -LiteralPath $appDir) { Remove-Item -LiteralPath $appDir -Recurse -Force -ErrorAction SilentlyContinue }
    if (Test-Path -LiteralPath $dataDir) {
        if (Test-Path -LiteralPath $ownerMarker) { Remove-Item -LiteralPath $dataDir -Recurse -Force -ErrorAction SilentlyContinue }
    }
    Remove-ItemProperty -Path $runKey -Name 'WAHU Kids Learn' -ErrorAction SilentlyContinue
    foreach ($p in @($oldBootstrap,$newBootstrap)) { if (Test-Path -LiteralPath $p) { Remove-Item -LiteralPath $p -Force -ErrorAction SilentlyContinue } }
}

# Never touch a real installation or learner tree.
if (Test-Path -LiteralPath $dataDir) {
    if (-not (Test-Path -LiteralPath $ownerMarker)) {
        throw "REFUSE: đã có learner data thật tại $dataDir; updater E2E không được chạm vào."
    }
    if ($CleanOwnedTestData) { Remove-TestArtifacts }
}
if (Test-Path -LiteralPath $appDir) { throw "REFUSE: app đã tồn tại tại $appDir" }
Assert (Test-Path -LiteralPath $probeExe) 'UpdateRuntimeSmoke probe chưa được build.'

$result = [ordered]@{}
try {
    New-Item -ItemType Directory -Force -Path $dataDir | Out-Null
    'owned-by-tools/build/Test-UpdaterApplyE2E.ps1' | Set-Content -LiteralPath $ownerMarker -Encoding ASCII

    # 1. Install old version with installer default task selection.
    $install = Start-Process -FilePath $OldInstallerPath -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/SP-') -PassThru -Wait
    Assert ($install.ExitCode -eq 0) "old install exit $($install.ExitCode)"
    Assert ((Read-AppVersion $appDir) -eq $OldVersion) 'old installed app_version mismatch'
    $result.old_install_exit = $install.ExitCode

    $runValue = (Get-ItemProperty -Path $runKey -Name 'WAHU Kids Learn' -ErrorAction Stop).'WAHU Kids Learn'
    Assert ($runValue -like '*WAHUKidsLearn.exe*--startup*') 'startup default was not enabled by old installer'

    # 2. Bootstrap old app and establish learner data/sentinel.
    $oldApp = Join-Path $appDir 'WAHUKidsLearn.exe'
    $boot = Start-Process -FilePath $oldApp -ArgumentList @('--bootstrap-smoke','--out',$oldBootstrap) -PassThru -Wait
    Assert ($boot.ExitCode -eq 0) "old bootstrap exit $($boot.ExitCode)"
    $oldText = Get-Content -Raw -LiteralPath $oldBootstrap
    Assert ($oldText.Contains('app_version=' + $OldVersion)) 'old bootstrap report version mismatch'
    Assert ($oldText.Contains('integrity=ok') -and $oldText.Contains('foreign_key_issues=0')) 'old DB not healthy'
    $dbPath = Join-Path $dataDir 'data\learning.db'
    Assert (Test-Path -LiteralPath $dbPath) 'old learner DB missing'
    $result.db_sha256_before_update = (Get-FileHash -Algorithm SHA256 -LiteralPath $dbPath).Hash
    $sentinel = Join-Path $dataDir 'data\updater-preserve-sentinel.txt'
    'preserve-across-real-updater-apply' | Set-Content -LiteralPath $sentinel -Encoding ASCII

    # Parent startup preference before the update.
    if ($KeepStartupEnabled) {
        $runBeforeUpdate = (Get-ItemProperty -Path $runKey -Name 'WAHU Kids Learn' -ErrorAction Stop).'WAHU Kids Learn'
        Assert ($runBeforeUpdate -like '*WAHUKidsLearn.exe*--startup*') 'startup was expected enabled before update'
        $result.startup_preference_before_update = 'enabled'
    } else {
        Remove-ItemProperty -Path $runKey -Name 'WAHU Kids Learn' -ErrorAction Stop
        $runDisabled = (Get-ItemProperty -Path $runKey -Name 'WAHU Kids Learn' -ErrorAction SilentlyContinue).'WAHU Kids Learn'
        Assert ([string]::IsNullOrWhiteSpace($runDisabled)) 'could not disable startup before update'
        $result.startup_preference_before_update = 'disabled'
    }

    # 3. Stage the new installer. Local mode uses the production staging service directly;
    # live mode lets the installed old app fetch the real GitHub channel feed in background.
    $stagedState = Join-Path $dataDir 'updates\staged-update.json'
    if ($UseLiveFeed) {
        $liveApp = Start-Process -FilePath $oldApp -PassThru
        $liveDeadline = [DateTime]::UtcNow.AddSeconds(120)
        while ([DateTime]::UtcNow -lt $liveDeadline -and -not (Test-Path -LiteralPath $stagedState)) { Start-Sleep -Milliseconds 300 }
        Assert (Test-Path -LiteralPath $stagedState) 'installed app did not stage update from live GitHub feed'
        Stop-OwnedAppProcesses
        $result.staging_source = 'github-live-feed'
    } else {
        & $probeExe --stage-local --config-dir (Join-Path $appDir 'config') --app-base $appDir --installer $NewInstallerPath --version $NewVersion
        Assert ($LASTEXITCODE -eq 0) "stage-local exit $LASTEXITCODE"
        $result.staging_source = 'local-production-staging-service'
    }
    Assert (Test-Path -LiteralPath $stagedState) 'staged-update.json missing'
    $stage = Get-Content -Raw -LiteralPath $stagedState | ConvertFrom-Json
    Assert ($stage.app_version -eq $NewVersion) 'staged state version mismatch'
    $stagedInstaller = Join-Path (Join-Path $dataDir 'updates') $stage.installer_relative_path
    Assert (Test-Path -LiteralPath $stagedInstaller) 'staged installer missing before apply'
    $result.staged_installer_sha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $stagedInstaller).Hash
    $expectedNewHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $NewInstallerPath).Hash
    Assert ($result.staged_installer_sha256 -eq $expectedNewHash) 'staged installer differs from expected new release installer'

    # 4. Normal startup detects staged update, creates verified backup, exits, and launches helper.
    $launcher = Start-Process -FilePath $oldApp -PassThru
    $result.old_launcher_pid = $launcher.Id

    $deadline = [DateTime]::UtcNow.AddSeconds(90)
    $seenNewVersion = $false
    while ([DateTime]::UtcNow -lt $deadline) {
        if ((Read-AppVersion $appDir) -eq $NewVersion) { $seenNewVersion = $true; break }
        Start-Sleep -Milliseconds 500
    }
    Assert $seenNewVersion 'timed out waiting for updater to install new version'
    $result.installed_version_after_update = Read-AppVersion $appDir

    # Wait for post-update app restart/status marker.
    $statusPath = Join-Path $dataDir 'updates\last-result.txt'
    $postDeadline = [DateTime]::UtcNow.AddSeconds(30)
    $postUpdateRecorded = $false
    while ([DateTime]::UtcNow -lt $postDeadline) {
        if (Test-Path -LiteralPath $statusPath) {
            $statusText = Get-Content -Raw -LiteralPath $statusPath
            if ($statusText.Contains('result=updated_to:' + $NewVersion)) { $postUpdateRecorded = $true; break }
        }
        Start-Sleep -Milliseconds 300
    }
    Assert $postUpdateRecorded 'post-update status was not recorded by restarted app'

    # Close only the app process owned by this E2E, never system-wide arbitrary processes.
    Stop-OwnedAppProcesses

    # 5. Verify cleanup, backup, data preservation and startup preference.
    Assert (-not (Test-Path -LiteralPath $stagedState)) 'staged state remained after successful update'
    Assert (-not (Test-Path -LiteralPath $stagedInstaller)) 'staged installer remained after successful update'
    Assert (Test-Path -LiteralPath $sentinel) 'learner sentinel lost during update'
    Assert (Test-Path -LiteralPath $dbPath) 'learner DB lost during update'
    $runAfterUpdate = (Get-ItemProperty -Path $runKey -Name 'WAHU Kids Learn' -ErrorAction SilentlyContinue).'WAHU Kids Learn'
    if ($KeepStartupEnabled) {
        Assert ($runAfterUpdate -like '*WAHUKidsLearn.exe*--startup*') 'updater disabled startup against Parent preference'
        $result.startup_enabled_preserved = $true
    } else {
        Assert ([string]::IsNullOrWhiteSpace($runAfterUpdate)) 'updater re-enabled startup against Parent preference'
        $result.startup_disabled_preserved = $true
    }

    $preUpdateDir = Join-Path $dataDir 'backups\pre_update'
    $metadataFiles = @(Get-ChildItem -LiteralPath $preUpdateDir -Filter '*.json' -File -ErrorAction SilentlyContinue | Sort-Object LastWriteTimeUtc -Descending)
    Assert ($metadataFiles.Count -ge 1) 'verified pre-update backup metadata missing'
    $meta = Get-Content -Raw -LiteralPath $metadataFiles[0].FullName | ConvertFrom-Json
    Assert ($meta.backup_type -eq 'manual' -and $meta.verified -eq $true) 'pre-update backup metadata not verified/manual'
    $backupDb = Join-Path $metadataFiles[0].DirectoryName $meta.database_file
    Assert (Test-Path -LiteralPath $backupDb) 'pre-update backup DB missing'
    $backupHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $backupDb).Hash
    Assert ($backupHash -eq $meta.database_sha256) 'pre-update backup SHA-256 mismatch'
    $result.pre_update_backup_metadata = $metadataFiles[0].FullName
    $result.pre_update_backup_sha256 = $backupHash

    # New binary must still pass full bootstrap DB/content gates.
    $newApp = Join-Path $appDir 'WAHUKidsLearn.exe'
    $newBoot = Start-Process -FilePath $newApp -ArgumentList @('--bootstrap-smoke','--out',$newBootstrap) -PassThru -Wait
    Assert ($newBoot.ExitCode -eq 0) "new bootstrap exit $($newBoot.ExitCode)"
    $newText = Get-Content -Raw -LiteralPath $newBootstrap
    Assert ($newText.Contains('app_version=' + $NewVersion)) 'new bootstrap report version mismatch'
    Assert ($newText.Contains('integrity=ok') -and $newText.Contains('foreign_key_issues=0')) 'new DB not healthy after update'
    $result.db_sha256_after_update = (Get-FileHash -Algorithm SHA256 -LiteralPath $dbPath).Hash
    $result.new_bootstrap_exit = $newBoot.ExitCode

    # 6. Uninstall updated app; learner data must still be preserved.
    $uninstaller = Join-Path $appDir 'unins000.exe'
    Assert (Test-Path -LiteralPath $uninstaller) 'updated uninstaller missing'
    $uninstall = Start-Process -FilePath $uninstaller -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART') -PassThru -Wait
    Assert ($uninstall.ExitCode -eq 0) "updated uninstall exit $($uninstall.ExitCode)"
    Assert (-not (Test-Path -LiteralPath (Join-Path $appDir 'WAHUKidsLearn.exe'))) 'updated app binary remained after uninstall'
    Assert (Test-Path -LiteralPath $dbPath) 'learner DB removed by updated uninstall'
    Assert (Test-Path -LiteralPath $sentinel) 'learner sentinel removed by updated uninstall'
    $result.uninstall_exit = $uninstall.ExitCode
    $result.test_result = 'PASS'
}
catch {
    $result.test_result = 'FAIL'
    $result.error = $_.Exception.Message
    throw
}
finally {
    $result.tested_at_utc = [DateTime]::UtcNow.ToString('o')
    $result | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $reportPath -Encoding UTF8
    Remove-TestArtifacts
}

$result | Format-List
Write-Host "UPDATER_APPLY_E2E_PASS report=$reportPath old=$OldVersion new=$NewVersion"
