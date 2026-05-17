# Scripts/

게임 C# 코드. 레이어를 **의존 방향이 한 방향으로만 흐르도록** 나눈다 — 위(게임 로직 / 표현 / UI)가 아래(OS 인프라 · 입력 라우팅 · 정책)를 참조하고, 그 반대는 금지.

## 레이어

| 폴더 | 책임 | 참조 가능 대상 |
|---|---|---|
| `Platform/` | OS 의존 인프라 — borderless·Always-on-Top 창, 비포커스 키보드 훅, Win32 / DwmApi 호출. 게임 로직을 모른다. | (없음) — 자세한 컨벤션은 [Platform/CLAUDE.md](Platform/CLAUDE.md) |
| `Interaction/` | 마우스 입력 라우팅 + 인터랙터블 인터페이스 계약. 게임 객체가 `IHoverable` / `IClickable` / `IShiftRightClickable`을 구현하면 매니저가 자동 라우팅. | (없음) — 자세한 컨벤션은 [Interaction/CLAUDE.md](Interaction/CLAUDE.md) |
| `PerformanceSetting/` | 프레임 레이트·윈도우 종횡비 등 런타임 *정책*. Win32 일부 직접 호출. | (없음) — 자세한 컨벤션은 [PerformanceSetting/CLAUDE.md](PerformanceSetting/CLAUDE.md) |
| `Animation/` | 스프라이트 애니메이션 등 순수 표현 컴포넌트. 게임 로직·OS를 모른다. | (없음) |
| `Character/` | 캐릭터 단일 개체의 자율 거동·친밀도. 입력 계약을 *구현*해 매니저로부터 호출을 받는다. | `Interaction/`, `Animation/` — 자세한 컨벤션은 [Character/CLAUDE.md](Character/CLAUDE.md) |
| `Gameplay/` | 게임 로직. `Platform/`의 인프라를 *소비*한다 (별 클릭 · 변신 등은 채워지는 중). | `Platform/`, `Animation/`, `Interaction/` |
| `UI/` | HUD·메뉴 표시. TextMeshPro 사용. | `Gameplay/`, `Character/` |

> 새 시스템(변신, 다중 모니터, 클릭 투과 등)이 구현되면 이 표에 위치를 같이 적는다.

## 현재 들어 있는 것

- `Platform/Window/BorderlessWindow.cs` — Always-on-Top + borderless + 투명 배경(Awake 1회 적용, HWND 노출).
- `Platform/Window/WindowResizeHandler.cs` — 마우스 드래그 리사이즈(WndProc 서브클래싱).
- `Platform/Window/HitTestCalculator.cs` — 마우스 좌표 → `ResizeHitZone` 판정(순수 C#, EditMode 테스트 가능).
- `Platform/Window/ResizeHitZone.cs` — `ResizeHitZone` enum + Win32 NCHITTEST 코드 매핑.
- `Platform/Input/GlobalKeyInput.cs` — 포커스 무관 전역 키 입력 소스. 두 OS 경로(WH_KEYBOARD_LL + InputSystem.onAnyButtonPress)를 단일 이벤트 `KeyPressed(Key)`로 추상화. 현재 keydown만, 모디파이어/조합키/keyup 미지원. (이전 명칭: `GlobalKeyboardHook` — `[MovedFrom]`으로 호환)
- `Platform/Input/Win32KeyMap.cs` — Win32 vkCode → `UnityEngine.InputSystem.Key` 매핑(순수 로직, EditMode 테스트 가능).
- `Interaction/InteractionInterfaces.cs` — `IHoverable` / `IClickable` / `IShiftRightClickable` 3개 계약.
- `Interaction/InputInteractionManager.cs` — 마우스 위치 → 콜라이더 → sortingLayer/sortingOrder 가장 높은 인터랙터블에 라우팅. 포인터-정지 시 재스캔 스킵 최적화 내장.
- `Interaction/MoonClickIdle2D.cs` — 별(가제) 컴포넌트. K키로 Active → 클릭 시 prefab 리스트의 다음 1개 스폰.
- `Interaction/InputInteractionTestProbe.cs` — 인터페이스 3개 구현, `Debug.Log`만 하는 시연/테스트용.
- `Interaction/OpaquePixelHover.cs` — IHoverable을 받아 sprite 픽셀 알파를 검사 → 불투명일 때만 UnityEvent 발사. 테스트용 sprite의 Read/Write 필요.
- `Interaction/PettingReactionTestProbe.cs` — "쓰다듬" 시각 반응 (틴트 + 스케일). OpaquePixelHover의 UnityEvent에 연결되는 테스트용 반응 컴포넌트.
- `PerformanceSetting/PerformanceSettings.cs` — VSync OFF + foreground/background `targetFrameRate` 전환.
- `PerformanceSetting/WindowAspectFitter.cs` — Win32로 윈도우 크기·종횡비·하단 도킹 강제. *`BorderlessWindow`와 같은 HWND를 만지므로 한 씬에 둘 다 둘 때 적용 순서 주의.*
- `Animation/SpriteAnimator.cs` — 프레임 배열을 fps마다 순환. `IsPlaying` / `Play` / `Stop` / `Toggle`.
- `Character/CharacterAffinity2D.cs` — Idle/Walk 자율 거동 + `IHoverable`로 친밀도 누적, 만점 시 Special 시각 전환, `Shift+우클릭`으로 리셋.
- `Gameplay/KeyCounter.cs` — `GlobalKeyInput` 구독, 키 입력 횟수 누적(`Count` / `CountChanged`). ※ README §2의 "별 클릭 수" 진척 메커니즘과는 별개.
- `Gameplay/AnimatorKeyToggle.cs` — 지정 키(기본 `Space`)가 눌리면 `SpriteAnimator` 재생/정지 토글.
- `UI/KeyCountLabel.cs` — `KeyCounter` 값을 TMP 라벨에 표시.

## 컨벤션

- **Namespace 미사용** (글로벌 namespace 유지) — Platform/ 등 모든 폴더 동일.
- **외부 참조는 인스펙터 (`[SerializeField] private`)** 또는 같은 GameObject의 `GetComponent`(Awake 1회 캐싱). 런타임 `Find` / `FindObjectOfType` 금지 — 루트 [CLAUDE.md](../../../CLAUDE.md) §4.2.
- **상호작용은 인터페이스로만.** 게임 객체는 `Interaction/`의 인터페이스를 구현하고 매니저로의 직접 의존은 두지 않는다.
- 그 외 네이밍·MonoBehaviour·성능 원칙은 루트 [CLAUDE.md](../../../CLAUDE.md) §4 참조.
