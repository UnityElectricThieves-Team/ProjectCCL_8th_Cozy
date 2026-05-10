# Scripts/

게임 C# 코드. 레이어를 **의존 방향이 한 방향으로만 흐르도록** 나눈다 — 위(게임 로직 / 표현 / UI)가 아래(OS 인프라)를 참조하고, 그 반대는 금지.

## 레이어

| 폴더 | 책임 | 참조 가능 대상 |
|---|---|---|
| `Platform/` | OS 의존 인프라 — borderless · Always-on-Top 창, 비포커스 키보드 훅, Win32 / DwmApi 호출. 게임 로직을 모른다. | (없음) — 자세한 컨벤션은 [Platform/CLAUDE.md](Platform/CLAUDE.md) |
| `Gameplay/` | 게임 로직. `Platform/`의 인프라를 *소비*한다 (별 클릭 · 친밀도 · 변신 등은 아직 채워지는 중). | `Platform/`, `Animation/` |
| `Animation/` | 스프라이트 애니메이션 등 순수 표현 컴포넌트. 게임 로직 · OS를 모른다. | (없음) |
| `UI/` | HUD · 메뉴 표시. TextMeshPro 사용. | `Gameplay/` |

> 새 시스템(별 클릭, 친밀도, 변신, 다중 모니터, 클릭 투과 등)이 구현되면 이 표에 위치를 같이 적는다.

## 현재 들어 있는 것

- `Platform/Input/GlobalKeyboardHook.cs` — 포커스 무관 키 입력 → `KeyPressed(Key)` 이벤트. 현재 keydown만, 모디파이어 / 조합키 / keyup 미지원 (필요 시 KeyEvent 형태로 확장).
- `Platform/Input/Win32KeyMap.cs` — Win32 vkCode → `UnityEngine.InputSystem.Key` 매핑 (순수 로직, EditMode 테스트 가능).
- `Gameplay/KeyCounter.cs` — `GlobalKeyboardHook` 구독, 키 입력 횟수 누적 (`Count` / `CountChanged`). ※ README §2의 "별 클릭 수" 진척 메커니즘과는 별개.
- `Gameplay/AnimatorKeyToggle.cs` — 지정 키(`_toggleKey`, 기본 `Space`)가 눌리면 `SpriteAnimator` 재생/정지 토글.
- `Animation/SpriteAnimator.cs` — 프레임 배열을 fps마다 순환. `IsPlaying` / `Play` / `Stop` / `Toggle`.
- `UI/KeyCountLabel.cs` — `KeyCounter` 값을 TMP 라벨에 표시.

## 컨벤션

- **Namespace 미사용** (글로벌 namespace 유지) — Platform/과 동일.
- **외부 참조는 인스펙터 (`[SerializeField] private`)** 또는 같은 GameObject의 `GetComponent`(Awake 1회 캐싱). 런타임 `Find` / `FindObjectOfType` 금지 — 루트 [CLAUDE.md](../../../CLAUDE.md) §4.2.
- 그 외 네이밍 · MonoBehaviour · 성능 원칙은 루트 CLAUDE.md §4 참조.