param(
    [Parameter(Mandatory = $true)]
    [string]$PortableZip,
    [Parameter(Mandatory = $true)]
    [string]$AppVersion,
    [Parameter(Mandatory = $true)]
    [string]$ExpectedPortableSha256,
    [Parameter(Mandatory = $true)]
    [string]$ExpectedGitCommit,
    [Parameter(Mandatory = $true)]
    [string]$ExpectedProductionArtManifestSha256,
    [string]$ReportPath,
    [int]$UiWindowTimeoutSeconds = 15
)

# Target-side compatibility rule: this script must run on the stock PowerShell 2.0
# shipped with Windows 7. Do not use $PSScriptRoot, Get-FileHash, Expand-Archive,
# ConvertFrom-Json, ConvertTo-Json, ordered hashtables, or newer syntax here.
$ErrorActionPreference = 'Stop'

function Assert-True([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}

function Get-ScriptDirectory {
    $path = $MyInvocation.ScriptName
    if ([string]::IsNullOrEmpty($path)) { $path = $MyInvocation.MyCommand.Path }
    if ([string]::IsNullOrEmpty($path)) { return [Environment]::CurrentDirectory }
    return [IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($path))
}

function Get-Sha256([string]$Path) {
    $stream = [IO.File]::OpenRead($Path)
    $sha = [Security.Cryptography.SHA256]::Create()
    try {
        $bytes = $sha.ComputeHash($stream)
        return ([BitConverter]::ToString($bytes)).Replace('-', '').ToUpperInvariant()
    }
    finally {
        $stream.Close()
        $sha.Dispose()
    }
}

function Get-OptionalSha256([string]$Path) {
    if (Test-Path -LiteralPath $Path -PathType Leaf) { return Get-Sha256 $Path }
    return $null
}

function Read-Text([string]$Path) {
    return [IO.File]::ReadAllText($Path)
}

function Get-KeyValue([string]$Text, [string]$Name) {
    $prefix = $Name + '='
    $lines = $Text -split "`r?`n"
    foreach ($line in $lines) {
        if ($line.StartsWith($prefix, [StringComparison]::Ordinal)) {
            return $line.Substring($prefix.Length)
        }
    }
    return $null
}

function Convert-JsonTextToObject([string]$Text) {
    Add-Type -AssemblyName System.Web.Extensions
    $serializer = New-Object System.Web.Script.Serialization.JavaScriptSerializer
    $serializer.MaxJsonLength = 67108864
    return $serializer.DeserializeObject($Text)
}

function Write-JsonFile([object]$Value, [string]$Path) {
    Add-Type -AssemblyName System.Web.Extensions
    $serializer = New-Object System.Web.Script.Serialization.JavaScriptSerializer
    $serializer.MaxJsonLength = 67108864
    $json = $serializer.Serialize($Value)
    $parent = [IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($Path))
    if (-not [string]::IsNullOrEmpty($parent)) { [IO.Directory]::CreateDirectory($parent) | Out-Null }
    $utf8 = New-Object -TypeName Text.UTF8Encoding -ArgumentList $false
    [IO.File]::WriteAllText($Path, $json, $utf8)
}

function Expand-Zip([string]$ZipPath, [string]$Destination) {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [IO.Directory]::CreateDirectory($Destination) | Out-Null
    [IO.Compression.ZipFile]::ExtractToDirectory($ZipPath, $Destination)
}

function Get-Net48Release {
    $release = 0
    try {
        $views = @([Microsoft.Win32.RegistryView]::Registry32)
        if ([Environment]::Is64BitOperatingSystem) { $views += [Microsoft.Win32.RegistryView]::Registry64 }
        foreach ($view in $views) {
            $base = [Microsoft.Win32.RegistryKey]::OpenBaseKey([Microsoft.Win32.RegistryHive]::LocalMachine, $view)
            try {
                $key = $base.OpenSubKey('SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full')
                if ($key -ne $null) {
                    try {
                        $value = $key.GetValue('Release', 0)
                        if ($value -ne $null -and [int]$value -gt $release) { $release = [int]$value }
                    }
                    finally { $key.Close() }
                }
            }
            finally { $base.Close() }
        }
    }
    catch {
        try {
            $value = (Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full' -ErrorAction Stop).Release
            if ($value -ne $null) { $release = [int]$value }
        }
        catch { }
    }
    return $release
}

function Wait-ForMainWindow([Diagnostics.Process]$Process, [int]$TimeoutSeconds) {
    $deadline = [DateTime]::UtcNow.AddSeconds([Math]::Max(1, $TimeoutSeconds))
    while ([DateTime]::UtcNow -lt $deadline) {
        Start-Sleep -Milliseconds 250
        $Process.Refresh()
        if ($Process.HasExited) { return $false }
        if ($Process.MainWindowHandle -ne [IntPtr]::Zero) { return $true }
    }
    return $false
}

function Stop-TestProcess([Diagnostics.Process]$Process) {
    if ($Process -eq $null) { return }
    try {
        $Process.Refresh()
        if ($Process.HasExited) { return }
        $null = $Process.CloseMainWindow()
        if (-not $Process.WaitForExit(5000)) { $Process.Kill(); $Process.WaitForExit() }
    }
    catch {
        try { if (-not $Process.HasExited) { $Process.Kill() } } catch { }
    }
}

$scriptDirectory = Get-ScriptDirectory
if ([string]::IsNullOrEmpty($ReportPath)) { $ReportPath = Join-Path $scriptDirectory 'win7-target-report.json' }
$PortableZip = [IO.Path]::GetFullPath($PortableZip)
$ReportPath = [IO.Path]::GetFullPath($ReportPath)
$ExpectedPortableSha256 = $ExpectedPortableSha256.Trim().ToUpperInvariant()
$ExpectedProductionArtManifestSha256 = $ExpectedProductionArtManifestSha256.Trim().ToUpperInvariant()
$ExpectedGitCommit = $ExpectedGitCommit.Trim().ToLowerInvariant()

$result = @{}
$result['report_schema_version'] = 1
$result['app_version'] = $AppVersion
$result['git_commit'] = $ExpectedGitCommit
$result['portable_zip'] = $PortableZip
$result['expected_portable_sha256'] = $ExpectedPortableSha256
$result['expected_production_art_manifest_sha256'] = $ExpectedProductionArtManifestSha256
$result['target_status'] = 'NOT_VERIFIED'
$result['test_result'] = 'FAIL'
$result['tested_at_utc'] = [DateTime]::UtcNow.ToString('o')

$extractRoot = Join-Path $env:TEMP ('wahu-win7-target-' + [Guid]::NewGuid().ToString('N'))
$preflightPath = Join-Path $env:TEMP ('wahu-win7-preflight-' + [Guid]::NewGuid().ToString('N') + '.json')
$bootstrap1 = Join-Path $env:TEMP ('wahu-win7-bootstrap1-' + [Guid]::NewGuid().ToString('N') + '.txt')
$bootstrap2 = Join-Path $env:TEMP ('wahu-win7-bootstrap2-' + [Guid]::NewGuid().ToString('N') + '.txt')
$installedRoot = Join-Path $env:LOCALAPPDATA 'WAHU Kids Learn'
$installedDb = Join-Path $installedRoot 'data\learning.db'
$installedRootExistedBefore = Test-Path -LiteralPath $installedRoot
$installedDbHashBefore = Get-OptionalSha256 $installedDb
$uiProcess = $null

try {
    Assert-True (Test-Path -LiteralPath $PortableZip -PathType Leaf) ('Portable ZIP not found: ' + $PortableZip)
    $portableSha = Get-Sha256 $PortableZip
    $result['portable_sha256'] = $portableSha
    Assert-True ($portableSha -eq $ExpectedPortableSha256) ('Portable ZIP SHA-256 mismatch. expected=' + $ExpectedPortableSha256 + ' actual=' + $portableSha)

    $os = Get-WmiObject Win32_OperatingSystem -ErrorAction Stop
    $version = [Environment]::OSVersion.Version
    $spMajor = [int]$os.ServicePackMajorVersion
    $result['machine_name'] = [Environment]::MachineName
    $result['os_caption'] = [string]$os.Caption
    $result['os_version'] = $version.ToString()
    $result['os_major'] = $version.Major
    $result['os_minor'] = $version.Minor
    $result['os_build'] = $version.Build
    $result['service_pack_major'] = $spMajor
    $result['os_architecture'] = [string]$os.OSArchitecture
    $result['powershell_version'] = $PSVersionTable.PSVersion.ToString()
    $result['is_target_windows7'] = ($version.Major -eq 6 -and $version.Minor -eq 1)
    Assert-True ([bool]$result['is_target_windows7']) ('This target is not Windows 7: ' + $version.ToString())
    Assert-True ($version.Build -ge 7601) ('Windows 7 build is older than SP1: ' + $version.Build)
    Assert-True ($spMajor -ge 1) ('Windows 7 Service Pack 1 is required. ServicePackMajorVersion=' + $spMajor)

    $netRelease = Get-Net48Release
    $result['net_framework_release_registry'] = $netRelease
    Assert-True ($netRelease -ge 528040) ('.NET Framework 4.8 is required. Release=' + $netRelease)

    Expand-Zip $PortableZip $extractRoot
    $required = @(
        'portable.mode',
        'WAHUKidsLearn.exe',
        'WAHU.SetupPreflight.exe',
        'System.Data.SQLite.dll',
        'e_sqlite3.dll',
        'config\install_manifest_v1.json',
        'data\schema\006_attempt_commit_key_immutability.sql',
        'content_packs\math_grade2_v1\question_bank_v1.json',
        'content_packs\math_grade2_v1\game_events_v1.json',
        'Assets\Generated\Ready\ASSET_SELECTION_MANIFEST.json'
    )
    foreach ($rel in $required) {
        Assert-True (Test-Path -LiteralPath (Join-Path $extractRoot $rel) -PathType Leaf) ('Portable payload missing: ' + $rel)
    }

    $artManifestPath = Join-Path $extractRoot 'Assets\Generated\Ready\ASSET_SELECTION_MANIFEST.json'
    $artManifestSha = Get-Sha256 $artManifestPath
    $result['production_art_manifest_sha256'] = $artManifestSha
    Assert-True ($artManifestSha -eq $ExpectedProductionArtManifestSha256) ('Production-art manifest SHA mismatch. expected=' + $ExpectedProductionArtManifestSha256 + ' actual=' + $artManifestSha)

    $runtimeManifest = Convert-JsonTextToObject (Read-Text (Join-Path $extractRoot 'config\install_manifest_v1.json'))
    $runtimeVersion = [string]$runtimeManifest['app_version']
    $result['runtime_manifest_app_version'] = $runtimeVersion
    Assert-True ($runtimeVersion -eq $AppVersion) ('Runtime app version mismatch: ' + $runtimeVersion)

    $preflightExe = Join-Path $extractRoot 'WAHU.SetupPreflight.exe'
    $appExe = Join-Path $extractRoot 'WAHUKidsLearn.exe'
    $preflight = Start-Process -FilePath $preflightExe -ArgumentList @('--out', $preflightPath, '--signature-target', $appExe) -PassThru -Wait
    $result['preflight_exit'] = $preflight.ExitCode
    Assert-True ($preflight.ExitCode -eq 0) ('Preflight exit=' + $preflight.ExitCode)
    Assert-True (Test-Path -LiteralPath $preflightPath -PathType Leaf) 'Preflight report missing.'

    $preflightJson = Convert-JsonTextToObject (Read-Text $preflightPath)
    $result['preflight_os_version'] = [string]$preflightJson['os_version']
    $result['preflight_service_pack'] = [string]$preflightJson['service_pack']
    $result['preflight_is_target_windows7'] = [bool]$preflightJson['is_target_windows7']
    $result['preflight_os_supported'] = [bool]$preflightJson['os_supported']
    $result['preflight_process_arch'] = [string]$preflightJson['process_arch']
    $result['preflight_net_release'] = [int]$preflightJson['net_framework_release']
    $result['preflight_net48_or_later'] = [bool]$preflightJson['net48_or_later']
    $result['preflight_sha2_readiness'] = [string]$preflightJson['legacy_sha2_readiness']
    $result['preflight_compatibility'] = [string]$preflightJson['compatibility_level']
    Assert-True ([bool]$result['preflight_is_target_windows7']) 'Preflight did not identify Windows 7.'
    Assert-True ([bool]$result['preflight_os_supported']) 'Preflight reports unsupported OS.'
    Assert-True ([bool]$result['preflight_net48_or_later']) 'Preflight reports .NET 4.8 unavailable.'
    Assert-True ([string]$result['preflight_process_arch'] -eq 'x86') ('App preflight is not x86: ' + [string]$result['preflight_process_arch'])
    Assert-True ([string]$result['preflight_compatibility'] -eq 'WIN7_SP1_RUNTIME_COMPATIBLE') ('Win7 readiness is not fully compatible: ' + [string]$result['preflight_compatibility'])

    $boot = Start-Process -FilePath $appExe -ArgumentList @('--bootstrap-smoke', '--out', $bootstrap1, '--portable') -PassThru -Wait
    $result['bootstrap_first_exit'] = $boot.ExitCode
    Assert-True ($boot.ExitCode -eq 0) ('First bootstrap exit=' + $boot.ExitCode)
    $bootText = Read-Text $bootstrap1
    Assert-True ((Get-KeyValue $bootText 'result') -eq 'PASS') 'First bootstrap result is not PASS.'
    Assert-True ((Get-KeyValue $bootText 'storage_mode') -eq 'PORTABLE') 'First bootstrap lost PORTABLE storage mode.'
    Assert-True ((Get-KeyValue $bootText 'app_version') -eq $AppVersion) 'First bootstrap app_version mismatch.'
    Assert-True ((Get-KeyValue $bootText 'process_arch') -eq 'x86') 'Runtime process is not x86.'
    Assert-True ((Get-KeyValue $bootText 'compatibility') -eq 'WIN7_SP1_RUNTIME_COMPATIBLE') 'Runtime bootstrap compatibility is not WIN7_SP1_RUNTIME_COMPATIBLE.'
    Assert-True ((Get-KeyValue $bootText 'schema_version') -eq '6') 'Runtime schema version is not 6.'
    Assert-True ((Get-KeyValue $bootText 'migration_version') -eq '6') 'Runtime migration version is not 6.'
    Assert-True ((Get-KeyValue $bootText 'integrity') -eq 'ok') 'SQLite integrity is not ok.'
    Assert-True ((Get-KeyValue $bootText 'foreign_key_issues') -eq '0') 'SQLite foreign-key issues detected.'
    Assert-True ((Get-KeyValue $bootText 'verified_content_packs') -eq '2') 'Bundled content packs were not both verified.'
    $result['bootstrap_schema_version'] = [int](Get-KeyValue $bootText 'schema_version')
    $result['bootstrap_migration_version'] = [int](Get-KeyValue $bootText 'migration_version')
    $result['bootstrap_provider_version'] = Get-KeyValue $bootText 'provider_version'
    $result['bootstrap_sqlite_version'] = Get-KeyValue $bootText 'sqlite_version'
    $result['bootstrap_integrity'] = Get-KeyValue $bootText 'integrity'
    $result['bootstrap_foreign_key_issues'] = [int](Get-KeyValue $bootText 'foreign_key_issues')
    $result['bootstrap_verified_content_packs'] = [int](Get-KeyValue $bootText 'verified_content_packs')

    $bootAgain = Start-Process -FilePath $appExe -ArgumentList @('--bootstrap-smoke', '--out', $bootstrap2, '--portable') -PassThru -Wait
    $result['bootstrap_second_exit'] = $bootAgain.ExitCode
    Assert-True ($bootAgain.ExitCode -eq 0) ('Second bootstrap exit=' + $bootAgain.ExitCode)
    $bootText2 = Read-Text $bootstrap2
    Assert-True ((Get-KeyValue $bootText2 'result') -eq 'PASS') 'Second bootstrap result is not PASS.'
    Assert-True ((Get-KeyValue $bootText2 'db_created') -eq 'False') 'Second bootstrap unexpectedly recreated the DB schema.'

    $uiProcess = Start-Process -FilePath $appExe -ArgumentList @('--portable') -PassThru
    $windowSeen = Wait-ForMainWindow $uiProcess $UiWindowTimeoutSeconds
    $result['ui_process_started'] = $true
    $result['ui_window_seen'] = $windowSeen
    if ($windowSeen) {
        $uiProcess.Refresh()
        $result['ui_window_title'] = [string]$uiProcess.MainWindowTitle
    }
    Assert-True $windowSeen 'WAHU child UI did not expose a main window within the target timeout.'
    Stop-TestProcess $uiProcess
    $uiProcess = $null

    $installedRootExistsAfter = Test-Path -LiteralPath $installedRoot
    $installedDbHashAfter = Get-OptionalSha256 $installedDb
    $result['installed_root_existed_before'] = $installedRootExistedBefore
    $result['installed_root_exists_after'] = $installedRootExistsAfter
    $result['installed_db_hash_before'] = $installedDbHashBefore
    $result['installed_db_hash_after'] = $installedDbHashAfter
    if (-not $installedRootExistedBefore) { Assert-True (-not $installedRootExistsAfter) 'Portable Win7 target test created installed learner-data root.' }
    if ($installedDbHashBefore -ne $null) {
        Assert-True ($installedDbHashBefore -eq $installedDbHashAfter) 'Portable Win7 target test modified installed learner DB.'
    }
    else {
        Assert-True ($installedDbHashAfter -eq $null) 'Portable Win7 target test created installed learner DB.'
    }

    $result['target_status'] = 'TARGET_SMOKE_VERIFIED'
    $result['test_result'] = 'PASS'
}
catch {
    $result['test_result'] = 'FAIL'
    $result['target_status'] = 'NOT_VERIFIED'
    $result['error_type'] = $_.Exception.GetType().FullName
    $result['error'] = $_.Exception.Message
}
finally {
    Stop-TestProcess $uiProcess
    $result['tested_at_utc'] = [DateTime]::UtcNow.ToString('o')
    try { Write-JsonFile $result $ReportPath } catch { }
    Remove-Item -LiteralPath $preflightPath -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $bootstrap1 -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $bootstrap2 -Force -ErrorAction SilentlyContinue
    if (Test-Path -LiteralPath $extractRoot) { Remove-Item -LiteralPath $extractRoot -Recurse -Force -ErrorAction SilentlyContinue }
}

if ([string]$result['test_result'] -ne 'PASS') {
    Write-Host ('WIN7_TARGET_SMOKE_FAIL report=' + $ReportPath + ' error=' + [string]$result['error'])
    exit 1
}

Write-Host ('WIN7_TARGET_SMOKE_PASS report=' + $ReportPath + ' portable_sha256=' + [string]$result['portable_sha256'])
exit 0
