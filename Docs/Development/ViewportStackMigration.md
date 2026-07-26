# 뷰포트 편집 스택 GameScene 이관 계획

> **범위**: `CameraFitter` → `BaseSpaceCameraFitter` 좌표 모델 교체와, 뷰포트 편집 스택
> (`ViewportScreenSettings` / `ViewportEditHandles` / `WindowMoveResizeGuide`)을 GameScene에 넣는 작업.
> **전제**: 창 스택(`WindowManager`)은 이미 GameScene에 들어가 있다 (커밋 `98738da`, `8c3de6f`).
> **배경 문서**: [WindowServiceHandover.md](WindowServiceHandover.md), [Docs/Planning/UserSettings.md](../Planning/UserSettings.md) §2.1.1
> **작성**: 2026-07-25 · 브랜치 `feature/viewport-stack`

---

## 1. 두 좌표 모델의 실측 대조

### 1.1 옛 모델 — `CameraFitter` (GameScene에 실제로 들어있는 값)

Main Camera(`Environment/Main Camera`)에 붙어 있고, 씬에 저장된 값은 다음과 같다.

| 필드 | 값 | 씬 라인 |
|---|---|---|
| `_minY` | 0 | GameScene.unity:1678 |
| `_maxY` | 100 | :1679 |
| `_referenceHeight` | 1080 | :1680 |

이 값으로 `Fit()`이 하는 일은 세 가지다.

```
worldPerPixel   = (_maxY - _minY) / _referenceHeight = 100 / 1080 = 0.0925926
orthographicSize = worldPerPixel × Screen.height × 0.5
camera.position  = (0, _minY + orthographicSize, -10)
camera.aspect    = Screen.width / Screen.height     ← 수동으로 고정됨
```

정리하면 **화면 1픽셀 = 0.0925926 월드 유닛**, 뒤집으면 **1 월드 유닛 = 10.8 화면 픽셀**이다.
그리고 앵커는 두 개다.

- **세로**: 창의 아래 변이 항상 월드 `y = 0` (`_minY`). 창 높이가 변해도 바닥은 붙박이다.
- **가로**: 창의 가로 중심이 항상 월드 `x = 0`. 즉 **가운데 정렬**이다.

### 1.2 새 모델 — `BaseSpaceCameraFitter`

```
orthographicSize = viewportPx.height / ppu × 0.5
baseLeft   = BR.x - baseSpaceSize.x / ppu
baseBottom = BR.y
camera.position = (baseLeft + (vx + vw/2)/ppu, baseBottom + (vy + vh/2)/ppu, z)
```

(`BR` = `_masterCanvasBottomRight`, `ppu` = `_pixelsPerUnit`)

여기서는 **1 월드 유닛 = ppu 화면 픽셀**이고, 앵커는 마스터 캔버스의 **우하단 한 점**이다.
가로 중심이 아니라 오른쪽 변이 기준이라는 게 옛 모델과 근본적으로 다른 지점이다.

### 1.3 두 모델이 실제로 어긋나는 곳

| 항목 | 옛 모델 | 새 모델 |
|---|---|---|
| 1픽셀의 뜻 | `_referenceHeight / (_maxY-_minY)` 에서 유도 → 10.8px/unit | `_pixelsPerUnit` 직접 지정 |
| 세로 앵커 | 창 아래 변 = `_minY` | 베이스 공간 아래 변 = `BR.y` |
| 가로 앵커 | 창 가로 중심 = `x=0` (**중앙**) | 베이스 공간 오른쪽 변 = `BR.x` (**우측**) |
| 보이는 영역 | 창 크기에 비례해 커짐 | 뷰포트 rect가 그대로 보이는 영역 |
| `camera.aspect` | 수동 고정 | 건드리지 않음 (창=뷰포트라 자동값이 맞음) |

**가로 앵커가 중앙에서 우측으로 바뀐 것이 핵심 난점이다.** 세로는 두 모델 모두 "아래 변 붙박이"라
숫자만 맞추면 정확히 일치하지만, 가로는 한쪽은 중앙 기준·한쪽은 우측 기준이라
**특정 해상도 하나에서만 일치시킬 수 있다.**

---

## 2. 현재 배치를 유지하는 값 (계산 결과)

### 2.1 `_pixelsPerUnit`

옛 모델의 픽셀당 월드 크기를 그대로 옮기면 된다.

```
ppu = _referenceHeight / (_maxY - _minY) = 1080 / 100 = 10.8
```

> **`_pixelsPerUnit = 10.8`**

검산: 캐릭터 스프라이트(`white-animal-cat-sprite-sheet.png`)는 프레임 256×256, 임포트 PPU 14다.
- 월드 크기 = 256 / 14 = 18.286 유닛
- 화면 크기 = 18.286 × 10.8 = **197.5 px** ← 지금 보이는 크기
- 만약 ppu를 설계 의도값인 100으로 두면 = 18.286 × 100 = **1829 px**. 9.26배로 부풀어 화면을 뚫는다.

### 2.2 `_masterCanvasBottomRight.y`

뷰포트가 베이스 공간 전체일 때(기본값) 새 모델의 아래 변 월드 y는:

```
camY - orthoSize = (BR.y + H/(2·ppu)) - H/(2·ppu) = BR.y
```

높이 `H`가 약분돼 사라진다. 옛 모델의 아래 변은 `_minY = 0`이므로:

