param(
    [string]$Destination
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
if ([string]::IsNullOrWhiteSpace($Destination)) {
    $Destination = Join-Path $root 'build\prerequisites\ndp48-x86-x64-allos-enu.exe'
}

$officialUrl = 'https://download.microsoft.com/download/f/3/a/f3a6af84-da23-40a5-8d1c-49cc10c8e76f/NDP48-x86-x64-AllOS-ENU.exe'
$expectedSha256 = '0A3A390C47E639D0F7FC65B21195FEE6B7F65B066F80F70C60FAB191D14B7E40'
$expectedBytes = 121346568L

function Assert-Redistributable([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "NET48_REDIST_MISSING: $Path"
    }

    $item = Get-Item -LiteralPath $Path
    if ($item.Length -ne $expectedBytes) {
        throw "NET48_REDIST_SIZE_MISMATCH expected=$expectedBytes actual=$($item.Length)"
    }

    $sha = (Get-FileHash -Algorithm SHA256 -LiteralPath $Path).Hash.ToUpperInvariant()
    if ($sha -ne $expectedSha256) {
        throw "NET48_REDIST_SHA256_MISMATCH expected=$expectedSha256 actual=$sha"
    }

    $signature = Get-AuthenticodeSignature -LiteralPath $Path
    if ($signature.Status -ne 'Valid') {
        throw "NET48_REDIST_SIGNATURE_INVALID status=$($signature.Status)"
    }
    if (-not $signature.SignerCertificate -or $signature.SignerCertificate.Subject -notmatch 'Microsoft Corporation') {
        throw "NET48_REDIST_SIGNER_INVALID signer=$($signature.SignerCertificate.Subject)"
    }

    return [ordered]@{
        path = (Resolve-Path -LiteralPath $Path).Path
        filename = $item.Name
        bytes = $item.Length
        sha256 = $sha
        signature_status = [string]$signature.Status
        signer = [string]$signature.SignerCertificate.Subject
    }
}

$parent = Split-Path -Parent $Destination
New-Item -ItemType Directory -Force -Path $parent | Out-Null

if (-not (Test-Path -LiteralPath $Destination -PathType Leaf)) {
    $partial = $Destination + '.partial'
    Remove-Item -LiteralPath $partial -Force -ErrorAction SilentlyContinue
    try {
        Invoke-WebRequest -UseBasicParsing -Uri $officialUrl -OutFile $partial
        $null = Assert-Redistributable $partial
        Move-Item -LiteralPath $partial -Destination $Destination -Force
    }
    finally {
        Remove-Item -LiteralPath $partial -Force -ErrorAction SilentlyContinue
    }
}

$evidence = Assert-Redistributable $Destination
$evidence.source_url = $officialUrl
$evidence.verified_at_utc = [DateTime]::UtcNow.ToString('o')
$manifestPath = Join-Path $parent 'net48-offline-redistributable.json'
$evidence | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $manifestPath -Encoding UTF8
$null = Get-Content -Raw -LiteralPath $manifestPath | ConvertFrom-Json

Write-Host "NET48_REDIST_PASS path=$($evidence.path) sha256=$($evidence.sha256) bytes=$($evidence.bytes) signer=Microsoft_Corporation"
Write-Output $evidence.path
