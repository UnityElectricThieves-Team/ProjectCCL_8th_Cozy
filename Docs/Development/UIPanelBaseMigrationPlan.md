# UIPanel_Base 마이그레이션 — 옛 Base 은퇴 계획

> 작업용 문서. 끝나면 지운다.
> **커밋 해시를 여기 직접 적는다** — 며칠에 걸쳐 참조하는 정답지다.

## 지금 상태

커밋 `e87135c`(2026-07-21)가 `UIPanel_Base`를 새 구조로 다시 만들었다.
옛 것은 `UIPanel_Base_old.prefab`으로 남았고, 패널 5개도 각각 `*_old.prefab`으로 보존됐다.
**도감만 새 Base로 옮겨졌다.** 나머지 넷은 아직 옛 Base에 얹혀 있다.

| 패널 | 씬이 쓰는 프리팹 | 올라탄 Base |
|---|---|---|
| Collection | `UIPanel_Collection/UIPanel_Collection.prefab` | 새 Base ✅ |
| Shop | `UIPanel_Shop_old.prefab` | 옛 Base |
| Settings | `UIPanel_Settings_old.prefab` | 옛 Base |
| Tutorial | `UIPanel_Tutorial_old.prefab` | 옛 Base |
| Inventory | `UIPanel_Inventory_old.prefab` | 옛 Base |

**목표**: 넷을 모두 새 Base로 옮기고, `UIPanel_Base_old.prefab`과 `*_old.prefab` 6개를 지운다.

`UIPanel_Inventory/UIPanel_Inventory.prefab`이 이미 새 Base의 배리언트로 만들어져 있다(미커밋).
다만 루트 이름 오버라이드가 없어서 **이름이 아직 `UIPanel_Base`** 이고, 씬은 아직 옛 것을 쓴다.

---

## 새 Base 구조

```
UIPanel_Base            (기본 1280×1080)
├─ Background           [Image]
└─ Area_VerticalLayoutGroup   [VerticalLayoutGroup]
    │   padding 0/0/0/0 · spacing 0 · UpperLeft
    │   ControlW 1 ControlH 1 · ForceExpandW 1 ForceExpandH 0
    ├─ Area_Header        [DraggablePanel, LayoutElement]  preferredHeight 160
    │   ├─ TitleBox → Background, Label
    │   └─ CloseButtonArea → CloseButton  (160×160)
    ├─ Area_TabSelection  [LayoutElement]  preferredHeight 0   ← 기본 접힘
    ├─ Area_Content       [LayoutElement]  flexibleHeight 1    ← 남은 공간 전부
    └─ Area_Footer        [LayoutElement]  preferredHeight 0   ← 기본 접힘
```

**높이는 앵커가 아니라 `LayoutElement`가 정한다.** `Area_*`의 앵커·sizeDelta가 전부 0인 건
정상이다 — `VerticalLayoutGroup`이 매번 덮어쓰기 때문이다. 배리언트에서 그 값들이 오버라이드로
잡혀도 실제 편집이 아니라 레이아웃이 남긴 자국이다.

탭이나 푸터가 필요한 패널만 해당 `LayoutElement`의 `preferredHeight`를 올려주면 된다.
**Base에 스크롤 기능은 없다.** 스크롤이 필요한 패널이 자기 `Area_Content` 안에 Scroll View를 넣는다.

---

## 도감이 템플릿이다

이미 끝난 도감이 나머지의 본보기다. 무엇을 했는지:

- 루트 크기만 배리언트에서 바꿨다 (**1536 × 1296**).
- `Area_TabSelection` / `Area_Footer`의 `preferredHeight`는 **건드리지 않았다**(0인 채로 접힘).
- `Area_Content`에는 레이아웃 그룹을 붙이지 않았다. 그냥 빈 컨테이너다.
- 내용물 둘(`Left_Character_ScrollView`, `Right_Character_Info`)을 `Area_Content`의 자식으로 넣고
  각각 모서리 앵커로 배치했다.
- **컨트롤러(`CollectionPanelContentController`)를 패널 루트가 아니라 `Area_Content` 오브젝트에 붙였다.**
- 스크롤은 별도 프리팹 `Left_Character_ScrollView.prefab`으로 뽑았다(Image + ScrollRect).

