작성일: 2026-09-25 · 갱신: 2026-11-03 (구현 결과 반영, D9 결정) · 상태: **구현 완료(4c0ff4c), 다중 모니터 실기 확인 대기**

# 다중 모니터 지원 — 결정과 구현

이 문서는 "창을 어느 모니터에 띄울지 고르는 기능"의 결정 근거, 지금 구현이 어떻게 되어 있는지, 그리고 아직
확인하지 못한 것을 정리한 것입니다. 처음에는 구현 전 조사 문서였고, 4c0ff4c에서 구현이 들어간 뒤 실제 코드에 맞춰
고쳐 썼습니다. 줄번호는 2026-11-03 시점 스냅샷입니다.

규칙의 정본은 rules에 있습니다. 모니터 설정의 저장·적용 규칙은 [settings.md](../../.claude/rules/unity/settings.md)의
"모니터" 항목, 베이스 공간의 원천은 [viewport-coordinates.md](../../.claude/rules/unity/viewport-coordinates.md)입니다.
이 문서는 그 규칙이 *왜* 그렇게 정해졌는지와 *무엇이 미확인인지*를 담습니다.

---

## 1. 요구사항과 결정

### 1.1 UX 요구사항

1. 인게임 뷰포트가 여러 모니터에 걸치지는 않는다.
2. 어떤 모니터에 띄울지 고를 수 있다. 선택지에는 번호와 해상도가 함께 보인다.
3. UI·구름·인게임 뷰포트가 선택에 따라 **다 같이** 옮겨간다.
4. Windows 배율(DPI) 설정 때문에 게임이 보이는 크기가 바뀌지 않는다 (§4).

기획 사본의 근거: `Docs/Planning/UserSettings.md:35`에 "주 디스플레이 설정 — 기본값 1, 다중 모니터 환경에서
캐릭터를 표시할 모니터 선택(오직 한 모니터에만 표시)"이 필수 항목으로 있습니다. Figma 옵션 화면에서는 그래픽 탭의
"모니터" 줄입니다.

### 1.2 결정 (2026-11-01 ~ 11-03, 국기님)

| # | 질문 | 결정 | 구현 |
|---|---|---|---|
| D1 | 모니터 번호 | **주 모니터가 1번.** 나머지 순서는 상관없다. 선택지에 번호와 해상도를 함께 표시 | 주 모니터 다음은 왼쪽 → 오른쪽(같으면 위 → 아래). 실행마다 번호가 바뀌지 않게 열거 순서가 아니라 위치로 정렬 |
| D2 | 선택을 어디에 저장하나 | **`settings.json`** | `SettingsFileFormat`의 `monitorDevice`·`monitorX/Y/Width/Height` |
| D3 | 무엇을 식별자로 저장하나 | **장치 이름 + 모니터 rect.** 못 찾으면 주 모니터로 가되 저장값은 덮지 않는다 | 그대로. 1번을 고르면 장치를 적지 않고 "늘 주 모니터"(빈 이름)로 저장 |
| D4 | 뷰포트를 모니터별로 기억하나 | **공통 1개** | 추가 작업 없음 (§3.4) |
| D5 | 선택한 모니터가 사라지면 | **주 모니터로 다시 맞춘다** | 다음 실행 때, 또는 "화면 다시 맞추기"를 누를 때 주 모니터로 간다 (D9 참고) |
| D6 | 변화를 자동으로 따라가나 | **모니터를 고르면 바로 옮긴다. 해상도 변경은 수동**("화면 다시 맞추기" 버튼) | 그대로 |
| D7 | 세로 모니터 | **지원한다.** 빌드해 보고 판단 | 코드상 막는 곳 없음. 실기 미확인 (§3.6) |
| D8 | 배율 모니터에서의 크기 | **Windows 배율과 무관하게 보이는 크기가 같아야 한다** | 지금 설계가 이미 만족 (§4) |
| D9 | 실행 중 모니터 분리·재연결을 알아채나 | **알아채지 않는다** (2026-11-03). 드문 일이라 상주 비용을 들이지 않고 버튼에 맡긴다 | 감지 코드 없음 (§5) |
| U1 | 선택 UI를 어디에 두나 | **설정 그래픽 탭의 드롭다운** (Figma 옵션 배치) | 그래픽 [저장] 흐름을 탄다 (§2.3) |
| R1 | 그래픽 [초기화]가 모니터를 되돌리나 | **되돌리지 않는다** (뷰포트와 같은 판단). **기획에 확인 중이라 답에 따라 바뀔 수 있다** | `ResetGraphicsToDefaults`가 크기 단계 셋만 되돌림 |

