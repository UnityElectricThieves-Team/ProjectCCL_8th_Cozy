# Character/

캐릭터(동물 / 변신 후 소녀) 단일 개체의 자율 거동·친밀도·시각 표현을 통합한다.

## 책임

- 한 캐릭터의 *상태 머신*과 *내부 수치*를 관리한다 — Idle/IdleAction/Walk 자율 행동 루프, Sleep 정책, 친밀도 누적, 폼(Animal/Girl) 변신.
- **단일 컴포넌트 부착 원칙** — GameObject에 사용자 정의 컴포넌트는 `BaseCharacterController` 하나만. 세부 책임은 4개 module(`StateModule`/`VisualModule`/`AffinityModule`/`ScaleModule`)이 nested로 분담.
- 마우스/키 입력 자체는 다루지 않는다 — 마우스 라우팅은 [Interaction/CLAUDE.md](../Interaction/CLAUDE.md)의 인터페이스(`IHoverable` 등)를 통해, OS-wide 입력은 [Platform/Input/](../Platform/Input/)의 컴포넌트를 *구독*해서 받는다.
- 캐릭터 *에셋*(스프라이트·애니메이션·프리팹) 자체는 [Characters/](../../Characters/CLAUDE.md)에. 이 폴더는 *코드만*.
- 자율 거동·상호작용 규칙은 [AILogic.md](../../../../Docs/Planning/AILogic.md), 액션 명세는 [AnimationList.md](../../../../Docs/Planning/AnimationList.md) 참조.

## 아키텍처

```
프리팹 루트 — Transform 외에 컴포넌트는 딱 하나
└─ [MonoBehaviour] BaseCharacterController   ← 단일 사용자 정의 컴포넌트 (non-sealed)
    ├─ [SerializeField] StateModule    _state         (nested, [Serializable])
    ├─ [SerializeField] VisualModule   _visual        (nested, [Serializable])
    ├─ [SerializeField] AffinityModule _affinity      (nested, [Serializable])
    └─ [SerializeField] ScaleModule    _scale         (nested, [Serializable])

자식 Visual GameObject — 보이는 것과 마우스 판정이 전부 여기
├─ Animator                  (BaseCharacterAnimatorController 자산 연결)
├─ SpriteRenderer
├─ BoxCollider2D             (마우스 히트 판정용)
├─ OpaqueHoverable           (IHoverable, 알파 검사 후 UnityEvent 발사 + 지금 호버 중인지 조회)
├─ HoldClickEvent            (좌클릭을 "누른 순간"과 "2초 도달"로 갈라 UnityEvent 발사)
└─ CharacterInteractionRelay (IShiftRightClickable — 친밀도 리셋만 위임)

자식 Shadow GameObject
└─ ShadowProjector           (바닥을 향해 판정해 그림자를 놓고, 멀어지면 폭을 줄인다)
```

**루트가 곧 발이다.** 발 위치를 나타내는 필드는 없다 — 프리팹에서 Visual 자식을 올려 스프라이트 아래 끝을
루트 원점에 맞춰 둔다. 그래서 `transform.position`이 그대로 발이고, 콜라이더나 스프라이트 bounds는 보지 않는다.
왜 이 규약인지와 새 캐릭터를 만들 때 지킬 것은 [character-ground.md](../../../../.claude/rules/unity/character-ground.md).

`BaseCharacterController`는 라이프사이클(`Awake`/`OnEnable`/`Update`/`OnDisable`/`OnDestroy`)을 받아 각 module로 위임. 종별 자식 클래스(`CatCharacterController` 등)는 Phase 10에서 도입 — `BaseCharacterController` 상속 + `RegisterExtraStates` hook.

## 통합 `CharacterState` enum

게임 로직 상태와 시각 상태가 **같은 enum 공유** (12개). StateModule이 정본, VisualModule은 같은 값을 `Animator.SetInteger`로 직결.

`Idle / Walk / Run / Sleep / WakeUp / Pet / Grabbed / Fall / Land / Transform / Interact / IdleAction`

Int 값은 [BaseCharacterAnimatorController.controller](../../Assets/Animations/AnimationSystem/BaseCharacterAnimatorController.controller) 자산의 State 인덱스와 1:1. 함부로 재정렬 금지.

## 현재 들어 있는 것

