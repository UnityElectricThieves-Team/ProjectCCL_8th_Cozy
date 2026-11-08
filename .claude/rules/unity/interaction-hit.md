---
paths:
  - "Project_Cozy/Assets/Scripts/Interaction/**/*.cs"
  - "Project_Cozy/Assets/Scripts/Gameplay/CloudController.cs"
  - "Project_Cozy/Assets/Scripts/Animation/CloudVisual.cs"
  - "Project_Cozy/Assets/Prefabs/Characters/*.prefab"
  - "Project_Cozy/Assets/Prefabs/Cloud.prefab"
  - "Project_Cozy/Assets/Assets/ProtoTypeCharacter/**"
  - "Project_Cozy/Assets/Art/**"
---

# 월드 오브젝트의 마우스 판정 규약

캐릭터·구름처럼 마우스로 만지는 월드 오브젝트의 판정 영역을 어떻게 정하는지에 대한 규칙입니다. 구체 설정은 프리팹과 그림 임포트 설정에서 확인하세요.

## 판정 모양은 그림에서 가져옵니다

판정 콜라이더는 `PolygonCollider2D` + `SpritePhysicsShapeSync`로 두고, **크기를 숫자로 적어 넣지 않습니다.** `SpritePhysicsShapeSync`가 지금 보이는 프레임의 외곽선(physics shape)을 콜라이더에 옮겨 담으므로, 애니메이션 프레임이나 좌우 반전이 바뀌면 판정도 따라 바뀝니다. 그림만 평행 이동하는 연출도 따라갑니다(아래 "구름 연출" 절).

숫자로 적어 둔 판정은 그림이 바뀔 때마다 낡습니다. 실제로 월드 PPU를 바꾸면서 사각형 콜라이더 크기를 그림 배율에 맞춰 두 번 손으로 환산해야 했습니다. 그림에서 유도하면 PPU를 바꾸든 아트를 교체하든 판정이 저절로 맞습니다.

판정이 거칠거나 너무 촘촘하면 **콜라이더가 아니라 그림 임포트 설정**(Sprite Editor의 Custom Physics Shape, Outline Tolerance)을 고칩니다.

## 콜라이더와 픽셀 검사는 하는 일이 다릅니다

- **콜라이더**는 1차 거름입니다. 바탕화면 클릭 통과(`WindowManager`)와 마우스 라우팅(`InputInteractionManager`)이 이 모양을 봅니다. 콜라이더 밖은 클릭이 바탕화면으로 넘어갑니다.
- **픽셀 검사**(`OpaqueHoverable`)는 최종 판정입니다. 호버·꾹 누르기·쓰담은 커서 아래 픽셀이 불투명할 때만 일어납니다.

외곽선은 다각형 근사라 다리 사이 같은 작은 투명 틈을 품을 수 있습니다. 그 틈에서는 클릭이 바탕화면으로 넘어가지 않지만, 픽셀 검사가 걸러 주므로 캐릭터가 반응하지도 않습니다. 둘은 기준이 다르니(외곽선 근사 vs 알파 임계값) **한쪽을 다른 쪽에 맞추려고 튜닝하지 않습니다.** 둘 중 하나를 빼면 판정이 사각형으로 돌아가거나, 바탕화면 클릭이 투명 여백에서 막힙니다.

## 콜라이더는 받는 쪽과 같은 GameObject에 둡니다

라우팅 매니저는 콜라이더가 붙은 GameObject에서 `IClickable` 등을 찾습니다(정본은 [Interaction/CLAUDE.md](../../../Project_Cozy/Assets/Scripts/Interaction/CLAUDE.md)의 "콜라이더 필수"). 그래서 그림이 자식에 있는 오브젝트(구름)는 콜라이더를 루트에 두고, `SpritePhysicsShapeSync`의 그림 칸에 자식의 SpriteRenderer를 연결합니다. 그림과 받는 쪽이 같은 GameObject(캐릭터의 Visual)면 칸을 비워 둬도 같은 GameObject에서 찾습니다.

새 콜라이더를 붙일 때는 **새 것을 먼저 붙이고 옛 콜라이더를 지웁니다.** `DraggableObject2D`·`HoldClickEvent`가 `Collider2D`를 요구해서, 마지막 콜라이더는 지워지지 않습니다.

## 구름 연출 — 그림만 움직이고, 판정은 따라갑니다

구름은 위아래로 살짝 떠다니고(기획 댓글 "약간 둥둥"), 해금할 수 있을 때 반짝입니다. 둘 다 Visual 자식의 `CloudVisual`이 하고, `CloudController`는 상태와 드래그 중인지만 넘깁니다. 수치는 `CloudVisual` 인스펙터에서 확인하세요.

- **루트가 아니라 그림(Visual)만 움직입니다.** 루트 위치의 주인은 드래그와 뷰포트 재배치입니다. 루트에 모션을 넣으면 같은 값을 셋이 다툽니다. 수치 라벨은 그림의 형제라 움직이지 않습니다 — 숫자가 흔들리면 읽기 어렵습니다.
- **판정은 `SpritePhysicsShapeSync`가 그림의 평행 이동을 콜라이더 offset으로 따라가게 합니다.** 콜라이더 모양의 주인을 이 컴포넌트 하나로 두려는 것입니다. 연출 쪽이 offset을 직접 쓰면, 외곽선을 다시 담을 때 이미 들어간 이동과 겹쳐 두 배가 됩니다. 비교는 콜라이더 로컬 좌표로 합니다 — 월드로 비교하면 루트가 끌려간 거리까지 offset에 더해집니다. 회전·배율 변화는 따라가지 않습니다.
- **그림은 마스터 캔버스 1px 단위로 끊어 옮깁니다.** offset을 바꾸면 물리 도형을 다시 만들기 때문에, 1px이 바뀔 때만 옮겨 그 횟수를 줄입니다.
- **활성 표시는 같은 그림을 흰색으로 겹친 덧그림(`Shine`)의 알파입니다.** 발바닥 그림은 한 장뿐이고 Figma에 활성 모습이 없어서, 옛 별 그림의 "하얗게 밝아졌다 돌아오는" 반짝임을 새 그림 없이 냈습니다. 상태가 둘뿐이고 그림이 한 장이라 Animator를 쓰지 않습니다. 덧그림에는 콜라이더도 `SpritePhysicsShapeSync`도 달지 않습니다 — 판정은 Visual 그림 하나에서 가져옵니다.
- **덧그림은 창 알파 합성과 부딪힐 수 있습니다.** 반투명 덧그림이 겹친 픽셀의 알파가 1 아래로 내려가면 반짝이는 동안 바탕화면이 비칩니다([ui-panels.md](ui-panels.md)의 반투명 절). 그렇게 보이면 덧그림만 가산(Additive) 머티리얼로 바꿉니다.
- **둥둥·반짝임 수치는 `GameBalance`가 아니라 `CloudVisual`에 둡니다.** `GameBalance`는 친밀도·하트·해금 기운처럼 게임 밸런스 수치를 출시 값과 테스트 값 두 벌로 모으는 곳입니다. 연출 값은 기획 수치표에 없고 두 벌이 필요 없어서, 화면을 보며 인스펙터에서 맞춥니다.
- 드래그하는 동안은 둥둥을 멈춥니다. 잡은 그림이 커서 아래에서 흔들리지 않게 하고, 놓으면 멈춘 자리에서 이어 가서 튀지 않게 하려는 것입니다(우리 판단).
