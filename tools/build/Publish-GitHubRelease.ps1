param(
    [Parameter(Mandatory = $true)]
    [string]$AppVersion,
    [switch]$ReplaceExistingAssets,
    [switch]$SkipE2E
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
Set-Location $root

function Require-File([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { throw "Thiếu file bắt buộc: $Path" }
}

function Sha256([string]$Path) {
    (Get-FileHash -Algorithm SHA256 -LiteralPath $Path).Hash
}

if ([string]::IsNullOrWhiteSpace($AppVersion)) { throw 'AppVersion không được rỗng.' }
if ($AppVersion -notmatch '^\d+\.\d+\.\d+(?:-[A-Za-z0-9.-]+)?$') { throw "AppVersion không hợp lệ: $AppVersion" }

$gitStatus = (& git status --porcelain=v1)
if ($LASTEXITCODE -ne 0) { throw 'Không đọc được git status.' }
if ($gitStatus) {
    throw "REFUSE_RELEASE_DIRTY_TREE: working tree chưa sạch. Commit/push toàn bộ source cần phát hành trước."
}

& git fetch origin main --quiet
if ($LASTEXITCODE -ne 0) { throw 'git fetch origin main thất bại.' }
$localHead = (& git rev-parse HEAD).Trim()
$remoteHead = (& git rev-parse origin/main).Trim()
if ($localHead -ne $remoteHead) {
    throw "REFUSE_RELEASE_HEAD_MISMATCH: local HEAD=$localHead, origin/main=$remoteHead"
}

$repo = (& gh repo view --json nameWithOwner -q .nameWithOwner).Trim()
if ($LASTEXITCODE -ne 0 -or $repo -ne 'WahuVN/WAHU-Kids-Learn') {
    throw "Sai GitHub repo hiện tại: $repo"
}
$visibility = (& gh repo view WahuVN/WAHU-Kids-Learn --json visibility -q .visibility).Trim()
if ($visibility -ne 'PUBLIC') { throw 'Release feed yêu cầu repo WahuVN/WAHU-Kids-Learn đang PUBLIC.' }

$installer = Join-Path $root "build\installer\WAHU-Kids-Learn-Setup-win7-x86-$AppVersion.exe"
$installerSha = Join-Path $root "build\installer\WAHU-Kids-Learn-Setup-win7-x86-$AppVersion.sha256"
$portable = Join-Path $root "build\portable\WAHU-Kids-Learn-Portable-win7-x86-$AppVersion.zip"
$portableSha = Join-Path $root "build\portable\WAHU-Kids-Learn-Portable-win7-x86-$AppVersion.sha256"
$updateManifest = Join-Path $root 'build\release\update-manifest.json'
$releaseManifest = Join-Path $root 'build\win7_x86\release_manifest_dev.json'
$allInOne = Join-Path $root "build\allinone\WAHU-Kids-Learn-Portable-AllInOne-win7-x86-$AppVersion.exe"
$allInOneSha = Join-Path $root "build\allinone\WAHU-Kids-Learn-Portable-AllInOne-win7-x86-$AppVersion.sha256"

foreach ($file in @($installer,$installerSha,$portable,$portableSha,$updateManifest,$releaseManifest)) { Require-File $file }

$runtimeManifest = Get-Content -Raw -LiteralPath $releaseManifest | ConvertFrom-Json
if ($runtimeManifest.app_version -ne $AppVersion) { throw 'release_manifest_dev.json app_version không khớp AppVersion.' }
if ($runtimeManifest.git_commit -ne $localHead) { throw "Build không thuộc HEAD hiện tại. build=$($runtimeManifest.git_commit), HEAD=$localHead" }

$releaseHasAllInOne = $false
$allInOneHash = $null
if ($runtimeManifest.PSObject.Properties.Name -contains 'all_in_one' -and $runtimeManifest.all_in_one.enabled -eq $true) {
    Require-File $allInOne
    Require-File $allInOneSha
    $allInOneHash = Sha256 $allInOne
    if (-not [string]::Equals([string]$runtimeManifest.all_in_one.artifact_sha256, $allInOneHash, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Portable All-in-One SHA-256 không khớp release manifest.'
    }
    if ([int64]$runtimeManifest.all_in_one.artifact_bytes -ne (Get-Item -LiteralPath $allInOne).Length) {
        throw 'Portable All-in-One size không khớp release manifest.'
    }
    if ($runtimeManifest.all_in_one.bundles_net48_offline -ne $true -or $runtimeManifest.all_in_one.e2e -ne 'PASS') {
        throw 'Portable All-in-One chưa có evidence bundled .NET 4.8 + E2E PASS.'
    }
    $allInOneShaLine = Get-Content -LiteralPath $allInOneSha | Select-Object -First 1
    if ($allInOneShaLine -notmatch [regex]::Escape($allInOneHash)) { throw 'File .sha256 của Portable All-in-One không khớp.' }
    $releaseHasAllInOne = $true
}

$update = Get-Content -Raw -LiteralPath $updateManifest | ConvertFrom-Json
if ($update.app_version -ne $AppVersion) { throw 'update-manifest.json app_version không khớp.' }
$actualInstallerSha = Sha256 $installer
if ($update.installer_sha256 -ne $actualInstallerSha) { throw 'update-manifest installer SHA-256 không khớp binary.' }
if ((Get-Item -LiteralPath $installer).Length -ne [int64]$update.installer_bytes) { throw 'update-manifest installer_bytes không khớp.' }
$shaLine = (Get-Content -LiteralPath $installerSha | Select-Object -First 1)
if ($shaLine -notmatch [regex]::Escape($actualInstallerSha)) { throw 'File .sha256 của installer không khớp.' }

$isDev = $AppVersion -match '-dev(?:$|[.-])'
if ($isDev) {
    if ($update.channel -ne 'dev' -or $update.production_signed -ne $false) {
        throw 'Dev release phải có channel=dev và production_signed=false.'
    }
} else {
    if ($update.channel -ne 'stable' -or $update.production_signed -ne $true) {
        throw 'REFUSE_UNSIGNED_STABLE: stable release phải production_signed=true.'
    }
    if ($runtimeManifest.signed -ne $true) { throw 'REFUSE_UNSIGNED_STABLE: release manifest chưa xác nhận signed=true.' }
}

if (-not $SkipE2E) {
    powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $root 'tools\build\Test-PortableE2E.ps1') -AppVersion $AppVersion
    if ($LASTEXITCODE -ne 0) { throw "Portable E2E fail: $LASTEXITCODE" }
    powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $root 'tools\build\Test-InstallerE2E.ps1') -AppVersion $AppVersion
    if ($LASTEXITCODE -ne 0) { throw "Installer E2E fail: $LASTEXITCODE" }
}

$tag = "v$AppVersion"
$title = "WAHU Kids Learn $AppVersion"
$feedTag = if ($isDev) { 'update-dev' } else { 'update-stable' }
$feedUrl = "https://github.com/WahuVN/WAHU-Kids-Learn/releases/download/$feedTag/update-manifest.json"
$allInOneReleaseNote = if ($releaseHasAllInOne) { "- Portable All-in-One: bundled official .NET Framework 4.8 offline runtime; SHA-256: $allInOneHash" } else { '- Portable All-in-One: not included in this build' }
$notes = @"
WAHU Kids Learn $AppVersion

- Windows 7 SP1+ / x86 runtime
- .NET Framework 4.8
- Installer SHA-256: $actualInstallerSha
$allInOneReleaseNote
- Git commit: $localHead
- Update channel: $($update.channel)
- Channel feed: $feedUrl

Learner data is stored outside the application directory and is preserved across reinstall/update by design.
"@

$assets = @($installer,$installerSha,$portable,$portableSha,$updateManifest)
if ($releaseHasAllInOne) { $assets += @($allInOne,$allInOneSha) }
$exists = $false
$previousErrorActionPreference = $ErrorActionPreference
try {
    $ErrorActionPreference = 'Continue'
    & gh release view $tag --repo WahuVN/WAHU-Kids-Learn 1>$null 2>$null
    $exists = ($LASTEXITCODE -eq 0)
}
finally { $ErrorActionPreference = $previousErrorActionPreference }

if ($exists) {
    if (-not $ReplaceExistingAssets) { throw "Release $tag đã tồn tại. Dùng -ReplaceExistingAssets nếu chủ động thay asset cùng tag." }
    $editArgs = @('release','edit',$tag,'--repo','WahuVN/WAHU-Kids-Learn','--title',$title,'--notes',$notes)
    if ($isDev) { $editArgs += '--prerelease' } else { $editArgs += '--latest' }
    & gh @editArgs
    if ($LASTEXITCODE -ne 0) { throw 'gh release edit thất bại.' }
    & gh release upload $tag @assets --repo WahuVN/WAHU-Kids-Learn --clobber
    if ($LASTEXITCODE -ne 0) { throw 'gh release upload thất bại.' }
} else {
    $args = @('release','create',$tag) + $assets + @('--repo','WahuVN/WAHU-Kids-Learn','--target',$localHead,'--title',$title,'--notes',$notes)
    if ($isDev) { $args += @('--prerelease','--latest=false') } else { $args += '--latest' }
    & gh @args
    if ($LASTEXITCODE -ne 0) { throw 'gh release create thất bại.' }
}

# Fixed technical release used as the channel pointer. Only update-manifest.json is mutable here.
$feedExists = $false
$previousErrorActionPreference = $ErrorActionPreference
try {
    $ErrorActionPreference = 'Continue'
    & gh release view $feedTag --repo WahuVN/WAHU-Kids-Learn 1>$null 2>$null
    $feedExists = ($LASTEXITCODE -eq 0)
}
finally { $ErrorActionPreference = $previousErrorActionPreference }

if (-not $feedExists) {
    & gh release create $feedTag $updateManifest --repo WahuVN/WAHU-Kids-Learn --target $localHead --title "WAHU Update Feed - $($update.channel)" --notes "Technical update pointer for the $($update.channel) channel." --prerelease --latest=false
    if ($LASTEXITCODE -ne 0) { throw 'Không tạo được channel update feed.' }
} else {
    & gh release upload $feedTag $updateManifest --repo WahuVN/WAHU-Kids-Learn --clobber
    if ($LASTEXITCODE -ne 0) { throw 'Không cập nhật được channel update feed.' }
}

$published = (& gh release view $tag --repo WahuVN/WAHU-Kids-Learn --json url -q .url).Trim()
$feedJson = (& gh release view $feedTag --repo WahuVN/WAHU-Kids-Learn --json assets) | ConvertFrom-Json
$feedAssets = @($feedJson.assets | Where-Object { $_.name -eq 'update-manifest.json' })
if ($feedAssets.Count -ne 1) { throw 'Channel feed không có đúng một update-manifest.json sau publish.' }
Write-Host "GITHUB_RELEASE_PASS tag=$tag url=$published"
Write-Host "UPDATE_FEED=$feedUrl"
