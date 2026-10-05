# Project Context — UnionDrive

## 1. 프로젝트 목적

Windows에서 회사 Synology NAS의 WebDAV를 일반 로컬/네트워크 드라이브처럼 마운트해서 사용하는 경량 프로그램을 개발한다.

현재 아래 조합으로 실제 연결 및 사용 테스트까지 완료했다.

- Windows 11
- Synology NAS
- Synology WebDAV HTTPS
- rclone
- WinFsp
- Z: 드라이브 마운트

현재 수동 구성은 정상적으로 작동한다.

최종 목표는 사용자가 rclone, WinFsp, CMD, VBS, 작업 스케줄러 등을 직접 설정하지 않아도 되는 간단한 Windows 프로그램을 만드는 것이다.

가칭 프로그램명:

UnionDrive


## 2. 개발 환경

- OS: Windows 11
- Language: C#
- Framework: .NET 10 / net10.0-windows
- IDE: VS Code + Codex
- Git 사용
- GitHub 사용 예정

가능하면 외부 런타임 의존성을 최소화한다.

프로그램 자체는 최대한 가볍게 만든다.


## 3. 현재 검증 완료된 NAS 연결 방식

Protocol:
WebDAV over HTTPS

현재 테스트 서버:

https://uniontech.synology.me:5006

주의:
실제 배포 버전에서는 서버 주소를 코드에 하드코딩하지 않고 설정에서 변경할 수 있도록 한다.

rclone remote의 현재 테스트 이름:

Union

WebDAV vendor:

Other

인증:

Username + Password


## 4. 현재 검증 완료된 rclone mount 명령

아래 명령으로 정상적으로 Z: 마운트되고 Windows 탐색기에서 파일 접근/읽기/쓰기가 되는 것을 확인했다.

rclone mount Union: Z: ^
 --vfs-cache-mode full ^
 --network-mode ^
 --volname "UNION" ^
 --dir-cache-time 30s ^
 --vfs-cache-max-size 10G ^
 --vfs-cache-max-age 24h

초기 테스트에서는 --dir-cache-time 5m도 사용했으나,
회사 공유 NAS 특성상 다른 사용자의 파일 변경사항을 비교적 빨리 반영하기 위해 30s를 기본값으로 고려한다.


## 5. 현재 PC에서 검증한 기존 구조

현재는 아래 구조로 정상 작동한다.

1. WinFsp 설치
2. rclone 설치
3. rclone config에서 WebDAV remote 생성
4. rclone mount 실행
5. VBS를 이용해 rclone CMD 창 숨김 실행
6. Windows 작업 스케줄러에서 로그인 후 자동 실행
7. Z: 자동 마운트

이 구조를 UnionDrive 프로그램 하나가 대신 관리하는 것이 목표다.


## 6. 프로그램 기본 동작

UnionDrive 실행 시:

1. WinFsp 설치 여부 확인
2. 필요한 rclone 실행환경 확인
3. 저장된 NAS 설정 로드
4. NAS 연결 가능 여부 확인
5. rclone mount 프로세스 시작
6. 지정된 드라이브 문자에 마운트
7. 연결 상태 모니터링
8. Windows tray에 상주

프로그램 종료 또는 사용자가 연결 해제를 선택하면 rclone mount 프로세스를 정상 종료한다.


## 7. rclone 배포 방식

사용자에게 rclone을 별도로 설치하도록 요구하지 않는다.

UnionDrive 설치 패키지 또는 프로그램 리소스에 공식 rclone.exe를 포함하는 방향을 우선 검토한다.

예:

UnionDrive/
 ├─ UnionDrive.exe
 └─ runtime/
      └─ rclone.exe

또는 embedded resource로 포함 후 필요 시 프로그램 데이터 디렉터리에 추출하는 방법도 검토한다.

rclone의 라이선스 및 재배포 조건을 확인하고 준수한다.


## 8. WinFsp 처리

WinFsp는 Windows filesystem driver이므로 단순 DLL 포함만으로 처리할 수 없으며 Windows에 정상 설치되어야 한다.

프로그램 시작 시 WinFsp 설치 여부를 확인한다.

미설치 상태라면 사용자에게 안내하고 설치할 수 있도록 한다.

최종 installer에서는 가능하면:

UnionDrive Setup
 → UnionDrive 설치
 → WinFsp 설치
 → 필요한 구성 완료

흐름으로 만든다.

WinFsp의 공식 배포/라이선스 조건을 확인하고 준수한다.

WinFsp 설치에는 관리자 권한이 필요할 수 있지만,
UnionDrive 자체는 가능하면 일반 사용자 권한으로 실행한다.


## 9. GUI 요구사항

화면은 최대한 단순하게 만든다.

예:

