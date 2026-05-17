# Platform/

OS 의존 코드 격리 레이어.
Always-on-Top, borderless + resize, 클릭 투과, 비포커스 키보드 입력 등
**게임 로직과는 무관하지만 데스크톱 펫이라서 필요한** 시스템 통합 코드를 여기에 모은다.

## 책임

- Win32 / WinAPI / DwmApi 등 OS 호출은 **모두 이 폴더 안에서만**. 게임 로직(별 클릭, 친밀도, 변신 등)은 P/Invoke 직접 호출 금지.
- Steam SDK 같은 외부 서비스는 여기 두지 않는다 — OS가 아닌 서비스이므로 별도 모듈로 분리. (구체 폴더 위치는 결정 시점에.)

## 하위 폴더

### Window/
borderless 창의 외형 / 거동 관리.

- `BorderlessWindow.cs` — Always-on-Top + borderless + 투명 배경 (Awake 1회 적용, HWND 노출)
- `WindowResizeHandler.cs` — 마우스 드래그 리사이즈 (WndProc 서브클래싱, `BorderlessWindow`에 의존)
- `HitTestCalculator.cs` — 마우스 좌표 → `ResizeHitZone` 판정 (순수 C#, EditMode 테스트 가능)
- `ResizeHitZone.cs` — `ResizeHitZone` enum + Win32 NCHITTEST 코드 매핑

### Input/
포커스 상태와 무관한 입력 수집.

- `GlobalKeyInput.cs` — 전역 키 입력 소스. `WH_KEYBOARD_LL` + `InputSystem.onAnyButtonPress`를 단일 이벤트 `KeyPressed(Key)`로 추상화 — 포커스 유무에 따라 두 OS 경로가 상호 배타적으로 fire하지만 소비자는 그 차이를 모름. 현재 keydown만, 모디파이어/조합키/keyup 미지원(필요 시 KeyEvent 형태로 확장). (이전 명칭: `GlobalKeyboardHook` — `[MovedFrom]`으로 prefab 호환)
- `Win32KeyMap.cs` — Win32 가상 키코드 → `UnityEngine.InputSystem.Key` 매핑. 순수 로직(`UnityEngine` 의존 없음 → EditMode 테스트 가능). LL 훅 경로에서 vkCode를 `Key`로 바꿀 때 쓴다.
- `InputSystem_Actions.inputactions` — Unity New Input System 액션 매핑 에셋. 입력 처리 코드와 함께 두기 위해 `Assets/` 루트에서 이쪽으로 이동.

## 작성 컨벤션

- **Editor 보호.** Win32 호출은 반드시 `#if !UNITY_EDITOR` 가드 안에서. Editor에서 실행하면 Unity Editor 자체의 창/입력이 망가진다.
- **델리게이트 GC 방지.** OS에 함수 포인터로 넘기는 콜백(`WndProc`, `LowLevelKeyboardProc` 등)은 **static 필드**에 보관. 인스턴스 필드만 두면 GC 수거 후 OS가 함수 호출 시점에 액세스 위반.
- **콜백 스레드 주의.** WndProc / LL 훅 콜백은 메시지 펌프 스레드에서 호출될 수 있다. `UnityEngine` API 직접 호출 금지 — `ConcurrentQueue`로 enqueue → 메인 스레드 `Update`에서 dequeue.
- **순수 로직은 `UnityEngine` 의존 없이.** `HitTestCalculator`처럼 좌표 계산만 하는 헬퍼는 `using UnityEngine`을 빼서 EditMode 테스트가 가능하게.
- **Namespace 미사용.** 팀 컨벤션에 따라 글로벌 namespace 유지.

## 추후 후보 (지금은 만들지 않음)

- `Display/` — 다중 모니터, DPI, 작업표시줄 회피 좌표 계산