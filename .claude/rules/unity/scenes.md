---
paths:
  - "**/*.unity"
---

# Unity 씬(.unity) 작업 규칙

## 머지 충돌 주의

`.unity` 파일은 텍스트(YAML)지만 Unity 내부 ID와 순서가 엮여 있어 **두 사람이 동시에 만지면 머지가 거의 항상 깨진다.**

- 씬은 사용자가 Unity에서 고치고, Claude는 바꿀 값만 안내한다. 이유와 예외 없는 범위는 [asset-editing.md](asset-editing.md)가 정본이다.

## override를 씬에 남기지 않는다

프리팹 인스턴스의 값을 씬에서 고치면 그 override가 `.unity`에 눌러앉아, 프리팹으로 뺀 효과를 깎는다. 씬을 얇게 유지하려는 이유는 루트 [CLAUDE.md](../../../CLAUDE.md) §2에 있다.

- 앵커·크기·색처럼 프리팹 전체에 적용되어야 하는 값은 **프리팹을 열어서** 고친다.
- 씬에서 프리팹 인스턴스를 손봤다면, 커밋 전에 그게 정말 이 인스턴스만의 값인지 확인한다.

## UIRoot 아래 순서와 UI 입력 모듈

- **호버 말풍선 층(`HoverTooltip`)은 UIRoot의 맨 뒤 형제로 둡니다.** 형제 순서가 그리기 순서라, 맨 뒤여야 패널과 메뉴 바 앞에 그려집니다. UIRoot 아래에 새 오버레이를 더할 때는 이 층 앞에 넣습니다.
- **EventSystem의 `InputSystemUIInputModule`에서 Send Pointer Hover To Parent를 끄지 않습니다.** 말풍선은 항목 루트에 한 번만 붙이고, 자식(토글·이름 글자)에 올린 호버가 부모로 올라오는 것에 기댑니다. 끄면 설정 행 등에서 말풍선이 뜨지 않습니다.
- 씬 오브젝트(메뉴 바 버튼 등)에 말풍선을 달 때도 [ui-panels.md](ui-panels.md)의 말풍선 절을 따릅니다.

## 캐릭터는 씬에 두지 않는다

GameScene에 캐릭터 프리팹 인스턴스를 놓지 않는다. 어떤 캐릭터가 화면에 나오는지는 저장 기록이 정하고, `CharacterOwnership`이 시작할 때 스폰한다. 씬에 놓으면 저장과 무관하게 늘 나오고, 같은 캐릭터를 기록으로도 스폰해 두 마리가 된다. 이유와 나머지 규약은 [character-ownership.md](character-ownership.md).

## 새 씬 추가

- `Project_Cozy/Assets/Scenes/` 아래에 추가.
- Build Settings에 등록 필요 (`ProjectSettings/EditorBuildSettings.asset` 변경됨 — Git 충돌 주의).

## 커밋

- 씬 변경 커밋은 prefix `scene` 권장.
- 씬 변경 시 동반되는 `.meta` 파일도 함께 커밋. `.meta`만 빠지면 다른 사람 환경에서 GUID 재할당이 일어난다.
