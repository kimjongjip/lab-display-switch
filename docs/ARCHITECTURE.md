# 구조와 수정 지점

이 앱은 같은 Windows 세션의 출력 토폴로지와 창 배치를 변경합니다. 네트워크 서버,
Mac 클라이언트, 영상 인코더, 원격 인증은 구현하지 않습니다.

```text
lab -> preparing -> awaiting-confirmation -> remote (Confirmed=true)
          |                 |
          +---- 오류/기한 만료/소유자 종료 ----> lab
remote -> restoring -> lab
remote + 예상 밖의 물리 출력 -> privacy-fault (Windows 잠금 요청)
```

transaction.json은 원자적으로 교체하며 세션별 named mutex가 전환을 직렬화합니다.
독립 guard는 확인 전 rollback을 수행합니다. 확인 후 단절/UI 종료만으로 복귀하지 않습니다.
정상 복귀는 원격 작업 중의 현재 창 배치를 물리 화면으로 옮기며, 실패 rollback은 전환
직전 창 배치를 복원합니다. guard는 원격 중 절전 방지를 요청합니다.

| 파일 | 책임 |
|---|---|
| Program.cs | CLI, WinForms, UI 단일 인스턴스 |
| MainForm.cs / TrayHotkeys.cs | 트레이, Home/End, 전환 중 복귀 요청 대기 |
| Displays.cs | Query/SetDisplayConfig, 장치 식별, 프로필 생성/검증 |
| Dpi.cs | 가상 DPI 설정, 범위/실측 배율 검증 |
| Windows.cs | 창 신원 확인, DPI 좌표, 최대화/최소화 복원·재검사 |
| Controller.cs / Guard.cs | 트랜잭션, rollback, privacy-fault, 절전 방지 |
| Installer.cs | 패키지 해시, 선택 GPU XML, 새 VDD 설치 |
| DriverMaintenance.cs | 앱 소유 VDD 설정 보완과 단일 장치 재시작 |
| CursorConfiguration.cs | 하드웨어 커서 크기·알파 설정 누락 보완 |
| CursorRecovery.cs | 특정 MouseKeys/커서 소실 상태만 재활성화 |
| Diagnostics.cs / DxgiDiagnostics.cs | 캡처 비용, DXGI, 커서 메타데이터 |
| Models.cs / Native.cs | 저장 모델, 파일 저장, Win32 인터롭 |

## 상태 및 하드웨어 의존성

`%LOCALAPPDATA%\LabDisplaySwitch`에 settings.json(Tested, Compact, RemoteLandscape,
RenderingGpu, VirtualKeys, LastMode), lab-profile.json(물리 화면), transaction.json
(소유자/기한/Before/After/창 목록), 로그/진단을 저장합니다. 다른 PC로 복사하면 안 됩니다.

드라이버 XML은 `%PROGRAMDATA%\LabDisplaySwitch\VDD\vdd_settings.xml`이며 HKLM의
`SOFTWARE\MikeTheTech\VirtualDisplayDriver\VDDPATH`가 가리킵니다. 새 설치 시 로컬
RenderingGpu를 XML 이스케이프하여 사용합니다. HardwareID는 `Root\MttVDD`이며
`ROOT\DISPLAY\0001` 같은 인스턴스 번호는 고정하지 않습니다.

RemoteLandscape=true일 때 Compact는 가로 QHD 2개/100%, 해제 시 4K 2개/150%입니다.
false이면 실제 프로필을 복제하고 CompactProfile로 축소합니다. 물리 모니터를 교체한 뒤에는
SaveLab과 원격 재확인이 필요합니다. 정확히 두 화면이라는 가정은 여러 계층에 존재합니다.

## 유지해야 하는 검증

- DPI 쓰기는 undocumented 요청 -3/-4입니다. 범위, 실제 Scale, virtual-only, 쓰기 후
  실측 검증을 없애지 않습니다. stale offset은 실제 100%로 제한된 경우에만 해석합니다.
- 회전 시 source 치수와 바탕 화면 경계는 다를 수 있습니다. 창 배치는 MONITORINFO
  작업 영역을 사용합니다. 실제 화면 수/위치/크기/방향도 확인합니다.
- 커서 자동 복구는 확정 원격 상태에서만 5초 간격으로 확인합니다. 두 번 연속으로
  MouseKeys ON + 감지 마우스 없음 + 커서 flags=0/handle=0일 때만 재활성화합니다.
- 정상 커서, 터치 CURSOR_SUPPRESSED, 사용자가 MouseKeys를 끈 상태는 건드리지 않습니다.
- 새 상태 필드를 추가하면 구버전 guard도 교체해야 합니다. 구버전 직렬화로 필드가 사라질 수 있습니다.
