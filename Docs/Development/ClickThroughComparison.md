# 클릭 통과(Click-Through) 구현 비교

2026-06-14

> **역사 문서 주의:** 이 문서의 §5 “ColorKey 채택” 결론은 2026-06-14 당시 결정이며 현재는 폐기됐다. 현행 구현은 DWM 알파 합성 + `WindowManager`의 hover-aware 클릭 통과다. 현재 구조와 비교는 [WindowServiceArchitecture.html](WindowServiceArchitecture.html)을 기준으로 본다.

데스크톱 펫 윈도우는 화면을 점유하면서도, 게임 오브젝트(캐릭터·별 등) 위가 아닐 때의 클릭은 뒤의 프로그램으로 통과시켜야 한다. 이 "클릭 통과(click-through)"를 두 개발자가 서로 다른 방식으로 구현했다. 본 문서는 두 구현의 동작·비용·트레이드오프를 사실 기반으로 정리한다.

**채택:** develop 통합 과정에서 윈도우 레이어를 koko 구현(`OverlayWindow` 스택)으로 일원화했고, 클릭 통과도 **koko의 ColorKey 방식(아래 방식 A)** 을 채택한다. (§5)

> **기준 시점:** §1·§2의 메커니즘 서술은 **2026-06-14 시점 `develop-kk`의 실제 코드**(`OverlayWindow.cs`, `WindowManager.cs`)를 직접 읽고 작성했다. 아래 "관련 선행 기록"은 작성 당시(5월)의 스터디 노트로, **현재 구현과 다를 수 있으니** 현재 코드의 사실 근거로 삼지 말고 배경 참고로만 볼 것.

관련 선행 기록 (과거 스터디 노트 — 현재 구현과 괴리 가능):
- [WS_EX_TRANSPARENT 시 마우스 입력 감지 (2026-05-23)](WsExTransparentMouseInputStudy.md)
- [투명 배경 구현을 위한 스터디 기록 (2026-05-24)](TransparentBackgroundStudy.md)

---

## 0. 용어

| 용어 | 의미 |
|---|---|
| `WS_EX_LAYERED` | 레이어드 윈도우 확장 스타일. DWM 알파 합성 / ColorKey의 전제. |
| `WS_EX_TRANSPARENT` | 윈도우 **전체**의 마우스 메시지를 아래 윈도우로 통과시키는 확장 스타일. |
| `LWA_COLORKEY` | `SetLayeredWindowAttributes`의 모드. **지정 색 픽셀**을 투명 + 클릭 통과 처리. |
| `WM_NCHITTEST` | OS가 "이 좌표가 창의 어느 영역인가"를 묻는 메시지. 리사이즈/이동 판정에 쓰임. |
| `GetCursorPos` | 포커스·통과 상태와 무관하게 OS-wide 커서 좌표를 반환하는 Win32 API. |

두 방식 모두 공통적으로 `WS_EX_LAYERED` + `DwmExtendFrameIntoClientArea`로 투명 배경을 만들고, **카메라 배경색을 키 색(검정)으로** 둔다. 차이는 "어떤 클릭을 통과시킬지 결정하는 메커니즘"에 있다.

---

## 1. 방식 A — koko: per-pixel `LWA_COLORKEY`

구현 위치: `Project_Cozy/Assets/Scripts/Platform/Window/OverlayWindow.cs` (`ApplyToWindow`), 정책은 `OverlayWindowController.cs`

### 동작
- 모드 적용(또는 부팅) 시 **1회** `SetLayeredWindowAttributes(hwnd, key, 255, LWA_COLORKEY)`를 호출한다. `key`는 카메라 배경색(검정).
- 이후 OS/DWM이 **픽셀 단위로** 판정한다: 키 색(검정) 픽셀 클릭은 아래 창으로 통과, **비검정(렌더된 캐릭터·UI) 픽셀 클릭은 창이 받는다.**
- 매 프레임 갱신하는 폴링이 없다. 스타일은 모드가 바뀔 때만 다시 적용한다.

### 모드별 (`eWindowMode`)
| 모드 | 적용 스타일 | 클릭 통과 |
|---|---|---|
| `Normal` | Transparent(LAYERED+ColorKey), `WS_EX_TRANSPARENT` OFF | 검정 픽셀만 통과, opaque 픽셀은 캐치 (per-pixel) |
| `PassThrough` | `WS_EX_TRANSPARENT` ON | 창 전체 통과 |
| `EditRegion` | Transparent OFF (불투명) | 통과 없음 (영역 리사이즈/이동용) |

### 판정 기준
- **렌더된 픽셀 색** (ColorKey와 같은 색이면 통과). 콜라이더와 무관하게, 화면에 실제로 그려진 비검정 픽셀이 그대로 클릭 표면이 된다.

---

## 2. 방식 B — kk: hover-aware `WS_EX_TRANSPARENT` 토글