용어: **모니터 rect**는 그 모니터가 가상 바탕화면 위에서 차지하는 사각형(왼쪽 위 x, y, 폭, 높이)입니다.
모니터 여러 대는 하나의 큰 좌표 평면에 나란히 놓이고, 주 모니터의 왼쪽 위가 (0, 0)입니다. 예를 들어 1920×1080
주 모니터 오른쪽에 2560×1440 모니터가 있으면, 두 번째 모니터의 rect는 (1920, 0, 2560, 1440)입니다.
**작업 영역**은 그 모니터에서 작업표시줄을 뺀 사각형이고, 창은 이것에 맞춰 놓입니다.

---

## 2. 지금 구현

### 2.1 구성

| 층 | 파일 | 하는 일 |
|---|---|---|
| Platform | `Platform/Display/MonitorList.cs` | `EnumDisplayMonitors` + `GetMonitorInfo`(MONITORINFOEX)로 장치 이름·모니터 rect·작업 영역·주 모니터 여부를 읽어 정렬해 돌려준다. 언제 부를지, 어느 모니터를 쓸지는 모른다 |
| Gameplay | `Gameplay/MonitorApplier.cs` | 설정의 선택을 목록에서 찾아(`IndexOf`, `:63`) 그 작업 영역을 `ViewportScreenSettings.SetTargetWorkArea`에 넣는다(`Apply`, `:75`). 목록을 들고 있다가 `Refresh`(`:55`) 때 다시 읽고 `MonitorsChanged`를 울린다. GameScene의 SettingsManager 오브젝트에 붙어 있다 |
| Gameplay | `Gameplay/SettingsManager.cs` / `SettingsFileFormat.cs` | `MonitorSelection`(장치 이름 + rect, 빈 이름 = 주 모니터)을 그래픽 값으로 든다. 저장값과 지금 값(미리보기)을 따로 든다 |
| PerformanceSetting | `PerformanceSetting/Viewport/ViewportScreenSettings.cs` | 받은 작업 영역으로 베이스 공간을 계산하고 창을 그 자리에 놓는다. 옮긴 뒤 창 크기를 다시 확인한다 |
| UI | `UI/GraphicsTabController.cs` | 모니터 드롭다운을 만들고(`BuildMonitorOptions`), 고르면 `SettingsManager.SetMonitor`를 부른다(`OnMonitorChanged`, `:99`). "화면 다시 맞추기"(`ReadjustWindow`, `:124`)가 `MonitorApplier.Refresh`를 먼저 부른다 |

Platform이 게임 설정을 모르고, Gameplay 적용자가 Platform을 부르는 구조는 항상 위·자동 실행의
`DesktopIntegrationApplier`와 같습니다(settings.md "OS와 맞닿는 설정").

### 2.2 모니터를 고를 때 흐름

```
[그래픽 탭 드롭다운에서 "모니터2 (2560×1440)" 선택]
  → GraphicsTabController.OnMonitorChanged → SettingsManager.SetMonitor (지금 값만 바뀜, 파일은 그대로)
  → SettingsManager.GraphicsChanged
  → MonitorApplier.Apply → IndexOf(장치 이름 → rect → 0번) → SetTargetWorkArea(그 모니터의 작업 영역)
      · 같은 작업 영역이면 아무 일도 하지 않는다 (크기 단계만 바뀌어도 GraphicsChanged가 울리기 때문)
      · 뷰포트 편집 중이면 편집이 끝날 때 옮긴다 (_relayoutAfterEdit, :366)
      · ready 전이면 들고 있다가 Start의 첫 배치에 쓴다
  → ViewportScreenSettings.Relayout
      → RefreshBaseSpace (:432, 원천 = 받은 작업 영역) → ApplyRegion으로 창을 옮김 → 카메라 재프레이밍
      → VerifyWindowRect (:398) 시작 — 3프레임 동안 창 rect가 목표와 다르면 다시 ApplyRegion
      → 적용 뷰포트를 보이는 영역 안으로 맞춤(저장 안 함), 미저장 뷰포트도 같이 맞춤
      → ViewportConfirmed → 배경 띠·장식·캐릭터 거주 영역·구름·캐릭터 기록이 새 뷰포트를 받는다
  → 다음 프레임: Screen.width/height가 새 크기로 바뀌고 UI CanvasScaler가 다시 계산된다
```

