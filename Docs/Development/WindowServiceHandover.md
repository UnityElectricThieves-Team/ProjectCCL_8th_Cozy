# WindowService 인수인계 문서

> **독자**: 이 프로젝트의 창(Window) 시스템 작업을 이어받는 AI/개발자.
> **범위**: 신 창 스택(WindowManager 계열)의 구조·설계 의도·최근 수정 이력·5인 리뷰 토론 결론·미해결 과제.
> **작성**: 2026-07-21. 로컬 시각 자료: [WindowService 구조와 크로마키 비교](WindowServiceArchitecture.html). 기존 Claude 아티팩트 없이 열 수 있는 독립형 HTML이다.

---

## 1. 무엇을 만들고 있나

바탕화면 상주형 데스크톱 펫(Project Cozy)의 **투명 오버레이 창 시스템**.
최우선 가치는 최적화 — "리소스 점유율이 곧 사용자 경험" (루트 CLAUDE.md).

핵심 요구사항:
- 창은 테두리 없이 투명하고, 캐릭터/UI 픽셀만 보인다 (DWM 알파 합성)
- 빈 곳 클릭은 뒤 앱으로 통과, 캐릭터/UI 위 클릭은 잡힌다 (hover-aware 클릭 통과)
- 평시엔 창 = 뷰포트 rect만 존재 (뷰포트 밖은 렌더링·합성 비용 0)
- 편집 모드에서 뷰포트를 핸들 드래그로 조정, 평시엔 상단 그립으로 창 이동
- 뷰포트 밖으로 밀려난 캐릭터를 어떻게 다룰지 (창이 없는 영역 = 렌더링·클릭 불가 영역) — **처리 방식 미확정, 미구현**

## 2. 확정된 설계 결정과 이유 (뒤집기 전에 반드시 읽을 것)

| 결정 | 이유 |
|---|---|
| **크로마키(ColorKey) → 알파 합성 전환** | 크로마키는 색만 보므로 검정 UI 글자에 구멍이 뚫림. 알파는 같은 검정이라도 농도로 구분. 반투명 UI도 가능해짐 |
| **`useFlipModelSwapchain: 0` (BitBlt) 유지** | Unity에서 알파 투명 창의 유일한 현실적 경로. 플립 모델은 DWM이 알파를 무시함. DirectComposition 우회는 네이티브 플러그인 규모라 기각. **비용은 창 면적에 비례** → 아래 "창=뷰포트" 절충이 이 비용의 관리 수단 |
| **카메라 클리어 색 = (0,0,0, α0)** | 알파 0이어도 색이 남으면 DWM이 색을 번지게 함. RGB는 반드시 검정. HDR/MSAA는 알파를 망가뜨려 OFF |
| **클릭 통과 = 매 프레임 폴링 + 창 단위 `WS_EX_TRANSPARENT` 토글** | `WS_EX_TRANSPARENT`는 픽셀이 아니라 창 전체 단위. 폴링(커서→콜라이더/UI 판정) 비용은 점 질의 1회라 캐릭터 30마리 규모에서도 무시 가능 — 성능 병목은 폴링이 아니라 렌더링과 창 면적임 (검증됨) |
| **Win32 호출은 상태 변화 프레임에만** | `_isClickThroughOn` 캐시 비교 후 다를 때만 `SetWindowLong` |
| **평시 창=뷰포트 / 편집 시 창=모니터 전체** | BitBlt 복사·DWM 합성 비용 절감의 핵심. 편집은 일시 상태라 전체 화면 비용 허용 |
| **창 이동/리사이즈는 OS에 위임 (NCHITTEST)** | WndProc 서브클래싱으로 `HTCAPTION`(상단 중앙 그립)/`HTLEFT` 등만 응답 — 커서, 드래그 추적, ESC 취소를 OS가 공짜로 처리 |
| **창 드래그 후 역동기화** | 사용자가 창을 직접 옮기면 `WM_EXITSIZEMOVE` → 창 rect를 새 뷰포트로 수용(카메라 재프레이밍 + 영속화). 없으면 다음 적용 때 원위치로 튕김 |

