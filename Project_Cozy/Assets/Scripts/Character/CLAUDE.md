# Character/

캐릭터(동물 / 변신 후 소녀) 단일 개체의 자율 거동과 친밀도 시스템.

## 책임

- 한 캐릭터의 *상태 머신*과 *내부 수치*를 관리한다 — Idle/Walk 페이즈, 친밀도 누적·소진, 만점 시 Special 시각 전환 등.
- 마우스/키 입력 자체는 다루지 않는다 — 입력은 [Interaction/CLAUDE.md](../Interaction/CLAUDE.md)의 인터페이스(`IHoverable` 등)를 통해 호출만 받는다.
- 캐릭터 *에셋*(스프라이트·애니메이션·프리팹) 자체는 [Characters/](../../Characters/CLAUDE.md)에. 이 폴더는 *코드만*.

## 현재 들어 있는 것

- `CharacterAffinity2D.cs` — 한 캐릭터의 Idle/Walk 자율 거동 + 친밀도 시스템.
  - Idle/Walk을 인스펙터 노출 범위(`Vector2`)에서 랜덤 시간으로 번갈아 — `transform.position` 직접 갱신(Rigidbody2D 미사용).
  - `IHoverable.OnHoverEnter`마다 친밀도 += `_affinityPerHoverEnter`. 최대 도달 시 Animator의 `VisualState`를 Special_Idle/Special_Walk로 자동 전환 (퍼리 변신의 시드).
  - `IShiftRightClickable.OnShiftRightClick`으로 친밀도 0 리셋 — [README.md](../../../../README.md) §2의 변신 해제 조작과 일치.
  - 만점 진입·해제 시 현재 페이즈의 남은 시간을 재추첨해 시각 변화가 즉시 반영되도록.

> 현재 이 컴포넌트가 부착된 `Character.prefab`은 `Assets/Prefabs/`에 있다. [Characters/CLAUDE.md](../../Characters/CLAUDE.md)의 *프리팹 콜로케이션 컨벤션*(프리팹은 사용하는 에셋과 같은 폴더에 둔다)과 어긋나므로, 정식 캐릭터로 승격될 때 `Characters/animals/<이름>/` 또는 `Characters/_test/<이름>/`로 이전 후보.

## 컨벤션

- **상호작용은 인터페이스로만.** `InputInteractionManager`로의 직접 의존 금지. `IHoverable` / `IClickable` / `IShiftRightClickable`(→ [Interaction/InteractionInterfaces.cs](../Interaction/InteractionInterfaces.cs))만 구현.
- **물리는 직접 갱신 — Rigidbody2D 미사용.** 매 프레임 `transform.position += ...` 패턴. *벽 충돌 처리가 필요하면 `Physics2D.OverlapBox` 등으로 다음 위치를 사전 검사하는 방식*을 검토.
- **Animator는 시각만, 분기는 C#.** 상태 결정은 코드에서 하고 Animator에는 Int 파라미터(`_visualStateParameter`)만 던진다. Animator 그래프가 게임 로직을 가지지 않도록.
- 그 외 네이밍·인스펙터 노출 규칙은 [Scripts/CLAUDE.md](../CLAUDE.md) + 루트 [CLAUDE.md](../../../../CLAUDE.md) §4 참조.

## 추후 후보 (지금은 만들지 않음)

- 동물 종별 특화 거동(고양이 냥냥펀치, 쥐 도망 등) — 별도 컴포넌트로 분리해 `CharacterAffinity2D`와 조합
- 그루핑된 캐릭터 간 상호작용(고양이↔쥐 추격) — 매니저 도입 후 결정
- 화면 밖 캐릭터 업데이트 스킵 — 루트 [CLAUDE.md](../../../../CLAUDE.md) §4.4 가이드 적용
