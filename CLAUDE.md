# CLAUDE.md

이 문서는 Project Cozy 저장소에서 작업하는 모든 사람과 AI(Claude Code)를 위한 가이드입니다.

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

토큰 절약과 정확한 컨텍스트 전달을 위해, 주요 서브 디렉토리에는 별도의 `CLAUDE.md`를 둘 수 있습니다. **루트 CLAUDE.md만 모든 정보를 담으려 하면 비대해지고, 정작 작업 중인 디렉토리의 컨벤션이 묻힙니다.**

### 서브 CLAUDE.md 작성 원칙

- **해당 디렉토리의 책임과 역할을 명시할 것.** 단순 파일 목록이 아니라:
  - 이 디렉토리가 **무엇을 담당하는가**
  - **각 파일/하위 폴더의 역할은 무엇인가**
  - **새 파일을 추가할 때의 컨벤션은 무엇인가**
- **루트 CLAUDE.md와 중복하지 말 것.** 서브 CLAUDE.md는 해당 디렉토리에 한정된 정보만 담습니다.
- **디렉토리 구조가 크게 바뀌면 함께 업데이트할 것.** 오래된 CLAUDE.md는 잘못된 정보보다 위험합니다.
- **간결하게.** 한 디렉토리당 한 화면을 넘기지 않는 것이 이상적.

### 현재 존재하는 서브 CLAUDE.md

- [Project_Cozy/Assets/Scripts/Platform/CLAUDE.md](Project_Cozy/Assets/Scripts/Platform/CLAUDE.md) — OS 의존(Win32) 코드 격리 레이어의 책임 / 컨벤션.
- [Project_Cozy/Assets/Characters/CLAUDE.md](Project_Cozy/Assets/Characters/CLAUDE.md) — 캐릭터 에셋 폴더 구조, 그림자 공통화, **프리팹 콜로케이션 컨벤션**(프리팹은 사용하는 자산과 같은 폴더에 둔다).

### 생성 후보 (코드/에셋이 채워지면 추가)

- `Project_Cozy/Assets/Scripts/CLAUDE.md` — 스크립트 아키텍처와 주요 시스템(별 클릭 / 친밀도 / 변신 / 다중 모니터 / 클릭 투과)의 위치.
- `Project_Cozy/Assets/Scenes/CLAUDE.md` — 씬 구성과 진입점.

---

## 5. CLAUDE.md 운영 규칙

이 프로젝트의 모든 CLAUDE.md 파일이 따라야 할 공통 규칙입니다.

### 위치
- 프로젝트 루트 1개
- 필요 시 `Project_Cozy/Assets/` 내 서브 디렉토리에 추가
- 자명한 폴더(파일 1-2개)에는 생략 가능
- `Project_Cozy/` 내부 규칙: _(적어주세요)_

### 내용 원칙
- 그 폴더의 안내 (무엇이 있고, 무엇부터 읽어야 하는지)
- 그 폴더의 작업 규칙
- 사람과 AI 둘 다 읽는 문서로 작성

### 단일 진실 원천
- 동일 정보를 여러 CLAUDE.md에 중복 적지 않음
- 하위 폴더의 동적 정보(파일 목록 등)는 그 폴더 CLAUDE.md에만
- ROOT CLAUDE.md는 구조와 진입점만

### 업데이트
- 폴더 구조 변경 시 함께 업데이트
- outdated된 CLAUDE.md는 잘못된 정보보다 위험 — 정기 점검

---

## 6. 공통 컨벤션

### 6.1 파일/폴더 명명

- 영문 사용 (한글 파일명 지양 — git 호환성)
- 각 단어 첫 글자 대문자, 단어 사이는 붙여서 (예: `Planning/`, `GameConcept.md`, `CharacterDesign.md`)
- 본문은 한국어 OK, 파일명만 영문

### 6.2 Git 워크플로우

_합의 필요_

---

## 7. AI 작업 시 공통 규칙

_합의 필요_

---

## 8. Behavioral Guidelines

일반적인 LLM 코딩 실수를 줄이기 위한 행동 지침. 위의 프로젝트 규칙과 함께 적용합니다.

Behavioral guidelines to reduce common LLM coding mistakes. Merge with project-specific instructions as needed.

Tradeoff: These guidelines bias toward caution over speed. For trivial tasks, use judgment.

### 8.1 Think Before Coding

Don't assume. Don't hide confusion. Surface tradeoffs.

Before implementing:

- State your assumptions explicitly. If uncertain, ask.
- If multiple interpretations exist, present them - don't pick silently.
- If a simpler approach exists, say so. Push back when warranted.
- If something is unclear, stop. Name what's confusing. Ask.

### 8.2 Simplicity First

Minimum code that solves the problem. Nothing speculative.

- No features beyond what was asked.
- No abstractions for single-use code.
- No "flexibility" or "configurability" that wasn't requested.
- No error handling for impossible scenarios.
- If you write 200 lines and it could be 50, rewrite it.

Ask yourself: "Would a senior engineer say this is overcomplicated?" If yes, simplify.

### 8.3 Surgical Changes

Touch only what you must. Clean up only your own mess.

When editing existing code:

- Don't "improve" adjacent code, comments, or formatting.
- Don't refactor things that aren't broken.
- Match existing style, even if you'd do it differently.
- If you notice unrelated dead code, mention it - don't delete it.

When your changes create orphans:

- Remove imports/variables/functions that YOUR changes made unused.
- Don't remove pre-existing dead code unless asked.

The test: Every changed line should trace directly to the user's request.

### 8.4 Goal-Driven Execution

Define success criteria. Loop until verified.

Transform tasks into verifiable goals:

- "Add validation" → "Write tests for invalid inputs, then make them pass"
- "Fix the bug" → "Write a test that reproduces it, then make it pass"
- "Refactor X" → "Ensure tests pass before and after"

For multi-step tasks, state a brief plan:

```
1. [Step] → verify: [check]
2. [Step] → verify: [check]
3. [Step] → verify: [check]
```

Strong success criteria let you loop independently. Weak criteria ("make it work") require constant clarification.

These guidelines are working if: fewer unnecessary changes in diffs, fewer rewrites due to overcomplication, and clarifying questions come before implementation rather than after mistakes..
