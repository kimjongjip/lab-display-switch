# 복구 및 문제 해결

## 화면 복귀

트레이 앱이 살아 있는 일반 바탕 화면이면 Ctrl+Alt+Home 또는 앱의 복귀 버튼을 사용합니다.
잠금/보안 바탕 화면에서는 이 키가 동작한다고 보장하지 않습니다. guard의 F12 조합은
레거시 비상 경로로 남아 있지만 F12는 시스템 예약 키라 유일한 복구 수단으로 쓰지 않습니다.

```powershell
# <version>을 실제 설치 폴더 이름으로 바꾸세요.
$app=Join-Path $env:LOCALAPPDATA 'LabDisplaySwitch\app\<version>\LabDisplaySwitch.exe'
$result=Join-Path $env:LOCALAPPDATA 'LabDisplaySwitch\manual-recovery.json'
Start-Process -FilePath $app -ArgumentList ('--restore --output "'+$result+'"') -WindowStyle Hidden -Wait
Get-Content -LiteralPath $result -Encoding UTF8
```

모니터 교체 후에는 과거 물리 프로필이 맞지 않을 수 있습니다. 실제 화면 앞에서 Windows
설정으로 실제 출력과 배치를 복구하고 다시 저장합니다. 모든 디스플레이 장치를 삭제하거나
과거 PC의 장치 ID를 무조건 적용하지 마세요.

## 가상 화면이 실제 화면 사이에 끼어 있음

현재 토폴로지와 창을 백업하고 실제 두 출력만 남긴 뒤 실제 경계를 맞춰 SaveLab합니다.
물리 DPI와 회전은 보존합니다. 개발 PC용 일회성 repair 스크립트는 공유 대상이 아닙니다.
대상 PC의 화면 순서와 장치 키로 계획을 새로 만들고 원격 구성을 재확인합니다.

## 배율 오류

events.log의 actual/desired/minimum/current/maximum을 확인합니다. 해상도 변경 후 이전
상대 배율이 남을 수 있습니다. 검사를 삭제하거나 물리 DPI를 무조건 바꾸지 않습니다.
가로 QHD/100% 원격 프로필을 사용하는 것도 선택지입니다. 불일치가 계속되면 복귀합니다.

## 마우스 없이 점 커서

`--cursor-status`로 표시 상태를 먼저 읽습니다. 개발 환경에서는 MouseKeys를 켜 두어도
분리 후 커서가 사라졌고, 마우스가 없는 상태에서 껐다 켜니 복구됐습니다. 1.0.3부터 트레이가
이 특정 상태를 감지합니다. 모든 커서 문제를 해결하는 기능은 아닙니다.

```powershell
.\scripts\mouse-without-hardware.ps1 -Mode Inspect
.\scripts\mouse-without-hardware.ps1 -Mode Enable
.\scripts\mouse-without-hardware.ps1 -Mode Restore -BackupPath 'Enable이 출력한 백업 경로'
```

이 설정은 숫자패드에도 영향을 줍니다. 현재 스크립트에서는 Num Lock ON이면 숫자 입력,
OFF이면 마우스 조작입니다. Mac에서 재접속·화살표/텍스트 커서·이중 커서를 확인합니다.

## 이중 커서/잔상

XML의 HardwareCursor, CursorMaxX/Y, AlphaCursorSupport를 확인합니다. 새 XML은 모두
포함합니다. `--preview-cursor-fix`는 미리보기만 저장합니다. 보호된 설정 변경과
`--apply-cursor-fix-in-lab`은 실제 화면 복귀 후에만 실행합니다. 앱 소유 VDDPATH인지
확인하며 타 제품 드라이버는 변경하지 않습니다. 재시작이 오래 걸려도 중복 시작하지 않습니다.

## 업데이트

1. 연구실 모드로 복귀하고 상태/설정을 백업합니다.
2. 트레이 메뉴에서 완전히 종료합니다. X는 종료가 아닙니다.
3. 현재 설치본에 `--stop-guard`를 전달하고 종료를 확인합니다.
4. `scripts/build.ps1 -Test`, 이어서 `scripts/install-app.ps1 -StartOnLogin -Launch`.
5. 새 UI/guard, 단축키, 원격 재접속을 확인합니다.

설치 스크립트는 manifest에서 버전을 읽습니다. 실행 중인 exe를 덮어쓰거나 이전 버전
폴더를 임의 삭제하지 않습니다. 구버전 guard는 새 설정 필드를 모르므로 함께 교체합니다.

제거할 때도 먼저 실제 화면으로 복귀합니다. 자동 시작 해제 후 UI/guard를 종료하고,
드라이버가 필요 없으면 정확한 HardwareID/인스턴스를 검증한 해당 VDD만 제거합니다.
GPU/Parsec/다른 VDD를 일괄 제거하지 않습니다.
