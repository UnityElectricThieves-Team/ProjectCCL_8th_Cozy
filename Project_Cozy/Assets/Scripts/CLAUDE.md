# Scripts/

게임 C# 코드. 레이어는 책임 단위로 나눈다. 아래 표의 참조 대상은 각 폴더가 *보통* 무엇에 기대는지를 적은 것이지 강제 규칙이 아니다 — 필요하면 반대 방향으로도 참조할 수 있다.

## 레이어

| 폴더 | 책임 | 주로 참조하는 대상 |
|---|---|---|
| `Platform/` | OS 의존 인프라 — borderless·Always-on-Top 창, 모니터 목록, 비포커스 키보드 훅, 시작 프로그램 등록, Win32 / DwmApi 호출. 게임 로직을 모른다. | (없음) — 자세한 컨벤션은 [Platform/CLAUDE.md](Platform/CLAUDE.md) |
| `Interaction/` | 마우스 입력 라우팅 + 인터랙터블 인터페이스 계약. 게임 객체가 `IHoverable` / `IClickable` / `IRightClickable` / `IShiftRightClickable`을 구현하면 매니저가 자동 라우팅. | `Platform/`(드래그 중 창 클릭 통과 정지) — 자세한 컨벤션은 [Interaction/CLAUDE.md](Interaction/CLAUDE.md) |
| `PerformanceSetting/` | 프레임 레이트·뷰포트 등 런타임 *정책*. OS 조작은 `Platform/`에 위임. | `Platform/`, `Interaction/`(월드 입력 잠금) — 자세한 컨벤션은 [PerformanceSetting/CLAUDE.md](PerformanceSetting/CLAUDE.md) |
| `Animation/` | 스프라이트 애니메이션 등 순수 표현 컴포넌트. 게임 로직·OS를 모른다. | (없음) |
| `Character/` | 캐릭터 단일 개체의 자율 거동·친밀도·시각. `BaseCharacterController` 단일 컴포넌트 + nested module(`StateModule`/`VisualModule`/`AffinityModule`/`ScaleModule`). | `Interaction/`, `Animation/`, `Platform/Input/`, `Gameplay/`(`GameBalance`의 수치) — 자세한 컨벤션은 [Character/CLAUDE.md](Character/CLAUDE.md) |
| `Gameplay/` | 게임 로직. `Platform/`의 인프라를 *소비*한다 (변신 등은 채워지는 중, 구름은 누르면 다음 캐릭터를 해금한다). 하트·스폰 기운처럼 씬에 하나만 두는 시스템이 여기 산다. 캐릭터 보유·배치 기록은 Contents가 아니라 여기의 `CharacterOwnership`이 저장한다 — 기록대로 캐릭터를 등장시키는 주체라서다. | `Platform/`, `Animation/`, `Interaction/`, `PerformanceSetting/`(뷰포트·좌표 환산 — 배경·장식 배치), `Character/`(캐릭터를 만들고 숨기고 다시 보인다) |
| `Contents/` | 상점·도감 같은 *콘텐츠* 시스템. 무엇을 가졌고 무엇을 쓰는 중인지를 들고 파일에 기록한다. 정의(ScriptableObject·JSON)와 상태(저장 파일)를 분리한다. | `Platform/Data/`, `Gameplay/`, `PerformanceSetting/`(뷰포트 저장 시 장식 회수) |
| `UI/` | HUD·메뉴 표시. TextMeshPro 사용. | 하위 레이어 자유 참조 (`Gameplay/`·`Character/`·`Interaction/`·`PerformanceSetting/`·`Platform/` 등) — UI는 최상위 표현층이라 하위를 향한 참조에 제한을 두지 않는다. 설정 UI가 창·뷰포트 정책(`PerformanceSetting/`의 `ViewportScreenSettings` 등)을 직접 제어하는 교차가 잦기 때문. |
| `Examples/` | 기능 확인용 하니스. 본편 동작에 필요한 것을 여기 두지 않는다. | 자유 |

> 새 시스템이 구현되면 이 표에 위치를 같이 적는다.

## 폴더별 진입점

전체 파일 목록은 폴더를 직접 보면 된다. 여기에는 **어디서부터 읽어야 하는지**만 적는다.

