# Character/

캐릭터(동물 / 변신 후 소녀) 단일 개체의 자율 거동과 친밀도 시스템 + 그 캐릭터들을 씬-레벨에서 지휘하는 *조정자(coordinator)*.

## 책임

- 한 캐릭터의 *상태 머신*과 *내부 수치*를 관리한다 — Idle/Walk 페이즈, 친밀도 누적·소진, 만점 시 Special 시각 전환 등.
- **씬-레벨 캐릭터 조정자**도 여기. 한 캐릭터의 거동이 아니라 *씬의 여러 캐릭터를 일괄 지휘*하는 매니저(예: 무입력 감지 시 모두 Sleep)는 한 캐릭터의 자율 거동과 *짝을 이루는 외부 명령자*이므로 같은 폴더에 둔다. 단 *입력 감지 등 OS 추상화*는 [Platform/Input](../Platform/Input/)에서만 소비하고, OS 호출을 직접 하지는 않는다.
- 마우스/키 입력 자체는 다루지 않는다 — 마우스 라우팅은 [Interaction/CLAUDE.md](../Interaction/CLAUDE.md)의 인터페이스(`IHoverable` 등)를 통해, OS-wide 입력은 [Platform/Input/](../Platform/Input/)의 컴포넌트 또는 `InputSystem` API를 *구독*해서 받는다.
- 캐릭터 *에셋*(스프라이트·애니메이션·프리팹) 자체는 [Characters/](../../Characters/CLAUDE.md)에. 이 폴더는 *코드만*.
- 자율 거동·상호작용 규칙은 [AI_Logic.md](../../../../Docs/AI_Logic.md), 액션 명세는 [Animation_List.md](../../../../Docs/Animation_List.md) 참조.

## 현재 들어 있는 것

- `CharacterAffinity2D.cs` — 한 캐릭터의 Idle/Walk 자율 거동 + 친밀도 시스템.
  - Idle/Walk을 인스펙터 노출 범위(`Vector2`)에서 랜덤 시간으로 번갈아 — `transform.position` 직접 갱신(Rigidbody2D 미사용).
  - `IHoverable.OnHoverEnter`마다 친밀도 += `_affinityPerHoverEnter`. 최대 도달 시 Animator의 `VisualState`를 Special_Idle/Special_Walk로 자동 전환 (퍼리 변신의 시드).
  - `IShiftRightClickable.OnShiftRightClick`으로 친밀도 0 리셋 — [README.md](../../../../README.md) §2의 변신 해제 조작과 일치.
  - 만점 진입·해제 시 현재 페이즈의 남은 시간을 재추첨해 시각 변화가 즉시 반영되도록.
  - 현재 친밀도는 `IHoverable.OnHoverEnter`-only로 누적하는 임시 단순화 — 정식 Petting 판정 룰(좌클릭 / hover+좌우 흔들기)은 [AI_Logic.md](../../../../Docs/AI_Logic.md).
- `CharacterBasicAI2D.cs` — Idle/Walk/Sleep/WakeUp/Fall/Land 6-상태 자율 거동. 친밀도와 분리된 단일 책임 컴포넌트.
  - 상태 머신은 State Pattern. `States/` 폴더의 6개 인스턴스를 Awake에서 한 번만 만들어 배열로 보관 → 전환 시 new 없음.
  - `RequestSleep` / `RequestWakeUp` / `RequestFall` public 메서드로 외부 트리거 수신 — Sleep은 같은 폴더의 [CharacterSleepPolicy](CharacterSleepPolicy.cs)(개체별)가 호출. deprecated인 [SleepController](SleepController.cs)도 같은 public API 사용. Sleep/Fall/Land/Pet/Grabbed 중에는 RequestSleep 무시 (짧은 트랜지션·점유 상태에선 외부 트리거 보류).
  - **바닥은 씬의 GameObject(`_groundLayerMask`에 속하는 Collider2D)이고**, 본 컴포넌트는 발 위치에서 아래로 짧은 raycast로 능동 질의(`TryGetGroundBelow`). 캐릭터는 *어떤 ground인지 모르고* "발 밑에 있나"만 묻는다 — 캐릭터가 바닥에 대한 정책 수치를 들고 다니지 않는다.
  - Start에서 `TryGetGroundBelow` 성공 → Idle, 실패(공중) → Fall. 자연스러운 자유낙하 후 hit 위치로 클램프하고 Land → Idle.
  - `StateChanged` 이벤트로 상태 라벨([CharacterStateLabel](../UI/CharacterStateLabel.cs))이 갱신을 받는다. *착지* 등의 게임 이벤트는 Physics 콜백이 아니라 *State 전환*(`FallState → LandState`)에서 처리한다.
  - 시각(애니메이션·색 틴팅)은 오늘 미연결 — Console + UI 라벨로만 동작 검증.