> **`_masterCanvasBottomRight.y = 0`** — 해상도와 무관하게 정확히 일치한다.

### 2.3 `_masterCanvasBottomRight.x`

뷰포트가 베이스 공간 전체(폭 `W`)일 때 새 모델의 가로 중심은:

```
camX = BR.x - W/ppu + W/(2·ppu) = BR.x - W/(2·ppu)
```

옛 모델의 가로 중심은 `0`이므로:

```
BR.x = W / (2 × 10.8) = W / 21.6
```

> **해상도에 따라 값이 달라진다.**

| 모니터 가로 해상도 W | `_masterCanvasBottomRight.x` |
|---|---|
| 1920 | **88.8889** |
| 2560 | 118.5185 |
| 3840 (마스터 캔버스 원본) | 177.7778 |

`WindowManager._maxSize`가 1920×1080으로 잡혀 있는 걸 보면 개발 기준 모니터는 1920×1080이다.
따라서 **작업용 권장값은 `(88.8889, 0)`**이다.

이 값을 쓰면 마스터 캔버스 전체(3840×2160)는 월드 좌표로
`x ∈ [-266.667, 88.889]`, `y ∈ [0, 200]`에 놓인다.
1920×1080 모니터의 베이스 공간은 그중 우하단 `x ∈ [-88.889, 88.889]`, `y ∈ [0, 100]`이고,
이건 옛 모델이 1080px 창에서 비추던 영역과 정확히 같다.

### 2.4 요약

```
_pixelsPerUnit          = 10.8
_masterCanvasBottomRight = (88.8889, 0)      ← 1920 가로 모니터 기준
```

---

## 3. 이 값이 완전히 무해하지 않은 세 지점

숫자를 맞춰도 남는 차이가 셋 있다. 셋 다 실제로 화면에서 보이므로 미리 알고 들어가야 한다.

### 3.1 작업 영역 → 모니터 전체 (세로로 ~48px 내려앉음)

지금 GameScene의 `WindowManager._maximizeToWorkArea`는 `1`이다. 즉 창이 **작업 영역**
(작업표시줄 위)을 채우고, 옛 모델의 월드 `y=0`은 **작업 영역 아래 변**이었다.

새 스택에서 `ViewportScreenSettings`의 기본 뷰포트는 "베이스 공간 전체" = **모니터 전체**다
(`TryGetMonitorRect`는 `rcMonitor`를 쓰며 작업표시줄을 포함한다). 그래서 월드 `y=0`이
**모니터 아래 변**으로 내려간다.

- 캐릭터 발 높이는 월드 `y = 7` (`Character.prefab:55`의 `_floorY`, `_footOffset`은 0).
- 화면상 = 7 × 10.8 = **바닥에서 75.6px 위**.
- 이관 전: 작업 영역 아래 변에서 75.6px 위 = 모니터 아래 변에서 75.6 + 48 = **123.6px 위**
- 이관 후: 모니터 아래 변에서 **75.6px 위**

→ **캐릭터가 작업표시줄 높이(Win11 100% 배율에서 보통 48px)만큼 아래로 내려와 보인다.**

대응 후보는 셋이었다.

- (a) 그대로 둔다. 새 모델에서는 "바닥 = 뷰포트 아래 변"이 정의이므로 이것도 맞는 동작이다.
- (b) `_masterCanvasBottomRight.y = -작업표시줄px / 10.8` (48px면 `-4.4444`)로 앵커를 내린다.
  월드 전체가 올라가 Star 등도 함께 움직이고, "마스터 캔버스 우하단"이라는 좌표 규약의 뜻이 흐려진다.
- **(c) `_floorY`를 `7 → 11.4444`로 올린다.** ← **채택 (2026-07-25, 빌드 확인 후)**

**채택: (c).** 캐릭터 하나만 영향을 받고 좌표 규약을 건드리지 않는다. `48 ÷ 10.8 = 4.4444`,
`7 + 4.4444 = 11.4444`. 이 값이면 발이 모니터 바닥에서 `11.4444 × 10.8 = 123.6px` 위에 서서
이관 전 화면상 위치와 정확히 같아진다.

> **주의 — 이 값은 작업표시줄을 추적하지 않는다.** 한 번 넣고 나면 "바닥은 모니터 아래 변에서
> 123.6px 위"라는 고정 상수일 뿐이다. 배율 150%로 작업표시줄이 72px인 기기에서는 캐릭터가
> 작업표시줄 안쪽에 서게 된다. 근본 해결은 §3.2(바닥을 뷰포트에서 유도)이며 그쪽 후속 작업에 속한다.

#### 3.1.1 작업표시줄을 덮지 않으려면 — 뷰포트로 잘라내는 방법

위 §3.1은 "월드가 48px 내려앉는 것"을 다뤘고, 이건 `_floorY`로 해결했다. 그런데 별개로
**창 자체가 작업표시줄을 덮는 것**이 거슬릴 수 있다. 이건 `_maximizeToWorkArea`를 다시 켜서
풀 문제가 아니다(§4.1 — WndProc이 죽는다). 대신 `ViewportScreenSettings._viewport`를
`(0, 48, 1920, 1032)`로 두면 된다. 베이스 공간은 원점이 **좌하단**이므로 `y = 48`은
"아래에서 48px 띄운다"는 뜻이고, 창은 작업표시줄 위에만 놓인다.

