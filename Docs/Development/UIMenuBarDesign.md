# 우하단 메뉴 바 + 메인 UI 화면 설계 (초안)

> 작성: develop-kk / 상태: **초안(토의용)** / 최종 검토 전 3회 적대 검수 완료.
> 이 문서는 **현서와 크로스체크**할 항목(특히 §7 CanvasScaler)을 포함합니다. 확정 아니고, 같이 다듬는 출발점입니다.

Figma: `CCL_8th_Cozy_Companion` (Inventory / Shop / Collection / Picture / Option 프레임 참고).

---

## 1. 목적과 범위

### 만들려는 것
- 화면 **우하단에 세로로 쌓인 버튼 바** (버튼 6개, 6번째는 미정이라 5 + TBD).
- 각 버튼을 누르면 대응하는 **메인 UI 화면(패널)** 이 뜬다.
- 메인 화면은 **한 번에 하나만** 보이고(상호배제), 6개 오브젝트는 모두 메모리에 상주하며 표시/숨김만 바뀐다.

### 이번 범위 **밖** (별도 논의)
- 각 메인 화면의 **내부 구성**(인벤토리 슬롯, 샵 상품 카드 등). 지금은 껍데기 패널만.
- **잠금(방해금지) 기능** — 선행 조건이 없어서 이번엔 뺀다. §6-A 참고.
- 6번째 버튼이 무엇인지.

### 핵심 관심사 (원 요구)
> "WindowResize / CameraFitter 등 창 단위 액션이 UI 위치·상호작용에 일으킬 수 있는 버그"를 중심으로 구조를 설계한다.

이 부분은 §6에서 집중적으로 다룬다.

---

## 2. 지금 씬(GameScene)의 실제 상태 — 먼저 맞춰두기

설계 얘기 전에, **현재 씬에 실제로 뭐가 있는지**부터 GUID로 확인한 사실을 공유한다. (인스펙터 값이 아니라 디스크의 씬 파일을 직접 읽음.)

| 항목 | 실제 상태 |
|---|---|
| **Canvas가 2개** | ① `UIRoot` — Overlay, CanvasScaler = **Constant Pixel Size / 800×600**(= Unity 기본값 그대로), `UIManager`가 여기 붙음. ② `CharacterScaleSetters` — 테스트용(`CharacterScaleClicker`), **Scale With Screen Size / 1920×1020 / Match=Width**, SortingOrder **100** |
| **CameraFitter** | 씬에 **있음**. 리사이즈마다 카메라를 다시 맞춤(refit). → UI가 신경 써야 할 실재 컴포넌트 |
| **WindowAspectFitter** | 씬에 **없음**. 즉 32:3 가로 띠 강제는 이 씬엔 적용 안 됨 |
| 창 스택 | `OverlayWindow` + `OverlayWindowController` + `WindowResizeHandler` + `WindowDebugOverlay` + `OverlayModeDemoButtons` + `WindowsCursorToUnityScreen` |
| 기본 창 크기 | 거의 전체화면 (`OverlayWindow.SetFullscreen`: 화면폭 × (화면높이-90)). EditRegion에서 리사이즈 가능(최소 200×200) |
| UI 입력 모듈 | `InputSystemUIInputModule` 존재 (New Input System용, 올바름) |
| 아직 씬에 없는 것 | `UIPanel` / `SettingsPanel` / `Phase0InputProbe` (전부 새 파일, 미배치) |

**현서에게 두 가지 확인 요청:**
1. `CharacterScaleSetters` 캔버스(SortingOrder 100, 테스트용)는 디버그 잔재로 보이는데, **지워도 될까?** 안 지우면 SortingOrder 100이라 새 메뉴 UI 위에 그려진다. 또 이 캔버스의 `CharacterScaleClicker`가 `ScaleMultiplierSettings.Character`에 값을 쓰는데, 설정 패널의 캐릭터 크기 슬라이더도 같은 값을 써서 나중에 충돌 소지가 있다.
2. 그 캔버스의 **1920×1020 / Match=Width**는 의도적으로 고른 값 같은데, 이유가 궁금하다(§7에서 이어짐).

