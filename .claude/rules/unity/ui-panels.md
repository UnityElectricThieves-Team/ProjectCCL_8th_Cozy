---
paths:
  - "Project_Cozy/Assets/Prefabs/UIPanels/**"
  - "Project_Cozy/Assets/Scripts/UI/**/*.cs"
---

# UI 패널 규칙

이 프로젝트의 UI 패널을 왜 이 모양으로 만드는지 적은 문서입니다. 구체 수치는 프리팹과 코드에서 확인하세요.

## 패널은 `UIPanel_Base`의 프리팹 배리언트로 만든다

새 패널을 씬에 직접 만들지 않고, `Assets/Prefabs/UIPanels/UIPanel_Base.prefab`의 배리언트로 만듭니다. 베이스가 들고 있는 것은 이렇습니다.

- `Area_Header` — 제목과 닫기 버튼. 여기를 잡아 패널을 끕니다.
- `Area_TabSelection` — 탭이 있는 패널만 씁니다.
- `Area_Content` — 패널의 본체.
- `Area_Footer` — 하단 영역.

베이스를 고치면 모든 패널에 반영되므로, 공통 외형·닫기 동작은 베이스에서 한 번만 정의합니다.

## 콘텐츠 컨트롤러는 `Area_Content`에 붙인다

패널 루트가 아니라 `Area_Content`에 붙입니다. 컨트롤러가 다루는 것이 그 영역의 내용물이므로, 붙는 위치와 책임 범위가 일치하는 쪽이 읽기 쉽고 슬롯 부모를 따로 참조로 물릴 필요도 없습니다.

> 상점 컨트롤러는 아직 옛 베이스의 패널 루트에 붙어 있습니다. 세 컨트롤러의 "패널 루트에 붙는다"는 주석도 이 규칙보다 오래된 것입니다. 새로 만들거나 옮길 때는 이 규칙을 따릅니다.

## 높이는 `LayoutElement`가 정한다

`Area_*`의 RectTransform 앵커나 sizeDelta를 직접 만지지 않습니다. 부모의 `VerticalLayoutGroup`이 자식 크기를 통제하므로 손으로 넣은 값은 매번 덮어써집니다. 프리팹에 직렬화된 값이 0으로 보이는 것도 그래서이고, 버그가 아닙니다.

- Header와 Footer는 고정 높이입니다.
- 남는 높이는 `Area_Content`만 먹습니다. 그래서 콘텐츠가 길어져도 헤더가 밀리지 않습니다.
- 높이를 바꿀 일이 있으면 그 `Area_*`의 `LayoutElement`를 고칩니다.

## 패널은 `CanvasGroup`으로 숨긴다 — `SetActive(false)`가 아니다

`UIPanel`이 알파와 클릭 차단만 끕니다. 재생성 비용이 없고 슬라이더 값 같은 내부 상태가 보존되며, 나중에 페이드 연출을 넣을 여지도 남습니다.

**그 대가로 GameObject가 계속 활성 상태입니다.** 즉 `OnEnable`은 씬 로드 시 한 번만 불리고, **패널을 다시 열 때는 불리지 않습니다.** 열 때마다 갱신해야 하는 표시는 `OnEnable`에 두면 안 되고, 상태가 바뀔 때 울리는 이벤트를 구독해야 합니다.

## 버튼 `OnClick`은 인스펙터에서 배선한다 — 코드의 `AddListener`가 아니다

컨트롤러가 `Awake`에서 `onClick.AddListener`로 거는 방식은 프리팹만 봐서는 어느 버튼이 무엇을 하는지 보이지 않습니다. 버튼을 찾으려면 코드를, 동작을 찾으려면 프리팹을 번갈아 봐야 해서 유지보수할 때 헷갈립니다. 닫기 버튼이 이미 인스펙터에서 `RequestClose()`를 무는 것과 같은 방식으로, 버튼의 `OnClick()`에 컨트롤러의 public 메서드를 직접 겁니다.

- 인스펙터 `OnClick()`은 enum 인자를 넘길 수 없습니다. `SetTab(SettingsTab)`처럼 enum을 받는 메서드는 `ShowGeneralTab()` 같은 버튼별 public 메서드로 쪼갭니다.
- 코드에서는 `Button` 참조 자체를 들고 있을 이유가 없어지므로, 탭 버튼 `[SerializeField]`는 배선을 옮기면서 같이 걷어냅니다.

> **TODO — 아직 코드로 배선된 곳.** 새로 만들 때는 위 규칙을 따르고, 아래는 손댈 때 옮깁니다.
> - `ShopPanelContentController` — 탭 2개 (`Awake`)
> - `MenuButtonBar` — 메뉴 버튼 → `UIManager.Toggle`
> - `ShopItemSlot`, `CollectionEntrySlot`, `BackgroundItemSlot` — 슬롯 버튼. 슬롯은 코드로 생성되므로 인스펙터 배선 대상은 슬롯 프리팹 안의 버튼입니다.

## 닫기 버튼은 `UIPanel.RequestClose()`에 건다

`UIPanel.Close()`를 직접 걸면 화면에서는 사라지지만 `UIManager`의 열린 패널 목록에는 남습니다. 그러면 ESC가 이미 닫힌 패널을 대상으로 헛 눌립니다. 패널이 자기 자신을 타깃으로 삼기 때문에 프리팹 안에서 배선이 완결되는 이점도 있습니다(씬의 `UIManager`를 프리팹에서 참조할 수 없습니다).

## 반투명은 의도보다 옅게 나온다

이 게임의 창은 바탕화면과 알파 합성되므로, 반투명하게 만든 UI가 빌드에서 예상보다 훨씬 옅게 보입니다. 에디터에서 맞춰놓고 빌드에서 다시 놀라는 일이 잦습니다.

**알파 값을 미리 보정해서 우회하지 마세요.** 근본 원인과 기각된 우회 방법들은 `Docs/Development/`의 창·뷰포트 문서에 정리돼 있습니다.
