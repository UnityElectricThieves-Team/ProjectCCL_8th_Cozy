---
paths:
  - "Project_Cozy/Assets/**/*.prefab"
---

# Prefab 작업 규칙

## 어디에 두는가

**씬에 배치되는 프리팹은 `Assets/Prefabs/` 아래에 둔다.** 캐릭터·별·달 같은 게임 오브젝트는 그 폴더 직하에, UI는 `Assets/Prefabs/UIPanels/` 아래에 둔다.

```
Assets/Prefabs/
├── Character.prefab            # 게임 오브젝트는 여기 직하
├── Star.prefab
└── UIPanels/
    ├── UIPanel_Base.prefab     # 패널들이 상속하는 베이스
    ├── UIPanel_Shop/           # 패널 하나 = 폴더 하나. 그 패널만 쓰는 부품을 같이 둔다
    │   ├── ShopItemRow.prefab
    │   └── ShopItemSlot.prefab
    └── UIObjects/              # 패널 경계를 넘어 재사용될 수 있는 범용 위젯
        ├── Dropdown.prefab
        └── PillToggle.prefab
```

**에셋과 프리팹을 같은 폴더에 모으는 콜로케이션은 `Assets/Characters/` 안에서만 적용한다.** 그 폴더는 캐릭터별 스프라이트·애니메이션이 모이는 곳이라, 캐릭터를 통째로 추가·삭제할 때 폴더 하나만 건드리면 되는 이점이 있다. 하지만 씬이 실제로 참조하는 프리팹은 `Assets/Prefabs/`에 모여 있어야 어디를 봐야 할지 헷갈리지 않는다.

> 이 규칙은 프리팹을 **새로 만들 때** 필요한데, 이 파일은 기존 `.prefab`을 열 때만 로드된다. 그래서 배치 규칙 요약이 루트 [CLAUDE.md](../../../CLAUDE.md) §2에도 한 줄 있다.

## 씬에 override를 남기지 않는다

프리팹 인스턴스의 값을 씬에서 고치면 그 override가 `.unity`에 눌러앉는다. `.unity`는 자동 머지가 안 되므로, override가 쌓이면 프리팹으로 뺀 효과가 사라진다.

- 값을 고칠 일이 있으면 **프리팹을 열어서** 고친다.
- 그 인스턴스만 달라야 하는 값(위치 등)은 어쩔 수 없지만, 앵커·크기·색처럼 프리팹 전체에 적용되어야 하는 값은 프리팹에서 고친다.

## 직접 편집하지 않는다

`.prefab`도 `.unity`처럼 YAML이지만 Unity 내부 ID와 엮여 있어 손으로 고치면 깨진다.

- Claude는 프리팹을 직접 텍스트 편집하지 않는다 — Unity Editor에서 사용자가 수정한다.
- 어떤 컴포넌트가 붙어 있는지 확인하는 grep은 괜찮다.
- 편집이 꼭 필요해 허락을 받았다면, **Unity에서 그 프리팹을 닫은 뒤** 편집한다. 열려 있으면 Unity가 메모리 상태로 덮어써서 양쪽 작업이 날아간다.

## .meta 짝꿍

`.prefab`을 커밋할 때 `.prefab.meta`도 반드시 함께 커밋한다. GUID가 빠지면 다른 환경에서 참조가 끊긴다.
