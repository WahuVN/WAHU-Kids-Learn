param(
    [Parameter(Mandatory = $true)]
    [string]$AppVersion
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
Set-Location $root

function Run-Gate([string]$Name, [string]$ScriptPath) {
    Write-Host "[$Name] bắt đầu"
    & powershell -NoProfile -ExecutionPolicy Bypass -File $ScriptPath -AppVersion $AppVersion
    if ($LASTEXITCODE -ne 0) { throw "$Name fail: $LASTEXITCODE" }
    Write-Host "[$Name] PASS"
}

# Không chạy hai gate này song song. Portable E2E cố ý giám sát
# %LOCALAPPDATA% để phát hiện cross-mode contamination, trong khi
# Installer E2E cần tạm tạo chính installed user root đó.
$portable = Join-Path $PSScriptRoot 'Test-PortableE2E.ps1'
$installer = Join-Path $PSScriptRoot 'Test-InstallerE2E.ps1'
if (-not (Test-Path -LiteralPath $portable -PathType Leaf)) { throw "Thiếu $portable" }
if (-not (Test-Path -LiteralPath $installer -PathType Leaf)) { throw "Thiếu $installer" }

Run-Gate 'Portable E2E' $portable
Run-Gate 'Installer E2E' $installer

Write-Host "DISTRIBUTION_E2E_PASS version=$AppVersion order=portable_then_installer"
