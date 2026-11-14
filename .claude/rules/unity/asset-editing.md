---
paths:
  - "Project_Cozy/Assets/**/*.prefab"
  - "Project_Cozy/Assets/**/*.unity"
  - "Project_Cozy/Assets/**/*.asset"
  - "Project_Cozy/Assets/**/*.anim"
  - "Project_Cozy/Assets/**/*.controller"
  - "Project_Cozy/Assets/**/*.overrideController"
  - "Project_Cozy/Assets/**/*.mat"
  - "Project_Cozy/Assets/**/*.mixer"
  - "Project_Cozy/Assets/**/*.preset"
  - "Project_Cozy/Assets/**/*.shadergraph"
  - "Project_Cozy/Assets/**/*.inputactions"
  - "Project_Cozy/Assets/**/*.scenetemplate"
  - "Project_Cozy/Assets/**/*.meta"
  - "Project_Cozy/ProjectSettings/**"
---

# Unity 파일을 누가, 어떻게 고치는가

Claude가 직접 고쳐도 되는 것과 사용자가 Unity에서 고칠 것을 가르는 정본입니다. 씬·프리팹·ProjectSettings의 편집 규칙은 모두 여기로 모았습니다.

## 원칙

- **기준은 "Claude가 고쳤을 때 충돌하거나 고장 날 가능성이 높은가" 하나입니다.** YAML이라서 금지하는 것이 아니라, YAML의 *구조*(참조, fileID, 줄 추가·삭제)를 건드리거나 Unity가 덮어쓸 수 있을 때만 피합니다. 이미 있는 줄의 값은 안전하게 고칠 수 있습니다.
- **C# 코드와 대부분의 수치, 단순 설정은 Claude가 맡습니다.** 이미지 크기·위치·정렬·글자 정렬·Raycast Target 같은 값을 하나하나 사용자에게 넘기면, 손이 많이 가는 데 비해 얻는 안전이 없기 때문입니다.
- **등급은 "누가, 어떤 절차로 고치는가"만 정합니다.** 어떤 값이 맞는지(기획 문구는 Figma, PPU 100, 시트 긴 변 이하의 Max Size, 커서 무압축, BGM Streaming 등)는 각 주제 rules가 정합니다. 2등급이라고 해서 그 값이 허용된다는 뜻은 아닙니다.
- **판단이 서지 않으면 3등급으로 봅니다.**
- **Unity를 닫는 절차는 두지 않습니다.** Unity를 다시 여는 데 오래 걸려서, 닫는 대신 사전작업 확인으로 같은 안전을 얻기로 했습니다(2026-11-08). 그래서 "Unity를 닫으면 고쳐도 되는" 등급은 만들지 않습니다. 사전작업으로 안전해지지 않는 파일(씬 등)은 사용자가 Unity를 켠 채로 고칩니다.
- 검색으로 읽는 것은 어느 파일이든 언제나 됩니다.

## 고장 나는 경로는 둘입니다

