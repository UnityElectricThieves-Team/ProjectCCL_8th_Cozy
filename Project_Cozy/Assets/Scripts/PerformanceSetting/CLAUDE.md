# PerformanceSetting/

런타임 성능·표시 정책.

## 책임

- 프레임 레이트, 윈도우 크기·종횡비·도킹 등 *프로그램 전체*에 적용되는 정책 컴포넌트.
- 데스크톱 펫이 다른 프로그램과 *공존*해야 한다는 제약에서 출발 — 백그라운드 시 점유 낮추기, 화면 한 줄에만 띠 모양으로 살기 등 [README.md](../../../../README.md) §3 가이드의 실현.
- 게임 로직과 무관. *정책*과 *인프라*를 분리한다. Platform/은 OS 기능을 *어떻게* 수행할지, PerformanceSetting/은 *무엇을* 적용할지 결정한다. 신규 P/Invoke는 반드시 `Platform/`에 둔다.

## 현재 들어 있는 것

- `PerformanceSettings.cs` — VSync OFF + 포커스 상태에 따라 `Application.targetFrameRate`를 foreground / background(기본 60/30)로 전환. `OnApplicationFocus`에 hook해 즉시 반응.
- `Viewport/ViewportScreenSettings.cs` — "화면 설정" 정책. 평시 창=뷰포트, 편집 중 모니터 전체 프리뷰, 저장·취소 상태를 관리.
- `Viewport/BaseSpaceCameraFitter.cs` — 베이스 공간 좌표 규약(마스터 캔버스 우하단 앵커 + PPU)의 소유자. `Frame()`으로 카메라를 지정 픽셀 영역에 프레이밍하고, `BaseRectToWorld()`로 베이스 공간 px rect를 월드 Rect로 변환한다(뷰포트 안팎 판정용).
- `Viewport/ViewportEditHandles.cs` — 편집 중 뷰포트 이동·8방향 크기 조절 UI.
- `Viewport/WindowMoveResizeGuide.cs` — 평시 창 이동 그립과 리사이즈 영역의 시각 안내.
- `WindowAspectFitter.cs` — 구 구현. Win32로 HWND를 직접 만져 창을 띠 모양으로 도킹한다. 후속은 `Platform/`의 `WindowManager`이며(`Viewport/` 스택이 아니다), `PerformanceSystemScene.unity:149`에서 아직 쓰이고 있어 남아 있다. 신규 코드에서 사용 금지.

## 컨벤션

- **씬에 1개씩.** 매니저성 컴포넌트지만 싱글톤은 아님 — 씬에 매니저 GameObject 1개. 다중 씬 전환이 생기면 `DontDestroyOnLoad` 또는 각 씬에 다시 두는 방식 중 결정.
- **Win32 충돌 주의.** 구 `WindowAspectFitter`와 `BorderlessWindow`는 현행 `WindowManager`와 같은 HWND를 만진다. 본편 통합 씬에서는 함께 활성화하지 않는다.
- **GameScene의 PPU는 10.8이다.** `BaseSpaceCameraFitter._pixelsPerUnit`의 설계 의도값은 100이지만, 옛 `CameraFitter`가 잡아둔 배치(1080px = 월드 100유닛)를 그대로 보존하려고 낮춰 잡았다. 100으로 되돌리려면 캐릭터 스프라이트 임포트 PPU와 `_floorY`·중력·이동 속도까지 함께 환산해야 한다 — 근거와 환산표는 [ViewportStackMigration.md](../../../../Docs/Development/ViewportStackMigration.md) §3.3.
- **에디터 보호.** Win32 호출은 [Platform/CLAUDE.md](../Platform/CLAUDE.md)와 동일 원칙 — `#if !UNITY_EDITOR` 가드 또는 에디터에서 안전한 분기. 에디터에서 호출하면 Unity Editor 창 자체가 망가질 수 있다.
- 그 외 네이밍 규칙은 [Scripts/CLAUDE.md](../CLAUDE.md) + [.claude/rules/unity/csharp.md](../../../../.claude/rules/unity/csharp.md) 참조.

## 추후 후보 (지금은 만들지 않음)

- 환경설정 UI에서 직접 fps 조절 — 현재 인스펙터 노출 필드를 외부 ScriptableObject로 빼고 UI 바인딩.
- Boss Key 모드 — README §3. 어디에 둘지(이 폴더 또는 `Gameplay/`)는 구현 시점에.
- `WindowAspectFitter`를 `Platform/`의 `WindowManager`로 대체하고, `PerformanceSystemScene`의 참조를 정리한 뒤 제거.