> ⚠️ **뷰포트 밖 캐릭터 회수는 확정 결정이 아니다 — 아래 표에서 제외했다.** 신호만 보내고 방치하면 영영 접근 불가라는 문제 인식은 유효하지만, "강제로 위치를 바꾸는 것이 게임 디자인상 적절한가"가 미결이라 `ViewportResidencyEnforcer` 코드에 `TODO`로 남아 있다. §6 보류 항목 참조.


## 3. 컴포넌트 구조 (신 스택)

```
Platform/Window/Core/WindowManager.cs      ← HWND 접점 유일 창구 (Win32 전부 여기)
    Awake: borderless → DWM 알파 투명(고정 ON) → topmost → WndProc 설치
    Update: 클릭 통과 폴링 (콜라이더 + UGUI 레이캐스트 + 리사이즈/캡션 핫존)
    WndProc(static): NCHITTEST / GETMINMAXINFO / EXITSIZEMOVE
    공개 API: ApplyRegion, ApplyMonitorFullscreen, TryGetMonitorRect, TryGetWindowRect,
              SetClickThroughSuspended, SetResizeSuspended, WindowRectChangedByUser(event)

Platform/Window/HitTesting/HitTestCalculator.cs ← 비클라이언트 히트 영역 계산
Platform/Window/HitTesting/ResizeHitZone.cs     ← 이동·8방향 리사이즈 판정값

PerformanceSetting/Viewport/ViewportScreenSettings.cs  ← 정책 레이어 (Win32 모름)
    평시/편집 상태 기계. EnterEdit/SaveEdit/CancelEdit/SetViewport/SetPreviewViewport
    이벤트: EditModeChanged, PreviewChanged, ViewportSaved(영속화용), ViewportApplied(확정 적용)
    창 드래그 역동기화 수신 (WindowRectChangedByUser 구독)

PerformanceSetting/Viewport/BaseSpaceCameraFitter.cs   ← 카메라를 베이스 공간 px 영역에 1:1 프레이밍
    베이스 공간 = 마스터 캔버스(3840×2160) 우하단을 모니터 해상도로 크롭
    BaseRectToWorld(): 뷰포트 px → 월드 Rect (회수 판정용)

PerformanceSetting/Viewport/ViewportEditHandles.cs     ← 편집 모드 핸들 드래그 UI (IMGUI, 편집 중만)
PerformanceSetting/Viewport/WindowMoveResizeGuide.cs   ← 평시 창 이동·리사이즈 시각 안내 (IMGUI 상시)
    ※ 실제 창 조작은 WindowManager/OS가 담당하며 이 컴포넌트는 안내만 담당

Gameplay/Viewport/ViewportResidencyEnforcer.cs         ← ⚠️ 골격만. ViewportApplied는 구독하지만
    OnViewportApplied 본문과 Recall()이 전부 주석 처리돼 실제로는 아무 일도 하지 않는다.
    막힌 지점: CharacterManager에 살아있는 캐릭터를 순회할 공개 API가 없다
    (현재 `AliveCount`(int)와 private 리스트뿐 — `Alive` 프로퍼티는 존재하지 않는다).
Gameplay/Viewport/IViewportExitListener.cs             ← 캐릭터 측 자체 연출 훅 (bool 반환 = 자체 처리 여부).
    계약만 정의됨 — 호출하는 코드가 없어 아직 구현체를 붙일 의미가 없다.

Examples/WindowFeatureTestPanel.cs            ← 테스트 하니스. 우하단 버튼 패널 런타임 생성
    EnsureCompanion으로 편집 핸들/평시 안내/회수기를 자동 장착 + Bind() 참조 주입
```

**구 스택 정리 상태**: `OverlayWindow*`, `WindowResizeHandler`, `RegionEditChrome` 코드는 제거됐다.