마지막 두 가지는 상점을 옮길 때 그대로 따를지 판단이 필요하다 — 상점 컨트롤러는 지금 루트에 있다.

---

## 채록 — 이미 끝났다

파일에서 직접 읽어냈다. **Unity 인스펙터를 열어 옮겨 적을 필요 없다.**

### 각 패널 루트 크기

| 패널 | 현재 크기 |
|---|---|
| Collection (새 Base) | 1536 × 1296 |
| Shop | (옛 배리언트 값 확인 필요 — 아래 "사람이 정할 것" 참조) |
| Settings | 960 × 1080 |
| Tutorial | 300 × 500 |
| Inventory | 300 × 500 |
| 새 Base 기본값 | 1280 × 1080 |

### Shop `Content`의 레이아웃 값 (새 Scroll View의 Content에 그대로 입력)

| VerticalLayoutGroup | 값 |
|---|---|
| padding L/R/T/B | 8 / 8 / 8 / 8 |
| spacing | 40 |
| childAlignment | UpperCenter |
| ChildControl Width / Height | ON / ON |
| ChildForceExpand Width / Height | ON / OFF |

| ContentSizeFitter | 값 |
|---|---|
| Horizontal Fit | Unconstrained |
| Vertical Fit | Preferred Size |

### Settings `Content`의 레이아웃 값

| VerticalLayoutGroup | 값 |
|---|---|
| padding L/R/T/B | 0 / 0 / 0 / 0 |
| spacing | 0 |
| childAlignment | UpperCenter |
| ChildControl Width / Height | **ON** / OFF |
| ChildForceExpand Width / Height | ON / OFF |

| ContentSizeFitter | 값 |
|---|---|
| Horizontal Fit | Unconstrained |
| Vertical Fit | Preferred Size |

> 씬에는 이 값들의 오버라이드가 **하나도 없다.** 위 프리팹 값이 실제로 쓰이는 값이다.
> (옛 계획서는 "씬이 켜둔 값"이라고 적었는데 사실이 아니었다.)

### 도감 Scroll View 설정 (새로 만들 때 참고)

ScrollRect: Horizontal OFF / Vertical ON / Elastic(0.1) / Inertia ON / Deceleration 0.135 /
Sensitivity 1 / 세로 스크롤바 Permanent.
Content: VLG padding 0, **spacing 20**, UpperLeft, ControlW·H ON, ForceExpandW ON / H OFF.
ContentSizeFitter: Horizontal Unconstrained / Vertical Preferred Size.

---

## 코드 쪽 위험은 거의 없다

`Assets/Scripts` 전체를 훑은 결과:

- 패널 프리팹을 이름으로 찾거나 `Resources.Load`, `transform.Find("Scroll View")` 같은
  **문자열 경로 접근이 한 건도 없다.**
- `UIManager`는 조회를 안 한다. `Open`/`Close`/`Toggle`에 `UIPanel`을 넘겨받는 싱글턴이다.
- 패널 등록은 **씬의 `MenuButtonBar._entries`** 에 인스펙터로 직렬화돼 있다
  (`Entry { Button button; UIPanel panel; }`).

→ 깨질 수 있는 건 **직렬화된 오브젝트 참조뿐**이다. 구체적으로 두 곳:

1. `ShopPanelContentController._content` — 옛 Scroll View 안의 `Content`를 가리킨다.
   나머지 직렬화 필드 8개는 프로젝트 에셋이나 루트 자식이라 안전하다.
2. 씬의 `MenuButtonBar._entries` — 패널 인스턴스를 교체하면 그 칸이 비므로 다시 넣어야 한다.

---

## Phase 1 — Tutorial · Inventory (가장 쉬움, 예행연습)

**커밋 1** — 해시 `________________`

둘 다 옛 Base의 **빈 배리언트**다. 추가된 오브젝트도, 컴포넌트도, 컨트롤러도 없다.
바꾸는 건 이름 · 크기 · 타이틀 문구뿐이다.

1. `UIPanel_Inventory/UIPanel_Inventory.prefab`(이미 있음)을 열어
   - 루트 이름을 `UIPanel_Inventory`로 바꾼다 (**지금 `UIPanel_Base`로 남아 있다**)
   - 루트 크기 300 × 500
   - `Area_Header`의 Label 문구 `인벤토리`