- **`BaseCharacterController.cs`** — 메인 컴포넌트, `IStateOwner` 구현. 라이프사이클 + 4 module + 지면(`IsFootOnGround`/`SnapToFloor`) + 거주 영역 + 자체 중력 + virtual hook(`RegisterExtraStates`) + 공개 메서드(`OnPetInput`/`Request{Sleep,WakeUp,Fall,Pet,Grab}`).
- **`CharacterState.cs`** — 통합 13-state enum + `CharacterForm` enum(Animal/Girl).
- **`IStateOwner.cs`** — State 클래스가 의존할 owner 인터페이스. 정책 수치·물리·지면 판정·ChangeState API 노출. 지면 *높이*는 내주지 않는다 — 판정은 `IsFootOnGround` 하나로 모여 있다. 걷기 목적지용으로 거주 영역의 *가로* 범위(`TryGetWalkRange`)만 내주는 것도 같은 이유다. 사각형을 통째로 주면 아래 변이 곧 지면 높이가 되어 버린다.
- **`CharacterInteractionRelay.cs`** — 자식 Visual에 부착, `IShiftRightClickable`만 책임 (Shift+우클릭 → 친밀도 리셋). `IHoverable`은 `OpaqueHoverable`에 양보하고, 좌클릭은 매니저 라우팅을 쓰지 않는다 — `HoldClickEvent`가 자체 폴링으로 누른 시간을 재야 하기 때문이다.
- **`Modules/StateModule.cs`** — State 머신 + Sleep 정책. 11 State 인스턴스 + `Request*` API + 잠금 가드(`IsLockedState`) + 접지 강제(`EnforceFloor`) + 입력 4채널 구독(InFocus·OutFocus). `RegisterState(IState)` 확장점. 수면은 확률이 아니라 무입력 시간만으로 결정되고, **캐릭터를 향한 좌클릭은 무입력 타이머를 초기화하되 캐릭터를 깨우지는 않는다**(자는 캐릭터를 누르면 기상 대신 쓰담이 뜬다).
- **`Modules/VisualModule.cs`** — Animator 단일 진입점. `Play(state)` / `PlayOneShot(state)` / `SetFacing` / `SetForm`. OneShot은 float timer 기반 (UniTask 미사용).
- **`Modules/AffinityModule.cs`** — 친밀도 수치 + `AffinityChanged` 이벤트. 시각 직접 제어 금지 — 값이 바뀌었다는 사실만 알리고 그걸로 무엇을 할지는 구독자가 정한다.
- **`Modules/ScaleModule.cs`** — 루트 `transform.localScale = _baseScale * User * Extra` 갱신. `ScaleMultiplierSettings.Character.Changed` 구독 + 호버 강조 같은 일시 `ExtraMultiplier` 슬롯 제공.
- **`ScaleMultiplier.cs` / `ScaleMultiplierSettings.cs`** — 직렬화 단위 + 종합 ScriptableObject. UI(`UI/CharacterScaleSlider.cs`)가 `Character.Value`를 set하면 `ScaleModule`이 구독해 적용. 단 그 슬라이더는 아직 어떤 씬·프리팹에도 배치되어 있지 않다.
- **`States/BaseCharacterState.cs`** — abstract. `OnEnter(IStateOwner)` / `Tick(IStateOwner, dt)` / `OnExit(IStateOwner)`.
- **`States/{Idle, IdleAction, Walk, Run, Sleep, WakeUp, Pet, Grabbed, Fall, Land, Transform}State.cs`** — 11개 State 클래스. `RunState`는 `WalkState` 상속(속도만 다름). `WalkState`는 시간이 아니라 *목적지 도착*으로 끝난다 — 거주 영역 안에서 목적지를 뽑고 거기까지 간다. `PetState`는 모션 시간이 지나면 스스로 Idle로 돌아간다 — 밖에서 꺼주는 API는 없다. `Interact`만 State 클래스가 없다 — enum에는 있지만 OneShot이라 `VisualModule.PlayOneShot`을 직접 호출한다.

## 상태 잠금 (기획서 §🛡️ 준수)

`StateModule.IsLockedState`가 `WakeUp`/`Land`/`Transform` 진행 중을 판정. 이들 중에는 `Request*` API 모두 무시 — 모션 중단 방지. Phase 8 이후 `Gift Drop`/`Consume`도 잠금 후보로 추가 검토.

## 컨벤션

- **마우스 상호작용은 인터페이스로만.** `InputInteractionManager`로의 직접 의존 금지. `IHoverable` / `IClickable` / `IShiftRightClickable`(→ [Interaction/InteractionInterfaces.cs](../Interaction/InteractionInterfaces.cs))만 구현.
- **OS-wide 입력은 [Platform/Input/](../Platform/Input/)의 컴포넌트 또는 `InputSystem` API를 *구독*해서 받는다**. Character는 *추상화된 입력 결과만 소비* — OS 호출(Win32 P/Invoke 등)은 직접 하지 않는다. `OutFocusKeyHook`/`OutFocusMouseHook`은 static 이벤트를 방송하므로, `StateModule`은 참조 없이 `OutFocusKeyHook.KeyPressed += ...`로 구독한다.
- **State 결정은 항상 코드 (StateModule)**. Animator 그래프는 시각만 — Int 파라미터 `VisualState` 하나만 받아 Any State → 각 state 트랜지션.
- **물리는 직접 갱신 — Rigidbody2D 미사용.** 매 프레임 `transform.position += ...` 패턴. 지면은 콜라이더가 아니라 수평선 하나이고, 접지는 매 프레임 강제된다(`StateModule.EnforceFloor`) — 세로를 스스로 쥐는 상태만 예외다. 게임 이벤트(착지, 먼지 등)는 Physics 발화가 아니라 State 전환에 묶는다. 지면 규약의 정본은 [character-ground.md](../../../../.claude/rules/unity/character-ground.md).
- **외부 참조는 인스펙터 또는 Singleton.** 런타임 Find / FindObjectOfType은 *씬 단일 인스턴스 보장 + Awake 1회* 케이스에만 허용.
- 그 외 네이밍·인스펙터 노출 규칙은 [Scripts/CLAUDE.md](../CLAUDE.md) + [.claude/rules/unity/csharp.md](../../../../.claude/rules/unity/csharp.md) 참조.

## 추후 후보 (지금은 만들지 않음)

- Sound/Fx + AnimationClip Animation Event — Phase 9
- 종별 자식 클래스(`CatCharacterController` 등) + 종 고유 state ID 체계 — Phase 10
- Gift Drop / Consume — 기획서 명시, 모션 자산 + 잠금 가드 확장 시
