# Lab Display Switch

Windows PC의 **실제 듀얼 모니터와 가상 듀얼 모니터를 전환하는 트레이 앱**입니다.
원격 작업 중 실제 모니터 출력을 숨기고, 복귀할 때 물리 화면과 창 배치를 복원합니다.
접속 인증·네트워크·화면 전송은 **Chrome 원격 데스크톱 등 별도 제품**이 담당합니다.

```text
Mac / 노트북 ── 원격 접속 프로그램 ── Windows의 같은 로그인 세션
                                        │
                   Lab Display Switch ──┤
                                        ├─ 연구실: 실제 화면 2개
                                        └─ 원격: 가상 화면 2개, 실제 출력 끔
```

작업 앱을 종료하거나 사용자 세션을 새로 만들지 않습니다. 실제 화면이 가로+세로여도
원격 화면만 가로 QHD 2개·100%로 사용할 수 있습니다. 전환 확인 전에는 독립 guard가
실패·시간 만료 시 복귀시키고, 확인 후에는 원격 연결 종료만으로 실제 화면이 켜지지 않습니다.

| 단축키 | 동작 |
|---|---|
| `Ctrl + Alt + Home` | 실제 모니터로 복귀 |
| `Ctrl + Alt + End` | 사전 검증한 원격 설정으로 화면 숨김 유지 |

X 버튼은 트레이로 숨깁니다. 단축키는 트레이 앱이 실행 중인 일반 바탕 화면에서 사용합니다.
다른 앱 내부 단축키와 겹칠 수 있고 잠금·로그인·UAC 화면에서는 보장하지 않습니다.

## 지원 범위

Windows x64, .NET Framework 4.8 계열, **실제 화면 2개와 지정한 VDD 화면 2개**가 전제입니다.
개발 PC의 Windows 11 Home/NVIDIA 환경에서 검증했습니다. 모든 GPU에서 검증한 범용 완제품은
아니며 AMD/Intel/복수 GPU/다른 DPI/기존 VDD는 대상 환경에서 검증해야 합니다.
Mac은 원격 클라이언트이며 이 앱의 설치 대상이 아닙니다.

## 다른 PC에서 시작

먼저 [새 PC 이전 가이드](docs/PORTING.md)를 읽으세요. 기존 PC의 AppData, 모니터 ID,
`settings.json`, `lab-profile.json`, `transaction.json`은 복사하지 않습니다.

Windows PowerShell에서 저장소를 clone한 폴더로 이동한 뒤:

```powershell
.\scripts\build.ps1 -Test
.\scripts\configure-machine.ps1 -ListGpus
.\scripts\configure-machine.ps1 -GpuName '목록에 나온 실제 GPU 이름' -RemoteLayout landscape
.\scripts\install-app.ps1 -StartOnLogin -Launch
```

기존 앱이 있다면 연구실 모드에서 UI와 guard를 종료하고 설정합니다. GPU 선택은 새
드라이버 설치용이며 기존 VDD를 변경하지 않습니다. 앱에서 **연구실 배치 저장** 후
**가상 화면 준비**를 누릅니다. 최초 드라이버 설치에는 관리자 확인이 필요합니다.

다른 기기에서 원격 접속을 확인한 다음 **원격 모드 · 화면 숨기기**를 누르고,
60초 안에 두 화면과 입력을 확인하여 **원격 모드 유지**를 누릅니다.
이 확인 전에는 퇴근 단축키가 활성화되지 않습니다.

## 다른 PC의 Codex에 전달할 프롬프트

> 이 PC에 Lab Display Switch를 맞춰줘. 먼저 AGENTS.md와 docs/PORTING.md,
> docs/ARCHITECTURE.md, docs/RECOVERY.md를 읽고 GPU·모니터 수·방향·배율·기존
> 가상 드라이버를 조사해. 다른 PC의 상태 파일이나 장치 ID를 복사하지 말고 새로 생성해.
> 먼저 빌드와 로직 테스트를 하고, 설치와 실제 화면 전환은 복구 경로를 준비한 뒤
> 단계적으로 진행해. 실행 중인 작업 앱을 종료하거나 재부팅하지 마.

## 문서

- [PORTING.md](docs/PORTING.md): GPU 선택, 설치, 대상 PC 검증
- [ARCHITECTURE.md](docs/ARCHITECTURE.md): 구조, 상태 전이, 수정할 코드
- [RECOVERY.md](docs/RECOVERY.md): DPI·모니터 교체·커서 문제와 복구
- [TESTING.md](docs/TESTING.md): 일반 테스트와 실제 화면 시험 구분
- [AGENTS.md](AGENTS.md): Codex 작업 규칙
- [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md): 드라이버 출처와 라이선스

## 로컬 상태와 한계

사용자 상태는 `%LOCALAPPDATA%\LabDisplaySwitch`, 드라이버 XML은
`%PROGRAMDATA%\LabDisplaySwitch\VDD`, 앱은 `%LOCALAPPDATA%\LabDisplaySwitch\app\<version>`에
저장됩니다. `artifacts/`, `dist/`, 개인 상태·로그·스크린샷은 Git에 올리지 않습니다.

출력 숨김은 Windows 잠금이나 접근 통제를 대체하지 않습니다. 재부팅 후 작업 세션 재생,
모든 앱/스냅 그룹의 완벽한 창 복원, GPU 리셋·모니터 연결 순간까지의 무노출은 보장하지 않습니다.
이 프로젝트 소스는 MIT 라이선스이며 드라이버 라이선스는 별도로 유지합니다.