1. **Unity가 덮어씁니다.** Unity가 그 파일을 메모리에 열어 두었거나, 저장·적용하지 않은 변경을 들고 있으면, 나중에 저장·적용할 때 메모리 판으로 디스크를 덮습니다. 그러면 Claude의 수정과 사용자의 작업이 함께 날아갑니다. 2026-07-19에 프리팹을 Unity에 연 채로 고치다가, 사용자가 Undo를 누르고 Auto Save가 돌면서 양쪽 작업이 모두 사라진 일이 있었습니다. 씬처럼 늘 로드된 파일은 사전작업으로도 메모리에서 내릴 수 없습니다.
2. **바꾼 값 너머가 조용히 깨집니다.** 오류 없이 다른 것이 끊깁니다. 네 가지 모양이 있습니다.
   - **연결 번호.** fileID·guid를 쓰거나, 줄을 넣고 빼면 참조가 끊깁니다.
   - **Unity가 그 값을 보고 다른 데이터를 다시 만드는 값.** 그림의 스프라이트 모드·텍스처 종류를 바꾸면 하위 스프라이트 표가 다시 만들어져, 애니메이션과 프리팹이 걸어 둔 참조가 끊깁니다. 애니메이션 샘플 수는 Unity가 모든 키의 시간까지 함께 바꾸는 값이라, 한 줄만 고치면 재생 속도가 그대로입니다(c9f37bf). 시트 긴 변보다 작은 Max Size는 Unity가 시트를 줄여 올려 1:1이 깨집니다.
   - **이름·순서·경로로 다른 곳이 기대는 값.** id·저장 키(상품·곡·캐릭터 id), 번역 키, 저장 형식 필드 이름, 정수로 저장되거나 Animator 같은 에셋이 정수로 참조하는 enum 값과 단계 번호, UnityEvent가 부르는 public 메서드의 이름과 시그니처, 글자가 같아야 하는 이름 짝(Animator 파라미터, 셰이더 프로퍼티, 믹서 파라미터), 코드가 문자열로 들고 있는 파일 경로가 여기에 듭니다.
   - **값 자체의 뜻을 넘어, 다른 기능이나 규약이 그 값을 전제로 동작하는 값.** 그림 PPU, Read/Write Enabled, 커서 그림의 압축·형식이 그렇습니다. 오브젝트·컴포넌트의 켜짐 상태도 여기에 듭니다. 싱글턴은 Awake에서 자신을 등록하고, 패널은 CanvasGroup으로 숨으며, 탭은 처음 켜질 때 Start가 돌기 때문입니다. Sorting Layer와 Order in Layer도 그렇습니다. 클릭 우선순위, 배경 마스크 범위, 그림자와 글자의 앞뒤가 이 값에 기댑니다. 다만 아래 2등급 예시의 단순 설정은 규약이 기대더라도 2등급이고, 대신 관련 rules를 먼저 읽습니다.

2등급은 "덮어쓰기를 사전작업으로 막을 수 있고, 2번에 해당하지 않는 값"입니다.

## 1등급: 그냥 고칩니다

- 대상: C# 코드와 코드 안의 수치(`GameBalance` 등), 문서, `.claude/` 규칙, `Localization/Strings.json`.
- 내용 규약은 그대로 지킵니다. `Docs/Planning/`은 고치지 않습니다(루트 CLAUDE.md §1). Strings.json의 문장은 [localization.md](localization.md)대로 기획(Figma)에 확인한 뒤 고칩니다. `GameBalance` 출시 값의 정본도 Figma입니다.

### C#에서 저장·에셋이 기대는 것을 바꿀 때

위 "이름·순서·경로로 기대는 값"을 코드나 Strings.json에서 바꿀 때는, 바꾸기 전에 기대는 곳(저장 파일, 프리팹·에셋, 번역 키, UnityEvent, Animator·셰이더·믹서, 경로 문자열)을 grep으로 찾습니다. 그리고 옛 데이터를 **살릴지 버릴지** 먼저 정합니다. 그냥 바꾸면 컴파일은 통과하는데 프리팹 값이나 사용자 저장값이 오류 없이 사라지거나 다른 뜻이 됩니다. 저장 파일은 Newtonsoft로 읽고, 모르는 속성은 무시하기 때문입니다.