2. `UIPanel_Tutorial/` 폴더를 만들고 새 Base의 배리언트 `UIPanel_Tutorial.prefab`을 만든다.
   - 이름 `UIPanel_Tutorial`, 크기 300 × 500, Label 문구 `당신을 위한 가르침`
3. GameScene에서 옛 인스턴스 둘을 지우고 새 프리팹을 같은 부모 아래에 넣는다.
4. **`MenuButtonBar._entries`에서 두 패널 칸을 다시 연결한다.**

**verify**: 플레이 → 메뉴 버튼으로 인벤토리·튜토리얼이 열리고 닫힌다.
타이틀 문구가 맞고, 타이틀을 잡아 드래그하면 움직인다. Console에 에러가 없다.

---

## Phase 2 — Settings

**커밋 2** — 해시 `________________`

6줄이 **프리팹 안**에 있다. 씬 작업이 아니라 프리팹 작업이다.

1. `UIPanel_Settings/UIPanel_Settings.prefab`을 새 Base의 배리언트로 만든다.
   이름 `UIPanel_Settings`, 크기 960 × 1080, Label 문구는 옛 것과 맞춘다.
2. `Area_Content` 안에 `GameObject > UI > Scroll View`를 만든다.
   가로 스크롤을 끄고 `Scrollbar Horizontal`을 삭제한다. `Area_Content`를 채우도록 stretch.
3. 새 `Content`에 위 채록표의 **Settings 값**을 입력한다.
   `ChildControlWidth`는 **ON** — 끄면 6줄이 폭을 잃고 뭉친다.
4. `SettingsRow_Toggle` 3개 · `SettingsRow_Dropdown` 3개를 새 `Content` 자식으로 넣는다.
   순서: `Row_AdministratorMode` / `Row_GirlChangeAvailable` / `Row_AlwaysOnTop` /
   `Row_CloudSize` / `Row_Language` / `Row_AffinityVisibility`
5. 씬 인스턴스를 교체하고 `MenuButtonBar._entries`를 다시 연결한다.

> 옛 프리팹에는 각 줄 라벨의 TMP 폰트 오버라이드가 씬에 걸려 있었다. 새 인스턴스에서
> 글자가 다르게 보이면 그 오버라이드가 안 따라온 것이다.

**verify**: 플레이 → 옵션 → 6줄이 세로로 쌓이고 **폭이 패널을 채우며** 휠로 스크롤된다.
드롭다운 3개를 열어 옵션 개수와 글자색이 전과 같다.

---

## Phase 3 — Shop (가장 복잡)

**커밋 3** — 해시 `________________`

옛 Shop이 루트에 직접 매단 것 셋: `Tab_Decoration`, `Tab_Background`, `HeartDisplay`.
런타임에 상품 행을 만들므로 `Content`에 저자 시점 내용물은 없다.

1. `UIPanel_Shop/UIPanel_Shop.prefab`을 새 Base의 배리언트로 만든다. 이름·크기·Label 설정.
2. 탭 2개를 **`Area_TabSelection`** 안에 넣고 그 `LayoutElement.preferredHeight`를 올린다
   (기본 0이라 안 올리면 접혀서 안 보인다). 높이는 아래 "사람이 정할 것" 참조.
3. `HeartDisplay`를 배치한다 — `Area_Header` 안이 자연스럽다. 옛 값은 루트 기준
   anchoredPosition (387, 350), 크기 200×72였으므로 그대로 쓰면 안 된다.
4. `Area_Content` 안에 Scroll View를 만들고(Phase 2의 2번과 동일),
   새 `Content`에 채록표의 **Shop 값**을 입력한다.
5. `ShopPanelContentController`를 붙이고 직렬화 필드 **9개를 전부** 연결한다.
   `_content`는 **새** `Content`를 가리켜야 한다.
   나머지 8개: `_decorationRowPrefab` `_decorationSlotPrefab` `_backgroundRowPrefab`
   `_backgroundSlotPrefab` `_decorationTab` `_backgroundTab` `_decorationTabImage` `_backgroundTabImage`
6. 씬 인스턴스를 교체하고 `MenuButtonBar._entries`를 다시 연결한다.