창은 OS 창 하나(HWND 하나)에 UI와 월드가 전부 들어 있어서, 창을 옮기면 요구 3이 저절로 충족됩니다.

### 2.3 저장 — 그래픽 [저장] 흐름

- 고르는 즉시 창이 옮겨 가지만 파일에는 아직 쓰지 않습니다. [저장](`CommitGraphics`)을 눌러야 파일에 남습니다.
- 저장하지 않고 패널을 닫거나 탭을 옮기면 미저장 팝업이 뜨고, 확인하면 `RevertGraphics`가 저장값으로 돌려
  `GraphicsChanged`가 울리고, `MonitorApplier`가 창을 원래 모니터로 돌려놓습니다.
- 미저장 판정(`HasUnsavedGraphics`)에 모니터가 들어갑니다. 비교는 `MonitorSelection.Equals`(이름과 rect가 모두 같음)입니다.
- 처음 설계에서는 일반 탭 항목으로 보고 "즉시 저장"을 생각했지만, Figma가 모니터를 그래픽 탭에 두어서 그래픽 탭의
  미리보기 → [저장] 규칙을 따릅니다. 고른 모니터가 실제로 쓸 만한지 화면을 보고 확정할 수 있어 오히려 맞습니다.

### 2.4 찾는 순서와 "늘 주 모니터"

- **장치 이름 → rect → 주 모니터(0번).** 장치 이름(`\\.\DISPLAY2` 같은 것)은 드라이버 재설치·도킹 스테이션에서 바뀔 수
  있고, rect는 Windows 디스플레이 설정에서 배치를 바꾸면 바뀌어서, 둘을 함께 보면 한쪽만 바뀐 경우는 찾을 수 있습니다.
- **못 찾아 주 모니터로 가도 저장값은 덮지 않습니다.** 모니터를 다시 꽂고 다음에 실행하면(또는 "화면 다시 맞추기"를
  누르면) 원래 선택이 돌아옵니다. `ViewportScreenSettings`가 "클램프로 줄어든 뷰포트는 저장하지 않는다"고 정한 것과
  같은 원칙입니다.
- **1번을 고르면 빈 이름("늘 주 모니터")으로 저장합니다.** Windows에서 주 모니터를 다른 모니터로 바꾸면 게임도 새 주
  모니터를 따라갑니다. 기본값도 빈 이름이라, 모니터 필드가 없던 옛 설정 파일은 저절로 주 모니터가 됩니다
  (settings.md의 "기본값을 바꿀 때는 저장 키 이름도 바꾼다" 문제가 생기지 않습니다).
- rect는 `RectInt`를 그대로 담지 않고 정수 넷으로 풀어 저장합니다.

### 2.5 부팅

- `SettingsManager`가 `DefaultExecutionOrder(-100)`의 `Awake`에서 저장값을 읽고, `MonitorApplier.Start`가 첫 `Apply`로
  목표 작업 영역을 넣습니다. `ViewportScreenSettings.Start`는 10프레임 뒤에 첫 배치를 하므로, 처음부터 고른 모니터에
  놓입니다. 순서 숫자를 새로 박지 않았습니다(실행 순서 하드코딩 정리 TODO와 별개).
- 부팅 진단 로그(`ViewportScreenSettings.Start`)가 작업 영역·화면 크기·베이스 공간·배율을 `Player.log`에 한 번 찍습니다.

### 2.6 "화면 다시 맞추기"