- **살린다(기본).** 직렬화 필드는 `[FormerlySerializedAs("옛이름")]`, 저장 형식 필드는 `[JsonProperty("옛이름")]`를 붙입니다. enum과 단계 목록은 끝에만 추가하고 순서를 바꾸지 않습니다.
- **버린다.** 사용자가 고른 적 없는 기본값을 새로 적용하려는 키 교체는 [settings.md](settings.md)의 "기본값을 바꿀 때는 저장 키 이름도 바꿉니다"를 따릅니다.
- 순서를 바꾸는 절차가 주제 rules에 따로 있으면 그것을 따릅니다(예: localization.md의 언어 목록은 코드와 프리팹을 함께 고칩니다).
- 짝이 되는 에셋 쪽(프리팹의 번역 키 글자, Animator 파라미터 등)이 3등급이면, 그쪽은 사용자에게 바꿀 값을 안내합니다.
- 어느 쪽인지 정할 수 없으면(사용자 저장값이 사라지는 경우) 사용자에게 묻습니다.
- 코드가 덮어쓰는 Order 상수를 바꿀 때는 [background.md](background.md)와 [decoration-placement.md](decoration-placement.md)를 먼저 읽습니다.

### C# 파일의 이름과 위치

- MonoBehaviour·ScriptableObject는 클래스 이름과 파일 이름을 함께 바꾸고, `.meta`도 함께 옮겨 guid를 유지합니다. 둘 중 하나만 바꾸거나 `.meta`를 빼면 프리팹에서 스크립트가 Missing이 됩니다.
- C# 파일은 `Editor`·`Plugins` 폴더 경계를 넘겨 옮기지 않습니다. `.asmdef`가 없어 폴더 이름으로 어셈블리가 갈리므로, 넘기면 빌드에서 Missing Script나 컴파일 실패가 납니다.

## 2등급: 사전작업 확인 뒤 Claude가 고칩니다

값마다 허락을 묻지 않습니다. 대신 작업 하나에 한 번 사전작업을 확인합니다.

- **대상:** 프리팹, ScriptableObject 에셋(`.asset`), 그림·오디오 임포트 설정(`.meta`)에서 이미 있는 줄의 숫자·글자·색·bool 값 가운데, 위 2번에 걸리지 않는 것.
  - UI·월드 오브젝트의 단순 설정: 이미지·RectTransform의 크기·위치·앵커·피벗(UI 앵커입니다. [viewport-coordinates.md](viewport-coordinates.md)가 고정하는 월드 앵커와 다릅니다), 레이아웃 그룹의 정렬·간격·여백, 글자 크기·색·정렬 방식·줄바꿈·Auto Size, Raycast Target, 색·투명도.
  - 데이터·임포트: 상품 가격, 그림 압축 품질(커서 그림 제외), 시트 긴 변 이상으로 Max Size 올리기.
- **2등급이 아닌 경우:**
  - 다른 그림·프리팹을 연결하는 일(이미지의 스프라이트 바꾸기 등)은 참조라 3등급입니다.
  - TMP의 지원이 끝난 옛 정렬 필드는 건드리지 않습니다.
  - 배리언트·중첩 프리팹에서 아직 덮어쓰지 않은 값은 디스크에 줄이 없습니다. 그 값을 바꾸는 일은 override 줄 추가라 3등급입니다. 그 원본을 쓰는 모든 곳(배리언트, 다른 프리팹 안의 중첩 인스턴스, 씬 인스턴스)이 같은 값을 가져야 할 때만 원본 프리팹의 줄을 고칩니다(2등급). 요청이 한 곳만 바꾸는 것이면 원본은 고치지 않고 사용자에게 값을 안내합니다. 그 한 곳이 씬 인스턴스면 [scenes.md](scenes.md)·[prefabs.md](prefabs.md)대로 정말 그 인스턴스만의 값(위치 등)인지 먼저 따지고, 아니면 프리팹(필요하면 배리언트)에서 바꾸도록 안내합니다.
