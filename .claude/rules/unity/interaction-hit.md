---
paths:
  - "Project_Cozy/Assets/Scripts/Interaction/**/*.cs"
  - "Project_Cozy/Assets/Scripts/Gameplay/StarController.cs"
  - "Project_Cozy/Assets/Prefabs/Character*.prefab"
  - "Project_Cozy/Assets/Prefabs/Star.prefab"
  - "Project_Cozy/Assets/Assets/ProtoTypeCharacter/**"
  - "Project_Cozy/Assets/Art/**"
---

# 월드 오브젝트의 마우스 판정 규약

캐릭터·별처럼 마우스로 만지는 월드 오브젝트의 판정 영역을 어떻게 정하는지에 대한 규칙입니다. 구체 설정은 프리팹과 그림 임포트 설정에서 확인하세요.

## 판정 모양은 그림에서 가져옵니다

판정 콜라이더는 `PolygonCollider2D` + `SpritePhysicsShapeSync`로 두고, **크기를 숫자로 적어 넣지 않습니다.** `SpritePhysicsShapeSync`가 지금 보이는 프레임의 외곽선(physics shape)을 콜라이더에 옮겨 담으므로, 애니메이션 프레임이나 좌우 반전이 바뀌면 판정도 따라 바뀝니다.

숫자로 적어 둔 판정은 그림이 바뀔 때마다 낡습니다. 실제로 월드 PPU를 바꾸면서 사각형 콜라이더 크기를 그림 배율에 맞춰 두 번 손으로 환산해야 했습니다. 그림에서 유도하면 PPU를 바꾸든 아트를 교체하든 판정이 저절로 맞습니다.

판정이 거칠거나 너무 촘촘하면 **콜라이더가 아니라 그림 임포트 설정**(Sprite Editor의 Custom Physics Shape, Outline Tolerance)을 고칩니다.

## 콜라이더와 픽셀 검사는 하는 일이 다릅니다

- **콜라이더**는 1차 거름입니다. 바탕화면 클릭 통과(`WindowManager`)와 마우스 라우팅(`InputInteractionManager`)이 이 모양을 봅니다. 콜라이더 밖은 클릭이 바탕화면으로 넘어갑니다.
- **픽셀 검사**(`OpaqueHoverable`)는 최종 판정입니다. 호버·꾹 누르기·쓰담은 커서 아래 픽셀이 불투명할 때만 일어납니다.

외곽선은 다각형 근사라 다리 사이 같은 작은 투명 틈을 품을 수 있습니다. 그 틈에서는 클릭이 바탕화면으로 넘어가지 않지만, 픽셀 검사가 걸러 주므로 캐릭터가 반응하지도 않습니다. 둘은 기준이 다르니(외곽선 근사 vs 알파 임계값) **한쪽을 다른 쪽에 맞추려고 튜닝하지 않습니다.** 둘 중 하나를 빼면 판정이 사각형으로 돌아가거나, 바탕화면 클릭이 투명 여백에서 막힙니다.

## 콜라이더는 받는 쪽과 같은 GameObject에 둡니다

라우팅 매니저는 콜라이더가 붙은 GameObject에서 `IClickable` 등을 찾습니다(정본은 [Interaction/CLAUDE.md](../../../Project_Cozy/Assets/Scripts/Interaction/CLAUDE.md)의 "콜라이더 필수"). 그래서 그림이 자식에 있는 오브젝트(별)는 콜라이더를 루트에 두고, `SpritePhysicsShapeSync`의 그림 칸에 자식의 SpriteRenderer를 연결합니다. 그림과 받는 쪽이 같은 GameObject(캐릭터의 Visual)면 칸을 비워 둬도 같은 GameObject에서 찾습니다.

새 콜라이더를 붙일 때는 **새 것을 먼저 붙이고 옛 콜라이더를 지웁니다.** `DraggableObject2D`·`HoldClickEvent`가 `Collider2D`를 요구해서, 마지막 콜라이더는 지워지지 않습니다.