> **단 이건 "무엇을 보는지"를 바꾸는 것이지 "물건이 어디 있는지"는 바꾸지 않는다.**
> 월드 좌표는 그대로다. 뷰포트를 위로 48px 올렸으니 캐릭터도 화면상 48px 아래로 내려가 보이고,
> `_floorY = 11.4444`와 겹쳐 적용하면 오히려 이관 전보다 높아진다. 둘 중 하나만 쓸 것.

#### 3.1.2 창이 모니터를 다 덮으면 아래 6px 띠가 작업표시줄 클릭을 먹는다

`_resizable: 1`이고 창이 모니터 전체를 덮으면, 화면 **맨 아래 6px**이 리사이즈 핫존이 된다.
`HitTestCalculator.cs:34`의 `nearBottom = mouseY >= winBottom - edgeThicknessPx`이고
`WindowManager._edgeThicknessPx`가 `6`이기 때문이다. 이 띠는 작업표시줄 아이콘의 하단과 겹치므로
**아이콘 아래쪽을 클릭하면 게임이 리사이즈로 먹어버린다.**

회피 방법은 §3.1.1의 뷰포트 잘라내기(창을 작업표시줄 위로 올리면 겹칠 일이 없다)거나,
`_edgeThicknessPx`를 줄이는 것이다. 이번 이관 범위에서는 다루지 않는다.

### 3.2 바닥이 뷰포트를 따라가지 않는다 (기획서와의 불일치)

`UserSettings.md` §2.1.1은 이렇게 못박고 있다.

> **땅바닥(캐릭터가 서는 지면)은 항상 뷰포트 하단 변에 포함되며 뷰포트와 함께 이동한다.**

옛 모델은 이걸 공짜로 만족했다 — 창 아래 변이 언제나 월드 `y=0`이었으니, 바닥선(`y=7`)은
항상 창 아래에서 75.6px 위였다. 그런데 새 모델은 **월드가 절대 고정이고 뷰포트가 그 위를 미끄러진다.**
`_floorY`는 `BaseCharacterController`에 박힌 월드 상수(`7`)라 뷰포트를 따라가지 않는다.

구체적으로: 편집 모드에서 뷰포트를 위로 Δpx 올리면 바닥선은 뷰포트 아래 변에서
`75.6 - Δ` px 위에 놓인다. **Δ > 75.6px이면 바닥이 창 아래로 빠져나가고,
캐릭터는 창 밖(보이지도, 클릭되지도 않는 영역)에 서게 된다.**

이건 이번 이관으로 새로 생기는 문제가 아니라, 새 모델이 드러내는 미구현 항목이다.
근본 해결은 `_floorY`를 뷰포트에서 유도하는 것(= `ViewportApplied` 구독 → 살아있는 캐릭터의
바닥선 갱신)인데, 이는 핸드오버 §6 보류 8번과 **같은 선행 조건**에 막혀 있다 —
`CharacterManager`에 살아있는 캐릭터를 순회하는 공개 API가 없다(`AliveCount`만 있고 목록은 private).

→ **이번 이관 범위에서는 다루지 않는다.** 좌표 모델 교체에 캐릭터 매니저 API 개방까지 얹으면
실패 시 원인 분리가 안 된다. 대신 §6 검증에서 "아래 변을 올리는 조작"을 별도 항목으로 떼어
현상을 확인만 하고, 후속 작업으로 남긴다.

### 3.3 `_pixelsPerUnit = 10.8`은 "절대 픽셀 1:1"이 아니다

`BaseSpaceCameraFitter._pixelsPerUnit`의 툴팁은 *"스프라이트 임포트 PPU와 동일해야 절대 픽셀
1:1이 성립"*이라고 적혀 있고, 기획서의 마스터 캔버스도 "에셋을 1:1 절대 픽셀로 배치"를 전제한다.
프로젝트의 다른 곳은 이미 PPU 100 기준이다 — figma 배경 PNG(`spritePixelsToUnits: 100`),
`CanvasScaler.referencePixelsPerUnit: 100`.

즉 `10.8`은 **설계 의도값이 아니라, 지금 씬의 배치를 보존하기 위한 값**이다.
지금은 무해하다 — GameScene의 월드 스프라이트는 프리팹 인스턴스 둘(캐릭터 PPU 14, Star)뿐이고
씬 자체에는 `SpriteRenderer`가 하나도 없다. 배경은 ScreenSpaceOverlay 캔버스 위라 카메라와 무관하다.

하지만 나중에 PPU 100짜리 월드 배경이 들어오는 순간 어긋난다. 정규화하려면
비율 `100 / 10.8 = 9.2593`으로 월드 단위 수치를 전부 나눠야 한다.

| 대상 | 현재 | PPU 100 정규화 시 |
|---|---|---|
| 캐릭터 스프라이트 임포트 PPU | 14 | 129.63 |
| `_floorY` | 7 | 0.756 |
| `_gravity` | 20 | 2.16 |
| `_walkSpeed` / `_runSpeed` | 1.5 / 3.5 | 0.162 / 0.378 |
| `_groundContactThreshold` | 1 | 0.108 |
| `Visual`의 BoxCollider2D `m_Size` | 16 × 16 | 1.728 × 1.728 |
| `Shadow` localPosition.y | -0.2 | -0.0216 |
| Star 위치 / 스프라이트 PPU | (30, 60) / — | (3.24, 6.48) / ×9.2593 |
| `_masterCanvasBottomRight` | (88.8889, 0) | (9.6, 0) |

