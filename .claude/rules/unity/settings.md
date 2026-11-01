---
paths:
  - "Project_Cozy/Assets/Scripts/Gameplay/Settings*.cs"
  - "Project_Cozy/Assets/Scripts/Gameplay/DesktopIntegrationApplier.cs"
  - "Project_Cozy/Assets/Scripts/Gameplay/GraphicsScaleSteps.cs"
  - "Project_Cozy/Assets/Scripts/Gameplay/WorldZoomApplier.cs"
  - "Project_Cozy/Assets/Scripts/UI/UIScaleApplier.cs"
  - "Project_Cozy/Assets/Scripts/UI/Settings*.cs"
  - "Project_Cozy/Assets/Scripts/UI/GraphicsTabController.cs"
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

## 저장 방식은 세 가지입니다

- **대부분은 바뀌는 즉시 저장합니다.** 설정은 가끔 바뀌는 값이라 저장 빈도를 묶을 이유가 없습니다.
- **볼륨은 슬라이더에서 손을 뗄 때 저장합니다.** 끄는 동안 매 프레임 값이 오기 때문입니다. 자세한 것은 [audio.md](audio.md).
- **그래픽 탭은 바꾸면 바로 화면에 보이고, [저장]을 눌러야 파일에 남습니다.** Figma가 "변경 즉시 실시간 동기화되어 보이다가 '저장' 버튼 누르면 최종 확정"으로 정했습니다. 저장하지 않고 패널을 닫거나 다른 탭으로 가면 미저장 팝업이 뜨고, 확인하면 마지막으로 저장한 값으로 돌아갑니다. [초기화]도 기본값을 미리보기로 보여 줄 뿐, 확정은 [저장]이 합니다. [초기화]가 되돌리는 것은 크기 단계(전체·캐릭터·UI 크기를 1배로)뿐이고, 화면 크기 [조정]으로 정한 뷰포트는 건드리지 않습니다. Figma 팝업 문구는 "모든 그래픽 설정을 기본값으로"라 뷰포트가 들어가는지 분명하지 않아, 기획에 확인할 때까지 이렇게 둡니다. 그래픽 탭의 값을 적용하는 쪽은 저장값이 아니라 지금 값을 읽습니다 — 미리보기도 화면에 보여야 해서입니다.

화면 크기 [조정]도 같은 흐름입니다. 편집 화면에서 [완료]로 나와도 확정되지 않고, [저장]을 눌러야 확정됩니다. 조정한 사각형은 `SettingsManager`가 아니라 뷰포트의 주인인 `ViewportScreenSettings`가 미저장 상태로 들고, 그래픽 탭의 [저장]과 미저장 확인이 크기 단계와 함께 다룹니다. 미저장 동안 월드는 저장한 뷰포트 그대로입니다.

전체 크기(줌)는 다른 크기 단계처럼 `SettingsManager`가 단계 번호로 들고, `WorldZoomApplier`가 지금 값을 뷰포트 정책에 넣습니다. [저장]은 크기 단계를 먼저 확정한 뒤 뷰포트를 확정합니다 — 뷰포트 확정 신호로 화면 밖 장식을 회수할 때 줌이 이미 확정된 값이어야 해서입니다. 줌이 무엇을 하는지는 [viewport-coordinates.md](viewport-coordinates.md).

`SettingsManager`는 그래픽의 저장한 값과 지금 값을 따로 듭니다. 한 칸에 섞으면 다른 설정(언어 등)이 즉시 저장될 때 아직 확정하지 않은 미리보기까지 파일에 들어갑니다.

**그래픽 값은 배율이 아니라 단계 번호로 저장합니다.** 실수를 저장하고 비교하면 오차 때문에, 바꾼 것이 없는데도 "저장 안 됨"으로 판정될 수 있습니다. 단계와 배율의 대응은 `GraphicsScaleSteps` 한 곳에만 둡니다. 지금 범위와 단계는 임시이고, 기획·아트와 빌드를 보며 다시 맞춥니다.

## 기본값을 바꿀 때는 저장 키 이름도 바꿉니다

저장은 모든 필드를 파일에 씁니다. 그래서 설정을 한 번이라도 바꾼 사람의 파일에는, 직접 고른 적이 없는 항목까지 그때의 기본값으로 적혀 있습니다. 이 상태에서 같은 키의 기본값만 바꾸면 옛 파일의 값이 이겨, 새 기본값이 기존 사용자에게 적용되지 않습니다.

옛 값이 사용자가 고른 것이 아니라고 판단될 때는 키 이름을 새로 바꿉니다. 옛 키는 읽을 때 무시되고, 다음 저장 때 파일에서 사라집니다. 항상 위가 그렇게 바뀌었습니다 — 토글이 실제로 아무 일도 하지 않던 시절의 `false`가 옛 파일에 남아 있었습니다.

반대로 사용자가 실제로 골랐을 수 있는 값이면 키를 바꾸지 않습니다. 그 선택을 지우게 됩니다.

## 항목별로 알아둘 것

- **관리자 모드**는 다른 앱을 쓰는 중에 들어온 입력도 스폰 기운으로 셀지를 정합니다. Windows 관리자 권한으로 실행하는 기능이 아닙니다. 캐릭터 수면 판정에는 걸지 않습니다 — 이유는 [character-state-machine.md](character-state-machine.md)의 수면 절.
- **자동 실행**은 현재 사용자의 시작 프로그램 레지스트리에 실행 파일 경로를 적습니다. 시작할 때마다 설정값대로 맞춥니다. 켜져 있으면 지금 실행 파일 경로로 덮어써서, 게임 폴더를 옮겨도 다음 부팅에 새 경로로 뜹니다. 빌드 폴더를 여러 개 번갈아 켜면 마지막에 켠 쪽이 등록되는 것은 감수합니다. 에디터에서는 아무것도 하지 않습니다.
