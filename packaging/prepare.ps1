#requires -Version 7.0
$ErrorActionPreference = 'Stop'
$vendorRoot = Join-Path $PSScriptRoot 'vendor'
$toolRoot = Join-Path $PSScriptRoot 'tools'
New-Item -ItemType Directory -Path $vendorRoot,$toolRoot -Force | Out-Null
function Download([string]$url, [string]$path, [string]$sha256 = '') {
    Invoke-WebRequest $url -OutFile $path
    if ($sha256 -and (Get-FileHash $path).Hash -ne $sha256) { throw "Checksum mismatch: $path" }
}
Download 'https://downloads.rclone.org/v1.75.1/rclone-v1.75.1-windows-amd64.zip' (Join-Path $vendorRoot 'rclone.zip') '200EB602C126D82AA38B51E0F6B9AE837473FF99B51278D3F6F837574C494D6E'
Download 'https://downloads.rclone.org/v1.75.1/SHA256SUMS' (Join-Path $vendorRoot 'rclone-SHA256SUMS')
Download 'https://github.com/winfsp/winfsp/releases/download/v2.1/winfsp-2.1.25156.msi' (Join-Path $vendorRoot 'winfsp-2.1.25156.msi') '073A70E00F77423E34BED98B86E600DEF93393BA5822204FAC57A29324DB9F7A'
Download 'https://github.com/jrsoftware/issrc/releases/download/is-6_4_3/innosetup-6.4.3.exe' (Join-Path $toolRoot 'innosetup-6.4.3.exe') 'F3C42116542C4CC57263C5BA6C4FEABFC49FE771F2F98A79D2F7628B8762723B'
foreach ($binary in @((Join-Path $vendorRoot 'winfsp-2.1.25156.msi'),(Join-Path $toolRoot 'innosetup-6.4.3.exe'))) {
    if ((Get-AuthenticodeSignature $binary).Status -ne 'Valid') { throw "Invalid signature: $binary" }
}
Expand-Archive -LiteralPath (Join-Path $vendorRoot 'rclone.zip') -DestinationPath (Join-Path $vendorRoot 'rclone') -Force
Download 'https://raw.githubusercontent.com/rclone/rclone/v1.75.1/COPYING' (Join-Path $vendorRoot 'Rclone-License.txt')
Download 'https://raw.githubusercontent.com/winfsp/winfsp/v2.1/License.txt' (Join-Path $vendorRoot 'WinFsp-License.txt')
Download 'https://raw.githubusercontent.com/dotnet/runtime/v10.0.12/LICENSE.TXT' (Join-Path $vendorRoot 'DotNet-License.txt')
Download 'https://raw.githubusercontent.com/dotnet/runtime/v10.0.12/THIRD-PARTY-NOTICES.TXT' (Join-Path $vendorRoot 'DotNet-ThirdParty-License.txt')
Download 'https://raw.githubusercontent.com/jrsoftware/issrc/is-6_4_3/license.txt' (Join-Path $vendorRoot 'InnoSetup-License.txt')
$toolInstall = Start-Process -FilePath (Join-Path $toolRoot 'innosetup-6.4.3.exe') -ArgumentList '/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/CURRENTUSER','/PORTABLE=1','/NOICONS',('/DIR=' + (Join-Path $toolRoot 'inno-6.4.3')) -WindowStyle Hidden -Wait -PassThru
if ($toolInstall.ExitCode -ne 0) { throw 'Inno Setup tool installation failed' }
