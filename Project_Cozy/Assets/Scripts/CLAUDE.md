# Scripts/

게임 C# 코드. 레이어는 책임 단위로 나눈다. 아래 표의 참조 대상은 각 폴더가 *보통* 무엇에 기대는지를 적은 것이지 강제 규칙이 아니다 — 필요하면 반대 방향으로도 참조할 수 있다.

## 레이어

| 폴더 | 책임 | 주로 참조하는 대상 |
|---|---|---|
| `Platform/` | OS 의존 인프라 — borderless·Always-on-Top 창, 비포커스 키보드 훅, Win32 / DwmApi 호출. 게임 로직을 모른다. | (없음) — 자세한 컨벤션은 [Platform/CLAUDE.md](Platform/CLAUDE.md) |
| `Interaction/` | 마우스 입력 라우팅 + 인터랙터블 인터페이스 계약. 게임 객체가 `IHoverable` / `IClickable` / `IShiftRightClickable`을 구현하면 매니저가 자동 라우팅. | (없음) — 자세한 컨벤션은 [Interaction/CLAUDE.md](Interaction/CLAUDE.md) |
| `PerformanceSetting/` | 프레임 레이트·뷰포트 등 런타임 *정책*. OS 조작은 `Platform/`에 위임. | `Platform/` — 자세한 컨벤션은 [PerformanceSetting/CLAUDE.md](PerformanceSetting/CLAUDE.md) |
| `Animation/` | 스프라이트 애니메이션 등 순수 표현 컴포넌트. 게임 로직·OS를 모른다. | (없음) |
| `Character/` | 캐릭터 단일 개체의 자율 거동·친밀도·시각. `BaseCharacterController` 단일 컴포넌트 + nested module(`StateModule`/`VisualModule`/`AffinityModule`/`ScaleModule`). | `Interaction/`, `Animation/`, `Platform/Input/` — 자세한 컨벤션은 [Character/CLAUDE.md](Character/CLAUDE.md) |
| `Gameplay/` | 게임 로직. `Platform/`의 인프라를 *소비*한다 (별 클릭 · 변신 등은 채워지는 중). 하트·스폰 기운처럼 씬에 하나만 두는 시스템이 여기 산다. | `Platform/`, `Animation/`, `Interaction/` |
| `Contents/` | 상점·도감 같은 *콘텐츠* 시스템. 무엇을 가졌고 무엇을 쓰는 중인지를 들고 파일에 기록한다. 정의(ScriptableObject·JSON)와 상태(저장 파일)를 분리한다. | `Platform/Data/`, `Gameplay/` |
| `UI/` | HUD·메뉴 표시. TextMeshPro 사용. | 하위 레이어 자유 참조 (`Gameplay/`·`Character/`·`Interaction/`·`PerformanceSetting/`·`Platform/` 등) — UI는 최상위 표현층이라 하위를 향한 참조에 제한을 두지 않는다. 설정 UI가 창·뷰포트 정책(`PerformanceSetting/`의 `ViewportScreenSettings` 등)을 직접 제어하는 교차가 잦기 때문. |
| `Examples/` | 기능 확인용 하니스. 본편 동작에 필요한 것을 여기 두지 않는다. | 자유 |

> 새 시스템(다중 모니터 등)이 구현되면 이 표에 위치를 같이 적는다.

## 폴더별 진입점

전체 파일 목록은 폴더를 직접 보면 된다. 여기에는 **어디서부터 읽어야 하는지**만 적는다.