---

## 3. 오브젝트 계층 모식도

새 UI는 **`UIRoot`(UIManager가 붙은 Overlay Canvas) 아래**에 둔다. Canvas를 **Screen Space - Overlay**로 두는 게 이 설계의 핵심 결정이다 — 이유는 §6(CameraFitter 분리).

```
EventSystem (InputSystemUIInputModule)          ← 이미 있음
UIManager (GameObject)                           ← 이미 있음
Canvas [Screen Space - Overlay]  = UIRoot        ← 이미 있음
├─ CanvasScaler                    ← §7 결정 후 확정 (지금은 Constant)
├─ GraphicRaycaster
│
├─ MenuButtonBar                   ← 신규. 우하단 앵커, 가장자리서 24px+ 안쪽, VerticalLayoutGroup
│   ├─ BarBackground (Image)                 ← Figma 세로 레일 배경. 순수 검정(0,0,0) 금지(§6)
│   ├─ Btn_Inventory  (Button + Image)
│   ├─ Btn_Shop
│   ├─ Btn_Collection
│   ├─ Btn_Picture
│   ├─ Btn_Option
│   └─ (6번째, TBD)
│
└─ Panels (중앙 컨테이너)
    ├─ Panel_Inventory   (UIPanel 그대로)
    ├─ Panel_Shop        (UIPanel 그대로)
    ├─ Panel_Collection  (UIPanel 그대로)
    ├─ Panel_Picture     (UIPanel 그대로)
    ├─ Panel_Option      (= SettingsPanel, 기존)
    └─ (6번째, TBD)
```

**앵커**: `MenuButtonBar`의 RectTransform 앵커와 피벗을 우하단 (1, 0)에 두면, 창을 리사이즈해도 바가 우하단에 자동으로 붙는다. 위치 계산 스크립트가 필요 없다.

---

## 4. 클래스 구조

### 4-1. 유니티 초심자용 결론 먼저
- **SpriteRenderer·Animator 필요 없음.** `SpriteRenderer`는 월드(캐릭터)용이고, UI에선 `Image`를 쓴다. hover 강조 + 눌린 이미지 교체는 **uGUI `Button`의 SpriteSwap 트랜지션**이 스크립트 0줄로 해준다(Highlighted / Pressed 스프라이트만 꽂으면 됨). Animator는 여러 프레임 애니메이션이 필요할 때만이고, 항상 떠 있는 앱에 버튼마다 Animator를 물리는 건 낭비라 안 쓴다.
- **6개 화면을 다 분리하는 게 맞다.** `UIManager`가 이미 "열린 패널 1개"만 추적하는 구조라 분리가 자연스럽고, "배경만 두고 내용 교체"는 관리가 더 복잡해진다.

### 4-2. 클래스 표

| 클래스 | 상태 | 책임 |
|---|---|---|
| `UIManager` | **재사용 (변경 0)** | 싱글톤. 열린 패널 1개 추적, `Open`(다른 건 닫고 교체)/`Close`/`Toggle`, ESC 닫기. → 요구사항 "한 번에 1개"·"토글"을 **이미 충족** |
| `UIPanel` | **재사용 + 필드 1개** | `CanvasGroup`으로 여닫기(현행 유지). 공통 `[SerializeField] Button _closeButton`(선택) 추가 → Awake에서 있으면 `Close(this)` 연결. Figma의 각 패널 X 버튼용 |
| `SettingsPanel` | **재사용 + 정리** | `Panel_Option` 역할. `_openButton`·`_lockButton`·`_uiScaleSlider` 배선 **제거**(사유는 §6-A, §7). `_editRegionButton`·`_fullscreenButton`·캐릭터 크기 슬라이더는 유지 |
| `MenuButtonBar` | **신규** | 버튼↔패널 쌍 배열을 들고, 각 버튼 클릭을 `UIManager.Toggle(panel)`에 연결. 매 프레임 로직 없음 |
| `DraggableObject2D` | **수정 1줄** | 드래그 시작 조건에 "UI 위가 아닐 때"만 통과하는 가드 추가(§6-드래그 누수) |

