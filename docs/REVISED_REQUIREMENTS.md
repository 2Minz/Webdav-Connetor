# 최신 요구사항 (2026-10-05)

사용자가 초기 Phase 1 제한을 해제하고 다음 범위를 요청했다.

1. 배포물은 installer 형태다.
2. 설치 시 UnionDrive와 rclone, WinFsp를 함께 준비한다.
3. uninstaller가 함께 설치한 프로그램과 자신의 설정/자동 실행/레지스트리 등을 정리한다.
4. 사용자가 installer를 직접 설치하고 실행해 확인할 수 있는 완성된 패키지를 전달한다.
5. 컴퓨터 리소스 사용을 최소화하고 메모리/핸들 누수가 없도록 구현·검증한다.
6. 여러 드라이브를 지원하고 드라이브별 연결/해제/설정을 독립적으로 관리한다.
7. 코딩 과정에 필요한 권한을 승인하며 확인 질문으로 반복 중단하지 않는다.

2026-10-05에 재부팅을 위해 사용자가 잠시 중단을 요청했고 같은 채팅에서 작업 재개를 요청했다.

구현 결정: 사용자 계정별 설치, self-contained .NET 10, 공식 rclone 1.75.1 및 WinFsp 2.1 Core, Windows Credential Manager, HKCU Run 자동 시작, 프로필별 VFS cache와 mount process, native WinForms/tray. 별도 scheduler/service는 만들지 않는다.

제거 범위는 UnionDrive가 소유한 현재 사용자의 데이터/프로세스/시작 항목과 installer가 설치한 동일 WinFsp product로 한정한다. 기존 WinFsp 및 다른 프로그램/사용자가 생성한 항목은 보존한다.

공식 WinFsp의 FLOSS 재배포 조건을 충족하기 위해 UnionDrive는 MIT 라이선스와 소스 ZIP을 포함하며 WinFsp 공식 MSI를 수정하지 않는다. 외부 공개 저장소 업로드는 수행하지 않았다.

## 2026-10-06 수정 요청

표시 이름 Webdav Connetor, 제목 MOUNTED DRIVE LIST, Developer - D.M.Kang. 주소·포트·공유 폴더 분리, 캐시는 세부 설정으로 이동, 창 크기 고정, 아이콘 통일, 탐색기 볼륨명 표시를 적용한다. 요구한 수정만 구현하며 기능 검증은 사용자가 직접 수행한다. 에이전트는 컴파일/패키징만 수행한다.