→ **별도 작업으로 분리한다.** 이번엔 `10.8`로 두되, 왜 100이 아닌지 코드/문서에 남겨
나중에 누가 "설계값은 100인데?" 하고 그것만 고치는 사고를 막는다.

---

## 4. 씬에서 함께 고쳐야 하는 것

값 계산과 별개로, 지금 씬 상태로는 새 스택이 **작동하지 않는다.** 아래 둘은 필수다.

### 4.1 `_maximizeToWorkArea`를 꺼야 한다 (필수)

`GameScene.unity:686`의 `_maximizeToWorkArea: 1`은 두 가지를 망가뜨린다.

1. **WndProc이 설치되지 않는다.** `WindowManager.Awake:185`가
   `wantResizable = _resizable && !_maximizeToWorkArea`로 계산하므로,
   `_resizable: 1`이어도 `false`가 되어 `InstallWndProc()`을 건너뛴다.
   → 상단 그립 창 이동 없음, 가장자리 리사이즈 없음, `WM_EXITSIZEMOVE` 없음
   → **`WindowRectChangedByUser`가 영원히 발행되지 않아 창 드래그 역동기화가 죽는다.**
2. **적용 경합.** `ApplyMaximizeAfterReady`(Awake 기점 10프레임)와
   `ViewportScreenSettings.Start`의 `ApplyNormal`(Start 기점 10프레임)이 같은 프레임에 겹친다.
   코루틴 등록 순서상 지금은 `ApplyRegion`이 나중에 이겨서 결과적으로는 맞지만,
   보장된 순서가 아니라 기대기엔 위험하다.

→ **`_maximizeToWorkArea`를 `0`으로.** 창 크기·위치의 주인은 이제 `ViewportScreenSettings`다.

### 4.2 편집 모드를 부를 진입점이 없다 (필수)

`EnterEdit` / `SaveEdit` / `CancelEdit`를 호출하는 코드는 프로젝트 전체에서
`Examples/WindowFeatureTestPanel.cs` 하나뿐이다. GameScene에는 이 컴포넌트가 없다.
이게 없으면 편집 스택을 넣어도 **들어갈 방법이 없어 검증 자체가 불가능하다.**

그리고 `ViewportEditHandles` / `WindowMoveResizeGuide` / `ViewportResidencyEnforcer`는
어느 씬·프리팹에도 배치되어 있지 않고, 오직 이 패널의 `EnsureCompanion<T>()`가
런타임에 붙여준다(핸드오버 §5.2-1의 "핸들이 안 보인다" 버그의 구조적 원인이기도 하다).

→ 이번 이관에서는 **`WindowFeatureTestPanel`을 GameScene에 넣어 검증한다.**
정식 설정 UI 진입점은 별도 작업.

### 4.3 함께 손보면 좋은 것 (선택)

- `WindowManager._maxSize`가 1920×1080이라 4K 모니터에서 평시 리사이즈가 잘린다
  (핸드오버 §6 보류 5번). 1920 모니터에서는 증상이 안 보이므로 이번엔 미뤄도 된다.

---

## 5. 단계별 이관 계획

씬(`.unity`) 직접 편집은 금지 규칙(`.claude/rules/unity/scenes.md`)이므로,
**아래 씬 작업은 전부 국기님이 Unity Editor에서 하셔야 한다.** 각 단계는 다음 단계로 넘어가기 전에
자체 확인 지점을 둔다 — 실패했을 때 어느 단계가 원인인지 갈라내기 위해서다.

### 0단계 — 준비 (완료)

- 브랜치 `feature/viewport-stack` 생성 ✅
- 이 문서 작성 ✅

### 1단계 — 카메라만 교체, 편집 스택 없이

가장 위험한 좌표 모델 교체를 **단독으로** 먼저 검증한다. 이 단계에서는 뷰포트도 편집도 없고,
"화면이 이관 전과 똑같이 보이는가"만 본다.

Unity Editor 작업:
1. `Environment/Main Camera`에서 **`CameraFitter` 컴포넌트 제거**
   (비활성화가 아니라 제거. `CameraFitter.Fit()`은 `camera.aspect`를 **수동 고정**하는데,
   Unity는 한 번 수동 설정된 aspect를 `ResetAspect()` 전까지 자동으로 되돌리지 않는다.
   남겨두면 창 리사이즈 후 렌더링과 `ScreenToWorldPoint` 판정이 전부 어긋난다.)
2. 같은 오브젝트에 **`BaseSpaceCameraFitter` 추가**
   - `_pixelsPerUnit` = `10.8`
   - `_masterCanvasBottomRight` = `(88.8889, 0)`
3. `Platform/OverlayWindow`의 `WindowManager`에서 **`_maximizeToWorkArea` 체크 해제**

> ⚠️ 이 상태에서는 `ViewportScreenSettings`가 없어 `Frame()`을 부르는 사람이 아무도 없다.
> 카메라는 씬에 저장된 `orthographicSize: 5`, 위치 `(0,0,-10)` 그대로 남는다.
> **1단계 단독 실행은 화면이 깨진 게 정상이다.** 2단계와 묶어서 확인해야 한다.
> 굳이 1단계만 보고 싶다면 인스펙터에서 임시로 `orthographicSize`를 48, 위치 y를 48로 넣어
> 대략 비슷한 그림이 나오는지만 본다.

**→ 실무적으로는 1·2단계를 한 번에 하고 2단계 확인 지점에서 판정하는 편이 낫다.**