`GraphicsTabController.ReadjustWindow`가 `MonitorApplier.Refresh`(목록을 다시 읽고 드롭다운을 다시 만들고, 선택을 다시
찾아 작업 영역을 넣음) → `ViewportScreenSettings.ReadjustWindow`(지금 목표로 창·카메라를 다시 맞춤) 순서로 부릅니다.
해상도·작업표시줄·모니터 구성이 바뀐 뒤 사용자가 누르는 유일한 경로입니다. 사용자의 선택을 바꾸는 일이 아니라
[저장] 흐름을 타지 않습니다.

### 2.7 에디터

- `MonitorList`는 Win32를 부르지 않고 지금 화면 하나("Editor")만 돌려줍니다.
- `MonitorApplier.Apply`는 에디터에서 아무것도 넣지 않습니다. 베이스 공간이 Game 뷰 크기를 따라야 하고, 창을 옮기면
  Unity Editor 창이 망가집니다. 그래서 **에디터에서는 모니터 전환을 확인할 수 없고, 빌드에서만 봅니다.**

---

## 3. 영역별로 따져 본 것

### 3.1 월드의 가로 범위는 모니터가 바뀌어도 변하지 않는다

베이스 공간의 폭은 항상 마스터 캔버스 폭(3840)이고 높이만 작업 영역의 종횡비를 따릅니다
(베이스 높이 = 작업 영역 높이 × 3840 / 작업 영역 폭). 앵커는 마스터 캔버스 우하단에 고정입니다. 따라서 모니터를 바꿔도
**베이스 공간의 윗변만 오르내리고**, 바닥과 좌우는 월드에서 그대로입니다. 16:9(1080p, 작업표시줄 48px)는 약 2064,
21:9(3440×1440)는 약 1554, 32:9(5120×1440)는 약 1044이고, 세로 모니터는 6800 가까이 커집니다.

### 3.2 WindowManager

- `ApplyRegion(x, y, w, h)`는 가상 화면 좌표를 그대로 받아, 다른 모니터로 옮기는 데 그대로 씁니다. 모니터 인자를 받는 새
  API는 만들지 않았습니다 — 목표 작업 영역을 `MonitorList`에서 이미 알고 있어서입니다.
- `TryGetWorkAreaRect`(`MonitorFromWindow` 기반)는 이제 목표를 받은 적이 없을 때(에디터 등)의 대체 경로로만 쓰입니다.
- `TryGetMonitorRect`는 여전히 호출자가 없습니다. 모니터 rect는 `MonitorList`가 줍니다.
- `TryGetWindowRect`는 `VerifyWindowRect`가 옮긴 뒤 창 크기를 확인하는 데 씁니다.
- GameScene의 `WindowManager`는 `_resizable: 0`이라 WndProc가 설치되지 않습니다. 모니터 기능은 WndProc 없이 동작합니다(§5).
- 옛 `WindowAspectFitter`·`BorderlessWindow`는 같은 HWND를 만지므로 함께 켜지 않습니다.

### 3.3 ViewportScreenSettings

- **베이스 공간의 원천이 "고른 모니터의 작업 영역"입니다**(`RefreshBaseSpace`). 창을 먼저 옮기고 `MonitorFromWindow`로
  다시 읽는 방식은 쓰지 않았습니다. 배율이 다른 모니터에서 Unity가 창 크기를 다시 잡으면 순서가 꼬이기 때문입니다.
- **편집 중에는 옮기지 않고 편집이 끝날 때 옮깁니다.** 그 사이 베이스 공간이 바뀌면 프리뷰가 옛 기준으로 남습니다.
  사용자가 고르는 경로는 편집 중에 설정 패널이 숨어 있어 일어나지 않지만, 방어로 둡니다.
- **미저장 뷰포트도 같이 맞춥니다**(`Relayout`이 `_pendingViewport`를 보이는 영역 안으로 클램프).
- **`Screen` 크기는 한 프레임 늦게 바뀝니다.** `Screen` 크기와 베이스 공간의 비율을 쓰는 쪽은 창 크기가 실제로 바뀌는
  다음 프레임까지 옛 값을 봅니다(`WindowServiceHandover.md` §6 "보류" 7번과 같은 현상).

