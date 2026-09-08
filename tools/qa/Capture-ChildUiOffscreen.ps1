param(
    [string]$Configuration = 'Release',
    [string]$OutputDirectory
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
Set-Location $root

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $root 'build\ui-captures\child-ui'
} elseif (-not [IO.Path]::IsPathRooted($OutputDirectory)) {
    $OutputDirectory = Join-Path $root $OutputDirectory
}
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null

$msbuild = @(
    'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe',
    'C:\Program Files\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe',
    'C:\Windows\Microsoft.NET\Framework\v4.0.30319\MSBuild.exe'
) | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $msbuild) { throw 'MSBuild was not found for ChildUiRuntimeSmoke.' }

$project = Join-Path $root 'tests\ChildUiRuntimeSmoke\WAHU.ChildUiRuntimeSmoke.csproj'
& $msbuild $project /restore /t:Rebuild "/p:Configuration=$Configuration" /p:Platform=x86 /v:minimal /nologo
if ($LASTEXITCODE -ne 0) { throw "Child UI smoke build failed: $LASTEXITCODE" }

$exe = Join-Path $root "tests\ChildUiRuntimeSmoke\bin\$Configuration\WAHU.ChildUiRuntimeSmoke.exe"
if (-not (Test-Path -LiteralPath $exe -PathType Leaf)) { throw "Child UI smoke executable is missing: $exe" }

$output = @(& $exe --capture-dir $OutputDirectory 2>&1)
$exitCode = $LASTEXITCODE
foreach ($line in $output) { Write-Host ([string]$line) }
if ($exitCode -ne 0) { throw "Child UI offscreen capture failed: $exitCode" }

$captureLine = @($output | Where-Object { [string]$_ -match '^CHILD_UI_CAPTURE_PASS files=(\d+) dir=(.+)$' })
if ($captureLine.Count -ne 1) { throw 'Child UI capture did not publish exactly one capture summary.' }
$match = [regex]::Match([string]$captureLine[0], '^CHILD_UI_CAPTURE_PASS files=(\d+) dir=(.+)$')
$captureCount = [int]$match.Groups[1].Value
if ($captureCount -le 0) { throw 'Child UI capture published zero PNG files.' }

$pngs = @(Get-ChildItem -LiteralPath $OutputDirectory -Filter *.png -File)
if ($pngs.Count -lt $captureCount) {
    throw "Capture directory has fewer PNG files than reported: reported=$captureCount actual=$($pngs.Count)"
}
if (@($pngs | Where-Object { $_.Length -le 1024 }).Count -ne 0) {
    throw 'At least one captured PNG is unexpectedly small.'
}

Write-Host "CHILD_UI_OFFSCREEN_CAPTURE_PASS files=$captureCount dir=$OutputDirectory"
