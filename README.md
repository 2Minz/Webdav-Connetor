# Webdav Connetor

Windows 11 x64에서 여러 Synology NAS의 HTTPS WebDAV를 드라이브로 연결하는 WinForms 클라이언트입니다. 드라이브별 설정·연결·연결 해제, 안전한 비밀번호 저장, 트레이, 로그인 자동 시작을 지원합니다.

## 설치와 사용

배포 파일: [Webdav Connetor 0.2.3 installer](https://github.com/2Minz/Webdav-Connetor/releases/download/v0.2.3/Webdav-Connetor-Setup-0.2.3-win-x64.exe)

[최신 배포](https://github.com/2Minz/Webdav-Connetor/releases/latest) · [소스 저장소](https://github.com/2Minz/Webdav-Connetor)

1. 인스톨러를 실행합니다. UnionDrive와 공식 rclone, .NET 10 Windows Desktop Runtime이 함께 설치됩니다. WinFsp가 없으면 공식 MSI의 Core 구성만 설치합니다. 설치 중 WinFsp에 대한 Windows 관리자 권한 요청(UAC)이 표시될 수 있습니다.
2. 기존에 사용하던 자동 마운트/CMD/VBS 작업이 같은 드라이브 문자를 사용한다면 먼저 해제합니다.
3. **드라이브 추가**에서 이름, HTTPS 서버 주소(접두사 없이 호스트만 입력), 포트, 공유 폴더(비우면 루트), NAS 계정, 비밀번호, 드라이브 문자, 볼륨 이름을 입력합니다. 필요한 수만큼 추가할 수 있으며 D:~Z: 사이에 중복 없이 지정합니다.
4. 목록에서 드라이브를 선택한 후 **연결**, **연결 해제**, **드라이브 열기**, **설정**을 각각 사용합니다. 연결 중인 프로필은 해제 후 변경할 수 있습니다.
5. 각 프로필의 **프로그램 시작 시 자동 연결**과 메인 화면의 **Windows 로그인 시 자동 시작**은 별도 옵션입니다.
6. 창의 X 버튼은 트레이로 숨깁니다. 명시적인 종료는 트레이 메뉴의 **종료**입니다.

인스톨러는 현재 사용자 계정의 `%LOCALAPPDATA%\Programs\UnionDrive`에 설치합니다. 앱은 일반 사용자 권한으로 실행하며 작업 스케줄러나 상주 Windows 서비스를 만들지 않습니다. 설치본은 인터넷 연결 없이 설치되고, 별도 .NET/rclone 설치가 필요하지 않습니다.

이 테스트 배포본에는 코드 서명이 없습니다. 실제 NAS 계정 및 깨끗한 Windows에서의 설치·드라이버 마운트·읽기·쓰기는 사용자가 확인할 항목입니다. 빌드나 테스트용 마운트 검증을 실제 NAS 검증으로 간주하지 않습니다.

## 데이터와 보안

- 비밀번호: 현재 사용자의 Windows Credential Manager (`UnionDrive/Profile/` 범위).
- 설정: `%LOCALAPPDATA%\UnionDrive\config.json`. 비밀번호 필드가 없습니다.
- 캐시: `%LOCALAPPDATA%\UnionDrive\Cache\<프로필 ID>`. 기본 프로필당 10 GB / 24시간, 폴더 갱신 30초.
- 로그: 같은 데이터 폴더의 `Logs`. 파일당 약 512 KB, 현재 파일과 백업 세 개로 제한합니다. 이벤트 코드와 프로필 ID만 기록하며 계정·URL·비밀번호·rclone 원문 출력은 기록하지 않습니다.
- 프로세스 소유권: `Sessions` 폴더에 PID/시작 시각/loopback 포트만 기록하고 제어 비밀번호는 Credential Manager에 보관합니다. 앱 충돌 후 남은 자신의 마운트를 다음 실행/제거 시 확인하고 정상 종료합니다.

자체 WebDAV 설정은 rclone 프로세스의 실행 환경으로만 전달하고 `--config NUL`을 사용합니다. 기존 사용자의 rclone 설정을 수정하지 않습니다. rclone obscure 결과를 디스크에 저장하거나 안전한 비밀번호 저장소로 취급하지 않습니다. TLS 인증서 검증을 끄지 않습니다.

## 종료 및 제거

Windows 설정의 설치된 앱 또는 시작 메뉴의 **UnionDrive 제거**를 사용합니다. 제거를 확정한 이후에만 정리를 시작합니다.

- 실행 중인 UnionDrive에 종료를 요청하고 자신의 모든 마운트를 정상 종료합니다.
- VFS 전송 중/대기/실패 파일이나 Dirty 캐시가 있으면 종료/제거를 보류합니다. 열린 NAS 파일을 저장하고 닫은 뒤 다시 시도하세요.
- 제거 성공 시 현재 사용자의 프로필, 인증정보, 로그, 캐시, 자동 시작 Run 항목, 설치용 레지스트리, 바로 가기와 앱/rclone/.NET 파일을 삭제합니다.
- 이 인스톨러가 새로 설치한 동일 버전의 WinFsp만 함께 제거합니다. 기존에 설치되어 있던 WinFsp 및 외부에서 다른 버전으로 교체한 WinFsp는 보존합니다.
- 다른 프로그램/사용자가 만든 rclone config, CMD/VBS, 스케줄러, 드라이브 및 레지스트리 항목은 변경하지 않습니다. UnionDrive 자체는 스케줄러를 생성하지 않습니다.
- 함께 설치한 WinFsp를 나중에 다른 프로그램도 사용하게 됐다면 제거 시 해당 프로그램에도 영향을 줄 수 있습니다. 설치/제거 후 Windows 재시작이 필요한 경우 인스톨러가 안내합니다.

Windows 종료 시 전체 드라이브에 대해 총 3초 범위에서 정상 종료를 시도합니다. 강제 OS 종료/앱 충돌 상황에서 전송 완료를 보장하지는 않습니다. 미전송 캐시의 자동 삭제는 하지 않습니다. 앱은 디스크의 Dirty 메타데이터를 확인해 설정 변경/프로필 삭제/제거로 미전송 데이터를 잃지 않도록 보호합니다.

## 경량화

Electron/WebView/별도 백그라운드 서비스/상시 UI 타이머 없이 native WinForms와 .NET 기본 라이브러리를 사용합니다. 드라이브당 rclone 프로세스 하나를 관리하고 파일당 읽기 버퍼는 4 MB로 제한합니다. 연결 확인 중에만 짧게 조회하며 정상 연결 상태에서 앱의 주기적 NAS polling은 없습니다. 네트워크 단절 상태의 파일 재시도는 rclone이 처리합니다. 프로세스 종료 또는 네트워크 복구 시 제한된 5/10/30/60초 재연결을 시도합니다.

프로세스/HTTP client/표준 출력 파이프/트레이/메뉴/이벤트 구독/폰트/타이머 취소 자원을 정리합니다. 리소스 테스트는 30번 화면 생성·폐기 및 10번 managed reconnect에서 GDI/User 핸들과 소유권 레코드 회수를 확인합니다. 이 결과가 모든 장기 사용 환경에서의 누수 부재를 보장하는 것은 아닙니다.

## 개발 및 검증

Windows 11 x64와 .NET 10 SDK에서:

```powershell
dotnet build UnionDrive.slnx -c Release -p:UseSharedCompilation=false
dotnet run --project tests/UnionDrive.Checks/UnionDrive.Checks.csproj -c Release
dotnet run --project src/UnionDrive.App/UnionDrive.App.csproj -c Release
```

Windows 통합 검증은 임시 테스트 credential을 저장하고 제거하며 실제 NAS/드라이브 매핑은 변경하지 않습니다. 검증 항목은 다중 프로필, HTTPS 및 중복 문자 검증, Credential Manager, dirty 캐시 보호, 시작 실패, stdout/stderr 처리, 인증된 RC, 동시 마운트/개별 해제, 재연결, 비정상 종료, 세션 종료, 소유 프로세스 복구 및 UI 자원 회수입니다.

개발 실행 시 `rclone.exe`는 앱 폴더의 runtime, 앱 폴더, PATH 순서로 탐색합니다. 배포본에는 runtime/rclone.exe가 포함됩니다. 소스는 GitHub 공개 저장소 2Minz/Webdav-Connetor에서 관리합니다.

## 인스톨러 재생성

PowerShell 7을 사용합니다. 빌드 준비 시에만 공식 의존 패키지와 .NET 런타임 다운로드를 위한 네트워크가 필요합니다.

```powershell
pwsh -NoProfile -File packaging/prepare.ps1
pwsh -NoProfile -File packaging/build.ps1
```

rclone 1.75.1 ZIP SHA256과 WinFsp 2.1.25156 MSI, Inno Setup 6.4.3의 SHA256/서명을 확인합니다. build.ps1은 self-contained win-x64 앱, 의존 바이너리, 라이선스와 앱 소스를 패키징하고 `dist`에 installer, 소스 ZIP, SHA256SUMS.txt를 생성합니다. 빌드 도구/원본 다운로드/출력물은 소스 ZIP에서 제외합니다.

## 오픈소스

UnionDrive: [MIT License](LICENSE). 앱의 소스 ZIP은 설치 폴더의 licenses에도 포함됩니다.

rclone: MIT License, https://rclone.org · https://github.com/rclone/rclone

WinFsp - Windows File System Proxy, Copyright (C) Bill Zissimopoulos
https://github.com/winfsp/winfsp

WinFsp 공식 MSI를 수정 없이 포함하며 GPLv3의 FLOSS 재배포 예외를 적용합니다. [공식 라이선스](https://github.com/winfsp/winfsp/blob/v2.1/License.txt)와 .NET/제3자 고지를 설치본에 포함합니다. 컴파일러는 [상업적 사용이 허용되는 Inno Setup 6.4.3](https://github.com/jrsoftware/issrc/blob/is-6_4_3/license.txt)을 사용합니다.

프로젝트 원문: [PROJECT_CONTEXT.md](docs/PROJECT_CONTEXT.md). 최신 범위: 다중 드라이브와 installer/uninstaller까지 구현하며 초기 Phase 1 제한은 사용자의 2026-10-05 요청으로 해제되었습니다.

## 0.2.1 화면 변경 및 사용자 확인

프로그램 표시 이름은 Webdav Connetor입니다. 기존 설치 경로·실행 파일명·설정·Credential 식별자는 업그레이드 호환을 위해 유지합니다. 캐시 10GB/24시간/폴더 갱신 30초는 세부 설정에서만 변경합니다. 메인/설정 창은 크기 고정이며 공통 세 개의 사각형 아이콘을 사용합니다. 라이선스 고지는 트레이 메뉴에 있습니다.

이 수정본은 사용자 지침에 따라 컴파일과 installer 패키징만 수행하며 작동 테스트는 실행하지 않습니다. 앞의 검증 기록은 0.2.0 기록입니다. 사용자가 설치 후 주소/포트/공유 폴더와 기존 프로필 유지, 개별 연결/해제, 탐색기 이름/아이콘, 작업표시줄/트레이 아이콘, 창 크기와 세부 설정을 확인합니다.

## 0.2.2 아이콘 색상

사용자가 0.2.1 정상 작동을 확인했습니다. 0.2.2는 공통 아이콘 사각형 세 개의 색상만 노란색(#FFD500)으로 변경합니다. 컴파일/패키징만 수행하며 작동 검증은 사용자가 수행합니다.

## 0.2.3 인증서 오류 안내

서버 주소 입력란의 예시 문구를 제거했습니다. 연결 사전 확인의 인증서 오류를 발급기관 신뢰·주소 불일치·만료/유효기간·Windows 인증기관 로딩·TLS 협상 실패로 구분합니다. raw rclone 출력은 저장/표시하지 않으며 TLS 검증을 끄지 않습니다.

인증서 오류는 HTTPS 연결 단계에서 발생하므로 공유 폴더나 계정 변경만으로 해결되지 않을 수 있습니다. DSM → 제어판 → 보안 → 인증서에서 연결 주소에 맞는 유효한 인증서를 **WebDAV Server**에 지정하세요. 사설 CA를 사용한다면 관리자가 확인한 CA를 Windows에서 신뢰하도록 설정해야 합니다. TLS 1.2 설정과 인증서의 주소/발급기관/유효기간 검증은 별개입니다.

배포 파일: `dist/Webdav-Connetor-Setup-0.2.3-win-x64.exe`. 이 수정본은 컴파일/패키징만 수행했고 앱 실행, NAS 접속, 설치 및 자동 테스트는 수행하지 않았습니다. 추가 NAS는 접속 주소와 맞지 않는 자체 서명 인증서가 WebDAV Server에 지정되어 있었습니다. 사용자가 주소에 맞는 인증서를 발급하고 WebDAV Server에 지정한 뒤 정상 작동을 확인했습니다.