- `GameScene` — 정리 완료. 죽은 컴포넌트 5개와 `DevTools_OverlayMode` 오브젝트를 제거하고 `WindowManager`를 부착했으며, DWM 알파 합성이 되도록 카메라 알파를 0으로, HDR·MSAA를 OFF로 맞췄다. 뷰포트 편집 스택(`ViewportScreenSettings` 등)은 아직 넣지 않았다 — `CameraFitter` → `BaseSpaceCameraFitter` 교체가 좌표 기준을 바꾸는 작업이라 분리했다.
- `TestHyeonScene` — 옛 컴포넌트가 비활성(`m_IsActive: 0`) 상태로 남아 있어 씬을 열면 Missing Script 경고가 뜬다. Unity Editor에서 제거해야 한다.
- `Core/BorderlessWindow`, `Input/ClickThroughProbe` — 별도 프로토타입 정리 대상으로 남아 있다.

## 4. 시간축 실행 흐름 (요약)

1. **프레임 0** — `WindowManager.Awake` (빌드 전용): HWND → 스타일 5종 적용
2. **+10프레임** — `ViewportScreenSettings.Start`: 모니터 읽기 → `ApplyNormal()` (창=뷰포트 배치 + 카메라 프레이밍 + `ViewportApplied` 발행). `_ready=true` 이전의 `EnterEdit`는 경고 로그 + 거부됨
3. **매 프레임** — 폴링: `GetCursorPos` → 좌표 변환 → `Physics2D.OverlapPoint` → (미적중 시) UGUI `RaycastAll` → 리사이즈/캡션 핫존 → 캐시와 다를 때만 `SetWindowLong`
4. **수시 (OS 스레드 가능)** — WndProc: 캡션/가장자리 히트 응답, 드래그 종료 시 volatile 플래그 → 메인 스레드가 `WindowRectChangedByUser`로 변환
5. **편집 왕복** — EnterEdit(suspend 먼저 → 풀스크린) ↔ Save/Cancel(→ `ApplyNormal` → `ViewportApplied` 발행). 이 이벤트를 받아 밖의 캐릭터를 처리하는 부분은 아직 비어 있다

상세 다이어그램은 문서 상단 로컬 HTML 참조.

## 5. 최근 세션 수정 이력

### 5.1 기능 추가 순서
1. 클릭 통과 폴링에 **UGUI 레이캐스트 추가** (`WindowManager.IsPointerOverUI`) — UI 버튼 위에서 클릭이 뒤로 새던 구멍 봉합. 무인자 `IsPointerOverGameObject`는 클릭 통과 중 포인터 갱신 정지로 stale할 수 있어 **좌표 직접 주입 RaycastAll** 방식 사용 (이 프로젝트의 표준 판정 방식)
2. **캡션 이동 핫존 연결** — `HitTestCalculator`/`ResizeHitZone`에 이미 있던 Caption/HTCAPTION을 WindowManager에 배선 (`_captionHeightPx=28`, `_captionWidthPx=220`)
3. **창 드래그 역동기화** — `WM_EXITSIZEMOVE` → `WindowRectChangedByUser` → 뷰포트 수용
4. **편집 핸들 드래그** (`ViewportEditHandles`) + **평시 창 조작 안내** (`WindowMoveResizeGuide`)
5. **캐릭터 회수 체계의 골격만** (`ViewportApplied` 이벤트 + `IViewportExitListener` 계약 + `ViewportResidencyEnforcer` 껍데기) — 동작하는 회수는 없다
6. **테스트 하니스** (`WindowFeatureTestPanel`) — 스폰/편집/프리셋 버튼 + 컴패니언 자동 장착

