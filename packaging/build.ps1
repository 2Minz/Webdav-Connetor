#requires -Version 7.0
param([string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
Push-Location $projectRoot
try {
    $compiler = Join-Path $PSScriptRoot 'tools\inno-6.4.3\ISCC.exe'
    $vendorRoot = Join-Path $PSScriptRoot 'vendor'
    $rclone = Join-Path $vendorRoot 'rclone\rclone-v1.75.1-windows-amd64\rclone.exe'
    $msi = Join-Path $vendorRoot 'winfsp-2.1.25156.msi'
    if (-not (Test-Path $compiler) -or -not (Test-Path $rclone) -or -not (Test-Path $msi)) {
        throw '공식 의존 패키지와 Inno Setup 컴파일러를 packaging/vendor, packaging/tools에 준비하세요.'
    }
    if ((Get-AuthenticodeSignature $msi).Status -ne 'Valid') { throw 'WinFsp MSI 서명 검증 실패' }
    $rcloneHash = (Get-FileHash (Join-Path $vendorRoot 'rclone.zip')).Hash
    $checksum = (Select-String -LiteralPath (Join-Path $vendorRoot 'rclone-SHA256SUMS') -Pattern 'rclone-v1.75.1-windows-amd64.zip').Line
    if (-not $checksum.StartsWith($rcloneHash, [StringComparison]::OrdinalIgnoreCase)) { throw 'rclone SHA256 불일치' }
    Expand-Archive -LiteralPath (Join-Path $vendorRoot 'rclone.zip') -DestinationPath (Join-Path $vendorRoot 'rclone') -Force
    $publish = Join-Path $PSScriptRoot 'publish'
    # Only a checked build output directory can be replaced; never remove source/vendor paths.
    $absolutePublish = [IO.Path]::GetFullPath($publish)
    if ($absolutePublish -ne [IO.Path]::GetFullPath((Join-Path $projectRoot 'packaging\publish'))) { throw 'Unexpected publish path' }
    if (Test-Path $absolutePublish) { Remove-Item -LiteralPath $absolutePublish -Recurse -Force }
    dotnet publish src/UnionDrive.App/UnionDrive.App.csproj -c $Configuration -r win-x64 --self-contained true -o $publish -p:DebugType=None -p:DebugSymbols=false -p:UseSharedCompilation=false -p:SatelliteResourceLanguages=ko
    if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed' }
    New-Item -ItemType Directory -Path (Join-Path $publish 'runtime'),(Join-Path $publish 'licenses') -Force | Out-Null
    Copy-Item -LiteralPath $rclone -Destination (Join-Path $publish 'runtime\rclone.exe')
    Copy-Item -LiteralPath 'LICENSE' -Destination (Join-Path $publish 'licenses\UnionDrive-MIT.txt')
    Copy-Item -Path (Join-Path $vendorRoot '*-License.txt') -Destination (Join-Path $publish 'licenses')
    Copy-Item -LiteralPath 'packaging\INSTALL-NOTES.txt' -Destination (Join-Path $publish 'INSTALL-NOTES.txt')
    New-Item -ItemType Directory -Path dist -Force | Out-Null
    $sourceArchive = Join-Path $projectRoot 'dist\Webdav-Connetor-0.2.2-source.zip'
    $sourceStage = Join-Path $projectRoot '.tmp\source-stage'
    if ([IO.Path]::GetFullPath($sourceStage) -ne [IO.Path]::GetFullPath((Join-Path $projectRoot '.tmp\source-stage'))) { throw 'Unexpected source staging path' }
    if (Test-Path $sourceStage) { Remove-Item -LiteralPath $sourceStage -Recurse -Force }
    New-Item -ItemType Directory -Path $sourceStage -Force | Out-Null
    # Explicit source files only; exclude credentials, build outputs and vendor/tool binaries.
    Get-ChildItem src,tests,docs -File -Recurse | Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' } | ForEach-Object {
        $relative = [IO.Path]::GetRelativePath($projectRoot, $_.FullName)
        $destination = Join-Path $sourceStage $relative
        New-Item -ItemType Directory -Path (Split-Path $destination -Parent) -Force | Out-Null
        Copy-Item -LiteralPath $_.FullName -Destination $destination
    }
    foreach ($file in @('LICENSE','README.md','AGENTS.md','UnionDrive.slnx','packaging\UnionDrive.iss','packaging\build.ps1','packaging\prepare.ps1','packaging\INSTALL-NOTES.txt')) {
        $destination = Join-Path $sourceStage $file
        New-Item -ItemType Directory -Path (Split-Path $destination -Parent) -Force | Out-Null
        Copy-Item -LiteralPath $file -Destination $destination
    }
    Compress-Archive -Path (Join-Path $sourceStage '*') -DestinationPath $sourceArchive -Force
    Copy-Item -LiteralPath $sourceArchive -Destination (Join-Path $publish 'licenses\UnionDrive-source.zip')
    & $compiler /Qp (Join-Path $PSScriptRoot 'UnionDrive.iss')
    if ($LASTEXITCODE -ne 0) { throw 'Inno Setup compilation failed' }
    Get-Item dist/Webdav-Connetor-Setup-0.2.2-win-x64.exe | Get-FileHash | ForEach-Object {
        "$($_.Hash)  $([IO.Path]::GetFileName($_.Path))" | Set-Content -LiteralPath (Join-Path $projectRoot 'dist\SHA256SUMS.txt') -Encoding ascii
    }
} finally { Pop-Location }
