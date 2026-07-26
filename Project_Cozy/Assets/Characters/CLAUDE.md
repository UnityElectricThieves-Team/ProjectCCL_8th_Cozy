# Characters/

캐릭터(동물 / 변신 후 소녀) 에셋과 그에 부속된 프리팹·애니메이션을 모은다.

## 책임

- 한 캐릭터에 필요한 자산(스프라이트, 애니메이션, 프리팹)을 **한 폴더 안에** 모은다.
- 캐릭터 코드(컨트롤러 / AI / 친밀도)는 여기에 두지 않는다 → `Assets/Scripts/Character/`에.
- 스프라이트 사이즈 / 픽셀화 파이프라인 정책은 이 문서에 적지 않는다. 기획 쪽 문서를 따른다.

## 폴더 구조 (현재 상태)

```
Characters/
└── _test/                      # 아트 파이프라인 표준을 거치지 않은 임시 픽스처 — 통째로 삭제 가능
    └── rabbit/
        ├── rabbit.prefab
        └── sprites/
```

지금은 `_test/rabbit/` 하나뿐이다. 정식 캐릭터의 폴더 구조와 승격 규칙은
[.claude/rules/unity/prefabs.md](../../../.claude/rules/unity/prefabs.md)에 있다.

## 컨벤션

### 에셋과 프리팹을 같은 폴더에

이 폴더 안에서는 캐릭터가 쓰는 스프라이트·애니메이션과 그 캐릭터의 프리팹을 한 폴더에 모은다.
캐릭터 단위로 추가·삭제가 폴더 하나만 건드리면 되고, 에셋 ↔ 프리팹 참조가 깨졌을 때 추적하기 쉽다.

**단, 이건 `Assets/Characters/` 안에서만 통하는 규칙이다.** 씬에 실제로 배치되는 프리팹은
`Assets/Prefabs/`에 모여 있다(본편 캐릭터 프리팹도 거기 있다). 프로젝트 전체의 프리팹 배치 규칙은
[.claude/rules/unity/prefabs.md](../../../.claude/rules/unity/prefabs.md)를 따른다.

### 그림자는 스프라이트에 굽지 않는다

캐릭터 스프라이트에 그림자를 그려 넣지 않는다. 실행 중에 런타임 컴포넌트가 캐릭터 아래에 그림자를 그린다.
아트는 그림자 없는 상태로만 만들면 된다.

### 명명 규칙 (임시 — 아트 파이프라인 확정 시 재검토)

- 캐릭터 폴더: 소문자 단수형 (`cat`, `dog`, `mouse`)
- 프리팹: `<animal>.prefab` / 변신체는 `<animal>_furry.prefab`
- 스프라이트 시트: `<animal>_<action>.png` (예: `cat_walk.png`) — `<action>` 후보 목록은 [AnimationList.md](../../../Docs/Planning/AnimationList.md) 참조.

## 새 캐릭터 추가 체크리스트

1. 캐릭터 폴더 생성 → 스프라이트는 `sprites/`, 애니메이션은 `animations/`
2. 컨트롤러 코드는 `Assets/Scripts/Character/`에 (이 폴더에 스크립트를 두지 않는다)
3. 프리팹에 컨트롤러 컴포넌트를 붙이고 참조 연결