### 2단계 — `ViewportScreenSettings` 배선

Unity Editor 작업:
1. 빈 GameObject `Viewport`를 만들고 `Environment` 아래에 둔다 (또는 `Platform/OverlayWindow`에 함께 부착).
   - 새 오브젝트를 만들면 **자식을 넣기 전에 위치를 원점 `(0,0,0)`으로** 맞춘다.
2. `ViewportScreenSettings` 추가하고 인스펙터에서 명시적으로 연결한다.
   (자동 탐색 `FindFirstObjectByType` 폴백이 있지만, 배선 실수가 조용히 넘어가는 걸 막으려면 직접 연결)
   - `_windowManager` → `Platform/OverlayWindow`
   - `_cameraFitter` → `Environment/Main Camera`
   - `_viewport` → `(0, 0, 0, 0)` 유지 (= 베이스 공간 전체)

**확인 지점 (빌드 필요)**: 이관 전 빌드와 나란히 띄워 비교한다.
- 캐릭터 크기가 같은가 (약 197px 높이)
- 캐릭터 가로 위치가 같은가 (화면 중앙)
- 캐릭터 발이 §3.1에서 예측한 대로 **작업표시줄 높이만큼만 아래로** 내려왔는가
  (그 이상 어긋나면 `_masterCanvasBottomRight` 계산이 틀린 것)
- Star가 화면 우상단의 같은 자리에 있는가

이 확인이 통과하면 **좌표 모델 교체는 끝난 것**이다. 이후 단계는 창·편집 기능 문제라
원인이 분리된다.

### 3단계 — 테스트 하니스 투입

Unity Editor 작업:
1. 아무 GameObject(예: 새로 만든 `Viewport`)에 `WindowFeatureTestPanel` 추가.
   - `Start()`에서 `EnsureCompanion`이 `ViewportEditHandles` / `WindowMoveResizeGuide` /
     `ViewportResidencyEnforcer`를 자동으로 붙이고 `Bind()`로 참조를 주입한다.

> ⚠️ 핸드오버 §5.2-1의 재발 방지: 이 자동 장착은 **컴파일이 통과해야만** 돌아간다.
> 컴파일 에러가 하나라도 있으면 핸들이 통째로 안 붙고, 증상은 "핸들이 안 보인다"로만 나타난다.
> 빌드 전에 Unity Console이 깨끗한지 반드시 먼저 확인한다.

**확인 지점 (빌드)**: 우하단에 버튼 패널이 뜨고 `[편집 시작]` 버튼이 활성 상태인가
(`IsReady`가 false면 잠겨 있다 → `ViewportScreenSettings` 초기화 실패 의심).

### 4단계 — 검증과 기록

§6의 시나리오를 빌드에서 돌리고, 어긋난 값은 인스펙터에서 조정한 뒤
**최종값을 이 문서에 반영**한다 (핸드오버 §7: 시각 수치 튜닝은 빌드에서 검증).

### 5단계 — `CameraFitter` 삭제

**전수 조사 결과, `CameraFitter`의 참조처는 GameScene 하나뿐이다.** 따라서 1단계에서
컴포넌트를 떼는 순간 이 클래스는 완전한 고아가 되고, 파일까지 이번 브랜치에서 지울 수 있다.

| 조사 대상 | 결과 |
|---|---|
| 씬·프리팹·에셋의 GUID `7ea7ce15…` 참조 | **`GameScene.unity:1675` 1건뿐** (Main Camera) |
| C# 코드의 `CameraFitter` 타입 참조 | **0건** (`BaseSpaceCameraFitter`는 이름만 비슷한 별개 타입) |
| 공개 API `MaxY` / `SetMaxY` 호출자 | **0건** — 정의만 있고 부르는 곳이 없다 |

작업 순서:

1. **씬에서 컴포넌트를 먼저 뗀다** (1단계). 파일을 먼저 지우면 GameScene의 Main Camera에
   Missing Script가 뜨고, 직렬화된 `_minY`/`_maxY`/`_referenceHeight`를 인스펙터에서 못 읽게 된다.
   (커밋 `75aac7a`에서 `TestHyeonScene`의 Missing Script를 치운 것과 같은 상황을 새로 만드는 셈이다.
   값 자체는 이 문서 §1.1에 기록해뒀으니 잃지는 않지만, 순서를 지키는 편이 깔끔하다.) — **완료**
2. `CameraFitter.cs`와 `CameraFitter.cs.meta`를 함께 삭제한다. `.cs`만 지우면 Unity가 고아 `.meta`를
   스스로 지우지만 커밋에 잔재가 남고, `.meta`만 지우면 Unity가 **새 guid**로 재생성해 참조가 끊긴다. — **완료**
3. 두 CLAUDE.md의 관련 줄을 **재작성**한다 (지우는 게 아니다 — 세 줄 모두 `WindowAspectFitter`와
   한 줄을 공유하고 있어, 이름만 빼면 살아있는 파일이 목록에서 사라지거나
   "`WindowAspectFitter`의 후속은 뷰포트 스택"이라는 **틀린 서술**이 남는다). — **완료**