| 폴더 | 여기서 시작 |
|---|---|
| `Platform/Window/` | `Core/WindowManager.cs` — HWND·WndProc의 단일 소유자. DWM 투명화, 클릭 통과, 창 영역 적용, 핫존 수치가 전부 여기 있다 |
| `Platform/Input/` | `GlobalKeyInput.cs` — 포커스 무관 키 입력. OutFocus 전용은 `OutFocusKeyHook`/`OutFocusMouseHook`(둘 다 static 이벤트로 방송) |
| `Platform/Data/` | `GameDataPaths.cs`(모든 저장 경로의 중앙 레지스트리) → `UserDataSaveIO.cs`(유저 데이터 단일 진입점) → `GameDataIO.cs`(직렬화·원자적 쓰기) |
| `Interaction/` | `InteractionInterfaces.cs`(3개 계약) → `InputInteractionManager.cs`(라우팅) |
| `PerformanceSetting/Viewport/` | `ViewportScreenSettings.cs`(평시·편집 뷰포트 정책) + `BaseSpaceCameraFitter.cs`(베이스 공간 좌표 규약의 소유자) |
| `Animation/` | `SpriteAnimator.cs`(프레임 순환), `ShadowProjector.cs`(캐릭터 아래 바닥을 향해 판정해 그림자를 놓고, 멀어질수록 폭을 줄인다) |
| `Character/` | `BaseCharacterController.cs` → `CharacterState.cs`(통합 enum) → `Modules/`, `States/` |
| `Gameplay/` | `HeartSystem.cs`(하트 재화), `SpawnPointManager.cs`(스폰 기운) |
| `Contents/` | `ShopSystem/ShopSystem.cs`(장식 소유), `ShopSystem/BackgroundSystem.cs`(배경 소유 + 활성 1개), `CollectionSystem/Model/CollectionData.cs`(도감 정의 — WPF 툴이 만든 JSON을 읽는다) |
| `UI/` | `UIManager.cs`(열린 패널 스택 + ESC) → `UIPanel.cs`(패널 공통 동작) |

## 사용 금지 · 제거 대기

새 코드에서 쓰면 안 되는 것들. 이름만 보면 멀쩡해 보여서 실수하기 쉽다.

- `Animation/SpriteAnimator.cs` — **캐릭터 외 사용처 전용**(별·달 등). `BaseCharacterController` 시스템은 `VisualModule`을 쓴다.
- `Platform/Window/Core/BorderlessWindow.cs`, `Platform/Window/Input/ClickThroughProbe.cs` — 구 프로토타입. 현행 스택과 같은 HWND를 만지므로 함께 활성화하면 충돌한다. 제거 대기.
- `PerformanceSetting/WindowAspectFitter.cs` — 구 구현. 후속은 `Platform/`의 `WindowManager`다. 제거 조건은 [PerformanceSetting/CLAUDE.md](PerformanceSetting/CLAUDE.md) 참조.
- `Platform/Data/SaveScheduler.cs` — 인프라만 있고 **고객이 0명이다.** `IPeriodicSaveable` 구현체도, 씬 배치도 없다. 여기 등록하면 조용히 저장이 안 된다. 새 저장은 `HeartSystem`/`ShopSystem` 골격(값이 바뀔 때 즉시 저장)을 따른다.
- `Gameplay/Viewport/ViewportResidencyEnforcer.cs` — 본문이 전부 주석 처리되어 **아무 일도 하지 않는다.** 씬에도 배치돼 있지 않다.

## 컨벤션

- **Namespace 미사용** (글로벌 namespace 유지) — Platform/ 등 모든 폴더 동일.
- **외부 참조는 인스펙터 (`[SerializeField] private`)** 또는 같은 GameObject의 `GetComponent`(Awake 1회 캐싱). 매 프레임 또는 빈번한 `Find` / `FindObjectOfType` 금지. *씬 단일 인스턴스라 가져와서 메서드를 호출하는* 컴포넌트는 Singleton `Instance` 패턴 사용(예: `HeartSystem`). *입력을 방송만 하는 소스*는 참조 대신 **static 이벤트**를 노출해 소비자가 구독한다(예: `OutFocusKeyHook.KeyPressed`).
- **상호작용은 인터페이스로만.** 게임 객체는 `Interaction/`의 인터페이스를 구현하고 매니저로의 직접 의존은 두지 않는다.
- 그 외 네이밍·MonoBehaviour·성능 원칙은 [.claude/rules/unity/csharp.md](../../../.claude/rules/unity/csharp.md) 참조.
