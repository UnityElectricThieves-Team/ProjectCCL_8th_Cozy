# 투명 배경 구현을 위한 스터디 기록

2026-05-24

Project Cozy의 핵심 컨셉인 *투명 배경 + 클릭 투과*를 [WindowManager](../../Project_Cozy/Assets/Scripts/Platform/Window/Core/WindowManager.cs)에 통합하는 작업을 진행하면서, *원리적으로는 표준 패턴인데도 Unity 6 / URP 17 환경에서 단순히는 작동하지 않는* 다층 함정을 만났다. 이 글은 그 시행착오와 최종적으로 작동에 도달한 정확한 설정 조합을 기록한다. 클릭 투과의 입력 처리 자체는 [WS_EX_TRANSPARENT ON 시 Unity의 마우스 입력 감지 (2026-05-23)](WsExTransparentMouseInputStudy.md)에 별도로 정리되어 있으므로 여기서는 *배경 투명화*에 집중한다.

## 기획 요구사항

- 게임이 작동되는 동안에도 다른 윈도우와 자유롭게 상호작용 가능해야 함.
- 화면 어디에든 위치할 수 있는 object(별·달 등) 및 추후 윈도우 아이콘·작업표시줄과의 상호작용 가능성을 고려해, 게임 윈도우는 화면 전체(또는 거의 전체)를 점유.
- 윈도우가 화면 대부분을 차지함에도 다른 앱 사용을 가로막지 않기 위해, 게임 윈도우의 대부분은 시각적으로 투명해야 함.
- 시각적으로 투명한 영역, 또는 마우스 아래에 인터랙터블 object가 없는 영역에서는 클릭이 게임 윈도우를 통과해 뒤의 윈도우로 전달되어야 함.

## 구현 아이디어

- 마우스 위치에서 콜라이더 충돌 여부를 매 프레임 판정 → 결과에 따라 `WS_EX_TRANSPARENT`(click-through) 비트를 동적 ON/OFF.
- `SetWindowLong` 호출(=비트 토글)은 OS 콜이라 매 프레임 호출은 비용이 큼 → 판정은 매 프레임 하되 *상태가 변할 때만* OS 콜이 일어나도록 캐싱해서 호출 횟수 최소화.
- Main Camera 배경을 `(0,0,0,0)`으로 클리어 → DWM 알파 합성기가 빈 영역을 데스크톱과 자연스럽게 합성.

## 구현 제약사항

- **`WS_EX_TRANSPARENT`가 ON인 동안에는 Unity InputSystem이 마우스 위치를 받지 못한다.** OS가 마우스 메시지를 우리 창에 전달하지 않기 때문(그게 click-through의 본질). 즉 Unity InputSystem에 의존해 *호버 여부를 판단해 OFF로 돌리는 것*이 불가능 — 한 번 ON 되면 다시 OFF될 수 없는 닭-달걀 문제. 해결: Win32 `GetCursorPos`로 글로벌 마우스 위치를 직접 받아 콜라이더 충돌 판정. (자세한 검증은 [2026-05-23 글](WsExTransparentMouseInputStudy.md) 참조)
- **URP의 디폴트 렌더링 경로는 알파 채널을 보존하지 않는다.** RT/백버퍼 포맷을 *알파 없는 포맷*으로 최적화하는 것이 디폴트라, 카메라가 `(0,0,0,0)`으로 클리어해도 최종 백버퍼엔 알파 1이 도달 → DWM이 합성할 알파 정보가 없어 통째로 불투명 처리됨. Unity Issue Tracker에 *"By Design"*으로 등록되어 있고, *Post Processing을 끄는 것만으로는 해결되지 않는다*. 우회 설정 다수 필요.
- **DXGI Flip Model Swapchain이 켜져 있으면 백버퍼가 DWM 합성을 우회하고 화면에 직접 표시된다**(Independent Flip 모드). 알파 보존을 아무리 잘 해도 DWM이 합성 단계에 끼어들지 못해 투명이 작동하지 않음. Unity 디폴트로 ON되어 있어 *반드시 명시적으로 OFF*해야 한다. 또한 이 토글은 *DX11에서만 끌 수 있으며*, DX12에서는 Flip Model이 강제되므로 DX12 빌드 자체가 사용 불가능하다(아래 §최종 구현 방법 참조).

## 시행착오 기록

처음엔 단순했다. `BorderlessWindow.cs`(레거시)의 패턴을 그대로 따라 `WindowManager`에 `WS_EX_LAYERED` 추가 + `DwmExtendFrameIntoClientArea(-1,-1,-1,-1)` + 카메라 `(0,0,0,0)` 검정 클리어를 했고, *작동할 것으로 기대*했다. 결과는 새카만 화면.

