# PerformanceSetting/

런타임 성능·표시 정책.

## 책임

- 프레임 레이트, 윈도우 크기·종횡비·도킹 등 *프로그램 전체*에 적용되는 정책 컴포넌트.
- 데스크톱 펫이 다른 프로그램과 *공존*해야 한다는 제약에서 출발 — 백그라운드 시 점유 낮추기, 화면 한 줄에만 띠 모양으로 살기 등 [README.md](../../../../README.md) §3 가이드의 실현.
- 게임 로직과 무관. OS 의존(Win32) 코드를 일부 직접 호출한다는 점에서 [Platform/](../Platform/CLAUDE.md)와 영역이 비슷하지만, *정책*과 *인프라*는 분리: Platform/은 *어떻게* 가능하게 하는지, PerformanceSetting/은 *무엇을* 적용할지.

## 현재 들어 있는 것

- `PerformanceSettings.cs` — VSync OFF + 포커스 상태에 따라 `Application.targetFrameRate`를 foreground / background(기본 60/30)로 전환. `OnApplicationFocus`에 hook해 즉시 반응.
- `WindowAspectFitter.cs` — Win32 호출로 윈도우 크기·위치를 강제. 종횡비(예: 32:3 가로 띠), 하단 도킹, 작업영역 클램프, 포커스 시 재적용 등 옵션. 데스크톱 펫이 화면 하단에 *띠처럼* 떠 있게 만드는 용도.

## 컨벤션

- **씬에 1개씩.** 매니저성 컴포넌트지만 싱글톤은 아님 — 씬에 매니저 GameObject 1개. 다중 씬 전환이 생기면 `DontDestroyOnLoad` 또는 각 씬에 다시 두는 방식 중 결정.
- **Win32 충돌 주의 — `BorderlessWindow`와 같은 HWND.** `WindowAspectFitter`와 [Platform/Window/BorderlessWindow](../Platform/Window/BorderlessWindow.cs)는 *같은 윈도우의 스타일 비트·`SetWindowPos`*를 만진다. 한 씬에 둘 다 두면 *마지막 호출이 이김*. 본편 통합 씬에서는 책임을 합친 `WindowManager`로 묶거나 적용 순서를 명시적으로 조율할 것.
- **에디터 보호.** Win32 호출은 [Platform/CLAUDE.md](../Platform/CLAUDE.md)와 동일 원칙 — `#if !UNITY_EDITOR` 가드 또는 에디터에서 안전한 분기. 에디터에서 호출하면 Unity Editor 창 자체가 망가질 수 있다.
- 그 외 네이밍 규칙은 [Scripts/CLAUDE.md](../CLAUDE.md) + 루트 [CLAUDE.md](../../../../CLAUDE.md) §4 참조.

## 추후 후보 (지금은 만들지 않음)

- 환경설정 UI에서 직접 fps 조절 — 현재 인스펙터 노출 필드를 외부 ScriptableObject로 빼고 UI 바인딩.
- Boss Key 모드 — README §3. 어디에 둘지(이 폴더 또는 `Gameplay/`)는 구현 시점에.
- `WindowManager` — `BorderlessWindow` + `WindowAspectFitter`를 합쳐 *한 곳에서* HWND를 만지는 형태로 정리.