| 폴더 | 여기서 시작 |
|---|---|
| `Platform/Window/` | `Core/WindowManager.cs` — HWND·WndProc의 단일 소유자. DWM 투명화, 클릭 통과, 창 영역 적용, 핫존 수치가 전부 여기 있다 |
| `Platform/Input/` | `GlobalKeyInput.cs` — 포커스 무관 키 입력. OutFocus 전용은 `OutFocusKeyHook`/`OutFocusMouseHook`(둘 다 static 이벤트로 방송). 무입력 판정은 마지막 입력 시각만 드는 `UserInputClock` |
| `Platform/Data/` | `GameDataPaths.cs`(모든 저장 경로의 중앙 레지스트리) → `UserDataSaveIO.cs`(유저 데이터 단일 진입점) → `GameDataIO.cs`(직렬화·원자적 쓰기) |
| `Interaction/` | `InteractionInterfaces.cs`(4개 계약) → `InputInteractionManager.cs`(라우팅), `WorldInputLock.cs`(월드 마우스 입력 공용 잠금) |
| `PerformanceSetting/Viewport/` | `ViewportScreenSettings.cs`(뷰포트 정책 + 베이스 공간 단위의 소유자) + `BaseSpaceCameraFitter.cs`(px→월드 환산 — 앵커·PPU의 소유자) |
| `Animation/` | `SpriteAnimator.cs`(프레임 순환), `CloudVisual.cs`(구름 그림의 둥둥과 활성 반짝임 — 판정은 `Interaction/SpritePhysicsShapeSync`가 따라간다), `ShadowProjector.cs`(캐릭터 아래 바닥을 향해 판정해 그림자를 놓고, 멀어질수록 폭을 줄인다) |
| `Character/` | `BaseCharacterController.cs` → `CharacterState.cs`(통합 enum) → `Modules/`, `States/` |
| `Gameplay/` | `CharacterManager.cs`(지금 화면에서 살아 움직이는 캐릭터 — 살아있는 목록·화면에 나온 캐릭터의 동시 상한·스폰 단일 지점) ↔ `CharacterOwnership.cs`(어떤 캐릭터를 가졌고 누가 나와 있어야 하는가 — 캐릭터별 기록을 즉시 저장한다. 뷰포트가 처음 확정되면 보유한 캐릭터를 모두 만들고 배치되지 않은 것은 숨긴다. 도감의 귀환·소환도 여기서 한다. 테스트용 해금은 컴포넌트 우클릭 메뉴 "테스트: 모든 캐릭터 해금". 씬에 놓인 캐릭터는 CharacterManager가 등록만 한다), `GameBalance.cs`(밸런스 수치 — 출시 값과 테스트 값 두 벌, 에디터·Development Build는 테스트 값), `HeartSystem.cs`(하트 재화), `AffinityHeartReward.cs`(친밀도가 오른 만큼 하트 지급 — 기준은 저장된 친밀도), `SpawnPointManager.cs`(스폰 기운), `Background/BackgroundStage.cs`(배경 프리팹을 그대로 뷰포트 아래 변에 깔고, 뷰포트 밖은 마스크로 자른다), `Decoration/DecorationLayer.cs`(화면에 놓인 장식을 지면에 맞춰 그린다 — 상점을 모른다. 배치 모드의 호버 강조·숨김·앞으로 당기기를 소유한다), `Decoration/DecorationPlacementController.cs`(장식 배치 모드(설치·이동·회수·제거) — 고르기·고스트·판정·확정/취소, 시작·종료를 모드 값과 함께 이벤트로 알린다), `LocalizationManager.cs`(번역 표 — stringID로 현재 언어 문장을 내준다. 프리팹 고정 문구는 `UI/LocalizedText.cs`가 붙어 받아 간다), `Audio/BgmPlayer.cs`(설정의 소리 값을 믹서·배경음악 재생에 반영 — 곡 목록은 `Audio/BgmCatalog.cs`), `DesktopIntegrationApplier.cs`(설정의 항상 위·자동 실행을 `Platform/`의 창·시작 프로그램 등록에 건다), `WorldZoomApplier.cs`(설정의 전체 크기를 `PerformanceSetting/`의 뷰포트 정책에 건다), `MonitorApplier.cs`(설정에서 고른 모니터를 찾아 그 작업 영역을 뷰포트 정책에 건다 — 모니터 목록은 `Platform/Display/MonitorList`) |
| `Contents/` | `ShopSystem/ShopCatalog.cs`(모든 상품 목록 에셋 — 장식·배경 시스템이 이것 하나를 읽는다. 아트 폴더에서 상품을 한꺼번에 만드는 메뉴는 `Editor/ShopItemGenerator.cs`의 "Cozy/Shop/아트 폴더에서 상품 만들기"), `ShopSystem/ShopSystem.cs`(장식 소유 — 가진 개수(제거하면 준다)·화면에 놓인 목록·회수 순번) → `ShopSystem/DecorationBinder.cs`(놓인 목록을 `Gameplay/Decoration/DecorationLayer`에 잇는다), `ShopSystem/BackgroundSystem.cs`(배경 소유 + 활성 1개) → `ShopSystem/BackgroundBinder.cs`(활성 배경을 `Gameplay/Background/BackgroundStage`에 잇는다), `GameProgressReset.cs`(설정의 [게임 초기화] — 진행을 저장하는 시스템마다 `ResetProgress`를 부른다), `CollectionSystem/Model/CollectionData.cs`(도감 정의 — WPF 툴이 내보낸 암호화 `collection.dat`. 항목의 `CharacterId`로 `Gameplay/CharacterOwnership`의 해금·친밀도 기록과 잇는다) |
| `UI/` | `UIManager.cs`(열린 패널 스택 + ESC, 장식 배치 모드·뷰포트 편집 중 패널 잠시 숨김 — 이동·회수·제거 모드는 표시된 패널을 남긴다, 닫기 가로채기·모달) → `UIPanel.cs`(패널 공통 동작 — 열림과 잠시 숨김을 따로 든다), `ConfirmPopup.cs`(공용 확인/취소 팝업 — 씬에 하나, `Instance`로 부른다. 대답이 필요 없는 안내는 `ShowNotice`), `CountPopup.cs`(몇 개인지 묻는 공용 팝업 — 인벤토리 장식 제거), `GameCursor.cs`(게임의 마우스 커서를 화면 배율로 줄여 하드웨어 커서로 건다), `PlacementGuide.cs`(배치 모드 커서·모드별 안내 라벨), `InventoryModeController.cs`(인벤토리 [이동]/[회수]/[제거] — 모드에 들어가고, 고른 장식을 `ShopSystem`에 넘긴다), `ViewportEditOverlay.cs`(뷰포트 편집 화면의 안내 문구와 [완료] 버튼), `UIScaleApplier.cs`(설정의 UI 크기를 패널 부모·메뉴 바·말풍선 층 배율로 건다), `CountLabel.cs`(구름·캐릭터 위의 수치 — 설정의 표시 방식을 따른다), `MarqueeText.cs`(칸보다 긴 글자를 흘린다), `HoverTooltip.cs`(호버 말풍선 — 씬에 하나 두고, 항목에는 키만 든 `HoverTooltipSource`를 붙인다) |

