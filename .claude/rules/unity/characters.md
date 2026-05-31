---
paths:
  - "Project_Cozy/Assets/Characters/**"
---

# Characters/ 작업 규칙

캐릭터(동물 / 변신 후 소녀) 에셋과 부속 프리팹·애니메이션.
디렉토리 오리엔테이션은 `Project_Cozy/Assets/Characters/CLAUDE.md` 참고.

## 폴더 구조 컨벤션

```
Characters/
├── animals/
│   ├── <animal>/
│   │   ├── <animal>.prefab          # 동물 형태 프리팹
│   │   ├── <animal>_furry.prefab    # 퍼리(소녀) 변신 프리팹
│   │   ├── sprites/
│   │   └── animations/
│   └── ...
└── _common/
    └── shadow.png                    # 공통 둥근 그림자
```

## 절대 지킬 것

- **캐릭터 코드(컨트롤러 / AI / 친밀도)는 Characters/에 두지 않는다.** → `Project_Cozy/Assets/Scripts/` 아래에.
- **프리팹은 사용 자산과 같은 폴더에.** 별도 중앙 폴더(`Assets/Prefabs/`) 만들지 않는다.
- 여러 캐릭터가 공유하는 자산은 `_common/`.

## 명명 규칙 (임시)

> 아트 파이프라인 확정 전까지의 임시 디폴트. 확정되면 재검토.

- 동물 폴더: 소문자 단수형 (`cat`, `dog`, `mouse`)
- 프리팹: `<animal>.prefab` / 변신체는 `<animal>_furry.prefab`
- 스프라이트 시트: `<animal>_<action>.png` (예: `cat_walk.png`)

## 새 캐릭터 추가 체크리스트

1. `Characters/animals/<animal>/` 생성
2. 스프라이트 → `sprites/`, 애니메이션 → `animations/`
3. 프리팹은 폴더 루트에 (`<animal>.prefab`)
4. 컨트롤러 코드는 `Assets/Scripts/` 아래에 (`Scripts/Character/`)
5. 프리팹에 컨트롤러 컴포넌트를 붙이고 참조 연결

## 그림자

모든 캐릭터 스프라이트 하단엔 공통 둥근 그림자가 들어간다. 구현 방식(스프라이트에 굽기 vs `_common/shadow.png`를 프리팹에서 합치기)은 아트 파이프라인 확정 후 결정.
