# Scripts/

게임 C# 코드. 레이어는 책임 단위로 나눈다. 아래 표의 참조 대상은 각 폴더가 *보통* 무엇에 기대는지를 적은 것이지 강제 규칙이 아니다 — 필요하면 반대 방향으로도 참조할 수 있다.

## 레이어

| 폴더 | 책임 | 주로 참조하는 대상 |
|---|---|---|
| `Platform/` | OS 의존 인프라 — borderless·Always-on-Top 창, 비포커스 키보드 훅, Win32 / DwmApi 호출. 게임 로직을 모른다. | (없음) — 자세한 컨벤션은 [Platform/CLAUDE.md](Platform/CLAUDE.md) |
| `Interaction/` | 마우스 입력 라우팅 + 인터랙터블 인터페이스 계약. 게임 객체가 `IHoverable` / `IClickable` / `IShiftRightClickable`을 구현하면 매니저가 자동 라우팅. | (없음) — 자세한 컨벤션은 [Interaction/CLAUDE.md](Interaction/CLAUDE.md) |
| `PerformanceSetting/` | 프레임 레이트·윈도우 종횡비 등 런타임 *정책*. Win32 일부 직접 호출. | (없음) — 자세한 컨벤션은 [PerformanceSetting/CLAUDE.md](PerformanceSetting/CLAUDE.md) |
| `Animation/` | 스프라이트 애니메이션 등 순수 표현 컴포넌트. 게임 로직·OS를 모른다. | (없음) |
| `Character/` | 캐릭터 단일 개체의 자율 거동·친밀도·시각. `BaseCharacterController` 단일 컴포넌트 + nested module(`StateModule`/`VisualModule`/`AffinityModule`). | `Interaction/`, `Animation/`, `Platform/Input/` — 자세한 컨벤션은 [Character/CLAUDE.md](Character/CLAUDE.md) |
| `Gameplay/` | 게임 로직. `Platform/`의 인프라를 *소비*한다 (별 클릭 · 변신 등은 채워지는 중). | `Platform/`, `Animation/`, `Interaction/` |
| `UI/` | HUD·메뉴 표시. TextMeshPro 사용. | 하위 레이어 자유 참조 (`Gameplay/`·`Character/`·`Interaction/`·`Platform/` 등) — UI는 최상위 표현층이라 하위를 향한 참조에 제한을 두지 않는다. 설정 UI가 창 정책(`Platform/`의 `OverlayWindowController` 등)을 직접 제어하는 교차가 잦기 때문. |

> 새 시스템(변신, 다중 모니터, 클릭 투과 등)이 구현되면 이 표에 위치를 같이 적는다.

## 현재 들어 있는 것