### 3.4 뷰포트 저장값 (D4)

뷰포트는 베이스 공간 px로 저장되고 베이스 폭이 늘 같으므로, **가로 위치·폭은 어느 모니터에서나 같은 의미**입니다.
높이는 짧은 모니터로 가면 클램프로 줄고, 저장값은 그대로라 돌아오면 복원됩니다. 모니터별 뷰포트는 두지 않았습니다.

### 3.5 월드 오브젝트

가로는 불변이고 윗변만 움직이므로, 걱정할 것은 높은 곳에 있는 것뿐입니다.

- **캐릭터:** `ViewportConfirmed` → `ViewportLivingAreaBinder`로 거주 영역이 다시 들어갑니다. 뷰포트 클램프는 높이와 y를
  줄이는 방향뿐이라 지면이 올라가지 않고, 지면이 내려가면 캐릭터는 떨어집니다.
- **장식:** 위치는 가로 하나를 베이스 공간 px로 저장하고 세로는 지면에 맞추므로(`ShopInventoryFileFormat`) 영향받지 않습니다.
  "뷰포트를 저장할 때 화면 밖 장식은 회수"는 사용자의 [저장]에 걸린 규칙이라(`Committed`), 모니터 전환의 클램프는
  회수를 일으키지 않습니다.
- **구름:** `CloudController`가 `ViewportConfirmed`마다 그림이 들어가는 범위를 다시 구해, 밖에 있으면 안쪽 랜덤 위치로
  옮깁니다. 짧은 모니터로 가도 화면 밖에 남지 않습니다.
- **달:** 씬에 고정된 월드 좌표이고 뷰포트에 반응하지 않습니다. 월드 y × 100이 가장 짧은 경우의 베이스 높이(32:9 약 1044)보다
  높으면 화면 밖에 걸립니다. 실기 확인 항목입니다(E8).

### 3.6 UI

- **배율:** CanvasScaler Match=Width라 모니터 폭을 따라 UI 크기가 바뀝니다. 4K에서 1080p 모니터로 가면 UI가 절반이 됩니다.
  월드도 같은 배율로 줄어 UI와 월드의 비율은 유지됩니다.
- **세로 모니터(D7):** 폭이 1080px이면 UI와 월드가 4K 대비 0.28배로 작아지고, 베이스 공간은 위로 길어집니다. 그래픽 탭의
  UI 크기·전체 크기 단계로 사용자가 키울 수 있지만, 지금 단계 범위로 충분한지는 빌드에서 봅니다.
- **드래그된 패널:** `DraggablePanel`은 드래그할 때와 UI 크기를 바꿀 때(`UIScaleApplier` → `ClampIntoCanvas`)만 캔버스 안으로
  당깁니다. 모니터 전환 뒤에는 당기지 않으므로, 세로가 짧은 모니터로 옮기면 위쪽에 옮겨 둔 패널이 화면 밖에 걸칠 수
  있습니다. 실기에서 문제가 되면 전환 뒤에도 당깁니다(E8). 이때 `Screen` 크기가 한 프레임 늦게 바뀌는 점(§3.3)에 주의합니다.

### 3.7 부팅 때 창이 처음 뜨는 모니터

Unity 플레이어는 시작할 때 자기가 정한 모니터에 창을 띄우고, 우리는 10프레임 뒤에 창을 고른 모니터로 옮깁니다.
고른 모니터가 다르면 **잠깐 다른 모니터에 떴다가 점프**하는 모습이 보일 수 있습니다. 창이 투명해서 거의 안 보일
수도 있습니다. Unity가 마지막 모니터를 기억하는 방식은 확인하지 않았습니다(E4).

### 3.8 음수 좌표

주 모니터의 왼쪽이나 위에 있는 모니터는 좌표가 음수입니다. `MonitorList`·`RectInt`·`ApplyRegion`은 음수를 그대로 받고,
`TryGetCursorContext`는 창 기준 상대 계산입니다. 빌드에서 확인한 적은 없습니다(E7).

### 3.9 성능

