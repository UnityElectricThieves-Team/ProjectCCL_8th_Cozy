# CLAUDE.md

이 문서는 Project Cozy 저장소에서 작업하는 모든 사람과 AI(Claude Code)를 위한 가이드입니다. 세션 시작 시 항상 로드되므로 **어떤 작업이든 알아야 하는 것만** 담습니다.

세부 규칙은 `.claude/rules/`로 분리되어 있습니다. 어느 층에 무엇을 적는지는 §4를 봅니다.

---

## 1. 프로젝트 개요

**Project Cozy** (가제) — 바탕화면에서 동작하는 방치형 데스크톱 컴패니언 / 클리커 게임.

**최우선 가치**: 애매한 기능보다 최적화가 우선입니다. 데스크톱에 항상 떠 있는 게임이므로 리소스 점유율이 곧 사용자 경험입니다. 이 한 줄이 아래 씬 전략과 코드 스타일의 근거입니다.

**기획은 Figma와 Notion에서 작업하고, 그쪽이 정답입니다.** 저장소에서는 기획 문서를 고치지 않습니다. `Docs/Planning/`에 있는 것은 개발자가 1차로 해석해 옮겨둔 사본이라, 원본과 어긋나면 Figma·Notion이 이깁니다. 컨셉·타겟·BM과 마일스톤은 `README.md`를 봅니다.

### 기술 스택

| 항목 | 값 |
|---|---|
| Engine | Unity **6000.3.10f1** (Unity 6) |
| Render Pipeline | URP 2D (17.3) |
| Input | New Input System (1.18) |
| 주요 2D 패키지 | 2D Animation, Aseprite Importer, PSD Importer, SpriteShape, Tilemap Extras |
| 비동기 | UniTask 2.5.11 (로컬 패키지) |
| 빌드 타겟 | Windows x86_64 (추후 확장 가능) |

> 정확한 패키지/Editor 버전은 [Project_Cozy/Packages/manifest.json](Project_Cozy/Packages/manifest.json), [Project_Cozy/ProjectSettings/ProjectVersion.txt](Project_Cozy/ProjectSettings/ProjectVersion.txt) 참조.

---

## 2. 씬 관리 전략

**`GameScene`(메인 씬)의 오브젝트 구조는 최대한 얇게 씁니다.** UI와 시스템은 프리팹으로 빼서 씬에는 인스턴스만 남깁니다. 목적은 여러 작업을 병렬로 진행할 수 있게 하는 것입니다 — `.unity`는 자동 머지가 안 되므로 씬이 두꺼울수록 동시 작업이 손으로 푸는 충돌로 이어집니다.

**프리팹으로 옮기는 것만으로는 부족합니다. 씬 인스턴스에 override를 남기지 않아야 효과가 있습니다.** 지금 씬은 대부분이 UI이고 프리팹화된 패널들조차 override가 씬에 눌러앉아 있어서, 앵커 하나를 손볼 때마다 씬 파일이 더러워집니다. 값을 고칠 일이 있으면 씬 인스턴스가 아니라 프리팹에서 고칩니다.

현재 씬 상태의 실측치와 이관 순서는 `Docs/Development/`에 있습니다. 씬 파일을 직접 다룰 때의 규칙은 [.claude/rules/unity/scenes.md](.claude/rules/unity/scenes.md), 프리팹을 어디에 둘지는 [.claude/rules/unity/prefabs.md](.claude/rules/unity/prefabs.md)를 봅니다.

**새 캐릭터 프리팹은 루트가 곧 발이어야 합니다** — Visual 자식을 올려 스프라이트 아래 끝을 루트 원점에 맞춥니다. 이유와 나머지 지면 규약은 [.claude/rules/unity/character-ground.md](.claude/rules/unity/character-ground.md)에 있는데, 그 파일은 기존 파일을 열 때만 로드되어 *새로 만드는* 순간에는 발화하지 않으므로 여기 한 줄 둡니다.

**마우스로 만지는 새 월드 오브젝트의 판정은 `PolygonCollider2D` + `SpritePhysicsShapeSync`로 그림에서 가져옵니다** — 콜라이더 크기를 숫자로 적어 넣지 않습니다. 이유와 픽셀 검사와의 역할 분담은 [.claude/rules/unity/interaction-hit.md](.claude/rules/unity/interaction-hit.md)에 있고, 새 프리팹을 만드는 순간에는 발화하지 않아 여기 한 줄 둡니다.

**새 장식 프리팹에는 콜라이더를 달지 않습니다** — 장식은 배경처럼 평소 클릭을 바탕화면으로 통과시켜야 하는 오브젝트라 위 판정 규약의 대상이 아닙니다. 콜라이더가 있으면 그 자리의 바탕화면이 눌리지 않습니다. 이유와 나머지 장식 규약은 [.claude/rules/unity/decoration-placement.md](.claude/rules/unity/decoration-placement.md)에 있고, 새 장식 프리팹을 만드는 순간에는 발화하지 않아 여기 한 줄 둡니다.

