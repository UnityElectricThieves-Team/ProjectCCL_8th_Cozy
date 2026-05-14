# Interaction/

마우스 입력 라우팅 + 인터랙터블 인터페이스 계약.

## 책임

- 카메라 좌표·콜라이더·sortingOrder를 봐서 *지금 마우스 아래에 어떤 인터랙터블이 있나*를 결정하고, 해당 객체에 메소드를 호출한다.
- 인터랙터블이 응답할 *계약*(어떤 메소드를 받는지)을 인터페이스로 노출한다 — 캐릭터/달/별 등 게임 객체가 이를 구현.
- 일부 *단순 상호작용 로직*(별 클릭 → 캐릭터 스폰처럼 한 컴포넌트로 끝나는 것)은 여기에 둘 수 있다. 큰 게임 시스템(친밀도·변신 등)은 자기 폴더([Character/](../Character/CLAUDE.md) 등)로 분리.

## 현재 들어 있는 것

- `InteractionInterfaces.cs` — `IHoverable` / `IClickable` / `IShiftRightClickable` 3개 계약. 모든 인터랙터블은 이 중 필요한 것만 구현하면 매니저가 자동 라우팅.
- `InputInteractionManager.cs` — 마우스 위치 → 월드좌표 → `Physics2D.OverlapPointNonAlloc` → sortingLayer/sortingOrder가 가장 높은 콜라이더에 라우팅. 마우스 픽셀 변화 없으면 재스캔 스킵하는 최적화 내장(`_skipRescanWhenPointerUnchanged`).
- `MoonClickIdle2D.cs` — 별(가제) 컴포넌트. `K`키로 Active 진입 → 클릭 시 prefab 리스트의 다음 1개 스폰 → 다시 Idle. 한 번 다 쓰면 더 이상 스폰 안 함.
- `InputInteractionTestProbe.cs` — 3개 인터페이스를 모두 구현하고 `Debug.Log`만 하는 시연/테스트용. 인터랙터블 셋업이 맞는지 확인할 때 GameObject에 부착.

## 컨벤션

- **콜라이더 필수.** 모든 인터랙터블은 같은 GameObject에 `Collider2D`가 있어야 매니저가 잡아낼 수 있다. `InputInteractionTestProbe`의 `OnValidate` 경고 패턴을 참고해 새 인터랙터블에도 같은 가드를 두면 셋업 실수를 빨리 잡는다.
- **인터페이스는 작게.** 새 상호작용 종류가 생길 때마다 인터페이스를 늘리기보다, 기존 셋 중 의미가 맞는 게 있으면 재사용. 정말 새 의미면 같은 파일에 추가.
- **매니저는 *어떤 객체가 무엇을 하는지* 모른다.** 매니저는 콜라이더 위치와 sortingOrder만 본다. 구체 행동은 인터페이스 구현 측. 매니저에 게임 로직을 직접 넣지 말 것.
- **포인터-이동-없음 최적화의 전제.** `_skipRescanWhenPointerUnchanged = true`는 *마우스가 정지 중인 동안 인터랙터블도 움직이지 않는다*는 전제 위에 있다. 펫이 마우스 밑으로 *알아서* 들어오는 시나리오가 생기면 끄거나, 인터랙터블 측에서 "위치 변경" 신호를 매니저에 push하는 메커니즘 추가.
- 그 외 네이밍 규칙은 [Scripts/CLAUDE.md](../CLAUDE.md) + 루트 [CLAUDE.md](../../../../CLAUDE.md) §4 참조.

## 추후 후보 (지금은 만들지 않음)

- 알파 픽셀 정밀 hover 게이트 — 콜라이더 안의 *투명 영역*에선 hover 무시. 인터랙터블 측 데코레이터 또는 매니저 옵션 둘 다 후보.
- 키보드 인터랙터블 계약 — 현재 키 입력은 `Platform/Input/GlobalKeyboardHook`가 *전역*으로 처리하고, 매니저는 마우스만. 통합 검토는 본편 통합 시점.