> **삭제 시점에 대한 기록**: 원래 이 단계는 4단계(빌드 검증) 통과 후로 잡혀 있었으나,
> 국기님 요청으로 1단계 직후에 앞당겨 실행했다. `CameraFitter`는 삭제 시점에 이미 완전한
> 고아였고(guid 참조 0건, 타입 참조 0건, `MaxY`/`SetMaxY` 호출자 0건), 삭제가 화면 상태에
> 영향을 주지 않는다 — 지금 화면이 깨져 보이는 원인은 2단계 미완이지 이 파일의 존재 여부가 아니다.
> 되돌려야 하면 `git checkout HEAD -- Project_Cozy/Assets/Scripts/PerformanceSetting/CameraFitter.cs*`로
> **같은 guid 그대로** 복구된다(`.meta`가 함께 돌아오므로 씬에 다시 붙일 수 있다).

> **`WindowAspectFitter`는 함께 지우면 안 된다.** 두 CLAUDE.md가 둘을 한 묶음으로 적어놨지만,
> `WindowAspectFitter`는 `PerformanceSystemScene.unity:149`에서 아직 살아서 쓰이고 있다.
> (GameScene에는 없다.) 이건 별도 판단이 필요한 항목이라 이번 브랜치 범위 밖에 둔다.

---

## 6. 검증 시나리오 (빌드 전용)

창 투명화·클릭 통과·창 배치는 전부 `#if !UNITY_EDITOR` 안에 있어 **Editor에서는 검증되지 않는다.**
아래는 모두 빌드 실행 기준이다.

### 6.0 Editor에서 미리 걸러낼 수 있는 것

Editor에서는 `TryGetMonitorRect`가 실패해 베이스 공간 = **게임 뷰 크기**로 대체된다
(`ViewportScreenSettings.RefreshBaseSpace`의 폴백). `_masterCanvasBottomRight.x = 88.8889`는
가로 1920 기준이므로, **게임 뷰를 반드시 1920×1080 고정 해상도로 두어야** 카메라가 중앙에 온다.
다른 크기면 가로가 어긋나는데, 이건 버그가 아니라 폴백의 성질이다.

Editor에서 확인 가능: 카메라 `orthographicSize`가 런타임에 `1080/10.8/2 = 50`이 되는지,
캐릭터가 화면 안에 정상 크기로 보이는지. 창 관련은 전부 확인 불가.

### 6.1 좌표 모델 (2단계 직후 — 가장 중요)

| # | 조작 | 기대 결과 |
|---|---|---|
| 1 | 실행 후 이관 전 빌드와 나란히 비교 | 캐릭터 크기 동일(≈197px), 가로 위치 동일(중앙) |
| 2 | 캐릭터 발 높이 측정 | 모니터 아래 변에서 **75.6px** 위. 이관 전보다 작업표시줄 높이만큼 낮다 |
| 3 | Star 위치 | 화면상 같은 자리 |
| 4 | 창 아래 변 위치 | 작업표시줄을 **덮는다** (모니터 전체가 뷰포트이므로 정상) |

2번이 75.6px가 아니라 엉뚱한 값이면 `_pixelsPerUnit`을,
1번의 가로가 어긋나면 `_masterCanvasBottomRight.x`를 의심한다.

### 6.2 창 기본 동작 (3단계 이후)

| # | 조작 | 기대 결과 |
|---|---|---|
| 5 | 캐릭터 위 클릭 | 잡힌다 |
| 6 | 빈 곳 클릭 | 뒤 앱으로 통과 |
| 7 | 테스트 패널 버튼 클릭 | 잡힌다 |
| 8 | 상단 중앙 주황 그립(220×28) 드래그 | 창이 이동하고, **캐릭터는 제자리에 남는다** (창은 뷰파인더) |
| 9 | 창 가장자리 드래그 | 리사이즈되고, 놓은 뒤 카메라가 새 영역을 비춘다 (역동기화) |

8·9가 아예 반응하지 않으면 §4.1의 `_maximizeToWorkArea`가 아직 켜져 있는 것이다
(WndProc 미설치 → NCHITTEST 응답 없음).

### 6.3 편집 모드

| # | 조작 | 기대 결과 |
|---|---|---|
| 10 | `[편집 시작]` | 창이 모니터 전체로 확장 + 화면 전체 반투명 딤(알파 0.45) + 주황 테두리·핸들 |
| 11 | **위/좌/우** 핸들로 축소 → `[저장]` | 창이 새 뷰포트 크기로 줄고, 캐릭터 발 높이는 그대로 |
| 12 | `[편집 시작]` → 핸들 조작 → `[취소]` | 조작이 폐기되고 이전 뷰포트로 복귀 |
| 13 | 편집 중 빈 공간 클릭 | 통과하지 않고 잡힌다 (`SetClickThroughSuspended`) |
| 14 | 편집 이탈 후 클리어 알파 | 다시 0으로 복원 — 창이 불투명하게 남으면 안 된다 |

**11번에서 위/좌/우만 쓰는 이유**: §3.2의 미해결 항목 때문이다. 아래 변을 올리면
바닥선(월드 y=7)이 뷰포트를 따라오지 않는다.

### 6.4 알려진 미해결 동작 (버그로 보고하지 말 것)

| # | 조작 | 현재의 정상 동작 |
|---|---|---|
| 15 | **아래** 핸들을 위로 75.6px 넘게 올리고 저장 | 바닥선이 창 아래로 빠져 캐릭터가 안 보인다 (§3.2) |
| 16 | 뷰포트를 줄여 캐릭터를 밖에 남기고 저장 | 캐릭터가 회수되지 않고 접근 불가 상태로 남는다 (핸드오버 §6 보류 8) |
| 17 | 뷰포트를 크게 줄였을 때 UI 크기 | UI가 같이 작아진다. `CanvasScaler`가 `ScaleWithScreenSize`(참조 3840×2160, match=Width)라 창 폭에 비례한다 |