그 후 *증상 토글*을 연쇄로 시도했다. Post Processing OFF, HDR OFF, Camera 알파 0 재확인, Graphics API를 Direct3D 11로 강제 — *당시엔 전부 효과 없음*. (이 중 Direct3D 11 강제는 그 시점엔 무용해 보였지만, 결국 *진짜 핵심 전제*임이 나중에 밝혀진다.) 이 시점에서 ColorKey(`SetLayeredWindowAttributes(LWA_COLORKEY)`)로 우회하는 안을 검토했지만, 반투명·드롭섀도우 표현이 원천 불가능하고 외곽선에 fringe가 생기는 등 시각 품질 손실이 결정적이라 기각.

다음으로 *Gemini와의 교차 검증*에서 두 가지 중요한 단서를 얻었다. (1) `WS_EX_LAYERED`와 DWM frame extension은 Windows 8 이후 양립 가능하다 — *"둘은 양립 불가"* 라고 단정한 것이 잘못된 통념이었음. (2) URP의 알파 손실은 `m_AllowPostProcessAlphaOutput`이라는 URP 17 신규 옵션과 관계됨. 이 옵션을 `1`로, 함께 `m_PrefilterAlphaOutput`을 `0`으로 (알파 셰이더 변형이 빌드 prefilter에서 제거되지 않도록) 변경했다. 그래도 새카만 화면.

