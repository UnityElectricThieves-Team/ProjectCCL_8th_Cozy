# CLAUDE.md

이 문서는 **Project Cozy** 저장소에서 작업하는 모든 개발자(사람과 AI 모두)를 위한 가이드입니다. AI 어시스턴트는 이 문서를 우선 컨텍스트로 사용하고, 사람은 새로 합류했을 때 이 문서로 프로젝트를 빠르게 이해할 수 있어야 합니다.

---

## 1. 프로젝트 개요

**Project Cozy** — 바탕화면에서 동작하는 방치형 데스크톱 펫 / 클리커 게임 (가제).

- **컨셉**: 겉은 코지(Cozy)한 방치형 데스크톱 펫, 속은 서브컬처 수집형 게임. 별을 클릭해 동물을 해금하고, 친밀도가 누적되면 일정 시간 동안 퍼리(소녀)로 변신.
- **최우선 가치**: **애매한 기능보다 최적화가 우선.** 데스크톱에 항상 떠 있는 게임이므로 리소스 점유율이 곧 사용자 경험.
- **BM**: 본편 무료 + Steam DLC (의상).
- 자세한 기획은 [README.md](README.md)와 [Docs/GameDesign.md](Docs/GameDesign.md) 참조.

### 기술 스택

| 항목 | 값 |
|---|---|
| Engine | Unity **6000.3.10f1** (Unity 6) |
| Render Pipeline | URP 2D (17.3) |
| Input | New Input System (1.18) |
| 주요 2D 패키지 | 2D Animation, Aseprite Importer, PSD Importer, SpriteShape, Tilemap Extras |
| 빌드 타겟 | Windows x86_64 (추후 확장 가능) |

> 정확한 패키지/Editor 버전은 [Project_Cozy/Packages/manifest.json](Project_Cozy/Packages/manifest.json), [Project_Cozy/ProjectSettings/ProjectVersion.txt](Project_Cozy/ProjectSettings/ProjectVersion.txt) 참조.

### 관련 문서

- [README.md](README.md) — 게임 기획서
- [Docs/](Docs/) — 게임 디자인, 일정, 온보딩 등 보조 문서 모음. 파일 목록은 디렉토리에서 직접 확인.

---

## 2. 프로젝트 구조