구현 위치: `Project_Cozy/Assets/Scripts/Platform/Window/WindowManager.cs` (`PollClickThrough`)

### 동작
- 기본적으로 창 전체에 `WS_EX_TRANSPARENT`를 **켜둔다**(전부 통과).
- 매 프레임 커서 아래에 게임 오브젝트(콜라이더)가 있는지 판정해서, 있으면 `WS_EX_TRANSPARENT`를 **끈다**(그 동안만 창이 클릭을 받음).
- 즉 **창 전체 단위의 이진 토글**을, Unity 측 hit-test 결과로 매 프레임 갱신한다.

### 매 프레임 판정 (`PollClickThrough`)
1. `GetCursorPos` (Win32) — 커서 스크린 좌표
2. `GetWindowRect` (Win32) — 창 사각형
3. Win32 좌표(Y-down) → Unity 좌표(Y-up) 변환
4. `camera.ScreenToWorldPoint` → 월드 좌표
5. `Physics2D.OverlapPoint` — 그 지점에 콜라이더가 있나
6. (resizable 동시 사용 시) `HitTestCalculator.Calculate` — 리사이즈 핫존인가
7. `shouldBeOn = !콜라이더위 && !리사이즈핫존`
8. **상태가 바뀐 프레임에만** `GetWindowLong`+`SetWindowLong`으로 `WS_EX_TRANSPARENT` 비트 토글

### 핵심 제약 — 커서 좌표를 `GetCursorPos`로 읽음
현재 코드 사실: `PollClickThrough`는 커서 좌표를 **`GetCursorPos`(OS-wide)** 로 읽고 Unity Input API(`Mouse.current`)를 쓰지 않는다.

배경(과거 스터디 노트 — 작성 시점 관찰): `WS_EX_TRANSPARENT`가 ON이면 OS가 마우스 메시지를 아래 창으로 라우팅해 **Unity `Mouse.current.position`은 멈추고 `GetCursorPos`만 갱신**되더라는 기록이 있다(→ [2026-05-23 기록](WsExTransparentMouseInputStudy.md)). 이 구조가 그대로라면, 통과 토글 판정을 Unity 입력에 의존할 경우 "한 번 통과 ON → 트리거 소실 → 복귀 불가" 데드락이 생기므로 OS-wide 폴링이 필요하다. *이 freeze 관찰 자체는 과거 기록이며 현재 환경에서 재검증된 값은 아니다 — 다만 현재 코드가 `GetCursorPos`를 쓰는 것은 이 제약과 일관된다.*

### 판정 기준
- **Physics2D 콜라이더**의 존재. 통과 여부는 보이는 픽셀이 아니라 콜라이더 모양으로 결정된다.

---

## 3. 성능 — 연산 비용

각 시나리오에서 **앱(Unity) 측** 프레임 비용을 비교한다.

| 시나리오 | A. koko (ColorKey) | B. kk (hover-aware 토글) |
|---|---|---|
| **매 프레임 (InFocus)** | **0** (앱이 매 프레임 하는 일 없음) | `GetCursorPos` + `GetWindowRect` + 좌표변환 + `ScreenToWorldPoint` + `Physics2D.OverlapPoint` (+resizable 시 `HitTestCalculator`) — **매 프레임 고정 수행** (idle-skip 없음) |
| **매 프레임 (OutFocus)** | **0** (동일) | InFocus와 동일. `GetCursorPos`는 전역이라 동작하며, Unity 루프가 돌도록 `runInBackground = true` 전제 |
| **Event 발생 시 — hover 경계 교차** | 해당 개념 없음 (경계가 픽셀이라 토글 자체가 없음) | 통과 ON↔OFF가 바뀌는 프레임에만 `GetWindowLong` + `SetWindowLong` 추가 |
| **Event 발생 시 — 실제 클릭** | OS가 클릭 픽셀 색으로 per-pixel 라우팅 (앱 추가 비용 없음) | OS가 현재 `WS_EX_TRANSPARENT` 비트에 따라 라우팅 (앱 추가 비용 없음) |
| **부팅 / 모드 변경 (1회성)** | 모드 변경 시 스타일+ColorKey+DWM 1회 적용 | 부팅 시 스타일 1회 적용 |

보충:
- A는 클릭 통과 판정을 **OS/DWM 컴포지터**가 입력 메시지마다 수행한다. 앱의 프레임 예산에는 들어가지 않는다.
- B는 클릭 통과 판정을 **앱의 프레임 루프**가 매 프레임 수행한다(커서가 멈춰 있어도 폴링은 돈다 — `WindowManager.PollClickThrough`에는 "포인터 변화 없으면 스킵" 최적화가 없다).
- 두 방식 모두 **실제 클릭 라우팅 자체**는 OS가 하므로 클릭당 앱 비용은 없다. 차이는 "통과 여부를 *결정*하는 연산을 어디서 매 프레임 하느냐"이다.