- **먼저 읽을 rules.** 그 값의 규약이 다른 rules에 있으면 먼저 읽습니다. 고칠 파일을 열어도 그 rules가 로드되지 않을 수 있기 때문입니다.
  - Raycast Target·투명도·Is Modal: [ui-panels.md](ui-panels.md)(클릭 통과, 막아 둔 버튼, 확인 팝업).
  - 배치 안내(PlacementGuide)의 피벗·Raycast Target·투명도: [decoration-placement.md](decoration-placement.md)와 ui-panels.md.
  - 그림의 크기·PPU·Max Size: viewport-coordinates.md와 [character-ground.md](character-ground.md). 판정 외곽선: [interaction-hit.md](interaction-hit.md). 커서 그림: ui-panels.md의 마우스 커서 절. 오디오: [audio.md](audio.md).
  - 임포트 설정에서 같은 이름의 필드가 플랫폼별로 여러 번 나오면, 플랫폼별 덮어쓰기가 켜진 블록이 이깁니다. 실제로 적용되는 블록을 고칩니다.
- **고치기 전 확인.** 같은 속성을 다른 것이 덮고 있지 않은지 봅니다. 덮고 있으면 디스크에서는 성공해도 화면에 나오지 않습니다.
  - 씬 인스턴스·배리언트의 override는 grep으로 찾습니다.
  - 레이아웃 그룹이 크기를 정하는 자식은 값을 넣어도 매번 덮입니다(ui-panels.md). 그런 값은 디스크에 0으로 저장되는 것이 정상입니다.
  - 실행 중에 값을 넣는 코드는 프리팹에 흔적이 없습니다. 그 오브젝트와 부모에 붙은 스크립트, 그리고 그 오브젝트를 직렬화 참조로 들고 있는 스크립트(프리팹 안에서 그 오브젝트의 fileID를 grep)를 열어 확인합니다. 예: UIPanel은 Awake에서 CanvasGroup의 투명도를 늘 다시 넣고, 설정 패널 컨트롤러는 형제 쪽 탭 Image의 색을 매번 다시 넣습니다.
- **사전작업.** 작업 하나에 한 번, 사용자에게 확인 질문으로 묶어 요청하고, **했다는 답을 받은 뒤** 편집합니다. 값을 무엇으로 바꿀지는 묻지 않습니다. 순서는 이렇습니다.
  1. Play 모드를 끝냅니다.
  2. 대상이 Prefab Mode나 편집 창에 열려 있으면 닫습니다. Prefab Mode 안의 Ctrl+S는 씬이 아니라 그 프리팹을 저장하므로 먼저 나옵니다.
  3. 열린 씬을 저장(Ctrl+S)하고 File > Save Project를 누릅니다.
  4. 임포트 설정이면 인스펙터에 Apply하지 않은 변경이 없게 하고 대상 선택을 풉니다.
- **편집은 파일을 읽은 뒤 편집 도구로만 합니다.** sed 같은 셸 편집은 쓰지 않습니다. 디스크가 바뀌었다는 경고와 이 규칙의 로드가 함께 빠지기 때문입니다.
- **메인 폴더에서 작업 트리의 YAML 에셋을 바꾸는 git 명령**(머지, pull, 브랜치 전환, rebase, cherry-pick, stash pop, reset 등)도 그 쓰기가 Unity가 붙은 디스크에 닿는 순간이라, 같은 사전작업을 먼저 요청합니다. 씬 저장 때문에 씬에 커밋 안 한 변경이 생겨 git이 머지를 거부하면, 그 씬 변경을 먼저 커밋할지 사용자와 정합니다. 씬은 git으로 되돌리지 않습니다.
- **편집 후 단계.** 사용자에게 Unity 창을 한 번 클릭해 디스크 변경을 다시 불러오게 안내합니다(Auto Refresh가 꺼져 있으면 Ctrl+R). 그 뒤 커밋 직전에 grep으로 값이 남았는지 다시 확인합니다. 편집 도구가 "디스크에서 바뀌었다"는 경고를 내거나 값이 되돌아가 있으면, 덮어쓰기가 진행 중이라는 신호이므로 바로 멈추고 보고합니다.

## 3등급: 사용자가 Unity에서 고치고, Claude는 값만 안내합니다

