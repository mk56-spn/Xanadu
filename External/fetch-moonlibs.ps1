# Downloads Moonside's prebuilt native libraries for MoonWorks into External/moonlibs/win64.
# The Windows twin of fetch-moonlibs.sh; unpacks with the tar that comes with Windows 10 and 11.
#   powershell -ExecutionPolicy Bypass -File External\fetch-moonlibs.ps1
$ErrorActionPreference = 'Stop'
$dest = Join-Path $PSScriptRoot 'moonlibs'
$url = 'https://moonside.games/files/moonlibs.tar.gz'
$sha = 'f4d78bfee67df9f59507670c1a91d6ae1f4ec18e67970c8f8e5c384323406f30'

if (Test-Path (Join-Path $dest 'win64/SDL3.dll')) {
    Write-Host "moonlibs already in $dest"
    exit 0
}

New-Item -ItemType Directory -Force -Path $dest | Out-Null
$tmp = Join-Path ([System.IO.Path]::GetTempPath()) ([System.IO.Path]::GetRandomFileName())
New-Item -ItemType Directory -Path $tmp | Out-Null
try {
    $archive = Join-Path $tmp 'moonlibs.tar.gz'
    Invoke-WebRequest -UseBasicParsing -Uri $url -OutFile $archive
    $actual = (Get-FileHash -Algorithm SHA256 -Path $archive).Hash.ToLowerInvariant()
    if ($actual -ne $sha) { throw "moonlibs.tar.gz checksum mismatch: expected $sha, got $actual" }
    tar -xzf $archive -C $dest win64
    if ($LASTEXITCODE -ne 0) { throw "tar failed ($LASTEXITCODE)" }
    Write-Host "moonlibs unpacked to $(Join-Path $dest 'win64')"
}
finally {
    Remove-Item -Recurse -Force $tmp
}