이 단계에서 *외부 사례 검색*으로 방향을 틀어 [OnyxAmber/UnityDesktopPetFramework](https://github.com/OnyxAmber/UnityDesktopPetFramework) (URP 데스크톱 펫 프레임워크 — UberPost.shader 패치 방식)와 [kirurobo/UniWindowController](https://github.com/kirurobo/UniWindowController) 등 검증된 오픈소스를 찾았다. 이들의 공통 요구사항은 *우리가 이미 적용한 알파 보존 설정과 동일*했고, 그럼에도 작동하지 않는다는 점에서 *어떤 메커니즘적 차단*이 우리 환경에 추가로 있다는 게 분명해졌다.

마지막 결정타는 두 가지가 동시에 풀렸다.

첫째, `ProjectSettings.asset`을 직접 열어 검토하던 중 `useFlipModelSwapchain: 1`을 발견했다. **DXGI Flip Model은 백버퍼를 DWM 합성에서 빼내 화면에 직접 표시**한다. 이게 켜져 있는 한 *어떤 알파 옵션도 의미가 없다*. Unity는 이 옵션이 기본 ON이며, 일반 게임에선 성능 이득이라 그대로 두는 게 보통이다 — 우리처럼 *DWM 합성에 의존하는 앱*에서만 끄야 한다.

둘째, 카메라 BackgroundColor를 디스크에서 직접 확인한 결과 `(0.502, 0.502, 0.502, 0.49)` 회색 + 반투명으로 저장되어 있었다. 인스펙터에서는 분명히 `(0,0,0,0)`으로 보였지만, **씬 변경을 `Ctrl+S`로 저장하지 않은 상태에서 빌드했기 때문에 디스크에는 이전 값이 남아 있었다.** Unity는 인스펙터 변경을 자동 저장하지 않는다. 이 두 가지를 동시에 잡고 빌드하니 곧바로 작동했다.

마지막으로 *DX12 사용 가능성*도 후속 검증했다. `useFlipModelSwapchain` 토글의 정식 이름이 *"Use Flip Model Swapchain **for D3D11**"*인 데서 추측한 가설 — *DX12에서는 Flip Model이 강제되어 우리가 끌 수 없을 것* — 을 직접 확인하기 위해, 다른 모든 설정은 그대로 두고 그래픽 API만 Direct3D 12로 바꿔 빌드했다. 결과는 다시 새카만 화면. 즉 **DX12 빌드는 우리 메커니즘으로 투명화 자체가 불가능**하며, Direct3D 11이 *권장*이 아니라 *필수*임이 확인됐다.

## 최종 구현 방법

### Project Settings → Player

| 항목 | 값 | 위치 |
|---|---|---|
| **Use DXGI Flip Model Swapchain for D3D11** | **OFF** | Resolution and Presentation |
| Fullscreen Mode | Windowed | Resolution and Presentation |
| Auto Graphics API for Windows | OFF | Other Settings |
| **Graphics APIs for Windows** | **Direct3D11 단독** (DX12 사용 금지) | Other Settings |

> ⚠️ **DX12 사용 금지 (검증 완료).** `useFlipModelSwapchain` 옵션의 정식 이름이 *"Use Flip Model Swapchain **for D3D11**"*인 데서 알 수 있듯, **DX12에서는 Flip Model이 강제 적용**되며 *우리가 끌 수 있는 토글이 존재하지 않는다.* DX12 빌드는 백버퍼가 DWM 합성을 우회하므로 투명이 *원리적으로* 작동하지 않는다. 실제 빌드 테스트로도 확인됨. Direct3D 11 필수.

> ※ `useFlipModelSwapchain`은 Unity 디폴트가 ON이라 *새 프로젝트를 만들거나 Player Settings를 재생성하면 자동으로 켜져 있다.* 이 게임에선 *반드시* 끄야 한다.

### URP Asset (`Assets/Settings/UniversalRP.asset`)

| 항목 | 값 | YAML 키 |
|---|---|---|
| HDR | OFF | `m_SupportsHDR: 0` |
| Opaque Texture | OFF | `m_RequireOpaqueTexture: 0` |
| **Allow Post Process Alpha Output** | **ON** | `m_AllowPostProcessAlphaOutput: 1` |
| **Prefilter Alpha Output** | **OFF** | `m_PrefilterAlphaOutput: 0` |

> ※ `m_AllowPostProcessAlphaOutput`은 *Post Processing을 끈 것과는 완전히 별개*. 알파 보존의 마스터 스위치이며, 이게 꺼져 있으면 다른 설정이 다 정상이어도 알파가 사라진다.

> ※ Quality Settings의 모든 Quality Level이 *동일한 URP Asset*을 가리키는지 확인. 빌드 디폴트 Quality(Standalone = Ultra)가 다른 URP Asset을 가리키면 위 설정이 빌드에 적용되지 않는다.

### Main Camera (씬에 1개)

| 항목 | 값 |
|---|---|
| Background Type | Solid Color |
| Background | **(0, 0, 0, 0)** — RGB·알파 모두 0 |
| HDR Rendering | Off |
| Post Processing | Off |

> ⚠️ **카메라 설정 변경 후 반드시 `Ctrl+S`로 씬 저장.** Unity는 인스펙터 변경을 자동으로 디스크에 반영하지 않는다. 저장하지 않고 빌드하면 *디스크의 옛 값*이 들어가서, 인스펙터엔 검정 알파 0인데 빌드엔 회색 반투명이 들어가는 식의 어긋남이 발생한다. (이번 작업에서 가장 오래 헤맨 함정)

### WindowManager.cs 핵심 로직

- **Awake 1회**: `WS_EX_LAYERED` 추가 + `DwmExtendFrameIntoClientArea(-1,-1,-1,-1)` 호출. 이 조합으로 *클라이언트 전체*가 DWM의 sheet-of-glass 모드가 되어, 백버퍼의 *알파 < 1*인 픽셀이 데스크톱과 합성된다.
- **Update 매 프레임** (Hover-Aware Click-Through ON일 때만):
  1. Win32 `GetCursorPos`로 글로벌 마우스 좌표 획득 (Unity InputSystem은 click-through ON 동안 마우스를 못 받으므로 우회).
  2. 카메라 좌표 변환 → `Physics2D.OverlapPoint`로 콜라이더 충돌 판정.
  3. *직전 프레임과 결과가 다를 때만* `SetWindowLong`으로 `WS_EX_TRANSPARENT` 비트 토글.
- 결과: 콜라이더 위에서는 클릭이 게임에 도달, 빈 영역에서는 OS가 클릭을 뒤의 윈도우로 통과시킨다.

## 교훈

- **DWM 알파 합성은 *백버퍼의 알파 채널*에 의존한다.** "검정 픽셀이 투명"이라는 흔한 통념은 RGB가 아니라 알파의 부수 결과일 뿐이다. URP가 백버퍼에 알파를 전달하지 않으면 어떤 카메라 설정도 무의미하다.
- **Unity 6 / URP 17의 디폴트 백버퍼 경로는 알파를 보존하지 않는다.** 이는 Unity가 *공식적으로 "By Design"으로 등록한 한계*이며, `m_AllowPostProcessAlphaOutput` 같은 신규 옵션은 *우회 수단으로 추가*된 것이지 디폴트 켜지지 않는다.
- **DXGI Flip Model은 DWM 합성 자체를 우회한다.** 윈도우 오버레이/데스크톱 펫처럼 DWM 합성에 의존하는 모든 앱에서 이 옵션은 *반드시 끄야 한다*. 그리고 이 옵션이 DX11 한정이라는 사실 때문에, **DX12에서는 같은 우회가 불가능해 그래픽 API 선택 자체가 강제된다**.
- **Unity 씬은 `Ctrl+S` 안 누르면 디스크에 반영되지 않는다.** 인스펙터의 값과 빌드/디스크 파일이 완전히 다를 수 있어, 디버깅 시 *반드시 디스크 파일을 직접 읽어* 검증해야 한다.
- **디버깅에서 외부 통념·추측에 의존한 토글 연쇄는 시간만 낭비시킨다.** 메커니즘을 정확히 이해하지 못한 상태에서 *카메라 알파, Post Processing, DX 버전*을 차례로 끄는 식의 시도는 실제 원인 지점을 한 번도 건드리지 못했다. 막혔을 때는 *프로젝트 설정 파일을 직접 열어* 무엇이 어떻게 저장되어 있는지 보는 것이 가장 빠른 길이었다.
