---
paths:
  - "Project_Cozy/Assets/**/*.prefab"
---

# Prefab 작업 규칙

## 어디에 두는가

**씬에 배치되거나 실행 중에 스폰되는 프리팹은 `Assets/Prefabs/` 아래에 둔다.** 캐릭터는 `Assets/Prefabs/Characters/`에(캐릭터는 씬에 놓지 않고 스폰한다 — [character-ownership.md](character-ownership.md)), 구름·달 같은 나머지 게임 오브젝트는 그 폴더 직하에, UI는 `Assets/Prefabs/UIPanels/` 아래에 둔다.

```
Assets/Prefabs/
├── Characters/
│   ├── Character.prefab        # 캐릭터 베이스. 캐릭터별 프리팹은 이것의 배리언트
│   ├── Character_Cheese.prefab
│   └── Character_Hera.prefab
├── Cloud.prefab                # 나머지 게임 오브젝트는 여기 직하
└── UIPanels/
    ├── UIPanel_Base.prefab     # 패널들이 상속하는 베이스
    ├── UIPanel_Shop/           # 패널 하나 = 폴더 하나. 그 패널만 쓰는 부품을 같이 둔다
    │   ├── ShopItemRow.prefab
    │   └── ShopItemSlot.prefab
    └── UIObjects/              # 패널 경계를 넘어 재사용될 수 있는 범용 위젯
        ├── Dropdown.prefab
        └── PillToggle.prefab
```

**에셋과 프리팹을 같은 폴더에 모으지 않는다.** 씬이 실제로 참조하는 프리팹은 `Assets/Prefabs/`에 모여 있어야 어디를 봐야 할지 헷갈리지 않는다. 캐릭터도 시트·클립(아트), 오버라이드(개발), 프리팹(개발)을 세 곳에 나눠 둔다 — 위치 표는 [Characters/CLAUDE.md](../../../Project_Cozy/Assets/Characters/CLAUDE.md)에 있다. `Assets/Characters/`에는 옛 테스트 픽스처만 남아 있다.

> 이 규칙은 프리팹을 **새로 만들 때** 필요한데, 이 파일은 기존 `.prefab`을 열 때만 로드된다. 그래서 배치 규칙 요약이 루트 [AI_INSTRUCTIONS.md](../../../AI_INSTRUCTIONS.md) §2에도 한 줄 있다.

## 씬에 override를 남기지 않는다

프리팹 인스턴스의 값을 씬에서 고치면 그 override가 `.unity`에 눌러앉는다. `.unity`는 자동 머지가 안 되므로, override가 쌓이면 프리팹으로 뺀 효과가 사라진다.

- 값을 고칠 일이 있으면 **프리팹을 열어서** 고친다.
- 그 인스턴스만 달라야 하는 값(위치 등)은 어쩔 수 없지만, 앵커·크기·색처럼 프리팹 전체에 적용되어야 하는 값은 프리팹에서 고친다.
- 단, **프리팹 루트의 RectTransform 값(크기·위치·앵커·피벗)은 프리팹만 고쳐서는 씬 인스턴스에 가지 않을 수 있다.** 씬 인스턴스 루트에 기록된 값이 표시 없이 이기기 때문이다. 바꿀 때 씬 인스턴스 값도 함께 맞춘다 — [scenes.md](scenes.md)의 "인스턴스 루트의 RectTransform 값" 절.

## 누가 고치는가

프리팹에서 Claude가 직접 고치는 것(이미 있는 줄의 단순 설정 값)과 사용자가 Unity에서 고칠 것(구조·참조·줄 추가)의 구분, 편집 전 사전작업은 [asset-editing.md](asset-editing.md)가 정본이다.

## .meta 짝꿍

`.prefab`을 커밋할 때 `.prefab.meta`도 반드시 함께 커밋한다. GUID가 빠지면 다른 환경에서 참조가 끊긴다.
