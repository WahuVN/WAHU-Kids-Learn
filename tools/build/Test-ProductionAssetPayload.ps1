param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('Tree','Zip')]
    [string]$Mode,
    [Parameter(Mandatory = $true)]
    [string]$Path,
    [string]$ExpectedManifestSha256
)

$ErrorActionPreference = 'Stop'

function Sha256File([string]$FilePath) {
    (Get-FileHash -Algorithm SHA256 -LiteralPath $FilePath).Hash
}

function Sha256Stream([System.IO.Stream]$Stream) {
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        $bytes = $sha.ComputeHash($Stream)
        return ([BitConverter]::ToString($bytes)).Replace('-', '')
    }
    finally { $sha.Dispose() }
}

function Assert-ManifestShape($Manifest) {
    if ($null -eq $Manifest -or $null -eq $Manifest.summary -or $null -eq $Manifest.assets) {
        throw 'Production asset manifest is missing summary/assets.'
    }
    $expected = [int]$Manifest.summary.totalSelected
    $assets = @($Manifest.assets)
    if ($expected -le 0 -or $assets.Count -ne $expected) {
        throw "Production asset manifest count mismatch. summary=$expected assets=$($assets.Count)"
    }
    $seen = @{}
    foreach ($asset in $assets) {
        $rel = [string]$asset.finalPath
        if ([string]::IsNullOrWhiteSpace($rel) -or [IO.Path]::IsPathRooted($rel) -or
            @($rel.Split('\')) -contains '..' -or -not $rel.EndsWith('.png', [StringComparison]::OrdinalIgnoreCase)) {
            throw "Invalid production asset finalPath: $rel"
        }
        $key = $rel.Replace('/', '\').ToLowerInvariant()
        if ($seen.ContainsKey($key)) { throw "Duplicate production asset finalPath: $rel" }
        $seen[$key] = $true
        if ([string]::IsNullOrWhiteSpace([string]$asset.sha256) -or [string]$asset.sha256 -notmatch '^[0-9A-Fa-f]{64}$') {
            throw "Invalid production asset sha256: $rel"
        }
        if ([long]$asset.bytes -le 0) { throw "Invalid production asset byte count: $rel" }
    }
    return $expected
}

function Assert-Tree([string]$Root) {
    if (-not (Test-Path -LiteralPath $Root -PathType Container)) { throw "Production asset tree does not exist: $Root" }
    $manifestPath = Join-Path $Root 'ASSET_SELECTION_MANIFEST.json'
    if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) { throw "Production asset manifest missing: $manifestPath" }
    $manifestHash = Sha256File $manifestPath
    if (-not [string]::IsNullOrWhiteSpace($ExpectedManifestSha256) -and
        -not [string]::Equals($manifestHash, $ExpectedManifestSha256, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Production asset manifest hash mismatch. expected=$ExpectedManifestSha256 actual=$manifestHash"
    }
    $manifest = Get-Content -Raw -LiteralPath $manifestPath | ConvertFrom-Json
    $expected = Assert-ManifestShape $manifest
    $pngs = @(Get-ChildItem -LiteralPath $Root -Recurse -File -Filter *.png)
    if ($pngs.Count -ne $expected) { throw "Production asset PNG count mismatch. expected=$expected actual=$($pngs.Count)" }
    foreach ($asset in @($manifest.assets)) {
        $rel = ([string]$asset.finalPath).Replace('/', '\')
        $file = Join-Path $Root $rel
        if (-not (Test-Path -LiteralPath $file -PathType Leaf)) { throw "Production asset file missing: $rel" }
        $item = Get-Item -LiteralPath $file
        if ([long]$item.Length -ne [long]$asset.bytes) {
            throw "Production asset byte count mismatch: $rel expected=$($asset.bytes) actual=$($item.Length)"
        }
        $hash = Sha256File $file
        if (-not [string]::Equals($hash, [string]$asset.sha256, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Production asset SHA256 mismatch: $rel"
        }
    }
    Write-Host "PRODUCTION_ASSET_PAYLOAD_PASS mode=Tree png_count=$expected manifest_sha256=$manifestHash path=$Root"
}

function Get-ZipEntrySha256($Entry) {
    $stream = $Entry.Open()
    try { return Sha256Stream $stream }
    finally { $stream.Dispose() }
}

function Read-ZipEntryText($Entry) {
    $stream = $Entry.Open()
    try {
        $reader = New-Object IO.StreamReader($stream, [Text.Encoding]::UTF8, $true)
        try { return $reader.ReadToEnd() }
        finally { $reader.Dispose() }
    }
    finally { $stream.Dispose() }
}

function Normalize-ZipEntryName([string]$Name) {
    if ($null -eq $Name) { return '' }
    return $Name.Replace('\', '/')
}

function Assert-Zip([string]$ZipPath) {
    if (-not (Test-Path -LiteralPath $ZipPath -PathType Leaf)) { throw "Portable ZIP does not exist: $ZipPath" }
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip = [IO.Compression.ZipFile]::OpenRead((Resolve-Path -LiteralPath $ZipPath).Path)
    try {
        $prefix = 'Assets/Generated/Ready/'
        $manifestName = $prefix + 'ASSET_SELECTION_MANIFEST.json'
        $manifestEntries = @($zip.Entries | Where-Object { [string]::Equals((Normalize-ZipEntryName $_.FullName), $manifestName, [StringComparison]::OrdinalIgnoreCase) })
        if ($manifestEntries.Count -ne 1) { throw "Portable ZIP must contain exactly one production asset manifest. count=$($manifestEntries.Count)" }
        $manifestEntry = $manifestEntries[0]
        $manifestHash = Get-ZipEntrySha256 $manifestEntry
        if (-not [string]::IsNullOrWhiteSpace($ExpectedManifestSha256) -and
            -not [string]::Equals($manifestHash, $ExpectedManifestSha256, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Portable ZIP production asset manifest hash mismatch. expected=$ExpectedManifestSha256 actual=$manifestHash"
        }
        $manifest = (Read-ZipEntryText $manifestEntry) | ConvertFrom-Json
        $expected = Assert-ManifestShape $manifest
        $pngEntries = @($zip.Entries | Where-Object {
            (Normalize-ZipEntryName $_.FullName).StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase) -and
            (Normalize-ZipEntryName $_.FullName).EndsWith('.png', [StringComparison]::OrdinalIgnoreCase)
        })
        if ($pngEntries.Count -ne $expected) { throw "Portable ZIP production asset PNG count mismatch. expected=$expected actual=$($pngEntries.Count)" }
        foreach ($asset in @($manifest.assets)) {
            $rel = ([string]$asset.finalPath).Replace('\', '/')
            $entryName = $prefix + $rel
            $entries = @($zip.Entries | Where-Object { [string]::Equals((Normalize-ZipEntryName $_.FullName), $entryName, [StringComparison]::OrdinalIgnoreCase) })
            if ($entries.Count -ne 1) { throw "Portable ZIP asset missing/duplicated: $entryName count=$($entries.Count)" }
            $entry = $entries[0]
            if ([long]$entry.Length -ne [long]$asset.bytes) {
                throw "Portable ZIP asset byte count mismatch: $entryName expected=$($asset.bytes) actual=$($entry.Length)"
            }
            $hash = Get-ZipEntrySha256 $entry
            if (-not [string]::Equals($hash, [string]$asset.sha256, [StringComparison]::OrdinalIgnoreCase)) {
                throw "Portable ZIP asset SHA256 mismatch: $entryName"
            }
        }
        Write-Host "PRODUCTION_ASSET_PAYLOAD_PASS mode=Zip png_count=$expected manifest_sha256=$manifestHash path=$ZipPath"
    }
    finally { $zip.Dispose() }
}

if ($Mode -eq 'Tree') { Assert-Tree $Path }
else { Assert-Zip $Path }