--------------------------------
          UNION DRIVE

 ● NAS 연결됨

 UNION (Z:)
 uniontech.synology.me

 [ 드라이브 열기 ]
 [ 연결 해제 ]

 ☑ Windows 시작 시 자동 연결

                  [ 설정 ]
--------------------------------

상태:

- 연결됨
- 연결 중
- 연결 끊김
- 오류

정도를 명확하게 표시한다.

화려한 UI보다 가볍고 빠른 프로그램을 우선한다.


## 10. System Tray

프로그램은 평소 Windows system tray에서 동작하도록 한다.

Tray menu 예:

UnionDrive
----------------
연결 상태
Z: 열기
연결
연결 해제
설정
----------------
종료

창의 X 버튼을 눌렀을 때 프로그램 종료보다는 tray 최소화를 기본 동작으로 고려한다.

명시적인 "종료"를 선택하면 mount 프로세스도 정상 종료한다.


## 11. 설정 화면

사용자가 최소한 아래 항목을 변경할 수 있도록 한다.

Server URL
예:
https://uniontech.synology.me:5006

Username

Password

Drive Letter
기본:
Z:

Volume Name
기본:
UNION

Windows 시작 시 자동 연결
On / Off

필요하다면 Advanced Settings에서:

VFS cache size
VFS cache age
Directory cache time

등을 변경할 수 있도록 한다.

일반 사용자는 rclone 옵션 자체를 알 필요가 없어야 한다.


## 12. 비밀번호 보안

NAS password를 평문 JSON/XML/config 파일에 저장하지 않는다.

Windows에서 제공하는 안전한 credential storage를 우선 사용한다.

후보:

- Windows Credential Manager
- Windows DPAPI

가능하면 Windows Credential Manager를 우선 검토한다.

로그에도 password가 절대 출력되지 않도록 한다.

rclone config를 직접 생성해야 하는 경우에도 credential 노출을 최소화한다.

rclone의 "obscure"는 강력한 암호화 저장소로 간주하지 않는다.


## 13. 자동 시작

현재 사용 중인:

CMD
VBS
Windows Task Scheduler

조합을 최종 사용자에게 요구하지 않는다.

UnionDrive 자체에서 Windows 로그인 시 자동 시작을 관리한다.

가능하면 사용자 권한에서 처리한다.

중복 실행을 방지한다.

예:

UnionDrive.exe --startup

등의 실행 인자를 사용할 수 있다.


## 14. Mount Process 관리

rclone mount는 UnionDrive의 child/background process로 관리한다.

필요 기능:

- 시작
- 종료
- 상태 확인
- 비정상 종료 감지
- 재연결
- stdout/stderr capture
- 오류 코드 처리

같은 드라이브 문자가 이미 사용 중이면 mount를 시도하지 않고 사용자에게 알려준다.

UnionDrive가 이미 실행 중이면 두 번째 instance가 mount process를 또 생성하지 않도록 single-instance 처리를 한다.


## 15. 네트워크 장애 처리

노트북 환경을 고려한다.

예:

회사
→ 집
→ Wi-Fi 변경
→ 절전
→ 최대 절전
→ 네트워크 끊김
→ 재연결

등이 빈번할 수 있다.

인터넷이 끊어졌다고 프로그램 자체가 죽으면 안 된다.

네트워크 복구 후 자동 재연결 기능을 고려한다.

단, 지나치게 짧은 간격으로 무한 재시도하지 않는다.

예:

5초
10초
30초
60초

등의 backoff 적용을 고려한다.


## 16. Windows 종료 처리

Windows 종료/로그오프 시 rclone process를 가능한 한 정상적으로 종료한다.

특히:

--vfs-cache-mode full

을 사용하기 때문에 NAS로 아직 write-back되지 않은 데이터가 있을 가능성을 고려해야 한다.

가능한 범위에서 graceful shutdown을 구현한다.

다만 Windows 종료를 과도하게 지연시키거나 강제로 막는 설계는 피한다.


## 17. 로그

문제 해결용 로그를 남긴다.

예:

%LOCALAPPDATA%\UnionDrive\Logs\

로그 내용:

- 프로그램 시작
- mount 시작
- mount 성공
- 연결 실패
- rclone 종료
- network 상태 변화
- 오류 메시지

Password 및 민감한 인증정보는 절대 기록하지 않는다.

로그 파일은 무한히 커지지 않도록 rotation을 적용한다.


## 18. 설정 저장 위치

예:

%LOCALAPPDATA%\UnionDrive\

구조 예:

UnionDrive/
 ├─ config.json
 ├─ Logs/
 ├─ Cache/
 └─ Runtime/
      └─ rclone.exe

Password는 config.json에 저장하지 않는다.


## 19. 캐시

rclone VFS cache 위치를 UnionDrive가 관리한다.

예:

%LOCALAPPDATA%\UnionDrive\Cache

기본 최대 크기:

10 GB

기본 max age:

24h