### 6.5 마무리

| # | 조작 | 기대 결과 |
|---|---|---|
| 18 | 멀티모니터: 창을 다른 모니터로 드래그 | 새 모니터 기준으로 재계산 (`RefreshBaseSpace`). 옛 모니터로 튕기지 않음 |
| 19 | 앱 종료 | 크래시 없음 (WndProc 원복 경로) |

---

## 7. 결정이 필요한 항목

이관을 시작하기 전에 국기님 판단이 필요한 건 하나뿐이고, 나머지는 이 문서의 권장안대로 진행 가능하다.

1. ~~**§3.1의 세로 48px 내려앉음**~~ — **결정 완료 (2026-07-25)**: (c) `_floorY`를 `7 → 11.4444`로.
   빌드에서 §3.1의 예측대로 내려앉는 것을 확인한 뒤 채택했고, `Character.prefab:55`에 반영했다.
2. §3.2(바닥이 뷰포트를 안 따라감), §3.3(PPU 100 정규화)는 **후속 작업으로 분리** — 이 문서의 판단.
3. §4.3(`_maxSize` 4K 대응)은 1920 개발 환경에서 증상이 없으므로 **미룸** — 이 문서의 판단.

---

## 7.5 빌드 검증 기록

### 2026-07-25 — §6.1 좌표 모델: 통과

씬 설정: `_pixelsPerUnit: 10.8`, `_masterCanvasBottomRight: (88.8889, 0)`,
`_maximizeToWorkArea: 0`, `ViewportScreenSettings`는 `Platform/OverlayWindow`에 부착
(협력자 두 개는 미할당 — `Start()`의 자동 탐색으로 동작).
테스트 패널 표시: `뷰포트 1920x1080 @(0,0)`, `모드: 평시` → 베이스 공간·뷰포트 기본값 정상.

| # | 항목 | 결과 |
|---|---|---|
| 1 | 캐릭터 크기 (≈197px) | ✅ `_pixelsPerUnit = 10.8` 확인 |
| 2 | 발 높이 | ⚠️ §3.1 예측대로 — 계산 오류 아님. (c)로 대응 |
| 3 | 가로 위치 (중앙) | ✅ `_masterCanvasBottomRight.x = 88.8889` 확인 |
| 4 | Star 위치 | ✅ |

**결론: 좌표 모델 교체는 검증 완료.** §2에서 계산한 두 값이 실측으로 맞았다.

### 씬 배선 최종 상태 (2026-07-26 확인)

§5의 Unity Editor 작업은 **전부 끝났다.** 디스크의 `GameScene.unity`에서 확인한 값이다.

| 대상 | 값 | 씬 라인 |
|---|---|---|
| `BaseSpaceCameraFitter._pixelsPerUnit` | `10.8` | :1716 |
| `BaseSpaceCameraFitter._masterCanvasBottomRight` | `(88.8889, 0)` | :1717 |
| `WindowManager._maximizeToWorkArea` | `0` | :688 |
| `WindowManager._resizable` | `1` | :689 |
| `ViewportScreenSettings` | `Platform/OverlayWindow`에 부착, `_viewport` = `(0,0,0,0)` | :698-716 |
| `WindowFeatureTestPanel` | 같은 오브젝트에 부착 | :717-733 |
| `BaseCharacterController._floorY` | `11.4444` (§3.1 (c)) | `Character.prefab:55` |

`ViewportScreenSettings._windowManager` / `_cameraFitter`와 `WindowFeatureTestPanel._viewportSettings`는
인스펙터 미할당(`fileID: 0`)이지만 **동작에 문제없다.** 세 필드 모두 `Awake`/`Start`에
`FindFirstObjectByType` 폴백이 있고(`ViewportScreenSettings.cs:69-70`,
`WindowFeatureTestPanel.cs:57`), 씬에 각 타입이 하나씩뿐이다.

### 2026-07-26 — §6.2 창 기본 동작 / §6.3 편집 모드 / §6.5 마무리: 통과

위 "씬 배선 최종 상태"의 값 그대로 빌드해 확인했다.

| # | 항목 | 결과 |
|---|---|---|
| 5~7 | 캐릭터 클릭 / 빈 곳 통과 / 패널 버튼 클릭 | ✅ |
| 8 | 상단 그립 드래그로 창 이동, 캐릭터는 제자리 | ✅ |
| 9 | 창 가장자리 드래그 → 리사이즈 + 카메라 역동기화 | ✅ |
| 10~14 | 편집 진입·핸들 조작·저장·취소·클리어 알파 복원 | ✅ |
| 19 | 앱 종료 시 크래시 없음 | ✅ |

9번은 `TestHyeonScene`이 `_resizable: 0`이라 선례가 없던 경로인데(§7.6) 통과했다.
8·9번이 도는 것으로 §4.1의 `_maximizeToWorkArea: 0` 전환이 의도대로 WndProc을 살렸다는 것도
함께 확인된 셈이다.

**결론: 이번 이관 범위의 검증은 끝났다.** 좌표 모델 교체(§6.1)와 창·편집 동작(§6.2·§6.3)이
모두 빌드에서 통과했다.

### 미실행 — 18번 (멀티모니터)