- 모니터 열거는 드롭다운을 처음 만들 때와 "화면 다시 맞추기"를 누를 때만 합니다. 상주 확인은 없습니다.
- 창 rect 재확인(`VerifyWindowRect`)은 창을 놓을 때만 3프레임 돕니다.
- 다른 GPU에 연결된 모니터(노트북 외장 모니터)는 GPU 간 복사 비용이 생길 수 있습니다. 주사율이 다른 모니터는 VSync를 끄고
  `targetFrameRate`로 고정하므로 영향이 적습니다.
- 클릭 통과·항상 위는 창 단위 스타일이라 모니터와 무관합니다. `ApplyRegion`은 `SWP_NOZORDER`라 Topmost가 유지됩니다.

---

## 4. DPI 배율 (D8)

**배율이 무엇인가.** Windows 설정 › 디스플레이 › "배율"(100%, 125%, 150% …)입니다. 같은 4K 모니터라도 150%로 두면
Windows가 글자와 창을 1.5배 크게 그립니다. 모니터마다 따로 정할 수 있어서, 노트북 화면은 150%, 외장 모니터는 100%처럼
섞여 있는 경우가 흔합니다.

**지금 설계가 D8을 만족하는 이유.** 게임은 UI와 월드 모두 **모니터의 실제 픽셀 폭**을 기준으로 크기를 정합니다
(CanvasScaler Match=Width, 베이스 공간 폭 기준 배율). 배율 설정은 계산에 들어가지 않으므로, 배율을 바꿔도 게임이
차지하는 화면 비율이 그대로입니다.

이것이 성립하려면 **프로세스가 모니터별 DPI 인식(Per-Monitor v2)으로 돌아야 합니다.** DPI 인식 모드는 OS가 좌표를 실제
픽셀로 줄지, 배율로 늘린 가상 좌표로 줄지를 정하는 프로세스 속성입니다. 인식하지 않는 프로세스는 Windows가 창을 배율만큼
늘려 그리고(흐릿해지고 커짐), 좌표도 가상 단위로 나와 클릭 통과 판정과 커서 좌표가 어긋납니다.

- **Unity 플레이어는 이미 모니터별 인식으로 돕니다 (2026-11-01 파일 확인).** 우리 코드는 DPI 인식을 설정하지 않지만,
  Unity 6000.3.10f1의 `windowsstandalonesupport/Variations/win64_player_nondevelopment_mono/WindowsPlayer.exe`에 박힌
  매니페스트가 `<dpiAwareness>PerMonitorV2</dpiAwareness>`를 선언합니다. 이 프로젝트는 Mono 백엔드라 이 파일이 쓰입니다.
  인식 모드는 창이 만들어지기 전에 정해지므로 **스크립트에서 다시 설정하지 않습니다.**
- 빌드 실행 중 실제 모드를 로그로 찍어 보는 실험(E1)은 생략했습니다(2026-11-02).

**배율이 다른 모니터로 옮기면 창 크기가 다시 바뀌는가 — 미확인, 그래서 방어합니다.** 모니터별 인식 창이 배율이 다른
모니터로 넘어가면 OS가 `WM_DPICHANGED`와 "권장 크기"를 보냅니다. Unity가 이것을 받아 창 크기를 바꾸면 우리가 맞춘
작업 영역 크기가 덮입니다.

- 정황상 바꿀 가능성이 높습니다(2026-11-02 조사). `UnityPlayer.dll`이 `GetDpiForWindow`·`AdjustWindowRectExForDpi`·
  `EnableNonClientDpiScaling` 등을 쓰고, Unity 이슈 트래커에 "배율이 다른 모니터로 옮기면 크기가 틀림, 위치를 두 번 넣어야
  할 때가 있음"(2023.2.5f1 수정) 같은 기록이 있습니다. `Screen.MoveMainWindowTo` 문서는 DPI에 대해 아무 말도 하지 않습니다.
- **대응:** 창을 놓은 뒤 3프레임 동안 창 rect를 다시 읽어 목표와 다르면 한 번 더 `ApplyRegion`합니다(`VerifyWindowRect`).
  Unity가 크기를 바꾸든 안 바꾸든 결과가 같아지므로 확인을 기다리지 않았습니다.