- `States/` — 6개 상태 클래스. `BaseCharacterState`(abstract) + `Idle/Walk/Sleep/WakeUp/Fall/Land`. 순수 C# 클래스(MonoBehaviour 아님)라 인스턴스 재사용·EditMode 테스트 가능.
  - Land는 Fall→Idle 사이의 짧은 트랜지션(착지 모션 placeholder). 실제 모션 자산이 붙으면 `LandState.OnEnter`에서 재생을 트리거.
  - 듀레이션 범위·속도·중력 등 정책 수치는 모두 owner(`CharacterBasicAI2D`)가 보유. State는 `owner.NextIdleDuration()` 같은 의미 있는 API만 호출하고 인스펙터 필드는 모른다.
- `SleepController.cs` — **[deprecated]** 씬 전역 일괄 수면 정책. `CharacterSleepPolicy`(개체별)로 대체. 코드/씬 인스턴스는 그대로 유지되나 신규 코드에서는 사용하지 않는다. 동시 활성 시 RequestSleep/WakeUp이 양쪽에서 호출돼 개체 정책이 무력화되므로 씬 인스턴스 비활성화 권장.
- `CharacterSleepPolicy.cs` — 캐릭터 개체별 수면 정책. 무입력 임계(`_idleThresholdSeconds`) 후 주기(`_sleepCheckInterval`)로 확률(`_sleepProbabilityPerCheck`) 검사 → `CharacterBasicAI2D.RequestSleep` 시도. 입력 감지 시 즉시 `RequestWakeUp`. 입력 4채널 구독: InFocus(`InputSystem.onAnyButtonPress` + `Mouse.current` 폴링) + OutFocus([OutFocusKeyHook](../Platform/Input/OutFocusKeyHook.cs) + [OutFocusMouseHook](../Platform/Input/OutFocusMouseHook.cs)). 개체차(Cat은 잘 안 자고 잠만보는 자주 잠 등)는 세 인스펙터 수치로 표현.

> **명명 차이 — 기획서 vs 코드**: 기획서([Animation_List.md](../../../../Docs/Animation_List.md), [AI_Logic.md](../../../../Docs/AI_Logic.md))는 이 상태를 `Spawn (착지)`로 명명하지만, *spawn*은 영어로 *나타남/생성*이라 *착지*에 의미가 맞지 않아 코드는 `Land`로 통일했다. 다음 yoojhong 회의의 기획서 명명 정합화 안건.

> 현재 이 컴포넌트가 부착된 `Character.prefab`은 `Assets/Prefabs/`에 있다. [Characters/CLAUDE.md](../../Characters/CLAUDE.md)의 *프리팹 콜로케이션 컨벤션*(프리팹은 사용하는 에셋과 같은 폴더에 둔다)과 어긋나므로, 정식 캐릭터로 승격될 때 `Characters/animals/<이름>/` 또는 `Characters/_test/<이름>/`로 이전 후보.

## 컨벤션

- **마우스 상호작용은 인터페이스로만.** `InputInteractionManager`로의 직접 의존 금지. `IHoverable` / `IClickable` / `IShiftRightClickable`(→ [Interaction/InteractionInterfaces.cs](../Interaction/InteractionInterfaces.cs))만 구현.
- **OS-wide 입력은 [Platform/Input/](../Platform/Input/)의 컴포넌트 또는 `InputSystem` API를 *구독*해서 받는다**. Character는 항상 *추상화된 입력 결과만 소비* — OS 호출(Win32 P/Invoke 등)은 직접 하지 않는다. 사용 가능한 입력 소스의 구체 사양은 [Platform/CLAUDE.md](../Platform/CLAUDE.md) 참조.
- **씬-레벨 조정자는 캐릭터 거동을 *명령*만**. State 결정은 항상 `CharacterBasicAI2D` 내부 (혹은 그 안의 State 클래스). 조정자는 `RequestSleep` 같은 *공개 메서드 호출*로만 영향을 준다 — 캐릭터 내부 상태를 직접 read/write 금지.
- **물리는 직접 갱신 — Rigidbody2D 미사용.** 매 프레임 `transform.position += ...` 패턴. 충돌 판정은 *Physics 콜백(`OnCollision...`/`OnTrigger...`)을 받지 않고*, `Physics2D.Raycast` 등으로 매 프레임 능동 질의(`CharacterBasicAI2D.TryGetGroundBelow`처럼). 게임 이벤트(착지, 먼지 등)는 Physics 발화가 아니라 State 전환에 묶는다.
- **Animator는 시각만, 분기는 C#.** 상태 결정은 코드에서 하고 Animator에는 Int 파라미터(`_visualStateParameter`)만 던진다. Animator 그래프가 게임 로직을 가지지 않도록.
- 그 외 네이밍·인스펙터 노출 규칙은 [Scripts/CLAUDE.md](../CLAUDE.md) + 루트 [CLAUDE.md](../../../../CLAUDE.md) §4 참조.

## 추후 후보 (지금은 만들지 않음)

- 동물 종별 특화 거동(고양이 냥냥펀치, 쥐 도망 등) — 별도 컴포넌트로 분리해 `CharacterAffinity2D`와 조합
- 그루핑된 캐릭터 간 상호작용(고양이↔쥐 추격) — 매니저 도입 후 결정
- 화면 밖 캐릭터 업데이트 스킵 — 루트 [CLAUDE.md](../../../../CLAUDE.md) §4.4 가이드 적용
