---
paths:
  - "Project_Cozy/Assets/Scripts/Gameplay/Settings*.cs"
  - "Project_Cozy/Assets/Scripts/Gameplay/DesktopIntegrationApplier.cs"
  - "Project_Cozy/Assets/Scripts/UI/Settings*.cs"
  - "Project_Cozy/Assets/Scripts/Platform/Startup/**"
  - "Project_Cozy/Assets/Prefabs/UIPanels/UIPanel_Settings/**"
---

# 유저 설정 규약

설정 패널의 값을 어디에 두고, 누가 어떻게 적용하는지에 대한 규칙입니다. 항목별 기본값은 `SettingsFileFormat`의 필드 초기화가 정본입니다. 소리 탭만의 규칙은 [audio.md](audio.md), 언어 저장 형식은 [localization.md](localization.md)에 있습니다.

## 값의 주인은 SettingsManager 하나이고, 적용하는 쪽은 그것만 구독합니다

설정 패널은 `SettingsManager`에 값을 쓰기만 하고, 값을 들고 있지 않습니다. 설정을 실제로 적용하는 쪽(배경음악, 캐릭터, 창, 스폰 기운 등)은 `SettingsManager`의 값을 읽고 그 이벤트를 구독합니다. UI를 구독하지 않습니다.

이렇게 두는 이유는 값이 바뀌는 길이 패널 하나가 아니기 때문입니다. 시작할 때 파일에서 복원하고, 나중에는 초기화처럼 패널 밖에서 바뀔 수도 있습니다. 적용하는 쪽이 UI를 보고 있으면 그런 길로 바뀐 값을 놓칩니다.

- **`Changed`는 어느 설정이 바뀌어도 울립니다.** 그래서 OS를 부르거나 비싼 일을 하는 쪽은 마지막으로 건 값과 다를 때만 다시 겁니다. 언어 하나 바꿀 때마다 창 순서를 다시 잡거나 레지스트리를 쓰면 안 됩니다.
- **OS와 맞닿는 설정은 Gameplay의 적용자가 Platform을 부릅니다.** 항상 위와 자동 실행은 `DesktopIntegrationApplier`가 `WindowManager.SetTopmost`와 `StartupRegistration`을 부릅니다. Platform은 게임 설정을 모르게 둡니다([platform.md](platform.md)의 책임 경계).

## 기본값을 바꿀 때는 저장 키 이름도 바꿉니다

저장은 모든 필드를 파일에 씁니다. 그래서 설정을 한 번이라도 바꾼 사람의 파일에는, 직접 고른 적이 없는 항목까지 그때의 기본값으로 적혀 있습니다. 이 상태에서 같은 키의 기본값만 바꾸면 옛 파일의 값이 이겨, 새 기본값이 기존 사용자에게 적용되지 않습니다.

옛 값이 사용자가 고른 것이 아니라고 판단될 때는 키 이름을 새로 바꿉니다. 옛 키는 읽을 때 무시되고, 다음 저장 때 파일에서 사라집니다. 항상 위가 그렇게 바뀌었습니다 — 토글이 실제로 아무 일도 하지 않던 시절의 `false`가 옛 파일에 남아 있었습니다.

반대로 사용자가 실제로 골랐을 수 있는 값이면 키를 바꾸지 않습니다. 그 선택을 지우게 됩니다.

## 항목별로 알아둘 것

- **관리자 모드**는 다른 앱을 쓰는 중에 들어온 입력도 스폰 기운으로 셀지를 정합니다. Windows 관리자 권한으로 실행하는 기능이 아닙니다. 캐릭터 수면 판정에는 걸지 않습니다 — 이유는 [character-state-machine.md](character-state-machine.md)의 수면 절.
- **자동 실행**은 현재 사용자의 시작 프로그램 레지스트리에 실행 파일 경로를 적습니다. 시작할 때마다 설정값대로 맞춥니다. 켜져 있으면 지금 실행 파일 경로로 덮어써서, 게임 폴더를 옮겨도 다음 부팅에 새 경로로 뜹니다. 빌드 폴더를 여러 개 번갈아 켜면 마지막에 켠 쪽이 등록되는 것은 감수합니다. 에디터에서는 아무것도 하지 않습니다.
