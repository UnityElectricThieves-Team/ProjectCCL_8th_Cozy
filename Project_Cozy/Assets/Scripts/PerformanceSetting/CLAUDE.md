# PerformanceSetting/

런타임 성능·표시 정책.

## 책임

- 프레임 레이트, 윈도우 크기·종횡비·도킹 등 *프로그램 전체*에 적용되는 정책 컴포넌트.
- 데스크톱 펫이 다른 프로그램과 *공존*해야 한다는 제약에서 출발 — 백그라운드 시 점유 낮추기, 화면 한 줄에만 띠 모양으로 살기 등 [README.md](../../../../README.md) §3 가이드의 실현.
- 게임 로직과 무관. *정책*과 *인프라*를 분리한다. Platform/은 OS 기능을 *어떻게* 수행할지, PerformanceSetting/은 *무엇을* 적용할지 결정한다. 신규 P/Invoke는 반드시 `Platform/`에 둔다.

## 현재 들어 있는 것

- `PerformanceSettings.cs` — VSync OFF + 포커스 상태에 따라 `Application.targetFrameRate`를 foreground / background(기본 60/30)로 전환. `OnApplicationFocus`에 hook해 즉시 반응.
- `Viewport/ViewportScreenSettings.cs` — "화면 설정" 정책. 정적 창(작업 영역 고정), 뷰포트 편집·저장·취소 상태를 관리한다. **베이스 공간 단위의 소유자** — 작업 영역을 마스터 캔버스 기준 px로 바꾸는 계산(`RefreshBaseSpace`)과 마스터 캔버스 폭 상수가 여기 있다. 규약은 [.claude/rules/unity/viewport-coordinates.md](../../../../.claude/rules/unity/viewport-coordinates.md).
- `Viewport/BaseSpaceCameraFitter.cs` — px→월드 환산(마스터 캔버스 우하단 앵커 + PPU)의 소유자. `Frame()`으로 카메라를 지정 픽셀 영역에 프레이밍하고, `BaseRectToWorld()`로 베이스 공간 px rect를 월드 Rect로 변환한다(뷰포트 안팎 판정용). 식은 px 단위를 모른다 — 단위는 위의 `ViewportScreenSettings`가 정한다.
- `Viewport/ViewportEditHandles.cs` — 편집 중 뷰포트 이동·8방향 크기 조절 UI.
- `Viewport/WindowMoveResizeGuide.cs` — 평시 창 이동 그립과 리사이즈 영역의 시각 안내.
- `WindowAspectFitter.cs` — 구 구현. Win32로 HWND를 직접 만져 창을 띠 모양으로 도킹한다. 후속은 `Platform/`의 `WindowManager`이며(`Viewport/` 스택이 아니다), `PerformanceSystemScene.unity:149`에서 아직 쓰이고 있어 남아 있다. 신규 코드에서 사용 금지.

## 컨벤션

- **씬에 1개씩.** 매니저성 컴포넌트지만 싱글톤은 아님 — 씬에 매니저 GameObject 1개. 다중 씬 전환이 생기면 `DontDestroyOnLoad` 또는 각 씬에 다시 두는 방식 중 결정.
- **Win32 충돌 주의.** 구 `WindowAspectFitter`와 `BorderlessWindow`는 현행 `WindowManager`와 같은 HWND를 만진다. 본편 통합 씬에서는 함께 활성화하지 않는다.
- **월드 PPU는 `BaseSpaceCameraFitter`의 코드 상수 100이다.** 인스펙터로 바꿀 수 없다. 예전에는 옛 카메라 배치를 보존하려고 씬 값을 낮춰 두었는데, 그 배율로 월드 단위 값(발 정렬·중력·이동 속도·월드 글자·씬 위치)을 모두 환산해 100으로 옮겼다. 그림의 임포트 PPU도 전부 100이다. 규칙과 이유는 [viewport-coordinates.md](../../../../.claude/rules/unity/viewport-coordinates.md).
- **창 동작 정지는 자기 것만 걸고 푼다.** `ViewportScreenSettings`는 편집에 들어갈 때 자신을 소유자로 클릭 통과·리사이즈 정지를 걸고, 편집을 나갈 때와 `OnDisable`에서 자기 정지만 푼다. 다른 기능(장식 배치 등)이 같은 정지를 걸고 있어도 건드리지 않기 위해서다. 규칙의 정본은 [Platform/CLAUDE.md](../Platform/CLAUDE.md)의 "창 동작 정지" 항목.
- **편집 중에는 월드 입력도 잠근다.** `ViewportScreenSettings`는 같은 자리(편집 진입·이탈, `OnDisable`)에서 자신을 소유자로 `WorldInputLock`을 걸고 푼다. 편집 핸들(`ViewportEditHandles`)은 IMGUI로 그려 EventSystem에 잡히지 않아서, 캐릭터·별의 "UI 위면 무시" 가드가 핸들을 못 본다. 잠그지 않으면 핸들을 잡는 클릭이 뒤의 캐릭터로 새어 쓰담·잡기가 일어날 수 있다. 잠금 규칙의 정본은 [Interaction/CLAUDE.md](../Interaction/CLAUDE.md).
- **에디터 보호.** Win32 호출은 [Platform/CLAUDE.md](../Platform/CLAUDE.md)와 동일 원칙 — `#if !UNITY_EDITOR` 가드 또는 에디터에서 안전한 분기. 에디터에서 호출하면 Unity Editor 창 자체가 망가질 수 있다.
- 그 외 네이밍 규칙은 [Scripts/CLAUDE.md](../CLAUDE.md) + [.claude/rules/unity/csharp.md](../../../../.claude/rules/unity/csharp.md) 참조.

## 추후 후보 (지금은 만들지 않음)

- 환경설정 UI에서 직접 fps 조절 — 현재 인스펙터 노출 필드를 외부 ScriptableObject로 빼고 UI 바인딩.
- Boss Key 모드 — README §3. 어디에 둘지(이 폴더 또는 `Gameplay/`)는 구현 시점에.
- `WindowAspectFitter`를 `Platform/`의 `WindowManager`로 대체하고, `PerformanceSystemScene`의 참조를 정리한 뒤 제거.
