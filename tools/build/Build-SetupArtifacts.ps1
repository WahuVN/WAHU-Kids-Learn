param(
    [string]$Configuration = 'Release',
    [string]$AppVersion = '0.1.0-dev',
    [switch]$CompileInstaller,
    [switch]$RequireInstaller
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
Set-Location $root

function Find-FirstExisting([string[]]$Paths) {
    foreach ($p in $Paths) { if ($p -and (Test-Path -LiteralPath $p)) { return $p } }
    return $null
}

function Require-File([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { throw "Thiếu file bắt buộc: $Path" }
}

function Sha256([string]$Path) {
    (Get-FileHash -Algorithm SHA256 -LiteralPath $Path).Hash
}

function Invoke-SmokeWithAssertions {
    param(
        [string]$Name,
        [string]$Path,
        [string]$PassPrefix,
        [object[]]$Arguments = @()
    )

    $output = @(& $Path @Arguments 2>&1)
    $exitCode = $LASTEXITCODE
    foreach ($line in $output) { Write-Host ([string]$line) }
    if ($exitCode -ne 0) { throw "$Name fail: $exitCode" }

    $pattern = '^' + [regex]::Escape($PassPrefix) + '\s+assertions=(\d+)\s*$'
    $matches = @()
    foreach ($line in $output) {
        $candidate = [regex]::Match([string]$line, $pattern)
        if ($candidate.Success) { $matches += $candidate }
    }
    if ($matches.Count -ne 1) {
        throw "$Name did not publish exactly one assertion count with prefix $PassPrefix."
    }
    return [int]$matches[0].Groups[1].Value
}

function Get-GitSourceStatus {
    param([string]$GitPath)
    return @(& $GitPath status --porcelain=v1 --untracked-files=all 2>$null)
}

$git = (Get-Command git -ErrorAction SilentlyContinue).Source
if (-not $git) { throw 'Không tìm thấy Git để khóa provenance của artifact.' }
Write-Host '[0/15] Source provenance / clean-tree gate'
$sourceGitCommit = (& $git rev-parse HEAD 2>$null).Trim()
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($sourceGitCommit)) { throw 'Không đọc được Git HEAD cho release artifact.' }
$sourceStatusBefore = @(Get-GitSourceStatus -GitPath $git)
if ($sourceStatusBefore.Count -ne 0) {
    throw ("REFUSE_DIRTY_SOURCE: release artifact chỉ được build từ Git tree sạch. Dirty entries: " + (($sourceStatusBefore | ForEach-Object { [string]$_ }) -join '; '))
}
& $git diff --check
if ($LASTEXITCODE -ne 0) { throw 'Git diff --check fail trước release build.' }
Write-Host "SOURCE_PROVENANCE_START_PASS git_commit=$sourceGitCommit"

$msbuild = Find-FirstExisting @(
    'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe',
    'C:\Program Files\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe',
    'C:\Windows\Microsoft.NET\Framework\v4.0.30319\MSBuild.exe'
)
if (-not $msbuild) { throw 'Không tìm thấy MSBuild.' }

Write-Host "[1/15] Restore + rebuild solution net48/x86 bằng $msbuild"
& $msbuild 'WAHUKidsLearn.sln' /restore /m /t:Rebuild "/p:Configuration=$Configuration" /p:Platform=x86 /v:minimal /nologo
if ($LASTEXITCODE -ne 0) { throw "MSBuild fail: $LASTEXITCODE" }

Write-Host '[1b/15] Build Child UI render smoke x86'
& $msbuild 'tests\ChildUiRuntimeSmoke\WAHU.ChildUiRuntimeSmoke.csproj' /restore /t:Rebuild "/p:Configuration=$Configuration" /p:Platform=x86 /v:minimal /nologo
if ($LASTEXITCODE -ne 0) { throw "Child UI smoke build fail: $LASTEXITCODE" }

$preflightSmoke = Join-Path $root "tests\SetupPreflightSmoke\bin\$Configuration\WAHU.SetupPreflight.Smoke.exe"
$behaviorSmoke = Join-Path $root "tests\BehaviorRuntimeSmoke\bin\$Configuration\WAHU.BehaviorRuntimeSmoke.exe"
$learningSessionSmoke = Join-Path $root "tests\LearningSessionRuntimeSmoke\bin\$Configuration\WAHU.LearningSessionRuntimeSmoke.exe"
$motionSmoke = Join-Path $root "tests\MotionRuntimeSmoke\bin\$Configuration\WAHU.MotionRuntimeSmoke.exe"
$childUiSmoke = Join-Path $root "tests\ChildUiRuntimeSmoke\bin\$Configuration\WAHU.ChildUiRuntimeSmoke.exe"
$contentSmoke = Join-Path $root "tests\ContentRuntimeSmoke\bin\$Configuration\WAHU.ContentRuntimeSmoke.exe"
$securitySmoke = Join-Path $root "tests\SecurityRuntimeSmoke\bin\$Configuration\WAHU.SecurityRuntimeSmoke.exe"
$audioSmoke = Join-Path $root "tests\AudioRuntimeSmoke\bin\$Configuration\WAHU.AudioRuntimeSmoke.exe"
$performanceSmoke = Join-Path $root "tests\PerformanceRuntimeSmoke\bin\$Configuration\WAHU.PerformanceRuntimeSmoke.exe"
$updateSmoke = Join-Path $root "tests\UpdateRuntimeSmoke\bin\$Configuration\WAHU.UpdateRuntimeSmoke.exe"
$sqliteSmoke = Join-Path $root "tests\SQLiteRuntimeSmoke\bin\$Configuration\WAHU.SQLiteRuntimeSmoke.exe"
$schemaSource = Join-Path $root 'data\schema\001_initial.sql'
$mathTemplateSource = Join-Path $root 'content_packs\math_grade2_v1\verified_templates_v1.json'
$mathLessonCatalogSource = Join-Path $root 'content_packs\math_grade2_v1\lesson_catalog_v1.json'
$mathQuestionBankSource = Join-Path $root 'content_packs\math_grade2_v1\question_bank_v1.json'
$mathGameEventSource = Join-Path $root 'content_packs\math_grade2_v1\game_events_v1.json'
Require-File $preflightSmoke
Require-File $behaviorSmoke
Require-File $learningSessionSmoke
Require-File $motionSmoke
Require-File $childUiSmoke
Require-File $contentSmoke
Require-File $securitySmoke
Require-File $audioSmoke
Require-File $performanceSmoke
Require-File $updateSmoke
Require-File $sqliteSmoke
Require-File $schemaSource
Require-File $mathTemplateSource
Require-File $mathLessonCatalogSource
Require-File $mathQuestionBankSource
Require-File $mathGameEventSource

Write-Host '[2/15] Setup preflight smoke'
$preflightAssertions = Invoke-SmokeWithAssertions -Name 'SetupPreflight smoke' -Path $preflightSmoke -PassPrefix 'SETUP_PREFLIGHT_SMOKE_PASS'

Write-Host '[3/15] Behavior runtime smoke'
$behaviorAssertions = Invoke-SmokeWithAssertions -Name 'Behavior runtime smoke' -Path $behaviorSmoke -PassPrefix 'BEHAVIOR_RUNTIME_SMOKE_PASS'

Write-Host '[4/15] Learning session vertical-slice smoke'
$learningSessionAssertions = Invoke-SmokeWithAssertions -Name 'Learning session runtime smoke' -Path $learningSessionSmoke -PassPrefix 'LEARNING_SESSION_RUNTIME_SMOKE_PASS' -Arguments @($schemaSource, $mathTemplateSource)

Write-Host '[5/15] Motion runtime smoke'
$motionAssertions = Invoke-SmokeWithAssertions -Name 'Motion runtime smoke' -Path $motionSmoke -PassPrefix 'MOTION_RUNTIME_SMOKE_PASS'

Write-Host '[5b/15] Child UI GDI+ render smoke (100% + 125%)'
$childUiAssertions = Invoke-SmokeWithAssertions -Name 'Child UI runtime smoke' -Path $childUiSmoke -PassPrefix 'CHILD_UI_RUNTIME_SMOKE_PASS'

Write-Host '[6/15] Content runtime + secure import smoke'
$contentAssertions = Invoke-SmokeWithAssertions -Name 'Content runtime smoke' -Path $contentSmoke -PassPrefix 'CONTENT_RUNTIME_SMOKE_PASS'

Write-Host '[6b/15] Production Math content + game-event validator'
$python = (Get-Command python -ErrorAction SilentlyContinue).Source
if (-not $python) { throw 'Không tìm thấy Python để chạy Math content validator.' }
$validatorOutput = @(& $python -B 'tools\math_content_validator\validate_math_content.py' --json 2>&1)
$validatorExit = $LASTEXITCODE
foreach ($line in $validatorOutput) { Write-Host ([string]$line) }
if ($validatorExit -ne 0) { throw "Math content validator fail: $validatorExit" }
$validatorText = ($validatorOutput | ForEach-Object { [string]$_ }) -join "`n"
$validatorResult = $validatorText | ConvertFrom-Json
if (-not $validatorResult.ok) { throw 'Math content validator returned ok=false.' }
$mathValidatorQuestions = [int]$validatorResult.metrics.questions
$mathValidatorValidQuestions = [int]$validatorResult.metrics.valid_questions
$mathValidatorEvents = [int]$validatorResult.metrics.game_events
$mathValidatorFirstFive = [int]$validatorResult.metrics.game_events_first_five
if ($mathValidatorQuestions -le 0 -or $mathValidatorValidQuestions -ne $mathValidatorQuestions) {
    throw "Math content validator question coverage invalid: valid=$mathValidatorValidQuestions total=$mathValidatorQuestions"
}
if ($mathValidatorEvents -le 0 -or $mathValidatorFirstFive -lt 5) {
    throw "Math game-event validator coverage invalid: events=$mathValidatorEvents first_five=$mathValidatorFirstFive"
}
Write-Host "MATH_CONTENT_VALIDATOR_PASS questions=$mathValidatorQuestions valid=$mathValidatorValidQuestions events=$mathValidatorEvents first_five=$mathValidatorFirstFive"

Write-Host '[6c/15] Full Math content/event/pool Python smoke'
$mathContentStdoutPath = [IO.Path]::GetTempFileName()
$mathContentStderrPath = [IO.Path]::GetTempFileName()
try {
    $mathContentProcess = Start-Process -FilePath $python -ArgumentList @('-B','-m','unittest','discover','tests/MathContentDataSmoke','-p','test*.py','-q') -RedirectStandardOutput $mathContentStdoutPath -RedirectStandardError $mathContentStderrPath -Wait -PassThru -NoNewWindow
    $mathContentTestExit = $mathContentProcess.ExitCode
    $mathContentStdout = if (Test-Path -LiteralPath $mathContentStdoutPath) { Get-Content -Raw -LiteralPath $mathContentStdoutPath } else { '' }
    $mathContentStderr = if (Test-Path -LiteralPath $mathContentStderrPath) { Get-Content -Raw -LiteralPath $mathContentStderrPath } else { '' }
    if (-not [string]::IsNullOrWhiteSpace($mathContentStdout)) { Write-Host $mathContentStdout.TrimEnd() }
    if (-not [string]::IsNullOrWhiteSpace($mathContentStderr)) { Write-Host $mathContentStderr.TrimEnd() }
    if ($mathContentTestExit -ne 0) { throw "Math content/event/pool smoke fail: $mathContentTestExit" }
    $mathContentTestText = $mathContentStdout + "`n" + $mathContentStderr
    $mathContentTestMatch = [regex]::Match($mathContentTestText, 'Ran\s+(\d+)\s+tests?')
    if (-not $mathContentTestMatch.Success) { throw 'Math content/event/pool smoke did not publish a test count.' }
    $mathContentTestCount = [int]$mathContentTestMatch.Groups[1].Value
    if ($mathContentTestCount -le 0) { throw 'Math content/event/pool smoke published zero tests.' }
    Write-Host "MATH_CONTENT_EVENT_POOL_SMOKE_PASS tests=$mathContentTestCount"
}
finally {
    Remove-Item -LiteralPath $mathContentStdoutPath,$mathContentStderrPath -Force -ErrorAction SilentlyContinue
}

Write-Host '[7/15] Parent PIN security smoke'
$securityAssertions = Invoke-SmokeWithAssertions -Name 'Security runtime smoke' -Path $securitySmoke -PassPrefix 'SECURITY_RUNTIME_SMOKE_PASS'

Write-Host '[8/15] Audio runtime smoke'
$audioAssertions = Invoke-SmokeWithAssertions -Name 'Audio runtime smoke' -Path $audioSmoke -PassPrefix 'AUDIO_RUNTIME_SMOKE_PASS'

Write-Host '[9/15] Performance autotune + degradation smoke'
$performanceAssertions = Invoke-SmokeWithAssertions -Name 'Performance runtime smoke' -Path $performanceSmoke -PassPrefix 'PERFORMANCE_RUNTIME_SMOKE_PASS'

Write-Host '[9b/15] GitHub updater manifest/staging smoke'
$updateAssertions = Invoke-SmokeWithAssertions -Name 'Update runtime smoke' -Path $updateSmoke -PassPrefix 'UPDATE_RUNTIME_SMOKE_PASS'

Write-Host '[10/15] SQLite/Data smoke + backup/restore integration'
$sqliteAssertions = Invoke-SmokeWithAssertions -Name 'SQLite runtime smoke' -Path $sqliteSmoke -PassPrefix 'SQLITE_RUNTIME_SMOKE_PASS' -Arguments @($schemaSource)

Write-Host '[10b/15] Math session persistence / rescue lifecycle smoke'
$dotnet = (Get-Command dotnet -ErrorAction SilentlyContinue).Source
if (-not $dotnet) { throw 'Không tìm thấy dotnet CLI để build MathSessionPersistenceRuntimeSmoke.' }
$mathPersistenceProject = Join-Path $root 'tests\MathSessionPersistenceRuntimeSmoke\WAHU.MathSessionPersistenceRuntimeSmoke.csproj'
Require-File $mathPersistenceProject
& $dotnet build $mathPersistenceProject -c $Configuration '-p:Platform=x86' --nologo
if ($LASTEXITCODE -ne 0) { throw "Math persistence smoke build fail: $LASTEXITCODE" }
$mathPersistenceSmoke = Join-Path $root "tests\MathSessionPersistenceRuntimeSmoke\bin\x86\$Configuration\net48\WAHU.MathSessionPersistenceRuntimeSmoke.exe"
Require-File $mathPersistenceSmoke
$mathPersistenceAssertions = Invoke-SmokeWithAssertions -Name 'Math session persistence runtime smoke' -Path $mathPersistenceSmoke -PassPrefix 'MATH_SESSION_PERSISTENCE_RUNTIME_SMOKE_PASS'

$publish = Join-Path $root 'build\win7_x86\publish'
if (Test-Path $publish) { Remove-Item -LiteralPath $publish -Recurse -Force }
New-Item -ItemType Directory -Force -Path $publish | Out-Null

Write-Host '[11/15] Stage publish tree'
$appOut = Join-Path $root "src\App\bin\$Configuration"
$preOut = Join-Path $root "tools\setup_preflight\bin\$Configuration"
$updaterOut = Join-Path $root "src\Updater\bin\$Configuration"
$runtimeFiles = @(
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
    'System.Data.SQLite.dll',
    'e_sqlite3.dll'
)
foreach ($name in $runtimeFiles) {
    $src = Join-Path $appOut $name
    Require-File $src
    Copy-Item -LiteralPath $src -Destination $publish -Force
}
foreach ($name in @('WAHU.SetupPreflight.exe','WAHU.SetupPreflight.exe.config')) {
    $src = Join-Path $preOut $name
    Require-File $src
    Copy-Item -LiteralPath $src -Destination $publish -Force
}
$updaterExe = Join-Path $updaterOut 'WAHU.Updater.exe'
Require-File $updaterExe
Copy-Item -LiteralPath $updaterExe -Destination $publish -Force

$productionAssetGate = Join-Path $root 'tools\build\Test-ProductionAssetPayload.ps1'
Require-File $productionAssetGate
$sourceAssetReady = Join-Path $root 'src\App\Assets\Generated\Ready'
$sourceAssetManifest = Join-Path $sourceAssetReady 'ASSET_SELECTION_MANIFEST.json'
Require-File $sourceAssetManifest
$productionAssetManifestSha256 = Sha256 $sourceAssetManifest
$sourceAssetManifestJson = Get-Content -Raw -LiteralPath $sourceAssetManifest | ConvertFrom-Json
$productionAssetPngCount = [int]$sourceAssetManifestJson.summary.totalSelected
& $productionAssetGate -Mode Tree -Path $sourceAssetReady -ExpectedManifestSha256 $productionAssetManifestSha256

$appAssetReady = Join-Path $appOut 'Assets\Generated\Ready'
& $productionAssetGate -Mode Tree -Path $appAssetReady -ExpectedManifestSha256 $productionAssetManifestSha256
$publishAssetReady = Join-Path $publish 'Assets\Generated\Ready'
New-Item -ItemType Directory -Force -Path $publishAssetReady | Out-Null
Copy-Item -Path (Join-Path $appAssetReady '*') -Destination $publishAssetReady -Recurse -Force
& $productionAssetGate -Mode Tree -Path $publishAssetReady -ExpectedManifestSha256 $productionAssetManifestSha256
Write-Host "PRODUCTION_ASSET_STAGE_GATE_PASS png_count=$productionAssetPngCount manifest_sha256=$productionAssetManifestSha256"

$dirs = @('config','policies','content_packs','curriculum','assets','data\schema')
foreach ($d in $dirs) { New-Item -ItemType Directory -Force -Path (Join-Path $publish $d) | Out-Null }
Copy-Item 'setup\config\*.json' (Join-Path $publish 'config') -Force
$runtimeInstallManifest = Join-Path $publish 'config\install_manifest_v1.json'
$runtimeInstall = Get-Content -Raw -LiteralPath $runtimeInstallManifest | ConvertFrom-Json
$runtimeInstall.app_version = $AppVersion
$runtimeInstall | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $runtimeInstallManifest -Encoding UTF8
Copy-Item 'core\motion\motion_policy_v1.json' (Join-Path $publish 'policies') -Force
Copy-Item 'core\learning\behavior\*.json' (Join-Path $publish 'policies') -Force
Copy-Item 'content_packs\*' (Join-Path $publish 'content_packs') -Recurse -Force
Copy-Item 'curriculum\*' (Join-Path $publish 'curriculum') -Recurse -Force
Copy-Item 'assets\verified_vectors' (Join-Path $publish 'assets') -Recurse -Force
Copy-Item 'data\schema\*.sql' (Join-Path $publish 'data\schema') -Force

# Hard deployment guards: these must be in the actual staged installer payload.
foreach ($name in @('WAHU.Learning.dll','WAHU.Motion.dll','WAHU.Content.dll','WAHU.Audio.dll','WAHU.Security.dll','WAHU.Performance.dll','WAHU.Session.dll','WAHU.Data.dll','WAHU.Updater.exe','System.Data.SQLite.dll','e_sqlite3.dll','data\schema\001_initial.sql','data\schema\002_attempt_immutability.sql','data\schema\003_math_attempt_idempotency_runtime.sql','data\schema\004_math_lesson_progress.sql','data\schema\005_math_runtime_pack_identity.sql','content_packs\math_grade2_v1\verified_templates_v1.json','content_packs\math_grade2_v1\lesson_catalog_v1.json','content_packs\math_grade2_v1\question_bank_v1.json','content_packs\math_grade2_v1\game_events_v1.json')) {
    Require-File (Join-Path $publish $name)
}

Write-Host '[12/15] Validate staged JSON + run staged preflight'
Get-ChildItem -LiteralPath $publish -Recurse -Filter *.json -File | ForEach-Object {
    $null = Get-Content -Raw -LiteralPath $_.FullName | ConvertFrom-Json
}
$reportPath = Join-Path $root 'build\preflight-publish.json'
& (Join-Path $publish 'WAHU.SetupPreflight.exe') --out $reportPath --signature-target (Join-Path $publish 'WAHUKidsLearn.exe')
if ($LASTEXITCODE -ne 0) { throw "Staged preflight fail: $LASTEXITCODE" }
$null = Get-Content -Raw -LiteralPath $reportPath | ConvertFrom-Json

Write-Host '[13/15] Generate reproducible hashes + dev release manifest'
$hashPath = Join-Path $root 'build\win7_x86\SHA256SUMS.txt'
$lines = Get-ChildItem -LiteralPath $publish -Recurse -File | Sort-Object FullName | ForEach-Object {
    $rel = $_.FullName.Substring($publish.Length).TrimStart('\')
    "$(Sha256 $_.FullName)  $rel"
}
$lines | Set-Content -LiteralPath $hashPath -Encoding ASCII

# Build a true portable payload. It is intentionally separate from installer publish
# and contains an explicit marker so runtime storage resolves to .\UserData.
$portableRoot = Join-Path $root 'build\win7_x86\portable'
if (Test-Path $portableRoot) { Remove-Item -LiteralPath $portableRoot -Recurse -Force }
New-Item -ItemType Directory -Force -Path $portableRoot | Out-Null
Copy-Item (Join-Path $publish '*') $portableRoot -Recurse -Force
Set-Content -LiteralPath (Join-Path $portableRoot 'portable.mode') -Value 'WAHU_KIDS_LEARN_PORTABLE_V1' -Encoding ASCII
if (Test-Path (Join-Path $portableRoot 'UserData')) { throw 'Portable package must never contain learner UserData.' }
$portableAssetReady = Join-Path $portableRoot 'Assets\Generated\Ready'
& $productionAssetGate -Mode Tree -Path $portableAssetReady -ExpectedManifestSha256 $productionAssetManifestSha256
Write-Host "PORTABLE_PRODUCTION_ASSET_TREE_GATE_PASS png_count=$productionAssetPngCount"

$portableHashPath = Join-Path $root 'build\win7_x86\PORTABLE_SHA256SUMS.txt'
Get-ChildItem -LiteralPath $portableRoot -Recurse -File | Sort-Object FullName | ForEach-Object {
    $rel = $_.FullName.Substring($portableRoot.Length).TrimStart('\')
    "$(Sha256 $_.FullName)  $rel"
} | Set-Content -LiteralPath $portableHashPath -Encoding ASCII

$portableOutDir = Join-Path $root 'build\portable'
New-Item -ItemType Directory -Force -Path $portableOutDir | Out-Null
$portableZip = Join-Path $portableOutDir "WAHU-Kids-Learn-Portable-win7-x86-$AppVersion.zip"
if (Test-Path $portableZip) { Remove-Item -LiteralPath $portableZip -Force }
Compress-Archive -Path (Join-Path $portableRoot '*') -DestinationPath $portableZip -CompressionLevel Optimal
Require-File $portableZip
& $productionAssetGate -Mode Zip -Path $portableZip -ExpectedManifestSha256 $productionAssetManifestSha256
Write-Host "PORTABLE_PRODUCTION_ASSET_ZIP_GATE_PASS png_count=$productionAssetPngCount"
$portableZipHash = Sha256 $portableZip
Set-Content -LiteralPath (Join-Path $portableOutDir "WAHU-Kids-Learn-Portable-win7-x86-$AppVersion.sha256") -Value "$portableZipHash  $(Split-Path $portableZip -Leaf)" -Encoding ASCII
Write-Host "PORTABLE_SHA256=$portableZipHash"

Write-Host '[13b/15] Portable bootstrap/data-isolation E2E'
$portableE2EScript = Join-Path $root 'tools\build\Test-PortableE2E.ps1'
Require-File $portableE2EScript
& $portableE2EScript -AppVersion $AppVersion -PortableZip $portableZip
$portableE2EReportPath = Join-Path $root 'build\portable_e2e_dev.json'
Require-File $portableE2EReportPath
$portableE2EReport = Get-Content -Raw -LiteralPath $portableE2EReportPath | ConvertFrom-Json
if ($portableE2EReport.test_result -ne 'PASS') { throw 'Portable E2E report is not PASS.' }
if ([int]$portableE2EReport.first_bootstrap_exit -ne 0 -or [int]$portableE2EReport.second_bootstrap_exit -ne 0) {
    throw 'Portable E2E bootstrap exit code is not zero.'
}
if ($portableE2EReport.zip_sha256 -ne $portableZipHash) { throw 'Portable E2E tested ZIP hash does not match the just-built artifact.' }
Write-Host "PORTABLE_E2E_GATE_PASS report=$portableE2EReportPath"

Write-Host '[13c/15] Final source provenance gate'
$gitCommit = (& $git rev-parse HEAD 2>$null).Trim()
if ($LASTEXITCODE -ne 0 -or $gitCommit -ne $sourceGitCommit) {
    throw "REFUSE_SOURCE_HEAD_CHANGED: HEAD đổi trong lúc build. start=$sourceGitCommit end=$gitCommit"
}
$sourceStatusAfter = @(Get-GitSourceStatus -GitPath $git)
if ($sourceStatusAfter.Count -ne 0) {
    throw ("REFUSE_SOURCE_MUTATED: source tree thay đổi trong lúc build. Entries: " + (($sourceStatusAfter | ForEach-Object { [string]$_ }) -join '; '))
}
& $git diff --check
if ($LASTEXITCODE -ne 0) { throw 'Git diff --check fail sau release build.' }
Write-Host "SOURCE_PROVENANCE_FINAL_PASS git_commit=$gitCommit"

$providerPath = Join-Path $publish 'System.Data.SQLite.dll'
$nativePath = Join-Path $publish 'e_sqlite3.dll'
$manifest = [ordered]@{
    schema_version = 5
    app_version = $AppVersion
    build_channel = 'dev'
    target_framework = 'net48'
    platform = 'x86'
    built_at_utc = [DateTime]::UtcNow.ToString('o')
    git_commit = $gitCommit
    msbuild = $msbuild
    signed = $false
    database = [ordered]@{
        schema_version = 5
        provider = 'System.Data.SQLite'
        provider_version = '2.0.4'
        provider_sha256 = Sha256 $providerPath
        native_package = 'SourceGear.sqlite3'
        native_package_version = '3.53.4'
        sqlite_engine_version = '3.53.4'
        native_binary = 'e_sqlite3.dll'
        native_sha256 = Sha256 $nativePath
        default_journal = 'DELETE'
        sqlite_runtime_smoke_assertions = $sqliteAssertions
    }
    gates = [ordered]@{
        preflight_smoke = 'PASS'
        preflight_smoke_assertions = $preflightAssertions
        behavior_runtime_smoke = 'PASS'
        behavior_runtime_smoke_assertions = $behaviorAssertions
        learning_session_runtime_smoke = 'PASS'
        learning_session_runtime_smoke_assertions = $learningSessionAssertions
        motion_runtime_smoke = 'PASS'
        motion_runtime_smoke_assertions = $motionAssertions
        child_ui_render_smoke = 'PASS'
        child_ui_render_smoke_assertions = $childUiAssertions
        content_runtime_smoke = 'PASS'
        content_runtime_smoke_assertions = $contentAssertions
        math_content_validator = 'PASS'
        math_content_validator_questions = $mathValidatorQuestions
        math_content_validator_valid_questions = $mathValidatorValidQuestions
        math_game_event_validator_events = $mathValidatorEvents
        math_game_event_validator_first_five = $mathValidatorFirstFive
        math_content_event_pool_smoke = 'PASS'
        math_content_event_pool_smoke_tests = $mathContentTestCount
        security_runtime_smoke = 'PASS'
        security_runtime_smoke_assertions = $securityAssertions
        audio_runtime_smoke = 'PASS'
        audio_runtime_smoke_assertions = $audioAssertions
        performance_runtime_smoke = 'PASS'
        performance_runtime_smoke_assertions = $performanceAssertions
        update_runtime_smoke = 'PASS'
        update_runtime_smoke_assertions = $updateAssertions
        sqlite_runtime_smoke = 'PASS'
        math_session_persistence_runtime_smoke = 'PASS'
        math_session_persistence_runtime_smoke_assertions = $mathPersistenceAssertions
        backup_restore_smoke = 'PASS'
        staged_payload_guard = 'PASS'
        production_asset_payload = 'PASS'
        production_asset_png_count = $productionAssetPngCount
        production_asset_manifest_sha256 = $productionAssetManifestSha256
        portable_production_asset_tree = 'PASS'
        portable_production_asset_zip = 'PASS'
        source_provenance = 'PASS'
        source_tree_clean = 'PASS'
        source_commit_stable = 'PASS'
        source_provenance_after_installer = 'PENDING'
        win7_target_smoke = 'PENDING'
        production_signing = 'PENDING'
    }
    publish_file_count = (Get-ChildItem -LiteralPath $publish -Recurse -File).Count
    sha256sums = 'build\win7_x86\SHA256SUMS.txt'
    portable = [ordered]@{
        mode_marker = 'portable.mode'
        root = 'build\win7_x86\portable'
        zip = "build\portable\WAHU-Kids-Learn-Portable-win7-x86-$AppVersion.zip"
        zip_sha256 = $portableZipHash
        contains_user_data = $false
        production_asset_png_count = $productionAssetPngCount
        production_asset_manifest_sha256 = $productionAssetManifestSha256
        e2e = 'PASS'
        e2e_report = 'build\portable_e2e_dev.json'
        first_bootstrap_exit = [int]$portableE2EReport.first_bootstrap_exit
        second_bootstrap_exit = [int]$portableE2EReport.second_bootstrap_exit
        installed_db_hash_before = $portableE2EReport.installed_db_hash_before
        installed_db_hash_after = $portableE2EReport.installed_db_hash_after
    }
}
$manifestPath = Join-Path $root 'build\win7_x86\release_manifest_dev.json'
$manifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $manifestPath -Encoding UTF8
$null = Get-Content -Raw -LiteralPath $manifestPath | ConvertFrom-Json

Write-Host '[14/15] Installer compiler discovery'
$iscc = Find-FirstExisting @(
    "$env:LOCALAPPDATA\Programs\Inno Setup 7\ISCC.exe",
    'C:\Program Files\Inno Setup 7\ISCC.exe',
    'C:\Program Files (x86)\Inno Setup 7\ISCC.exe'
)
$installerPath = Join-Path $root "build\installer\WAHU-Kids-Learn-Setup-win7-x86-$AppVersion.exe"
if ($CompileInstaller -or $RequireInstaller) {
    if (-not $iscc) {
        if ($RequireInstaller) { throw 'Không tìm thấy ISCC.exe.' }
        Write-Warning 'ISCC.exe chưa có; bỏ qua compile installer.'
    } else {
        Write-Host "[15/15] Compile installer bằng $iscc"
        & $iscc "/DAppVersion=$AppVersion" 'setup\installer\WAHU_Kids_Learn.iss'
        if ($LASTEXITCODE -ne 0) { throw "Inno Setup compile fail: $LASTEXITCODE" }
        Require-File $installerPath
        $installerHash = Sha256 $installerPath
        Set-Content -LiteralPath (Join-Path $root "build\installer\WAHU-Kids-Learn-Setup-win7-x86-$AppVersion.sha256") -Value "$installerHash  $(Split-Path $installerPath -Leaf)" -Encoding ASCII
        Write-Host "INSTALLER_SHA256=$installerHash"

        $updateReleaseDir = Join-Path $root 'build\release'
        New-Item -ItemType Directory -Force -Path $updateReleaseDir | Out-Null
        $updateChannel = if ($AppVersion -match '-dev($|[.-])') { 'dev' } else { 'stable' }
        if ($updateChannel -eq 'stable') { throw 'Stable update feed requires the production signing pipeline; unsigned stable manifest is forbidden.' }
        $installerFileName = Split-Path $installerPath -Leaf
        $updateManifest = [ordered]@{
            schema_version = 1
            app_version = $AppVersion
            channel = $updateChannel
            installer_url = "https://github.com/WahuVN/WAHU-Kids-Learn/releases/download/v$AppVersion/$installerFileName"
            installer_sha256 = $installerHash
            installer_bytes = (Get-Item -LiteralPath $installerPath).Length
            production_signed = $false
            min_windows = '6.1sp1'
        }
        $updateManifestPath = Join-Path $updateReleaseDir 'update-manifest.json'
        $updateManifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $updateManifestPath -Encoding UTF8
        $null = Get-Content -Raw -LiteralPath $updateManifestPath | ConvertFrom-Json
        Write-Host "UPDATE_MANIFEST=$updateManifestPath"
    }
} else {
    Write-Host '[15/15] Installer compile chưa được yêu cầu.'
}

Write-Host '[15b/15] Final post-installer source provenance gate'
$gitCommitAfterInstaller = (& $git rev-parse HEAD 2>$null).Trim()
if ($LASTEXITCODE -ne 0 -or $gitCommitAfterInstaller -ne $sourceGitCommit) {
    throw "REFUSE_SOURCE_HEAD_CHANGED_AFTER_INSTALLER: HEAD đổi sau installer step. start=$sourceGitCommit end=$gitCommitAfterInstaller"
}
$sourceStatusAfterInstaller = @(Get-GitSourceStatus -GitPath $git)
if ($sourceStatusAfterInstaller.Count -ne 0) {
    throw ("REFUSE_SOURCE_MUTATED_AFTER_INSTALLER: source tree thay đổi sau installer step. Entries: " + (($sourceStatusAfterInstaller | ForEach-Object { [string]$_ }) -join '; '))
}
& $git diff --check
if ($LASTEXITCODE -ne 0) { throw 'Git diff --check fail sau installer step.' }
$manifest.gates.source_provenance_after_installer = 'PASS'
$manifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $manifestPath -Encoding UTF8
$manifestFinal = Get-Content -Raw -LiteralPath $manifestPath | ConvertFrom-Json
if ($manifestFinal.gates.source_provenance_after_installer -ne 'PASS' -or $manifestFinal.git_commit -ne $sourceGitCommit) {
    throw 'Release manifest final provenance write/round-trip failed.'
}
Write-Host "SOURCE_PROVENANCE_AFTER_INSTALLER_PASS git_commit=$gitCommitAfterInstaller"

Write-Host "BUILD_SETUP_ARTIFACTS_PASS publish=$publish"