- 위 2번에 걸리는 모든 에셋 편집: 컴포넌트·계층·참조 바꾸기, 줄 추가·삭제, fileID·guid, 배리언트 override 항목 추가·삭제(stripped 블록 포함), 참조 필드와 참조 목록, 스프라이트 모드·텍스처 종류·PPU·Read/Write Enabled·Max Size 낮추기·커서 그림 압축·슬라이스 정보·스프라이트별 피벗, 애니메이션 샘플 수·커브·이벤트, id·저장 키·번역 키 글자, Animator 파라미터처럼 짝이 있는 이름 글자, 오브젝트·컴포넌트 켜고 끄기, Sorting Layer와 Order in Layer. 순서 값은 체계를 다시 정할 때도 사용자가 Unity에서 넣습니다.
- 파일 단위로 3등급:
  - 씬(`.unity`). 늘 로드되어 있어 덮어쓰기 위험이 가장 크고, 값은 원래 프리팹에서 고치는 것이 규칙입니다(scenes.md).
  - `ProjectSettings/*`. 잘못 고치면 프로젝트가 열리지 않을 수 있고, Unity가 수시로 다시 저장합니다.
  - Animator(`.controller`, `.overrideController`). 상태와 전이가 모두 fileID로 엮여 있습니다.
  - 늘 로드되어 에디터가 계속 고쳐 쓰는 에셋: `Assets/Fonts/*SDF.asset`, `Assets/TextMesh Pro/Resources/` 아래 `.asset`, `Assets/Settings/*.asset`(URP·Renderer), `Assets/UniversalRenderPipelineGlobalSettings.asset`, `Assets/DefaultVolumeProfile.asset`, `.mixer`, `.inputactions`.
- 위에 없는 Unity 파일 종류(`.mat`, `.shadergraph`, `.preset`, `.scenetemplate` 등)도 기본은 3등급입니다.
- 새 YAML 에셋이나 `.meta`를 손으로 만드는 일도 3등급입니다. guid를 지어내야 하므로 Unity의 Create 메뉴로 만듭니다.

### 예외: 게임 버전

ProjectSettings의 게임 버전 한 줄은 사용자가 "버전을 올리자"고 말했을 때만 Claude가 고칩니다. ProjectSettings는 늘 로드되어 있어 사전작업만으로는 확실하지 않으므로 확인 단계를 둡니다.

1. 사전작업에 Project Settings 창 닫기를 더합니다.
2. 고친 뒤 편집 후 단계를 거치고, 사용자에게 Player Settings에 새 버전이 보이는지 확인을 부탁합니다. Unity가 켜진 채 밖에서 바뀐 ProjectSettings를 다시 읽는지는 확인되지 않았기 때문입니다.
3. 안 보이면 Unity가 옛 값을 들고 있는 것이니, 사용자가 Player Settings에 직접 입력합니다.
4. 커밋은 새 버전을 확인한 뒤에 하고, [project-settings.md](project-settings.md)대로 다른 작업과 분리합니다.

## 파일 옮기기