**verify**: 플레이 → 상점 → 장식/배경 탭 전환에 슬롯이 채워지고 휠로 스크롤된다.
직렬화 필드 9개가 인스펙터에서 하나도 비어 있지 않다. 하트 표시가 보인다.
(구매·하트 차감은 상점 P3 미착수 상태라 이 verify에서 제외한다.)

---

## Phase 4 — 옛 프리팹 삭제

**커밋 4** — 해시 `________________`

**사전 확인**: 씬의 패널 5개가 모두 새 Base 계열이고, Phase 1~3의 verify가 전부 통과한다.

지울 것 7개:
`UIPanel_Base_old.prefab`, `UIPanel_Shop_old.prefab`, `UIPanel_Settings_old.prefab`,
`UIPanel_Tutorial_old.prefab`, `UIPanel_Inventory_old.prefab`, `UIPanel_Collection_old.prefab`
(+ 각 `.meta`)

**verify**: Phase 1~3의 verify를 그대로 재실행해 통과한다.
Console에 `MissingReferenceException`이 없다.
`git status`에 의도치 않은 `.meta`나 `ProjectSettings/*` 변경이 없다.

---

## 사람이 정할 것

Unity를 열어 눈으로 보고 정해야 하는 것들. 파일에서는 답이 안 나온다.

1. **Shop 탭 영역 높이** — 탭 2개를 `Area_TabSelection`으로 옮길 때 `preferredHeight` 얼마로 할지.
   지금 탭이 루트에 떠 있어서 "원래 높이"라는 게 없다.
2. **Shop의 HeartDisplay 자리** — `Area_Header` 안인지, 아니면 `Area_TabSelection` 옆인지.
3. **각 패널 크기를 유지할지** — Tutorial·Inventory가 300×500인데 새 Base의 `Area_Header`가
   160이라 헤더가 화면의 3분의 1을 먹는다. 크기를 키우거나 헤더를 줄여야 할 수 있다.
4. **Shop 루트 크기** — 옛 배리언트 값을 인스펙터에서 확인.
5. **컨트롤러를 어디 붙일지** — 도감은 `Area_Content`에 붙였고 상점은 루트에 붙어 있다.
   통일할지 각자 두는지.

### 기준 스크린샷

Phase 1 시작 전에 플레이 모드에서 5개 패널을 열어 스크린샷을 남긴다.
`Docs/Development/baseline/` 에 `shop.png` `settings.png` `tutorial.png` `inventory.png` `collection.png`.
각 Phase의 verify에서 이것과 대조한다.

---

## 사고 시 응급 절차

**당황해서 저장하지 말 것.** 순서대로.

1. Unity에서 **저장하지 않는다.** `Ctrl+Z`를 몇 번 시도한다.
2. 안 되면 **Unity를 저장 없이 종료**한다(에디터가 메모리의 옛 상태를 파일에 덮어쓰는 걸 막는다).
3. 터미널에서 되돌린다.
   - 특정 파일만: `git checkout -- <경로>`
   - 해당 커밋 전체: `git revert <위에 적어둔 해시>`
4. Unity를 다시 실행하고 재임포트가 끝날 때까지 기다린다.

> **`GameScene.unity`는 부분 되돌림이 안 된다.** 씬을 되돌리기 전에 `git stash`로 현재 상태를
> 먼저 보관한다.

`*_old.prefab`은 Phase 4 전까지 그대로 남아 있으므로, 그 전까지는 씬 인스턴스를 옛 프리팹으로
되돌리는 것만으로도 복구된다.

---

## 이번 범위 밖

- **도감 패널 구성** — 이미 새 Base 위에 있다. 남은 다듬기는 별도 작업.
- **`SettingsPanelContentController` 배선과 탭 3개** — 현재 미부착.
- **팝업 4개** — `UIManager`에 모달 개념이 없어 뒤 패널이 계속 클릭 가능하다. 착수 전 결정 필요.
- **`ShopPanelContentController`의 죽은 직렬화 데이터** — 프리팹 YAML에 C# 클래스에 없는
  `_decorationItems` · `_backgroundItems` 키가 남아 있다. 다음 저장 때 Unity가 알아서 버린다.