---

## 4. 개발 / 유저 편의성

### 4.1 개발 편의성

| 항목 | A. koko | B. kk |
|---|---|---|
| 통과 판정 기준 | 렌더 픽셀 색 — 클릭 통과 목적의 별도 콜라이더 불필요 | Physics2D 콜라이더 — 인터랙터블마다 콜라이더를 아트에 맞춰 셋업 필요 |
| 좌표 처리 | 불필요 (OS가 픽셀로 판정) | Win32↔Unity 좌표 변환을 직접 구현 |
| Unity 입력 | 무관 (per-pixel은 입력 API와 독립) | 사용 불가 — `GetCursorPos` 폴링 필수 (§2 제약) |
| 리사이즈 공존 | 모드 분리(`EditRegion`=불투명)로 처리 | 리사이즈 핫존 위에서는 통과 OFF 유지 등 예외 처리 필요 |
| 제어 위치 | OS 스타일 (코드 개입 최소) | C# 런타임 로직 (조건을 코드로 세밀 제어 가능) |
| 아트 제약 | 키 색(검정) 픽셀은 투명·통과가 됨 → 가시 아트에 순검정(0,0,0) 사용 시 구멍 발생 주의 | 없음 (콜라이더가 판정) |

### 4.2 유저 편의성

| 항목 | A. koko | B. kk |
|---|---|---|
| 통과 입도 | 픽셀 단위 | 창 전체 이진 (커서 위치 기준으로 프레임마다 전체 전환) |
| 반응 지연 | OS 실시간 합성 → 프레임 지연 없음 | 토글이 다음 프레임 반영 → 빠른 커서 이동 시 경계에서 1프레임 오차 가능 |
| 시각-클릭 일치 | 렌더된 픽셀과 1:1 | 콜라이더 모양 정확도에 의존 (콜라이더와 스프라이트가 어긋나면 클릭 영역도 어긋남) |
| **OutFocus 상호작용** | 렌더된 opaque 픽셀(캐릭터·UI) 클릭이 **per-pixel로 창에 즉시 전달** — 폴링 불필요. 클릭이 창을 활성화시키므로 **포커스 없는 상태에서도 UI 상호작용이 바로 동작** | `GetCursorPos` 폴링 + 콜라이더 hit-test로 동작(`runInBackground` 전제). 커서가 콜라이더 위로 들어와 통과가 OFF로 **전환된 뒤** 클릭이 창에 전달됨 → 통과 토글이 클릭보다 **선행**해야 함 |

**OutFocus 상호작용 — 대응 트레이드오프** (위 행을 부연; §4.1의 판정 기준·아트 제약과 연결)

- **즉시성 / 첫 클릭**
  - A(koko): opaque 픽셀에 닿는 즉시 전달 → 선행 상태 전환이 없어 **막 진입한 대상의 첫 클릭도 그대로 잡힘**.
  - B(kk): 통과 OFF 전환이 클릭에 **선행해야** 함 → 막 진입한 대상을 같은 프레임에 빠르게 누르면 토글이 아직 ON이라 **첫 클릭이 아래 창으로 통과될 수 있음**(frame-ordering 의존).
- **클릭 타겟 정의**
  - A(koko): 타겟 = **렌더된 픽셀 색**. 키 색(검정)으로 그려진 부분은 클릭 불가(투명·통과) — 가시 아트의 색 규약에 묶임.
  - B(kk): 타겟 = **콜라이더**. 렌더 픽셀과 무관 → 투명/키색으로 보이는 영역도 콜라이더만 있으면 클릭 가능하고, 판정 조건을 C#에서 추가·제어할 수 있음. 대신 콜라이더를 아트와 맞춰 유지해야 함.

---

## 5. 채택 결정

본 프로젝트는 develop 통합 과정에서 **윈도우 레이어를 koko 구현(`OverlayWindow` 스택)으로 일원화**했다. 두 윈도우 시스템은 같은 HWND를 점유하므로 공존할 수 없어 하나를 골라야 했고, 윈도우·환경 영역은 koko가 담당해 온 맥락에 따라 koko 구현을 기준으로 삼았다.

이 결정에 따라 **클릭 통과 방식은 koko의 ColorKey(`LWA_COLORKEY`) 방식(방식 A)을 채택**한다.

- 채택 구현: `Platform/Window/OverlayWindow.cs` + `OverlayWindowController.cs` (모드: `Normal` / `PassThrough` / `EditRegion`)
- kk의 `WindowManager.cs`(방식 B, hover-aware 토글)는 파일은 보존하되 통합 씬(`GameScene`)에서는 사용하지 않는다.

§1~§4는 향후 작업·논의 시 두 접근의 사실관계를 참조하기 위한 기록이다.