## 사용 금지 · 제거 대기

새 코드에서 쓰면 안 되는 것들. 이름만 보면 멀쩡해 보여서 실수하기 쉽다.

- `Animation/SpriteAnimator.cs` — **캐릭터 외 사용처 전용**(달 등). `BaseCharacterController` 시스템은 `VisualModule`을 쓴다.
- `Platform/Window/Core/BorderlessWindow.cs`, `Platform/Window/Input/ClickThroughProbe.cs` — 구 프로토타입. 현행 스택과 같은 HWND를 만지므로 함께 활성화하면 충돌한다. 제거 대기.
- `PerformanceSetting/WindowAspectFitter.cs` — 구 구현. 후속은 `Platform/`의 `WindowManager`다. 제거 조건은 [PerformanceSetting/CLAUDE.md](PerformanceSetting/CLAUDE.md) 참조.
- `Platform/Data/SaveScheduler.cs` — 인프라만 있고 **고객이 0명이다.** `IPeriodicSaveable` 구현체도, 씬 배치도 없다. 여기 등록하면 조용히 저장이 안 된다. 새 저장은 `HeartSystem`/`ShopSystem` 골격(값이 바뀔 때 즉시 저장)을 따른다. 예외는 셋이다. 슬라이더처럼 매 프레임 값이 오는 볼륨은 손 뗄 때 저장하고([audio.md](../../../.claude/rules/unity/audio.md)), 설정 그래픽 탭은 [저장]을 누를 때 저장한다([settings.md](../../../.claude/rules/unity/settings.md)). 입력마다 오르는 스폰 기운은 `SpawnPointManager`가 몇 초마다, 그리고 끝날 때 저장한다.

## 컨벤션

- **Namespace 미사용** (글로벌 namespace 유지) — Platform/ 등 모든 폴더 동일.
- **외부 참조는 인스펙터 (`[SerializeField] private`)** 또는 같은 GameObject의 `GetComponent`(Awake 1회 캐싱). 매 프레임 또는 빈번한 `Find` / `FindObjectOfType` 금지. *씬 단일 인스턴스라 가져와서 메서드를 호출하는* 컴포넌트는 Singleton `Instance` 패턴 사용(예: `HeartSystem`). *입력을 방송만 하는 소스*는 참조 대신 **static 이벤트**를 노출해 소비자가 구독한다(예: `OutFocusKeyHook.KeyPressed`).
- **상호작용은 인터페이스로만.** 게임 객체는 `Interaction/`의 인터페이스를 구현하고 매니저로의 직접 의존은 두지 않는다.
- 그 외 네이밍·MonoBehaviour·성능 원칙은 [.claude/rules/unity/csharp.md](../../../.claude/rules/unity/csharp.md) 참조.
