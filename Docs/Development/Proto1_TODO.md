# 1차 프로토 TODO

**마감**: 2026.6  
**목표**: 기본 캐릭터 1~2개로 대강 구동되는 수준  
**참고**: [로드맵.md](../로드맵.md) | [GameDesign.md](../GameDesign.md) | [AI_Logic.md](../AI_Logic.md) | [Animation_List.md](../Animation_List.md)

상태: `[ ]`   
진행 중: `[~]`  
완료: `[x]` 
블로커: `[!]`

## 구현 순서 (권장)

창 투과 → 별 충전/스폰 → 캐릭터 Grab/Fall/Landing → Petting 판정 → 아트 연결

아트 스프라이트가 없을 경우 임시 placeholder로 로직 먼저 구현.

---

## 창 / 렌더링

- [x] 투명 배경 + Borderless 창 *(OverlayWindow: WS_POPUP + LAYERED + ColorKey)*
- [x] Always-on-Top (앞단에 계속 유지가 되는지) *(HWND_TOPMOST + WM_EXITSIZEMOVE 보정)*
- [x] 클릭 투과 — 투명 영역 투과, 스프라이트 영역만 클릭 판정 *(LWA_COLORKEY per-pixel)*
- [x] 클릭/키보드 입력 시 카운트 누적 (별 시스템 연동) *(InputCounter)*

---

## 별 시스템 *(가제)*

- [x] 백그라운드 키보드/마우스 입력 수집 (게임 포커스 없어도 동작) *(OutFocusKeyHook/MouseHook, 전용 스레드 LL 훅)*
- [x] 입력 횟수 누적 카운트 *(InputCounter.Count, 스폰 시 ReduceSpawnEnergy 차감)*
- [~] 캐릭터별 해금 조건 충족 시 별 반짝임 연출 *(StarInputThreshold→UnityEvent 로직 O, 연출은 아트 의존)*
- [~] 반짝이는 별 클릭 → 해당 캐릭터 1개 스폰 후 Idle 복귀 *(StarClickCharacterSpawner 구현 O, 단 GameScene엔 데모버튼(CharacterSpawner)만 배치)*
- [x] 스폰 연출 — 화면 상단에서 낙하 후 착지 *(스폰 +Y 오프셋 → Fall→Land→Idle)*
- [ ] 별 스프라이트 — Idle(대기), Active(반짝임) *(아트, 코드로 확인 불가)*

> 해금 조건은 캐릭터마다 다름. 1차 프로토 기준 캐릭터 1종: 키보드/마우스 입력 100회 이상

---

## 캐릭터 AI

- [x] `Idle` — 기본 대기, 일정 시간 후 Walk 전환 *(IdleState)*
- [ ] `Idle Action` — 하품/기지개 등 일회성 특수 대기 *(선택)* *(enum/State 없음)*
- [x] `Walk` — 화면 내 랜덤 좌표 이동, 도착 시 Idle 복귀 *(WalkState, 좌/우 랜덤)*
- [~] `Run` — Walk보다 빠른 달리기 *(RunState 클래스만 존재, ChangeState(Run) 트리거 미연결)*
- [x] `Sleep` — X분 OS 입력 없으면 수면 진입 *(무입력 30초↑ + 5초마다 30% 확률)*
- [x] `Wake Up` — 재입력 감지 시 기상 모션 후 Idle 복귀 *(WakeUpState)*
- [ ] 화면 경계 처리 — 모니터 밖으로 나가지 않도록 *(신 시스템 미구현, 구 CharacterBrain에만 잔존)*
- [x] 바닥 기준선 설정 — 작업표시줄 상단 기준 착지 판정 *(y=_floorY 평면)*

---

## 캐릭터 물리 / 상태

- [x] `Grabbed` — 좌클릭으로 들어 올리기, 마우스 좌표에 고정 *(GrabbedState 마우스 추종 + 프리팹 ClickableEvent→RequestGrab)*
- [x] `Fall` — 클릭 해제 시 중력으로 낙하 *(FallState 자체 중력)*
- [x] `Landing` — 바닥 충돌 후 착지 모션 → Idle 복귀 *(LandState)*
- [x] 상태 잠금 — Landing 중 Grab/Petting 입력 무시 *(IsLockedState: WakeUp/Land/Transform)*

---

## 마우스 상호작용

- [x] 캐릭터 Hover 판정 (스프라이트 영역 기준) *(OpaqueHoverable 알파 검사)*
- [x] 좌클릭 → Grab *(프리팹 ClickableEvent→RequestGrab)*
- [~] 마우스 호버 중 좌우 흔들기 → Petting 판정 *(흔들기 제스처 미구현 — 호버 진입으로 대체 판정)*
- [x] `Petting` — 쓰담 모션 재생, 친밀도 증가 *(PetState + PettingReaction + AffinityModule)*
- [x] `Shift + 우클릭` → 친밀도 초기화 *(CharacterInteractionRelay)*

---

## 친밀도

- [~] Petting → 친밀도 증가 (상한 없음) *(증가 구현 O, 단 코드엔 상한 100 존재 — 기획 '상한 없음'과 불일치)*
- [x] 친밀도 만점 시 Special Idle / Special Walk 비주얼 전환 *(AffinityModule.SpecialActivated → StateModule.SpecialMode)*

---

## 아트

> 캐릭터 1개 = 동물 폼 + 소녀 폼 한 쌍. 둘 다 1차 필수.  
> 애니메이션 상세 설명 → [Animation_List.md](../Animation_List.md)
>
> ⚠️ 아래 항목은 **아트(스프라이트/클립) 산출물**이라 코드 점검으로 완료 여부를 판정할 수 없음 — 아트 측 확인 필요. 로직(상태머신)은 위 "캐릭터 AI/물리" 섹션 참조.

### 동물 폼

- [ ] `Idle` — 기본 대기
- [ ] `Idle Action` — 하품/기지개 등 특수 대기 *(선택)*
- [ ] `Walk` — 이동
- [ ] `Run` — 달리기 *(선택)*
- [ ] `Sleep` — 수면
- [ ] `Wake Up` — 기상
- [ ] `Petting` — 쓰담 받는 반응
- [ ] `Grabbed` — 들어 올려짐, 바둥거림
- [ ] `Fall` — 낙하 체공
- [ ] `Landing` — 착지
- [ ] `Transform` — 변신 이펙트 *(소녀 폼과 동일 에셋 공유)*
- [ ] `Interact` — 캐릭터별 특수 상호작용 *(상세는 Animation_List.md 참조)*

### 소녀 폼

- [ ] `Idle` — 기본 대기
- [ ] `Idle Action` — 옷매무새 다듬기/기지개 등 특수 대기 *(선택)*
- [ ] `Walk` — 이동
- [ ] `Run` — 달리기 *(선택)*
- [ ] `Sleep` — 수면
- [ ] `Wake Up` — 기상
- [ ] `Petting` — 쓰담 받는 반응
- [ ] `Grabbed` — 들어 올려짐, 바둥거림
- [ ] `Fall` — 낙하 체공
- [ ] `Landing` — 착지
- [ ] `Gift Drop` — 선물 드롭
- [ ] `Transform` — 변신 이펙트 *(동물 폼과 동일 에셋 공유)*
- [ ] `Interact` — 캐릭터별 특수 상호작용 *(상세는 Animation_List.md 참조)*

### 이후 TODO 기술 체크 

- [ ] 스토이 설명을 위한 다이얼로그가 필요할지?
- [ ] 클로드 문서 불필요한거 정리하기