설정에서 변경 가능하도록 고려한다.

캐시 삭제 기능도 향후 추가 가능하도록 구조를 만든다.


## 20. 기본 rclone 옵션

초기 기본값:

--vfs-cache-mode full
--network-mode
--volname UNION
--dir-cache-time 30s
--vfs-cache-max-size 10G
--vfs-cache-max-age 24h

필요하면:

--cache-dir

도 UnionDrive 전용 cache directory로 명시한다.

옵션을 코드 여러 곳에 직접 문자열로 흩뿌리지 말고 configuration/model로 관리한다.


## 21. 프로그램 경량화 원칙

가장 중요한 요구사항 중 하나다.

- 불필요한 background service 금지
- 불필요한 polling 최소화
- Electron 사용하지 않음
- WebView 기반 UI 사용하지 않음
- 과도한 dependency 사용하지 않음
- 불필요한 animation 사용하지 않음

WinForms 또는 가벼운 Windows native 계열 UI를 우선 검토한다.

CPU idle 사용량은 사실상 0%에 가깝게 유지하는 것을 목표로 한다.

메모리 사용량도 가능한 한 작게 유지한다.


## 22. 아키텍처

처음부터 과도하게 복잡하게 만들지 않는다.

하지만 최소한 다음 역할은 분리한다.

UI
MountManager
RcloneManager
CredentialManager
ConfigurationManager
StartupManager
WinFspDetector
NetworkMonitor
Logger

각 기능은 교체/수정 가능하도록 모듈화한다.

거대한 MainForm 하나에 모든 로직을 넣지 않는다.


## 23. 향후 확장 가능성

초기 버전은 Synology WebDAV 하나만 지원한다.

그러나 향후:

- 여러 NAS
- 여러 drive
- 다른 WebDAV server
- SFTP
- OneDrive
- Google Drive

등 rclone backend를 추가할 가능성이 있다.

따라서 내부적으로 ConnectionProfile 개념을 두는 것을 고려한다.

하지만 V1에서 필요하지 않은 기능을 미리 과도하게 구현하지 않는다.


## 24. V1 목표

V1에서는 아래 기능만 완성하면 된다.

1. Synology WebDAV 설정
2. Credential 안전 저장
3. Z: mount
4. Disconnect
5. Drive 열기
6. 상태 표시
7. System tray
8. Windows 자동 시작
9. rclone background process 관리
10. WinFsp 설치 여부 확인
11. 기본 로그
12. 네트워크 끊김/복구에 대한 기본적인 안정성


## 25. 개발 원칙

- 한 번에 전체 프로그램을 생성하지 말 것.
- 단계별로 구현하고 매 단계 build/test할 것.
- 필요 이상의 코드를 만들지 말 것.
- 기능별 클래스를 분리할 것.
- 기존 정상 동작 기능을 수정할 때 regression을 고려할 것.
- security-sensitive code는 특히 단순하게 유지할 것.
- 비밀번호/credential을 source code에 절대 넣지 말 것.
- 서버 주소와 계정 역시 가능한 한 설정값으로 처리할 것.
- 사용자가 요청하지 않은 기능을 임의로 대량 추가하지 말 것.


## 26. 첫 개발 단계

우선 전체 코드를 생성하지 말고 다음 순서로 진행한다.

Phase 1:
- 프로젝트 기본 구조 생성
- WinForms 기반 최소 UI
- rclone.exe 경로 확인
- WinFsp 설치 여부 확인
- 기존 사용자의 rclone config를 이용해 Union:을 Z:로 mount/unmount
- 연결 상태 UI 표시

Phase 1에서는 Credential Manager나 installer까지 구현하지 않는다.

현재 PC에는 이미:

- rclone 설치 완료
- WinFsp 설치 완료
- rclone remote "Union" 설정 완료

상태이므로 기존 환경을 이용해서 프로그램의 핵심 mount/unmount 기능부터 검증한다.

Phase 1이 정상 동작한 후:

Phase 2:
- 자체 ConnectionProfile
- Credential Manager
- 자체 rclone configuration

Phase 3:
- tray
- auto start
- reconnect
- logging

Phase 4:
- rclone bundled deployment
- WinFsp installer integration
- UnionDrive Setup 제작

순서로 진행한다.


## 27. Codex 작업 방식

작업을 시작하기 전에:

1. 현재 repository 상태 확인
2. 필요한 파일만 생성
3. 구현 계획을 간단히 설명
4. Phase 1만 구현
5. dotnet build 실행
6. build error가 있으면 수정
7. 변경된 파일과 테스트 방법을 설명

첫 작업에서 Phase 2~4까지 한꺼번에 구현하지 않는다.

이 프로젝트의 핵심 목표는:

"RaiDrive 대신 사용할 수 있는 매우 가볍고 단순한 회사 Synology WebDAV Windows Drive Client"

이다.