### 5.2 "핸들이 안 보인다" 버그의 원인 (재발 방지용 기록)
1. **[근본] 컴파일 실패**: 평시 안내 컴포넌트의 이름이 변경됐는데 하니스가 옛 이름을 참조 → Assembly-CSharp 전체 빌드 실패 → 핸들을 장착하는 유일한 경로가 실행된 적 없음. 핸들 컴포넌트는 어떤 씬/프리팹에도 배치돼 있지 않음(GUID 전수 검색 0건) — **런타임 자동 장착에 의존하는 구조는 컴파일 에러 하나로 전멸한다**
2. **[구조] 기본 뷰포트=화면 전체**: 바깥 딤 4조각이 전부 면적 0, 테두리는 모니터 최외곽 3px — 수학적으로 아무것도 안 보임
3. **[빌드 한정] DWM 목적지 알파**: IMGUI는 straight-alpha라 알파 0 배경 위에서 목적지 알파를 못 채워 딤이 의도(0.45)보다 훨씬 옅게(≈0.2) 합성됨. **Editor에서 재현 안 되고 빌드에서만 나타남**

## 6. 검토 결론 (채택/기각/보류)

렌더링, 입력·좌표, 수명주기·배선, Win32·스레딩, UX·성능 다섯 관점으로 나눠 검토한 결과.

### 채택 (모두 반영 완료)
- 편집 중 **카메라 클리어 알파 0.45 전환/복원** (`ViewportEditHandles._editBackdropAlpha`) — 전역 딤 신호 + DWM 목적지 알파 확보 겸용. 이탈/파괴 시 복원 보장
- 핸들 **시각·히트 공통 12px 인셋 클램프** — "보이는 곳 = 잡히는 곳" 원칙(프로젝트 관례)
- 모서리 **원형 4개 + 변 바(pill) 4개** (히트존 8방향 유지), 액센트 **주황 (1, 0.58, 0.10)** 통일
- 드래그 시 이동하는 변을 베이스 공간에 직접 클램프 — 사후 `ClampToBaseSpace`에만 맡기면 오버슈트 때 반대편 앵커가 밀림
- `EnterEdit`: **suspend를 풀스크린 확장보다 먼저**, `_ready` 전 호출은 **로그+거부** (큐잉 기각 — 10프레임 뒤 갑자기 전체화면 전환이 더 나쁜 UX). UI는 `IsReady`로 버튼 잠금
- 역동기화 진입 시 `RefreshBaseSpace()` — 멀티모니터 드래그 시 옛 모니터로 스냅백하던 버그
- `UninstallWndProc`에서 `_originalProc` **Zero화 금지** — 원복 후에도 디스패치된 메시지가 들어와 종료 시 간헐 크래시
- `useGUILayout=false`, `Texture2D.whiteTexture`, 참조 `Bind()` 주입(Find 순서 의존 제거), 구독 멱등화

### 기각 (다시 제안하지 말 것, 근거 포함)
- **편집 중 클리어 알파 1.0 (완전 불투명)**: 데스크톱을 보면서 뷰포트를 배치하는 편집 UX가 죽음 + 복원 실패 시 불투명·topmost·클릭 흡수 창 = 사실상 화면 잠금
- **"UGUI로 옮기면 DWM 흐림 해결"**: UGUI 기본 셰이더도 straight-alpha라 동일. 이관하더라도 알파 기록 머티리얼(`Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha`)이 별도로 필요
- **EnterEdit 요청 큐잉**, **Update 내 lazy Find** (csharp.md 규칙 위반 — 주입/초기화 경로 Find로 대체)
- **WindowManager에 UI 점유 플래그 미러** (평시 캡션 존이 UI 가릴 때): 1프레임 지연 오판 + UI 지식이 Platform 레이어로 역류. 캡션 위치·폭 조정으로 해결할 것