### 4-3. 신규 `MenuButtonBar` 개요

```csharp
public class MenuButtonBar : MonoBehaviour
{
    [System.Serializable]
    public struct Entry { public Button button; public UIPanel panel; }

    [SerializeField] private Entry[] _entries;   // 5개 + 6번째는 정해지면 추가(가변)

    private void Awake()
    {
        foreach (var e in _entries)
            if (e.button != null)
                e.button.onClick.AddListener(() => UIManager.Instance.Toggle(e.panel));
    }

#if UNITY_EDITOR
    private void OnValidate()   // 셋업 실수(빈 배선) 조기 발견 — 코드베이스 컨벤션
    {
        foreach (var e in _entries)
            if (e.button == null || e.panel == null)
                Debug.LogWarning($"[MenuButtonBar] Entry에 빈 참조가 있습니다 ({name}).", this);
    }
#endif
}
```

- 버튼의 hover/눌림 시각은 여기서 안 건드린다 — uGUI Button SpriteSwap이 담당.
- 배선은 **코드(AddListener)**로 한다. 인스펙터 드래그 연결은 씬 파일에 숨어 리뷰/머지에 안 보이기 때문(SettingsPanel과 같은 방침).

### 4-4. 함수 책임 한눈에

| 기능 | 담당 | 방식 |
|---|---|---|
| 버튼 hover/눌림 비주얼 | uGUI Button | SpriteSwap (스크립트 0) |
| 버튼 클릭 → 패널 열기/전환 | `MenuButtonBar` → `UIManager` | `Toggle` / `Open` |
| 패널 표시·숨김 + 상호배제 | `UIManager` + `UIPanel` | 현행 |
| 패널 X 닫기 | `UIPanel._closeButton` → `UIManager.Close` | 코드 배선 |
| 리사이즈 시 위치 | RectTransform 우하단 앵커 | 자동 |
| 리사이즈 시 크기 | CanvasScaler | §7 결정 후 |
| 드래그 누수 방지 | `DraggableObject2D` 가드 | 코드 1줄 |

---

## 5. 숨김 방식 (성능)

요구사항: "숨김/표시는 성능상 더 나은 방식으로, Always-On-Top / per-pixel Click-Through를 해치지 않게."

- **기본안: 현행 `CanvasGroup.alpha = 0` 유지.** `UIPanel`이 이미 이 방식이다. 숨길 때 alpha 0 + 클릭 차단으로 끄므로 상태(슬라이더 값 등)가 보존되고, Always-On-Top·클릭 투과에 아무 영향이 없다(Normal 모드에서 Overlay UI의 불투명 픽셀은 그대로 클릭됨).
- **주의**: alpha=0이어도 숨은 패널은 드로우콜을 낸다. 5개가 항상 그려지는 셈이라, **나중에 프로파일링에서 문제로 잡히면** 그때 각 패널을 nested Canvas로 감싸 `Canvas.enabled`를 alpha와 함께 끄는 방식으로 올리면 된다(상태·API 그대로, 추가만). **지금은 재작성하지 않는다** — 측정 없이 최적화부터 하지 않는다.

---

## 6. 창 액션 side-effect 분석 (핵심)

창 모드와 리사이즈가 UI에 어떻게 얽히는지 정리한다. 이게 이 설계의 진짜 목적이다.