**새 배경 스프라이트는 Mesh Type을 Full Rect, pivot을 Center로 임포트합니다** — 배경은 Tiled 드로우 모드로 가로 반복하므로 Tight 메시로는 타일이 깨집니다. 나머지 배경 규약은 [.claude/rules/unity/background.md](.claude/rules/unity/background.md)에 있고, 같은 이유로 여기 한 줄 둡니다.

**월드의 px 값(뷰포트·배경 높이·최소 크기 등)은 마스터 캔버스 기준 px이고, 화면에서는 작업 영역 폭 비율로 줄어듭니다** — UI의 CanvasScaler와 같은 배율입니다. 새로 px 크기 필드를 갖는 소비자를 만들 때는 [.claude/rules/unity/viewport-coordinates.md](.claude/rules/unity/viewport-coordinates.md)를 봅니다. 새 파일에는 발화할 규칙이 없어 여기 한 줄 둡니다. 프리팹의 월드 위치값은 ×100 하면 마스터 캔버스 px입니다.

**새 그림은 임포트 PPU를 Unity 기본값(100)으로 두고, 시트의 긴 변이 Max Size를 넘지 않게 합니다** — 월드 PPU가 코드 상수 100이라 그대로 두면 그림 1px = 마스터 캔버스 1px가 됩니다. 크기가 안 맞으면 PPU가 아니라 그림을 고칩니다. 새 그림을 임포트하는 순간에도 발화할 규칙이 없어 여기 한 줄 둡니다. 이유는 같은 viewport-coordinates.md에 있습니다.

**새 TMP 폰트 에셋은 Multi Atlas Textures를 켭니다** — Dynamic 아틀라스가 한 장뿐이면 꽉 찬 뒤 처음 나오는 글자가 네모(□)가 되고, 한글 UI는 빌드에서도 한 번 실행 중에 한계를 넘습니다. 이유와 메모리 비용은 [.claude/rules/unity/localization.md](.claude/rules/unity/localization.md)의 폰트 절에 있고, 새 에셋을 만드는 순간에는 발화하지 않아 여기 한 줄 둡니다.

---

## 3. 코드 스타일

정본은 [.claude/rules/unity/csharp.md](.claude/rules/unity/csharp.md)입니다. 이 문서에 규칙을 복사해두지 않습니다 — 두 곳에 적으면 반드시 갈라집니다.

프로젝트 전체에 걸리는 제약 하나만 여기 적습니다. **이 게임은 바탕화면에 항상 떠 있으므로, 매 프레임 할당과 메모리 누수가 곧 사용자 경험을 깎습니다.** §1의 최우선 가치가 코드에 드러나는 지점입니다.

**화면에 보이는 문장은 프리팹·코드에 직접 넣지 않고 키로 찾아 씁니다** — 다섯 개 언어로 번역되기 때문입니다. 나머지 규약은 [.claude/rules/unity/localization.md](.claude/rules/unity/localization.md)에 있습니다. 새 UI 스크립트나 새 로컬라이제이션 코드를 만들 때는 그 파일이 발화하지 않을 수 있어 여기 한 줄 둡니다.

---

## 4. 문서는 3층이고, 각 층이 담는 것이 다릅니다

| 층 | 위치 | 담는 것 | 로드 시점 |
|---|---|---|---|
| 루트 | `CLAUDE.md` (이 문서) | 프로젝트 *전체*에 영향을 주는 큰 단위 규칙. 씬 전략, 코드 스타일의 정본 위치, 문서 층의 역할, 민감 영역 | 세션 시작 시 항상 |
| 중간 | `.claude/rules/*.md` | **사람의 의도** — 왜 코드가 이 모양인지. 기획의 정답은 Figma·Notion이고, 이 층은 개발자가 그것을 1차로 해석한 결과 | `paths`에 매칭되는 파일을 **도구로 열 때** |
| 구현체 | 서브 디렉토리 `CLAUDE.md` | 지금 구현체가 어떻게 되어 있는지. 폴더별 진입점, 사용 금지 항목 | 해당 파일을 열 때 |

### 토큰 관리 — 함정 두 가지

**항상 로드되는 것은 이 문서와 `paths`가 없는 rules 파일뿐입니다.** `paths`가 있는 rules는 그 글로브에 걸리는 파일을 도구로 열 때만 들어옵니다. 세션 시작에 자동으로 주입되는 이 문서는 `paths` 매칭을 발화시키지 않습니다.

- **글로브가 남의 패키지를 잡지 않게 합니다.** `**/*.cs` 같은 넓은 글로브는 `Packages/` 안의 외부 소스까지 잡아서, 우리 컨벤션과 무관한 파일을 열 때도 규칙이 따라옵니다.
- **규칙이 필요한 순간에 발화하는지 확인합니다.** 새 프리팹을 어디에 둘지 정하는 규칙을 `**/*.prefab`에 걸어두면, 정작 새로 만들 때는 읽을 `.prefab`이 없어서 발화하지 못합니다. 실제로 그래서 프리팹 대부분이 옛 규칙과 다른 곳에 놓였습니다. 규칙을 쓸 때는 "이 규칙을 어길 수 있는 파일이 내 `paths`에 걸리는가"를 확인하고, 안 걸리면 이 문서처럼 항상 로드되는 곳에 한 줄 둡니다.