### 보류 (차기 과제, 우선순위 순)
1. **편집 핸들·평시 창 안내 UI의 UGUI 이관** — IMGUI의 상시 비용 제거 + 클릭 통과 판정(`IsPointerOverUI`)에 자연 편입되어 suspend 단일 실패점 해소. 이관 시 알파 기록 머티리얼 필수 (위 기각 사유 참조)
2. **남은 프로토타입 정리** — `BorderlessWindow`/`ClickThroughProbe` 제거 및 씬의 Missing Script 정리
3. 평시 **캡션 존(상단 중앙 220×28)이 콜라이더/UGUI보다 우선**하는 문제 — 캐릭터/UI가 그 자리에 오면 클릭이 창 이동으로 흡수됨. 캡션 위치·폭 조정으로 대응
4. `Application.runInBackground` 확인 — 꺼져 있으면 포커스 상실 시 폴링 정지 → 클릭 통과 상태 동결
5. `WM_GETMINMAXINFO`의 `_maxSize`(1920×1080)가 4K 모니터에서 평시 리사이즈를 제한 — 모니터 rect로 갱신
6. Editor에서 게임뷰 < 720×480이면 `ClampToBaseSpace`가 min>max로 깨진 rect 생성 — `MinViewportSize`를 베이스 공간과 먼저 min 연산
7. EnterEdit 직후 1~2프레임 `Screen.width/height`가 옛 창 크기 (첫 프레임 히트테스트 빗나감 가능) — 1프레임 지연 활성화 검토
8. **뷰포트 밖 캐릭터를 어떻게 다룰지 결정** — 코드는 `ViewportResidencyEnforcer`에 껍데기만 있고 본문이 주석 처리돼 있다. 두 가지가 막혀 있다. (a) 강제로 위치를 옮기는 것이 게임 디자인상 적절한지 미결, (b) `CharacterManager`에 살아있는 캐릭터를 순회할 공개 API가 없음(`AliveCount`만 있고 목록은 private). 방향이 정해지면 (b)를 먼저 열어야 한다
9. 캐릭터 "걸어서 복귀" 연출 — 위 8번이 정해진 뒤 `IViewportExitListener` 구현체를 상태 머신(WalkState 계열)에 연결

## 7. 작업 시 반드시 지킬 것

- **씬(.unity) 파일 직접 편집 금지** (`.claude/rules/unity/scenes.md`) — 그래서 런타임 자동 장착(EnsureCompanion) 구조가 존재함. 씬 변경은 사용자에게 Unity Editor 작업으로 안내
- 평시 창 이동·리사이즈 안내 컴포넌트의 확정 이름은 `WindowMoveResizeGuide.cs`
- Win32/P-Invoke는 **`Platform/` 폴더에만** (`.claude/rules/unity/platform.md`). 정책 레이어는 Win32를 모른다
- WndProc 관련 코드는 **static + volatile** 규칙 유지 (메시지 스레드에서 호출될 수 있음). 인스턴스 멤버 접근 금지
- 매 프레임 할당 금지, 이벤트 구독은 반드시 해제 (csharp.md)
- 새 .cs 추가 시 `.meta` 파일 동반 커밋
- **시각 수치 튜닝은 빌드에서 검증** — DWM 합성 때문에 Editor와 빌드의 겉모습이 다름 (§5.2-3)

## 8. 검증 시나리오 (빌드)

1. 실행 → 캐릭터 위 클릭(잡힘) / 빈 곳 클릭(뒤로 통과) / 테스트 패널 버튼 클릭(잡힘)
2. 상단 중앙 주황 그립 드래그 → 창 이동 → 카메라가 새 위치의 베이스 공간을 비추는지 (캐릭터가 창을 따라오지 않고 제자리에 있어야 정상 — 창은 "뷰파인더")
3. 창 가장자리 드래그 → 리사이즈 → 뷰포트 역동기화 확인
4. [편집 시작] → 화면 전체가 반투명 딤 + 주황 테두리/핸들 → 핸들 드래그로 축소 → [저장] → 창이 새 뷰포트 크기로 줄어드는지 확인
   (뷰포트 밖에 남은 캐릭터는 아직 처리되지 않는다 — 회수 미구현이므로 접근 불가 상태로 남는 것이 현재의 정상 동작이다)
5. [취소] 경로, 멀티모니터 드래그, 앱 종료 시 크래시 없음 확인