### 6-1. CameraFitter / 리사이즈 → **Overlay로 완전 분리**
- `CameraFitter`는 리사이즈 때 `Screen.width/height`만 읽어 **카메라**의 orthoSize·위치·aspect만 바꾼다. 어떤 Canvas·RectTransform도 건드리지 않는다(코드로 확인).
- UI Canvas를 **Screen Space - Overlay**로 두면 카메라를 아예 안 쓰므로, CameraFitter가 뭘 하든 UI는 영향이 **0**이다.
- 리사이즈 시 UI 위치는 RectTransform 앵커가 알아서 붙인다. → **원 요구의 "CameraFitter 상호작용 버그"는 이 결정으로 구조적으로 차단된다.**

> 만약 UI를 Screen Space - Camera나 World Space로 뒀다면 CameraFitter가 UI를 같이 흔들어 버그가 났을 것이다. Overlay가 정답인 이유.

### 6-2. 창 모드 3종과 UI 클릭 가능성

| 모드 | 창 상태 | UI 클릭? | 이번 설계 대응 |
|---|---|---|---|
| **Normal** | 투명 + ColorKey. 캐릭터·UI 불투명 픽셀은 클릭됨, 빈 곳은 통과 | **O** | 정상 동작 |
| **PassThrough(잠금)** | 창 전체 클릭 통과(WS_EX_TRANSPARENT) | **X** (UI도 통과됨) | 이 슬라이스에서 **제외** (§6-A) |
| **EditRegion** | 불투명 + 가장자리 리사이즈 핫존 | O | 바를 가장자리서 **24px+ 안쪽**에 (§6-B) |

### 6-A. 잠금(PassThrough)을 이번에 빼는 이유 — 선행 조건 부재
잠금에 들어가면 창 전체가 클릭 통과가 된다. 그러면 잠금을 푸는 유일한 인앱 경로인 잠금 버튼조차 **눌리지 않는다.** 그런데 현재 코드에는 **잠금을 풀어주는 전역 핫키 구독이 아예 없다**(`ToggleMode`를 부르는 건 설정의 잠금 버튼과 데모뿐). 즉 지금 잠금을 켜면 **앱 강제종료 말고는 빠져나올 방법이 없다.**

→ 이번 슬라이스에서는 `SettingsPanel`의 `_lockButton` 배선을 **코드에서 제거**한다(인스펙터에서 실수로 연결해 밟는 사고를 원천 차단). 잠금은 나중에 **전역 핫키 잠금해제(`GlobalKeyInput`/`OutFocusKeyHook` → `ToggleMode`)를 Platform/Gameplay에 먼저 만든 뒤** 재도입한다. (별도 작업 항목)

### 6-B. EditRegion과 버튼 바의 겹침
EditRegion에선 창 가장자리가 리사이즈 핫존이다(변 6px, 모서리 12px). OS가 이 영역 클릭을 uGUI보다 **먼저** 가로채므로, 버튼이 모서리에 딱 붙으면 그 버튼은 죽는다. → **바를 가장자리에서 24px 이상 안쪽**에 두면 충돌이 없다. (EditRegion 종료는 `RegionEditChrome`의 중앙 "완료" 버튼으로 가능하니, 바를 굳이 EditRegion에서 숨길 필요는 없다.)

### 6-C. ColorKey 검정 구멍
per-pixel 투과는 순수 검정 (0,0,0) 픽셀을 투명 + 통과로 처리한다. → **UI 아트에 순수 검정을 쓰지 말 것**(짙은 회색으로 회피). 배경 이미지·아이콘 모두 해당.

### 6-D. 좌표 freeze로 인한 hover 잔류 (사소)
투명(빈) 픽셀 위에선 `Mouse.current`가 멈춘다. 불투명한 바 위에선 정상 갱신되므로 버튼 hover는 잘 뜬다. 다만 **바에서 빈 공간으로 곧장 커서가 빠질 때** 하이라이트가 잠깐 남을 수 있다(기능 문제 아님, 다른 불투명 위로 가면 즉시 정상화). 클릭에는 영향 없다.