- **1등급 파일**(C#, 문서, Strings.json 등)은 Claude가 `.meta`와 함께 옮깁니다. 경로 문자열로 기대는 곳은 grep으로 찾아 함께 고칩니다. C# 파일은 Editor·Plugins 경계를 넘기지 않습니다.
- **Unity 에셋**(프리팹, ScriptableObject, 그림·오디오, 씬, Animator, 설정 에셋 등, 그 에셋이 든 폴더 포함)은 사용자가 Unity의 Project 창에서 옮기거나 이름을 바꿉니다. Unity가 `.meta`와 빌드 목록을 함께 챙기기 때문입니다.
  - 옮기기 전에 Claude가 그 경로·파일 이름·폴더 이름에 기대는 곳을 grep으로 찾아 알립니다. Unity에서 옮겨도 이런 문자열은 따라오지 않습니다. 대상은 코드, `.claude/rules`의 paths와 CLAUDE.md 링크(경로가 바뀌면 그 규칙이 오류 없이 더는 로드되지 않습니다), ProjectSettings의 경로 문자열(예: Preset Manager의 BGM 폴더 필터 — 고치는 일은 3등급이라 값만 안내)입니다.
  - 예: 상품 생성 메뉴는 장식·배경 폴더와 상품 목록 에셋의 경로, 폴더 안 파일 이름을 상수로 들고 있습니다. 목록 에셋을 옮기면 메뉴가 옛 경로에 두 번째 목록을 새로 만들고, 씬은 옮긴 목록을 계속 가리켜, 이후 새 상품이 상점에서 조용히 빠집니다.
  - 기대는 코드는 1등급이라 Claude가 함께 고치고, 저장 키가 바뀌는 경우가 생기면 위의 "살릴지 버릴지"를 따릅니다.
  - 오디오를 BGM 폴더로 옮겨도 Streaming 프리셋은 처음 임포트할 때만 걸립니다(audio.md).

## git으로 되돌리기 (`git checkout --`, `git restore`)

- 커밋하지 않은 변경을 버리는 일이라, **되돌릴 파일 목록을 보여 주고 사용자에게 명시적으로 허락받습니다**(목록 하나에 한 번).
- 되돌려도 되는 것은 **되돌려서 잃을 것이 잡음뿐인 파일**이고, 다음 셋으로 한정합니다. 이 밖의 파일(URP 설정 에셋 등)은 되돌리지 않고 사용자에게 보여 판단을 구합니다.
  - TMP 폰트 SDF의 Dynamic 아틀라스가 부푼 것
  - Unity가 저절로 건드린 ProjectSettings
  - Unity가 저절로 건드린 `.meta`
- 되돌리기 전에 2등급 사전작업(저장, Play 종료)을 거치고, 되돌린 뒤에는 편집 후 단계로 Unity가 다시 불러오게 합니다. Unity가 메모리 판으로 다시 쓰더라도 잡음이 돌아올 뿐 작업은 잃지 않습니다.
- **씬은 git으로 되돌리지 않습니다.** 의도한 변경이 같은 파일에 섞여 있어서 통째로 되돌리면 그것까지 사라집니다. 씬 diff에 딸려 온 override는 사용자가 Unity에서 정리합니다. 삭제된 오브젝트를 가리키는 것은 Remove Unused Overrides로 지우고, 레이아웃이 매번 다시 쓰는 0값 override는 정상이라 그대로 둡니다.
- diff에서 일부 줄만 골라 빼는 것은 줄 삭제라 3등급입니다. YAML 에셋의 머지 충돌도 [git.md](../git.md)대로 손으로 풀되, 어느 쪽을 살릴지는 사용자와 정합니다.

### 예외: TMP 폰트 SDF 설정 커밋

폰트 에셋 설정을 바꾸면 아틀라스 잡음이 함께 쌓여서, 설정만 골라 커밋해야 합니다.

- 대상은 커밋된 옛 글자 데이터가 그 설정 아래서도 그대로 유효한 설정뿐입니다(Multi Atlas 켜기, 굵기 표 연결 등). 포인트 크기·여백·아틀라스 크기처럼 옛 글자 데이터와 섞이면 표시가 어긋나는 설정은 예외가 아니며, 그때 무엇을 커밋할지는 사용자와 정합니다. 대체 폰트는 폰트 에셋이 아니라 전역 설정에 겁니다(localization.md).
- 순서:
  1. 사전작업을 한 번 거칩니다.
  2. 설정은 사용자가 인스펙터에서 바꾸고, 디스크에 저장된 것을 확인합니다.
  3. 사용자 허락을 받습니다.
  4. Claude가 바뀐 설정 줄의 diff를 먼저 떠 둡니다.
  5. git으로 되돌리고, 곧바로 그 줄만 다시 넣습니다. 여러 줄인 참조도 diff를 글자 그대로 옮기고 새로 쓰지 않습니다.
  6. 그 뒤 한 번만 편집 후 단계를 거칩니다.