§6.5의 18번(창을 다른 모니터로 드래그 → `RefreshBaseSpace` 재계산)은 **검증 장비가 없어
돌리지 못했다.** 개발 환경이 단일 모니터다. 미검증 상태로 남겨두고, 멀티모니터 환경이
생기면 이 항목부터 확인한다.

관련해서 §4.3의 `_maxSize` 1920×1080 제약(4K 모니터에서 평시 리사이즈가 잘림)도 같은 이유로
증상을 볼 수 없다. 두 항목은 함께 확인하는 편이 낫다.

---

## 7.6 TestHyeonScene과의 차이 (참고)

창 스택을 먼저 검증한 씬이라 선례로 보게 되는데, GameScene과 설정이 달라 그대로 비교하면 오해가 생긴다.

- **`_resizable`이 다르다.** `TestHyeonScene.unity:185`는 `_resizable: 0`이라 **OS 가장자리 리사이즈가
  원래 없었다.** 그 씬에서 쓰던 "리사이즈"는 전부 편집 모드의 핸들 드래그다. GameScene은 `1`이라
  오히려 기능이 하나 더 많고, §6.2의 9번(가장자리 드래그 → 역동기화)은 **GameScene에서 처음 검증되는
  경로**다. 선례가 없으니 실패해도 회귀가 아니다.
- **`BaseSpaceCameraFitter` 값은 참고할 게 없다.** 그 씬은 Unity 기본값(`100` / `(0,0)`) 그대로다.
  검증된 좌표 값의 선례는 GameScene이 처음이다.
- **`m_EditorClassIdentifier: NormalWindowSetting`은 Missing Script이 아니다**
  (`TestHyeonScene.unity:224`). guid `868470e68cc223d4898a829a7923a176`는 현재
  `WindowMoveResizeGuide.cs.meta`와 같다 — 클래스 개명 흔적이고 참조는 살아 있다.
  (커밋 `75aac7a`에서 정리한 Missing Script 6건과는 별개다.)

---

## 8. 창·UI 구조 결정 — 하이브리드 (2026-07-25 확정)

이관 도중 드러난 문제: **창 밖에는 아무것도 그릴 수 없다.** Unity는 OS 창 백버퍼에만 그리고
DWM은 그 사각형만 합성한다. 그래서 뷰포트를 작게 줄이면 상점·도감 같은 UI를 띄울 자리가 없다.

구체적으로 GameScene의 `UIRoot`는 `ScreenSpaceOverlay` + `CanvasScaler`(ScaleWithScreenSize,
참조 3840×2160, `matchWidthOrHeight: 0` = 가로 100% 매칭)라 배율이 `창 폭 ÷ 3840`으로 정해진다.

- 가로를 줄이면 비례 축소 — 창 폭 720에서 0.1875배. 잘리진 않지만 글자가 오늘의 2.7배 작아진다.
- **세로만 줄이면 잘린다.** 배율이 폭만 보므로, 창이 1920×480이어도 배율은 0.5 그대로다.
  `UIPanel_Base.prefab`(참조 1280×1080)은 640×540으로 그려지는데 창 높이가 480이라 위아래로 잘린다.

### 채택: 평시 창=뷰포트 유지 + UI/편집 중에만 모니터 전체로 확장

세 안(① 창을 항상 모니터 전체로 두고 `camera.rect` 도입 / ② 하이브리드 / ③ 현행 유지 + UI를
작은 뷰포트에 맞게 설계) 중 **②를 채택**한다.

이유는 ①이 뒤집는 것이 측정된 결정이기 때문이다. `ProjectSettings.asset:98`의
`useFlipModelSwapchain: 0`(BitBlt) 때문에 합성 비용이 창 면적에 비례하고, 인수인계 문서가
"성능 병목은 폴링이 아니라 렌더링과 창 면적임 (검증됨)"이라고 적고 있다. ①은 이 비용을 상시
지불하게 되어 루트 CLAUDE.md의 "최적화가 우선"과 충돌한다.

②는 편집 모드가 이미 쓰는 패턴이라 새 메커니즘이 필요 없다 —
`EnterEdit()` → `ApplyMonitorFullscreen()` + `SetClickThroughSuspended(true)`,
`ExitEdit()` → `ApplyNormal()`.

### 후속 작업 (이번 이관 범위 밖)

기존 코드는 확장 메커니즘을 재사용 가능하게 열어뒀지만(`ApplyMonitorFullscreen()`이 public이고
이름이 편집 전용이 아니며, suspend API 주석도 "편집 모드 **등**에서"), UI를 위한 확장은 설계에 없다.
두 가지를 열어야 한다.

1. **카메라 재프레이밍 없이 창만 넓히는 경로.** 지금 `EnterEdit()`는 창 확장 + 클릭 통과 정지 +
   리사이즈 정지 + 카메라를 베이스 공간 전체로 재프레이밍을 한 덩어리로 한다. 마지막이 편집 전용이라,
   UI 패널이 원하는 "창은 넓히되 카메라는 뷰포트 그대로"를 표현할 방법이 없다.
2. **`_isEditing`(bool)을 확장 요청 카운트로.** 지금은 불리언 하나라 편집 모드와 UI 패널이
   동시에 창을 넓히려 하면 서로를 덮어쓴다.

그리고 `Scripts/UI/` 전체에서 `WindowManager`/`ViewportScreenSettings` 참조가 0건이라,
UI 쪽에 확장을 요청하는 훅도 새로 만들어야 한다.