### 6-E. 드래그 누수 — 실재하는 버그, 1줄로 막음
`DraggableObject2D`는 입력 매니저를 안 거치고 **자체로** 마우스를 폴링한다. 그래서 "포인터가 UI 위인가" 검사를 안 한다. **패널이 열려 있어도 그 뒤 캐릭터가 드래그로 끌려오는** 버그가 난다. → 드래그 시작 조건에 아래 한 줄을 추가한다:

```csharp
// 47행 근처, press 시작 조건에 추가
&& (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject())
```
(`using UnityEngine.EventSystems;` 추가.) 진행 중인 드래그는 첫 프레임에만 판정하므로 끊기지 않고, 캐릭터 위에 UI가 없을 때(정상 드래그)는 방해하지 않는다.

---

## 7. 현서 크로스체크 항목 — CanvasScaler 정책 (열린 질문)

**이게 이 문서에서 제일 같이 정해야 하는 부분이다.**

### 상황
- `UIRoot`(우리 메뉴가 들어갈 캔버스)는 **Constant Pixel Size / 800×600** 인데, 이건 Unity가 캔버스 만들면 나오는 **기본값 그대로**라 아무도 의도적으로 고른 게 아닐 가능성이 크다.
- 반면 테스트 캔버스는 **Scale With Screen Size / 1920×1020 / Match=Width** 로, **누군가 의도적으로** 설정했다. (현서?)

### 두 모드의 차이
| 모드 | 창을 키우면/줄이면 | 문제 |
|---|---|---|
| **Constant Pixel Size** | UI가 픽셀 고정 (안 커지고 안 줄어듦) | 작게 리사이즈한 창(최소 200×200)에서 6버튼 바 + 중앙 패널이 **창을 넘치거나 덮음**. 4K에선 UI가 상대적으로 작아짐 |
| **Scale With Screen Size** | 기준 해상도 대비 비율로 UI가 같이 커지고 줄어듦 | 넘침 없음. 단 `scaleFactor` 기반 슬라이더는 무의미해짐 |

### 잠정 권장
- **`UIRoot`를 Scale With Screen Size로.** 이 씬은 32:3 띠 강제가 없고(WindowAspectFitter 미배치) 창이 전체화면~축소까지 넓게 변하므로, 자동 축소가 되는 쪽이 안전하다. 참고로 `CameraFitter`는 캐릭터의 **화면 픽셀 크기를 창 크기와 무관하게 고정**한다 — UI도 극단적으로 안 변하는 게 캐릭터와 톤이 맞는다.
- **기준 해상도·Match 축은 현서 의견이 필요.** 테스트 캔버스가 이미 `Match=Width / 1920×1020`을 쓰는데, 이게 의도라면 메뉴 UI도 거기 맞추는 게 일관적이다. 다만 **왜 1080이 아니라 1020인지, 왜 Height가 아니라 Width인지** 최초 의도를 알아야 우리가 맞추든 바꾸든 결정할 수 있다.

### 참고: 처음에 내가 틀렸던 점 (투명하게 공유)
초안에서 나는 "Match=Height / 1080"을 권했는데, 적대 검수에서 **이 프로젝트엔 그게 오히려 나쁠 수 있다**는 지적을 받았다. (만약 가로로 긴 창이 지배적이라면 Height 기준은 UI를 과하게 줄인다.) 그래서 지금은 **현서가 이미 고른 Width 기준이 더 맞을 수 있다**고 보고, 단정 대신 크로스체크로 남긴다.

### 딸린 변경
- `SettingsPanel`의 "UI 크기" 슬라이더(`_uiScaleSlider → canvasScaler.scaleFactor`)는 **Constant 모드에서만** 동작한다. Scale With Screen Size로 가면 이 배선은 죽는다. 그래서 이번 슬라이스에서는 이 배선을 **일단 제거**하고, CanvasScaler 정책이 확정된 뒤 그 모드에 맞는 방식으로 다시 붙인다.

---

## 8. 구현 계획 — 2트랙