### 서브 디렉토리 CLAUDE.md
- [Project_Cozy/Assets/Characters/CLAUDE.md](Project_Cozy/Assets/Characters/CLAUDE.md) — 캐릭터 에셋 폴더
- [Project_Cozy/Assets/Scripts/CLAUDE.md](Project_Cozy/Assets/Scripts/CLAUDE.md) — **코드 작업의 진입점.** 레이어 구분, 폴더별 진입점, 사용 금지 항목
- [Project_Cozy/Assets/Scripts/Character/CLAUDE.md](Project_Cozy/Assets/Scripts/Character/CLAUDE.md) — 캐릭터 상태 머신과 module 구조
- [Project_Cozy/Assets/Scripts/Interaction/CLAUDE.md](Project_Cozy/Assets/Scripts/Interaction/CLAUDE.md) — 마우스 라우팅과 인터랙터블 계약
- [Project_Cozy/Assets/Scripts/PerformanceSetting/CLAUDE.md](Project_Cozy/Assets/Scripts/PerformanceSetting/CLAUDE.md) — 프레임·뷰포트 정책
- [Project_Cozy/Assets/Scripts/Platform/CLAUDE.md](Project_Cozy/Assets/Scripts/Platform/CLAUDE.md) — OS 의존(Win32) 코드 격리 레이어

### .claude/rules/ 인덱스
- `git.md` — `paths` 없음, 항상 로드
- `figma.md` — `paths` 없음, 항상 로드. 기획 원본인 Figma 파일을 어떤 도구로 읽는지. **기본은 브라우저이고, MCP는 명시 요청이나 정확한 수치가 필요할 때만**
- `docs-conventions.md` — 문서·CLAUDE.md·rules를 쓸 때
- `unity/csharp.md`, `unity/scenes.md`, `unity/prefabs.md`, `unity/ui-panels.md`, `unity/platform.md`, `unity/project-settings.md` — 해당 파일을 열 때
- `unity/character-ground.md` — 캐릭터 지면·발 규약. Character 스크립트·캐릭터 프리팹·캐릭터 스프라이트 임포트 설정을 열 때
- `unity/character-shadow.md` — 캐릭터 그림자 규약. `ShadowProjector`·캐릭터 프리팹을 열 때
- `unity/character-state-machine.md` — 캐릭터 상태 전이 확정안. **캐릭터 행동 로직의 유일한 본.** Character 스크립트·캐릭터 프리팹·Animator 자산을 열 때
- `unity/interaction-hit.md` — 월드 오브젝트의 마우스 판정 규약(판정은 그림 외곽선에서, 콜라이더와 픽셀 검사의 역할 분담, 콜라이더 위치). Interaction 스크립트·`StarController`·캐릭터·별 프리팹·캐릭터 그림 임포트 설정을 열 때
- `unity/background.md` — 배경 띠 규약(뷰포트 아래 변 고정, 고정 높이, Tiled 반복, 콜라이더 금지). 배경 스크립트·`Background.prefab`·배경 아트 임포트 설정을 열 때
- `unity/viewport-coordinates.md` — 월드 좌표 단위 규약(베이스 공간 px = 마스터 캔버스 기준 px, 폭 기준 배율, 월드 PPU 상수 100·앵커 불변, 저장 단위). 뷰포트·배경·장식 스크립트를 열 때
- `unity/decoration-placement.md` — 장식 배치 모드(설치·이동·회수·제거)의 공통 골격, 설치 가능 판정과 색, 앞뒤 순서, 구매 시점과 저장, 장식에 콜라이더를 달지 않는 이유, 인벤토리 갱신 시점. 상점·인벤토리·장식 스크립트, 장식 프리팹을 열 때
- `unity/localization.md` — 다중 언어 규약(지원 언어 5개와 BCP 47 태그 저장, 번역 원본은 Figma, 키 기반 문자열·변수 이름 규칙, 번역 대상 범위, 미지원 언어 안내). UI·Contents 스크립트, 설정 스크립트, UI 패널 프리팹, 폰트를 열 때

---

## 5. 절대 건드리지 말 것

**Unity가 자동 생성하는 캐시** — `Project_Cozy/Library/`, `Project_Cozy/Temp/`, `Project_Cozy/Logs/`, `Project_Cozy/obj/`, `Project_Cozy/Build(s)/`, `Project_Cozy/UserSettings/`. 읽지도 수정하지도 않습니다.

**커밋되면 안 되는 것** — `.gitignore`에 들어 있지만 실수하면 큰 문제가 되는 둘입니다.

- `.env`, `.env.*` — Figma 액세스 토큰 등 개인 키가 들어 있습니다.
- `Tool/CCLCozyGameTool/GameData/` — 미공개 콘텐츠 데이터입니다. `.gitignore`가 "Never commit this"로 표시해 둔 곳입니다.
