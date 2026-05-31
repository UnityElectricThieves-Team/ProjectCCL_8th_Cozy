# CLAUDE.md

이 문서는 Project Cozy 저장소에서 작업하는 모든 사람과 AI(Claude Code)를 위한 진입점입니다.
세부 규칙은 `.claude/rules/`에서 관리합니다.

---

## 1. 프로젝트 개요

**Project Cozy** (가제) — 바탕화면에서 동작하는 방치형 데스크톱 컴패니언 / 클리커 게임.

- **컨셉**: 바탕화면에 항상 살아 숨 쉬는 캐릭터들과 함께하는 서브컬처 수집형 콘텐츠. 스폰되는 곳(미정)에 유저의 키보드/마우스 입력이 누적되면 동물을 해금하고, 친밀도를 쌓으면 일정 조건에서 소녀로 변신.
- **타겟**: 서브컬처를 향유하는 2-30대
- **시장 포지션**: 데스크톱 컴패니언 형태의 서브컬처 게임. 게이머가 자택 PC에 켜두는 콘텐츠
- **레퍼런스 톤**: 블루아카이브 등 일본 서브컬처 게임의 비주얼/문법
- **최우선 가치**: 애매한 기능보다 최적화가 우선. 데스크톱에 항상 떠 있는 게임이므로 리소스 점유율이 곧 사용자 경험.
- **BM**: 본편 무료 + Steam DLC (의상 등)

자세한 기획은 `Docs/Planning/` 참고. 마일스톤은 `README.md` 참고.

### 기술 스택

| 항목 | 값 |
|---|---|
| Engine | Unity **6000.3.10f1** (Unity 6) |
| Render Pipeline | URP 2D (17.3) |
| Input | New Input System (1.18) |
| 주요 2D 패키지 | 2D Animation, Aseprite Importer, PSD Importer, SpriteShape, Tilemap Extras |
| 빌드 타겟 | Windows x86_64 (추후 확장 가능) |

> 정확한 패키지/Editor 버전은 [Project_Cozy/Packages/manifest.json](Project_Cozy/Packages/manifest.json), [Project_Cozy/ProjectSettings/ProjectVersion.txt](Project_Cozy/ProjectSettings/ProjectVersion.txt) 참조.

---

## 2. 프로젝트 구조

```
ProjectCCL_8th_Cozy/
├── CLAUDE.md                    # 이 문서 (사람/AI 진입점)
├── README.md                    # 외부 대상 프로젝트 소개 + 마일스톤
├── Docs/                        # 영역별 문서
│   ├── Planning/                # 기획
│   ├── Development/             # 개발
│   └── Art/                     # 아트
└── Project_Cozy/                # Unity 프로젝트 루트
    ├── Assets/
    │   ├── Characters/          # 캐릭터(동물/소녀) 스프라이트, 애니메이션, 프리팹
    │   │   └── animals/
    │   ├── Scenes/              # Unity 씬 (.unity)
    │   ├── Scripts/             # 게임 로직 C# 코드
    │   └── Settings/            # URP / 렌더 파이프라인 설정
    ├── Packages/                # Unity 패키지 매니페스트
    └── ProjectSettings/         # Unity 프로젝트 설정 (편집 시 주의)
```

### 절대 건드리지 말 것 (.gitignore 대상)

`Project_Cozy/Library/`, `Project_Cozy/Temp/`, `Project_Cozy/Logs/`, `Project_Cozy/obj/`, `Project_Cozy/Build(s)/`, `UserSettings/` 등은 Unity가 자동 생성/관리하는 캐시입니다. AI는 이 디렉토리들을 읽거나 수정하지 마십시오.

---

## 3. 작업 영역별 진입점

작업 시작 시 본인 영역의 폴더를 먼저 확인하고, **필요하면 다른 영역도 참고**하세요. 영역 간 작업은 자주 교차합니다.

- **기획 작업** → `Docs/Planning/`
- **개발 작업** → `Docs/Development/`
- **아트 작업** → `Docs/Art/`

---

## 4. 서브 디렉토리 CLAUDE.md

- [Project_Cozy/Assets/Scripts/CLAUDE.md](Project_Cozy/Assets/Scripts/CLAUDE.md) — 스크립트 레이어 구조(`Platform` / `Interaction` / `PerformanceSetting` / `Animation` / `Character` / `Gameplay` / `UI`)와 의존 방향, 현재 파일 목록.
- [Project_Cozy/Assets/Scripts/Platform/CLAUDE.md](Project_Cozy/Assets/Scripts/Platform/CLAUDE.md) — OS 의존(Win32) 코드 격리 레이어의 책임 / 컨벤션.
- [Project_Cozy/Assets/Scripts/Interaction/CLAUDE.md](Project_Cozy/Assets/Scripts/Interaction/CLAUDE.md) — 마우스 입력 라우팅 + 인터랙터블 인터페이스 계약(`IHoverable` / `IClickable` / `IShiftRightClickable`).
- [Project_Cozy/Assets/Scripts/PerformanceSetting/CLAUDE.md](Project_Cozy/Assets/Scripts/PerformanceSetting/CLAUDE.md) — 프레임 레이트·윈도우 종횡비 등 런타임 정책. *`BorderlessWindow`와 HWND 공유 주의*.
- [Project_Cozy/Assets/Scripts/Character/CLAUDE.md](Project_Cozy/Assets/Scripts/Character/CLAUDE.md) — 캐릭터 단일 개체의 자율 거동·친밀도 상태 머신.
- [Project_Cozy/Assets/Characters/CLAUDE.md](Project_Cozy/Assets/Characters/CLAUDE.md) — 캐릭터 에셋 폴더 구조, 그림자 공통화, **프리팹 콜로케이션 컨벤션**(프리팹은 사용하는 자산과 같은 폴더에 둔다).