이 기능은 **코드로 되는 부분**과 **사람이 유니티 에디터에서 해야 하는 부분**이 갈린다. (Claude는 `.unity` 씬 파일을 직접 편집하지 않는다 — 씬 규칙.)

### 트랙 1 — 코드 (Claude가 작성/수정 가능)
1. `MenuButtonBar` 신규 작성 → **verify**: 컴파일 통과, OnValidate 경고 로직 포함.
2. `UIPanel`에 `_closeButton`(선택) 필드 + Awake 연결 → **verify**: 컴파일 통과, 기존 여닫기 불변.
3. `SettingsPanel` 정리: `_openButton`·`_lockButton`·`_uiScaleSlider` 배선 제거 → **verify**: 컴파일 통과, `_editRegionButton`/`_fullscreenButton`/캐릭터 슬라이더 유지.
4. `DraggableObject2D` 드래그 가드 1줄 추가 → **verify**: 컴파일 통과.

> 코드 트랙 완료 기준 = **컴파일 성공 + 경고 없음 + 기존 참조 안 깨짐.**

### 트랙 2 — 씬 구성 + 빌드 검증 (사람이 에디터에서)
> 주의: 아래 검증은 **빌드에서만** 유효하다. 에디터에선 투명·클릭통과·리사이즈가 `#if !UNITY_EDITOR`로 꺼져 있어 확인이 안 된다.

1. `UIRoot` 아래 `MenuButtonBar`(우하단 앵커, 24px inset) + 버튼 + `Panels` 구성, 버튼 SpriteSwap 스프라이트 지정.
2. (선행 게이트 **G0**) `Phase0InputProbe`를 `UIRoot`에 붙여 빌드 → 투명 오버레이에서 **버튼 클릭이 수신되고** `IsPointerOverGameObject`가 버튼 위에서 true인지 확인. **이게 통과돼야** 이 설계 전체(표준 uGUI 전제)가 성립한다.
3. G1: 6버튼을 순서대로 눌러 **해당 패널만** 뜨는지(동시 2개 없음).
4. G2: 같은 버튼 재클릭 / X 버튼 → 닫힘.
5. G3: 창을 작게~전체화면으로 리사이즈 → 바가 우하단 유지 + 창 밖으로 안 넘침(§7 정책에 의존).
6. G4: 패널 열린 채 그 위를 드래그 → 뒤 캐릭터가 안 움직임.

---

## 9. 확정된 결정 요약

| # | 결정 |
|---|---|
| A | 잠금(PassThrough)은 이번 제외. `SettingsPanel._lockButton` 배선 제거. 전역 핫키 잠금해제 구현 후 재도입 |
| B | `Scripts/CLAUDE.md`의 UI 레이어 참조 제한 제거 완료 — UI는 하위 레이어(Platform 포함)를 자유 참조 |
| C | **미확정.** CanvasScaler 정책은 §7대로 현서와 크로스체크 후 확정 |

---

## 부록: 적대 검수 이력

이 설계는 독립 리뷰어(별도 에이전트)에게 **총 5회** 적대적 검수를 받으며 다듬었다. 주요 교정:
- **UIModeGate(모드별 UI 자동 숨김 컴포넌트)를 폐기** — 상태 꼬임 + 레이어 위반 유발. 정적 규칙(inset, 잠금 제외)으로 대체.
- **씬 모델 정정** — 초안이 "Canvas 1개 / 32:3 띠"로 잘못 봤으나, 실제로는 Canvas 2개 + CameraFitter 있음 + WindowAspectFitter 없음. §2가 실측 반영본.
- **CanvasScaler 권장을 Height→미확정으로 하향** — 이 프로젝트 창 형태엔 Width 기준이 더 맞을 수 있어 현서 크로스체크로 전환(§7).
- **과설계 제거** — `_closeButton`용 상속형 OnValidate 등 검수 대응으로 붙였던 복잡성을 다시 걷어냄(선택 필드엔 경고 대상이 없음).
