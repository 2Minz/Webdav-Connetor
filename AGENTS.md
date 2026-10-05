# UnionDrive 작업 지침

## 현재 범위

- 작업 전 README.md, docs/PROJECT_CONTEXT.md, docs/REVISED_REQUIREMENTS.md를 읽는다.
- 최신 사용자 요청이 초기 Phase 1 제한보다 우선한다. 다중 HTTPS WebDAV 드라이브, 개별 설정/연결/해제, Credential Manager, 트레이/자동 시작, 배포 installer 및 uninstaller까지 구현한다.
- Windows 11 x64, C#, .NET 10 / net10.0-windows, native WinForms를 사용한다.
- Git/GitHub 사용은 예정 사항이다. 저장소 상태를 확인하고 저장소가 없으면 보고한다.

## 작업 방식

- repository/파일 상태를 확인하고 필요한 변경 계획을 간단히 설명한다.
- 사용자가 요청한 수정만 구현한다. 작동 검증은 사용자가 직접 수행한다. 에이전트는 앱/installer 실행, 설치, 마운트 및 자동/회귀 테스트를 수행하지 않는다. 배포물 생성에 필요한 컴파일/패키징은 수행한다.
- UI, 프로필/설정, credential, 마운트, 자동 시작, 프로세스 복구, 로그 및 패키징 역할을 분리한다.
- 변경 파일, 빌드/검증 결과, 실행 방법과 미검증 사항을 설명한다.
- 사용자는 코딩에 필요한 권한을 승인했다. 환경의 실제 sandbox/escalation 규칙은 준수한다.

## 보안과 데이터

- 비밀번호를 소스, JSON/XML/config, 명령줄 또는 로그에 저장/출력하지 않는다. Windows Credential Manager를 사용한다.
- 앱 설정/캐시/로그는 %LOCALAPPDATA%\UnionDrive 아래에서 관리한다. 서버 주소와 계정은 사용자 프로필 설정이다.
- 자체 rclone 설정은 --config NUL과 child process 환경으로 전달한다. obscure를 보안 저장소로 간주하지 않는다.
- TLS 인증서 검증을 끄지 않는다. NAS 접근 확인은 읽기 전용으로 수행한다.
- raw rclone output은 비우거나 안전한 오류 범주로만 분류하며 원문을 표시/기록하지 않는다.
- 인증된 loopback RC만 사용하고 PID/프로세스 시작 시각/실행 파일을 검증한다.
- 기존 rclone config, 외부 마운트, 사용자 CMD/VBS/스케줄러와 레지스트리는 변경하지 않는다.

## 안정성과 리소스

- 프로필별 rclone process를 개별 관리한다. 사용 중인 drive letter 및 중복 instance를 차단한다.
- VFS 전송 중/대기/실패 및 Dirty cache가 있으면 정상 해제/설정 변경/삭제/제거를 보류한다. 강제 종료나 미전송 캐시 삭제로 우회하지 않는다.
- 앱 충돌 후 자신의 남은 process를 소유권 기록으로 복구한다. 정리 과정에서 junction/symlink를 따라 삭제하지 않는다.
- Windows 종료는 전체 드라이브에 대해 총 3초 이내의 best effort로 처리한다.
- Electron/WebView, 별도 service, 상시 UI/NAS polling을 추가하지 않는다. 재연결은 이벤트 및 제한된 backoff를 사용한다.
- process, stream, HTTP client, tray, font, timer와 이벤트 구독을 명시적으로 정리한다. 리소스 누수 부재를 검증 범위 이상으로 단정하지 않는다.

## 설치/제거와 배포

- 최종 전달물은 dist의 installer EXE다. 공식 rclone, .NET runtime, 공식 WinFsp MSI, 라이선스와 앱 소스를 포함한다.
- UnionDrive는 일반 사용자 권한으로 실행하고 WinFsp 설치/제거만 Windows 관리자 권한을 요청한다.
- WinFsp는 driver의 정상 설치가 필요하다. DLL 동봉으로 설치를 대체하지 않는다.
- 기존 WinFsp를 보존하고 이 installer가 설치한 동일 product만 제거한다. 제거 정리는 사용자의 제거 확정 후에만 실행한다.
- 자신의 설정/credential/cache/log/자동 시작/설치 registry/바로 가기/앱 파일을 정리한다. 자체 scheduler/service를 만들지 않는다.
- 공식 라이선스와 재배포 조건을 준수한다. 현재 MIT UnionDrive와 수정하지 않은 WinFsp MSI를 FLOSS 예외에 따라 배포하며 고지/소스를 제공한다.
- installer 컴파일러는 상업적 사용이 허용되는 Inno Setup 6.4.3이다. PowerShell 7의 packaging/prepare.ps1 및 build.ps1로 재생성한다.
- 실제 NAS와 깨끗한 Windows에서의 driver install/mount/read/write를 mock 검증으로 대체하거나 검증 완료로 보고하지 않는다.