- 3프레임 뒤에 Unity가 또 크기를 바꾸는 경우는 막지 못합니다. 실기에서 그런 증상이 보이면 횟수를 늘리거나 다른 방법을 찾습니다.

**알아둘 성질 하나.** "배율과 무관"하다는 것은 *같은 모니터에서* 배율을 바꿔도 게임이 그대로라는 뜻입니다. 모니터끼리
비교하면 게임은 늘 모니터 폭에 비례하므로, 27인치 4K와 14인치 노트북에서 눈에 보이는 실제 크기(cm)는 다릅니다.

---

## 5. 실행 중 모니터 분리·재연결 (D9 — 감지하지 않음)

**Windows는 알림을 보냅니다.** 모니터를 꽂거나 뽑거나 해상도를 바꾸면 Windows가 모든 최상위 창에 `WM_DISPLAYCHANGE`를
보냅니다. 받으려면 창 메시지 처리기(WndProc)를 설치해야 하는데, 지금 GameScene에서는 설치되지 않습니다.

**그래도 감지하지 않기로 했습니다 (2026-11-03).** 실행 중에 모니터를 뽑는 일은 드물고, 받으려면 비용이 있습니다.

| 방법 | 걸리는 것 |
|---|---|
| WndProc에서 `WM_DISPLAYCHANGE` 받기 | WndProc 설치가 지금 리사이즈 기능에 묶여 있어 떼어내야 한다(`WindowManager`의 `wantResizable` 분기). 떼면 `WM_GETMINMAXINFO`가 창 최대 크기를 `_maxSize`(1920×1080)로 묶는 것도 같이 풀어야 한다 — 안 그러면 4K 모니터에서 창이 1920×1080에 갇힌다. WndProc는 종료 시 크래시 이력이 있는 경로다(`UninstallWndProc` 주석) |
| 1초에 한 번 모니터 목록 확인 | viewport-coordinates.md가 피하기로 한 "상주 확인" 그 자체다 |

**그래서 지금 동작은 이렇습니다.**
- 고른 모니터를 뽑으면 Windows가 창을 남은 모니터로 옮기는 경우가 많지만, 게임의 작업 영역·카메라는 옛 모니터 기준으로
  남습니다. 사용자가 "화면 다시 맞추기"를 누르면 목록을 다시 읽고, 고른 모니터가 없으니 주 모니터에 맞춥니다.
- 다음 실행 때는 처음부터 주 모니터에 놓입니다. 저장값은 그대로라 모니터를 다시 꽂으면 원래 모니터로 돌아옵니다.
- 해상도·작업표시줄 변화도 같은 버튼으로 맞춥니다(D6). 이 판단은 viewport-coordinates.md의 "작업 영역이 바뀌어도 저절로
  다시 맞추지 않는다" 규약과 같습니다.

실기에서 버튼만으로는 불편하다고 판단되면 위 표의 두 방법 중 하나로 다시 엽니다.

---

## 6. 남은 확인 (빌드·실기)

개발 환경이 단일 모니터라 모니터 전환 자체는 아직 한 번도 돌려 보지 못했습니다. 실기 확인은 다중 모니터 환경에서 빌드로
합니다. 에디터에서는 확인할 수 없습니다(§2.7).

| # | 실험 | 확인할 것 | 모니터 1대로 |
|---|---|---|---|
| E2' | 게임을 켠 채 Windows 배율을 100% → 125%로 바꾸기 | 창이 작업 영역 크기 그대로인가(우하단 UI·배경이 제자리인가). Unity가 DPI 변화로 창 크기를 바꾸는지 보는 시험이다. 지금은 부팅·전환 때만 재확인하므로, 실행 중 배율 변경은 "화면 다시 맞추기"로 복구되는지도 함께 본다 | 가능 |
| E2 | 배율이 다른 모니터로 전환 | 전환 뒤 창이 작업 영역에 맞는가(`VerifyWindowRect`가 충분한가) | 불가 |
| E4 | 모니터 2를 고르고 종료 후 재실행 | 처음에 어느 모니터에 뜨는가. 점프가 보이는가 | 불가 |
| E5 | "화면 다시 맞추기"를 빌드에서 실행 | 배경 띠·장식·캐릭터 거주 영역·구름·편집 핸들까지 정상인가 | 가능 (작업표시줄을 옮겨서) |
| E6 | 실행 중 고른 모니터 분리 → 버튼 → 재연결 → 버튼 | 버튼으로 주 모니터에 맞고, 재연결 뒤 원래 모니터로 돌아오는가. 드롭다운이 갱신되는가 | 불가 |
| E7 | 주 모니터 왼쪽/위의 모니터로 전환 | 음수 좌표에서 클릭 통과·호버·드래그가 맞는가 | 불가 |
| E8 | 종횡비가 다른 모니터(21:9, 32:9, 세로) | 달·드래그된 패널이 화면 안에 남는가. 세로에서 UI를 읽을 수 있는가 | 불가 |
| E9 | 그래픽 탭에서 모니터를 바꾼 뒤 [저장] 없이 닫기 | 미저장 팝업 → 확인 시 원래 모니터로 돌아오는가 | 불가 |

