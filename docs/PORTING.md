# 새 PC로 이전하기

## 1. 읽기 전용 조사

사용자가 실제 모니터 앞에 있는지 먼저 확인합니다. 최초 설치/장치 재시작을 유일한 원격
접속 경로에 의존하여 자동 진행하지 않습니다. 실행 중인 연구 작업을 종료하지 않습니다.

```powershell
Get-CimInstance Win32_OperatingSystem | Select-Object Caption,Version,OSArchitecture
Get-CimInstance Win32_VideoController | Select-Object Name,DriverVersion,PNPDeviceID
Get-PnpDevice -Class Display -PresentOnly
.\scripts\build.ps1 -Test
.\scripts\configure-machine.ps1 -ListGpus
```

빌드한 exe에 `--diagnose --output artifacts\diagnosis.json`을 전달하면 화면과 설정을
읽을 수 있습니다. GUI exe는 PowerShell의 `Start-Process -Wait`로 완료를 기다리세요.
진단 파일은 장치 식별자가 있으므로 Git에 올리지 않습니다.

| 항목 | 판단 및 변경 지점 |
|---|---|
| 실제 화면 수 | 현재 정확히 2개. 바꾸려면 Displays.SaveLab/RemoteProfile, Controller, VirtualKeys, UI, tests를 함께 변경 |
| GPU | PCI 장치 이름을 정확히 선택. 복수 GPU에서는 렌더링 GPU를 근거 없이 선택하지 않음 |
| 성능 | DXGI 성공과 CRD 전체 지연은 별도. 이 앱은 CRD 인코더를 설정하지 않음 |
| 회전 | source Width/Height와 회전 후 바탕 화면 경계가 다를 수 있음. Rotation 및 MONITORINFO 함께 확인 |
| 배율 | 물리 화면 배율은 보존. DPI 쓰기는 가상 화면만 허용 |
| 원격 배치 | landscape는 가로 QHD 2개/100% 또는 4K 2개/150%. match-lab은 물리 배치 기반 |
| 기존 VDD/Parsec | 임의 제거/덮어쓰기 금지. HardwareID, 버전, VDDPATH, 소유권을 먼저 확인 |
| OS/CPU | x64 전용. ARM64와 다른 Windows 버전의 드라이버 호환성은 미검증 |

## 2. 로컬 설정

앱과 guard를 종료한 연구실 모드에서 실행합니다. 다른 PC의 파일은 가져오지 않습니다.

```powershell
.\scripts\configure-machine.ps1 -GpuName '목록의 GPU 이름' -RemoteLayout landscape
# 실제 화면 방향을 따르려면 -RemoteLayout match-lab
# landscape에서 가로 4K/150%를 원하면 -NativeResolution
```

설정 백업 후 RenderingGpu, RemoteLandscape, Compact를 저장하고 Tested=false로 만듭니다.
드라이버나 실제 화면은 바꾸지 않습니다. VirtualKeys는 대상 PC에서 새로 감지합니다.

## 3. 설치 및 검증

1. Windows 설정에서 실제 두 화면만 활성화하고 배치·방향·DPI·주 화면을 맞춥니다.
2. `scripts/install-app.ps1 -StartOnLogin -Launch`로 사용자별 앱을 설치합니다.
3. 앱에서 **연구실 배치 저장**을 누릅니다.
4. 아래 패키지 서명을 확인하고 **가상 화면 준비**로 드라이버를 설치합니다. UAC가 표시됩니다.
5. 다른 기기의 Chrome 원격 데스크톱 등에서 화면·키보드·마우스를 확인합니다.
6. 원격 모드를 켜고 60초 안에 두 화면·방향·DPI·입력을 확인하여 유지합니다.
   확인할 수 없으면 자동 복귀를 기다리거나 연구실 복귀를 사용합니다.
7. 원격 연결 종료 후에도 실제 화면이 숨겨져 있는지, 재접속 가능한지 확인합니다.
8. 실제 키보드 Home 복귀/End 숨김, 창 배치, 마우스 분리 시 커서를 확인합니다.

```powershell
Get-AuthenticodeSignature .\vendor\vdd\MttVDD.dll, .\vendor\vdd\mttvdd.cat |
    Select-Object Path,Status,SignerCertificate
```

GPU 이름/PCI 존재 확인은 렌더링·인코딩 성능 보증이 아닙니다. 다중 GPU는 실제 VDD
렌더링 경로를 검증해야 합니다. `--dxgi-diagnose` 성공도 CRD의 실제 캡처 경로 증명이 아닙니다.

## 기존 VDD가 있는 PC

Root\MttVDD가 있으면 설치기는 새 설치로 덮어쓰지 않습니다. 준비 버튼은 기존 가상 화면을
사용할 수 있지만 임의 패키지 버전의 호환성을 검증하지는 않습니다. VDDPATH가 이 앱의
ProgramData 경로가 아니면 커서 설정 변경도 거부합니다. 기존 출처/설정을 백업하고
공존/공유 여부를 결정하세요. RenderingGpu를 바꿔도 기존 XML/드라이버는 자동 변경되지 않습니다.

## Codex가 남길 결과

OS/GPU/드라이버 버전, 화면 배치와 원격 프로필, 변경 파일, 백업 위치, 로직 테스트와
실제 클라이언트 확인 결과를 구분해 기록합니다. 미검증 항목과 복구 명령도 적습니다.
장치 ID·창 핸들 등 개인별 상세 진단은 `artifacts/`에만 저장합니다.
