# Characters/

**본편 캐릭터는 이 폴더에 있지 않다.** 지금 여기 남은 것은 `_test/rabbit/` 하나이고, 아트 파이프라인 표준을 거치지 않은 임시 픽스처라 통째로 삭제해도 된다.

```
Characters/
└── _test/
    └── rabbit/
        ├── rabbit.prefab
        └── sprites/
```

## 본편 캐릭터는 세 곳에 나뉘어 있다

| 무엇 | 어디 | 누가 만든다 |
|---|---|---|
| 스프라이트 시트·클립 | `Assets/Art/<이름>/` (`Image/`, `Clip/`) | 아트 |
| 오버라이드 컨트롤러 (폼별 클립 세트) | `Assets/Assets/Animations/AnimationSystem/<이름>_Override` | 개발 |
| 프리팹 | `Assets/Prefabs/Characters/Character_<이름>.prefab` | 개발 |

아트 폴더 안의 개별 `.controller`(`AnimatorController/`)는 아트 테스트 씬에서 미리 보기용으로 쓰는 것이라 본편에서는 쓰지 않는다.

## 새 캐릭터를 붙이는 순서

1. **시트 임포트 설정을 맞춘다.** PPU는 100 그대로 두고, 칸 단위 격자로 자르고, Read/Write를 켠다. Read/Write는 마우스 판정이 픽셀을 읽기 때문에 필요하다([Interaction/CLAUDE.md](../Scripts/Interaction/CLAUDE.md)의 `OpaqueHoverable`). 피벗은 같은 프리팹의 다른 폼과 발 위치가 맞게 넣는다([character-ground.md](../../../.claude/rules/unity/character-ground.md)).
2. **오버라이드 컨트롤러를 만든다.** 베이스는 `BaseCharacterAnimatorController`이고, 그 캐릭터에 있는 클립만 채운다. 클립이 없는 칸은 비워 둔다. 다른 캐릭터의 클립으로 채우면 그 상태에서 다른 캐릭터 그림이 튀어나온다.
3. **프리팹은 `Character.prefab`의 배리언트로 만든다.** 구조(Visual·판정·앵커)는 물려받고, 오버라이드·시작 폼·기본 스프라이트·모션 시간처럼 캐릭터마다 다른 값만 바꾼다. 크기 배율(Base Scale)은 바꾸지 않는다 — 지금 베이스 프리팹에 들어 있는 배율은 모든 캐릭터에 거는 임시 축소다([character-ground.md](../../../.claude/rules/unity/character-ground.md)).
4. **모션 시간을 실제 재생 시간에 맞춘다.** 쓰담·특수 대기는 클립이 끝나는 시점이 아니라 프리팹 인스펙터의 시간 값으로 끝난다. 재생 시간은 클립 길이 ÷ 상태 Speed라서, 클립의 프레임 수나 fps를 바꿀 때뿐 아니라 아래의 재생 속도를 바꿀 때도 이 값을 함께 바꾼다.

## 재생 속도

본편 캐릭터 클립은 그림 한 장을 1/30초씩 보여주는 30fps로 맞춘다. 아트가 보낸 클립이 12fps로 와서 너무 느렸기 때문에, Animation 창에서 클립마다 Samples를 30으로 바꿨다. 이 프로젝트의 Unity에서는 Samples를 바꾸면 키 간격도 같은 비율로 바뀌므로, 키를 손으로 옮길 필요는 없다.

- **새 클립을 받으면 Samples부터 확인한다.** 클립은 아트가 만든 자산이라, 같은 클립을 다시 받으면 이 수정이 덮인다.
- 클립 전체가 아니라 상태별로 조금씩 더 빠르게·느리게 하고 싶으면 `BaseCharacterAnimatorController`의 상태 Speed를 쓴다. 오버라이드는 클립만 갈아 끼우므로 모든 캐릭터와 두 폼에 함께 걸린다.
- 재생 속도를 바꾸면 위 4번의 모션 시간 값과 걷기·달리기 속도를 같이 본다. 걷기 클립만 빨라지고 이동 속도가 그대로면 발이 미끄러져 보인다.
5. **씬에 놓지 않고 `CharacterOwnership`의 대응표에 id와 프리팹을 등록한다.** id는 저장 키라 한번 정하면 바꾸지 않는다. 이유는 [character-ownership.md](../../../.claude/rules/unity/character-ownership.md).

캐릭터 코드(컨트롤러 / AI / 친밀도)는 [Scripts/Character/](../Scripts/Character/CLAUDE.md)에 있다. 아트 폴더에 스크립트를 두지 않는다.

## 그림자는 스프라이트에 굽지 않는다

캐릭터 스프라이트에 그림자를 그려 넣지 않는다. 실행 중에 런타임 컴포넌트가 캐릭터 아래에 그림자를 그린다.
아트는 그림자 없는 상태로만 만들면 된다.
