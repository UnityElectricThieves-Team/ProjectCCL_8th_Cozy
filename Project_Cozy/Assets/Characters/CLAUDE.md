# Characters/

캐릭터(동물 / 변신 후 소녀) 에셋과 그에 부속된 프리팹·애니메이션을 모은다.

## 책임

- 한 캐릭터에 필요한 자산(스프라이트, 애니메이션, 프리팹)을 **한 폴더 안에** 모은다.
- 캐릭터 코드(컨트롤러 / AI / 친밀도)는 여기에 두지 않는다 → `Assets/Scripts/` 아래에. (스크립트 폴더 구조는 별도 확정 예정.)
- 스프라이트 사이즈 / 픽셀화 파이프라인 자체의 정책은 [GameDesign.md](../../../Docs/GameDesign.md) §2 참조. 이 문서엔 적지 않는다(중복 방지).

## 폴더 구조

```
Characters/
├── animals/                    # 정식 동물 (한 동물 = 폴더 하나)
│   ├── cat/
│   │   ├── cat.prefab          # 동물 형태 프리팹
│   │   ├── cat_furry.prefab    # 퍼리(소녀) 변신 프리팹
│   │   ├── sprites/
│   │   └── animations/
│   └── dog/
├── _common/
│   └── shadow.png              # 공통 둥근 그림자
└── _test/                      # 테스트/프로토타입용 임시 캐릭터 픽스처 — 자유롭게 삭제 가능
    └── rabbit/                 #   (아트 파이프라인 표준 전 단계 에셋. 정식 동물은 animals/ 로.)
        ├── rabbit.prefab
        └── sprites/
```

## 컨벤션

### 프리팹 콜로케이션 — 프로젝트 컨벤션

**프리팹은 사용하는 에셋과 같은 폴더에 둔다.** 별도의 `Assets/Prefabs/` 같은 중앙 폴더는 만들지 않는다.

- 예: 고양이 프리팹 → `Characters/animals/cat/cat.prefab`
- 이유:
  - 캐릭터 단위로 추가/삭제가 폴더 하나만 건드리면 되어, [README.md:67](../../../README.md#L67)의 운영 정책(*"인기 없는 캐릭터는 과감히 삭제"*)과 잘 맞는다.
  - 에셋 ↔ 프리팹 참조 깨짐을 추적하기 쉽다.
- 예외: 여러 캐릭터가 공유하는 자산(공통 그림자 등)은 `_common/`.
- 예외: 테스트/프로토타입용 임시 캐릭터 픽스처는 `_test/<name>/` (예: `_test/rabbit/`). 아직 아트 파이프라인 표준(픽셀화 등)을 거치지 않은 에셋도 여기 둔다. 정식 동물로 승격할 때 `animals/`로 옮기고 표준을 적용한다. `_test/`는 언제든 통째로 삭제 가능.

이 규칙은 캐릭터 외 영역(UI, 이펙트 등)에도 동일하게 적용한다. 새 자산 카테고리가 추가되면 그 폴더의 CLAUDE.md에 같은 원칙을 적는다.

### 그림자 공통화

모든 캐릭터 스프라이트 하단엔 공통의 둥근 그림자가 포함된다 ([README.md:55](../../../README.md#L55)).
구현 방식(스프라이트에 굽기 vs `_common/shadow.png`를 프리팹에서 합치기)은 아트 파이프라인 확정 시점에 결정.

### 명명 규칙 (임시 — 아트 파이프라인 확정 시 재검토)

> 아래는 임시 디폴트입니다. [GameDesign.md §2](../../../Docs/GameDesign.md)의 아트 파이프라인이 확정되면 그 시점에 재검토합니다.

- 동물 폴더: 소문자 단수형 (`cat`, `dog`, `mouse`)
- 프리팹: `<animal>.prefab` / 변신체는 `<animal>_furry.prefab`
- 스프라이트 시트: `<animal>_<action>.png` (예: `cat_walk.png`) — `<action>` 후보 목록은 [Animation_List.md](../../../Docs/Animation_List.md) 참조.

## 새 캐릭터 추가 체크리스트

1. `Characters/animals/<animal>/` 생성
2. 스프라이트 → `sprites/`, 애니메이션 → `animations/`
3. 프리팹은 폴더 루트에 (`<animal>.prefab`)
4. 컨트롤러 코드는 `Assets/Scripts/` 아래 캐릭터 코드 위치에 (구조 확정 후 갱신)
5. 프리팹에 컨트롤러 컴포넌트를 붙이고 참조 연결