`ViewportStackMigration.md`의 미실행 18번("창을 다른 모니터로 드래그")은 창을 끌어 옮기는 경로가 없어진 지금은 E2·E6이
대신합니다.

---

## 7. 확인 상태

| 사실 | 상태 | 근거 |
|---|---|---|
| 모니터 목록은 Win32 열거로 읽고 주 모니터가 0번이다 | 코드 확인 | `MonitorList.cs` |
| 선택은 장치 이름 + rect로 저장하고, 못 찾아도 저장값을 덮지 않는다 | 코드 확인 | `MonitorApplier.IndexOf`, `SettingsManager.CommitGraphics` |
| 그래픽 [초기화]는 모니터를 되돌리지 않는다 | 코드 확인 — 기획 답변 대기 | `SettingsManager.ResetGraphicsToDefaults` |
| 실행 중 모니터 분리·재연결을 감지하는 코드가 없다 | 코드 확인 (D9 결정대로) | `MonitorApplier` 주석, WndProc 미설치 |
| GameScene에서 WndProc가 설치되지 않는다 | 디스크 확인 (2026-11-01) | `GameScene.unity`의 `WindowManager` `_resizable: 0` |
| 번호 정렬에 EditMode 테스트가 없다 | 코드 확인 | 정렬이 `MonitorList` 안의 private 함수다. 조사 단계에서는 테스트를 붙이려 했지만 구현에서 빠졌다 |
| Unity 플레이어가 Per-Monitor v2로 선언되어 있다 | 파일 확인 (2026-11-01) | 6000.3.10f1 `WindowsPlayer.exe` 매니페스트 |
| Unity가 DPI 변화 때 창 rect를 다시 계산하는 코드를 갖고 있다 | 파일 확인 + 이슈 트래커 정황 — 실제로 크기를 바꾸는지는 미확인 | `UnityPlayer.dll`, 실험 E2'·E2 |
| 베이스 폭 고정 → 모니터 전환 시 윗변만 움직인다 | 코드에서 유도 — 빌드 미확인 | `ViewportScreenSettings`, `BaseSpaceCameraFitter` |
| 모니터 전환이 빌드에서 동작한다 | **미확인** | 실험 E2·E4·E6·E7·E9 |
| 단일 모니터 1920×1080 부팅 로그 | 실측 (2026-09 `Player.log`) | 작업 영역 1920×1032, 베이스 (3840, 2064), 배율 0.5 |

참고한 Unity 이슈 트래커 기록(2026-11-02 조사):
- [player window resolution is slightly skewed when using multiple displays and one of them has a different scaling value](https://issuetracker.unity3d.com/issues/player-window-resolution-is-slightly-skewed-when-using-multiple-displays-and-one-of-them-has-a-different-scaling-value)
- [windows moving window with position across monitors with multiple dpi has the wrong size](https://issuetracker.unity3d.com/issues/windows-moving-window-with-position-across-monitors-with-multiple-dpi-has-the-wrong-size)
- [unity player doesnt scale to match screen resolution correctly when moving between screens](https://issuetracker.unity3d.com/issues/unity-player-doesnt-scale-to-match-screen-resolution-correctly-when-moving-between-screens)