### Platform/
- `Platform/Window/BorderlessWindow.cs` — Always-on-Top + borderless + 투명 배경(Awake 1회 적용, HWND 노출).
- `Platform/Window/WindowResizeHandler.cs` — 마우스 드래그 리사이즈(WndProc 서브클래싱).
- `Platform/Window/HitTestCalculator.cs` — 마우스 좌표 → `ResizeHitZone` 판정(순수 C#, EditMode 테스트 가능).
- `Platform/Window/ResizeHitZone.cs` — `ResizeHitZone` enum + Win32 NCHITTEST 코드 매핑.
- `Platform/Input/GlobalKeyInput.cs` — 포커스 무관 전역 키 입력 소스. 두 OS 경로(WH_KEYBOARD_LL + InputSystem.onAnyButtonPress)를 단일 이벤트 `KeyPressed(Key)`로 추상화.
- `Platform/Input/OutFocusKeyHook.cs` — OutFocus 키 입력만 **static 이벤트** `KeyPressed(Key)`로 방송. 소비자는 인스턴스 참조 없이 `OutFocusKeyHook.KeyPressed +=`로 구독. 씬당 1개(OS-wide hook)라 중복 부착만 Awake에서 방지.
- `Platform/Input/OutFocusMouseHook.cs` — OutFocus 마우스 down 3종을 **static 이벤트** `ButtonPressed(MouseButton)`로 방송. 구독 방식·단일 인스턴스 원칙은 위와 동일.
- `Platform/Input/Win32KeyMap.cs` — Win32 vkCode → `UnityEngine.InputSystem.Key` 매핑(순수 로직, EditMode 테스트 가능).

### Interaction/
- `Interaction/InteractionInterfaces.cs` — `IHoverable` / `IClickable` / `IShiftRightClickable` 3개 계약.
- `Interaction/InputInteractionManager.cs` — 마우스 위치 → 콜라이더 → sortingLayer/sortingOrder 가장 높은 인터랙터블에 라우팅. `GetComponent<I*>`로 컴포넌트 1개만 잡음.
- `Interaction/MoonClickIdle2D.cs` — 별(가제). K키로 Active → 클릭 시 prefab 리스트의 다음 1개 스폰.
- `Interaction/DraggableObject2D.cs` — 마우스 좌클릭 드래그로 transform 위치 갱신.
- `Interaction/InputInteractionTestProbe.cs` — 인터페이스 3개 구현, `Debug.Log`만 하는 시연/테스트용.
- `Interaction/OpaqueHoverable.cs` — IHoverable. sprite 픽셀 알파 검사 → 불투명일 때만 UnityEvent 발사.
- `Interaction/ClickableEvent.cs` — IClickable. 클릭 시 UnityEvent 발사.

### PerformanceSetting/
- `PerformanceSetting/PerformanceSettings.cs` — VSync OFF + foreground/background `targetFrameRate` 전환.
- `PerformanceSetting/WindowAspectFitter.cs` — Win32로 윈도우 크기·종횡비·하단 도킹 강제.

### Animation/
- `Animation/SpriteAnimator.cs` — 프레임 배열을 fps마다 순환. `IsPlaying` / `Play` / `Stop` / `Toggle`. **캐릭터 외 사용처 전용** (별·달 등). 새 `BaseCharacterController` 시스템엔 사용 금지.

### Character/  ⭐ 새 통합 구조
- `Character/BaseCharacterController.cs` — 캐릭터 메인 컴포넌트(non-sealed, IStateOwner 구현). 4 module 보유 + Ground/중력/물리.
- `Character/CharacterState.cs` — 통합 13-state enum + `CharacterForm` enum(Animal/Girl).
- `Character/IStateOwner.cs` — State 클래스가 의존할 owner 인터페이스.
- `Character/CharacterInteractionRelay.cs` — 자식 Visual에 부착, IShiftRightClickable만 책임 (친밀도 리셋).
- `Character/ScaleMultiplier.cs` — 직렬화 가능한 배수 단위(`Value` + `Changed` 이벤트).
- `Character/ScaleMultiplierSettings.cs` — 게임 내 모든 ScaleMultiplier를 모은 ScriptableObject. 현재 `Character` 하나, 향후 UI/Background 확장.
- `Character/Modules/StateModule.cs` — State 머신 + Sleep 정책 + SpecialMode 분기 + `IsLockedState` 가드. 11 State 등록 + `Request*` API.
- `Character/Modules/VisualModule.cs` — Animator 단일 진입점(`Play`/`PlayOneShot`(float timer)/`SetFacing`/`SetForm`).
- `Character/Modules/AffinityModule.cs` — 친밀도 수치 + 3 이벤트(`AffinityChanged`/`SpecialActivated`/`SpecialReleased`).
- `Character/Modules/ScaleModule.cs` — `_baseScale * User * Extra` 곱셈으로 루트 `transform.localScale` 갱신. `ScaleMultiplierSettings.Character.Changed` 구독.
- `Character/States/BaseCharacterState.cs` — abstract state 베이스.
- `Character/States/{Idle, Walk, Run, Sleep, WakeUp, Pet, Grabbed, Fall, Land, SpecialIdle, SpecialWalk}State.cs` — 11개 State 클래스. `Run`/`Special*`은 `Walk`/`Idle` 상속.

### Gameplay/
- `Gameplay/SpawnPointManager.cs` — 스폰 포인트의 '스폰 기운'을 관리. 입력 4채널을 `CurrentEnergy`(소비형)+`CumulativeEnergy`(누적)로 쌓고 스폰 시 차감. (저장 연결은 아직 미구현.)
- `Gameplay/SpawnPointFileFormat.cs` — 스폰 기운의 저장 데이터 컨테이너(`CurrentEnergy`+`CumulativeEnergy`). `HeartFileFormat`과 같은 패턴.
- `Gameplay/AnimatorKeyToggle.cs` — 지정 키 누르면 `SpriteAnimator` 재생/정지 토글.

### UI/
- `UI/DebugCounterLabel.cs` — `SpawnPointManager`의 스폰 기운을 매 프레임 폴링해 TMP 라벨에 표시.
- `UI/CharacterStateLabel.cs` — `BaseCharacterController.State.StateChanged`를 구독해 현재 상태 이름을 TMP 라벨에 표시(테스트용).

## 컨벤션

- **Namespace 미사용** (글로벌 namespace 유지) — Platform/ 등 모든 폴더 동일. *예외*: 민준 deprecated 코드는 `Prototype.Minjun` namespace로 격리.
- **외부 참조는 인스펙터 (`[SerializeField] private`)** 또는 같은 GameObject의 `GetComponent`(Awake 1회 캐싱). 매 프레임 또는 빈번한 `Find` / `FindObjectOfType` 금지. *씬 단일 인스턴스라 가져와서 메서드를 호출하는* 컴포넌트는 Singleton `Instance` 패턴 사용(예: `HeartSystem`). *입력을 방송만 하는 소스*는 참조 대신 **static 이벤트**를 노출해 소비자가 구독한다(예: `OutFocusKeyHook.KeyPressed`).
- **상호작용은 인터페이스로만.** 게임 객체는 `Interaction/`의 인터페이스를 구현하고 매니저로의 직접 의존은 두지 않는다.
- 그 외 네이밍·MonoBehaviour·성능 원칙은 루트 [CLAUDE.md](../../../CLAUDE.md) 참조.