```
ProjectCCL_8th_Cozy/
├── CLAUDE.md                    # 이 문서 (AI/개발자 진입점)
├── README.md                    # 프로젝트 기획서
├── Docs/                        # 게임 디자인 / 일정 / 온보딩 등 보조 문서
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

## 3. 서브 디렉토리 CLAUDE.md

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

## 4. 코드 작성 규칙

> 아래 규칙은 Unity / C# 표준에서 출발한 합리적 디폴트입니다. 팀 합의로 변경 가능하며, 변경 시 이 문서를 업데이트합니다.

### 4.1 네이밍 컨벤션 (C#)

| 대상 | 규칙 | 예 |
|---|---|---|
| 클래스 / public 멤버 / 메소드 | `PascalCase` | `AnimalController`, `OnClicked()` |
| private 필드 | `_camelCase` | `_currentAffinity` |
| 로컬 변수 / 파라미터 | `camelCase` | `clickCount` |
| 상수 / `static readonly` | `UPPER_SNAKE_CASE` | `MAX_AFFINITY` |
| 인터페이스 | `IPascalCase` | `IClickable`, `IPettable` |
| 이벤트 | `OnSomethingHappened` | `OnAnimalUnlocked` |
| 파일명 | 클래스명 + `.cs` | `AnimalController.cs` |

### 4.2 Unity / 게임 코드 원칙

- **MonoBehaviour는 단일 책임 원칙을 따른다.** "AnimalController" 하나가 이동·클릭·애니메이션·AI를 모두 담지 말 것. 기능별 컴포넌트로 분리.
- **인스펙터 노출은 `[SerializeField] private`로**. `public` 필드 노출은 금지(외부 수정 진입점이 됩니다).
- **`Update()` 남용 금지.** 필요 없으면 메소드 자체를 삭제. 대안: 이벤트, 코루틴, `InvokeRepeating`, `Job System`.
- **`GameObject.Find` / `FindObjectOfType` 런타임 호출 금지.** 부팅 시 1회 캐싱하거나 인스펙터 참조 / DI.
- **객체 풀링.** 동물·이펙트·별 등 빈번히 생성되는 오브젝트는 풀링 적용.
- **2D 한정.** `SpriteRenderer`, `Rigidbody2D`, `Collider2D`, `Physics2D` 사용. 3D 컴포넌트 금지.
- **할당(Allocation) 주의.** 매 프레임 호출되는 코드에서 `new`, LINQ, 박싱, `string` 연결 금지. 핫 패스에선 캐싱 / `StringBuilder` / `List<T>` 재사용.
- **`.meta` 파일은 항상 함께 커밋.** 빠지면 다른 팀원의 프로젝트가 깨집니다.

### 4.3 데스크톱 펫 특수 사항

- **Win32 / WinAPI 호출은 별도 어댑터 클래스에 격리.** 코어 게임 로직과 OS 의존을 섞지 말 것.
- **클릭 투과(click-through), Always-on-top, 다중 모니터 코드는 한 곳에 모을 것.** 여러 곳에 흩어지면 디버깅 지옥.
- **유저 환경 설정**(볼륨, 투명도, Boss Key 등)은 한 곳에서 일괄 직렬화. 구체 방식(`ScriptableObject` / `JsonUtility` / `PlayerPrefs` 등)은 구현 시점에 결정.

### 4.4 성능 가이드라인 (최우선 원칙)

> README의 *"애매한 기능보다 최적화가 우선"* 원칙을 따릅니다.

- **절전 모드(프레임 제한) 옵션을 항상 염두에 두고 코드를 작성.** 60fps와 30fps에서 모두 동작해야 함.
- **화면 밖 캐릭터는 업데이트 스킵 (off-screen culling).**
- **스프라이트 아틀라스 적극 활용.** 드로우콜 절감.
- **픽셀 청크 규격을 일괄 고정.** 16/32/64px 중 선택 (README §4 참조).

### 4.5 커밋 / 브랜치

- **커밋 메시지**: 간결하고, 무엇을 했는지 한 줄에 드러나게. 한글/영문 자유.
- **`main` 직접 푸시 지양.** 기능 단위 브랜치 → PR 권장.
- **하나의 커밋 = 하나의 논리적 변경.** 무관한 변경을 섞지 말 것.

---

## 5. Behavioral Guidelines (Andrej Karpathy)

> 출처: <https://github.com/forrestchang/andrej-karpathy-skills/blob/main/CLAUDE.md>
>
> 일반적인 LLM 코딩 실수를 줄이기 위한 행동 지침. 위의 프로젝트 규칙과 함께 적용합니다.

Behavioral guidelines to reduce common LLM coding mistakes. Merge with project-specific instructions as needed.

**Tradeoff:** These guidelines bias toward caution over speed. For trivial tasks, use judgment.

### 5.1 Think Before Coding

**Don't assume. Don't hide confusion. Surface tradeoffs.**

Before implementing:
- State your assumptions explicitly. If uncertain, ask.
- If multiple interpretations exist, present them - don't pick silently.
- If a simpler approach exists, say so. Push back when warranted.
- If something is unclear, stop. Name what's confusing. Ask.

### 5.2 Simplicity First

**Minimum code that solves the problem. Nothing speculative.**

- No features beyond what was asked.
- No abstractions for single-use code.
- No "flexibility" or "configurability" that wasn't requested.
- No error handling for impossible scenarios.
- If you write 200 lines and it could be 50, rewrite it.

Ask yourself: "Would a senior engineer say this is overcomplicated?" If yes, simplify.

### 5.3 Surgical Changes

**Touch only what you must. Clean up only your own mess.**

When editing existing code:
- Don't "improve" adjacent code, comments, or formatting.
- Don't refactor things that aren't broken.
- Match existing style, even if you'd do it differently.
- If you notice unrelated dead code, mention it - don't delete it.

When your changes create orphans:
- Remove imports/variables/functions that YOUR changes made unused.
- Don't remove pre-existing dead code unless asked.

The test: Every changed line should trace directly to the user's request.

### 5.4 Goal-Driven Execution

**Define success criteria. Loop until verified.**

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

---

**These guidelines are working if:** fewer unnecessary changes in diffs, fewer rewrites due to overcomplication, and clarifying questions come before implementation rather than after mistakes.
