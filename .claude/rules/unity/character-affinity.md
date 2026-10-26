---
paths:
  - "Project_Cozy/Assets/Scripts/Character/Modules/AffinityModule.cs"
  - "Project_Cozy/Assets/Scripts/Character/CharacterInteractionRelay.cs"
  - "Project_Cozy/Assets/Scripts/Gameplay/BridgeAffinityHeart.cs"
  - "Project_Cozy/Assets/Scripts/Gameplay/CharacterOwnership.cs"
  - "Project_Cozy/Assets/Scripts/Gameplay/CharacterOwnershipFileFormat.cs"
  - "Project_Cozy/Assets/Scripts/UI/CharacterStateLabel.cs"
---

# 캐릭터 친밀도 규약

친밀도를 어떻게 들고, 저장하고, 하트로 바꾸는지에 대한 규칙입니다. 구체 수치(쓰담 1회당 오르는 양, 하트 지급 단위)는 프리팹 인스펙터에서 확인하세요.

## 친밀도는 누적값 하나이고, 줄어들지 않습니다

기획에 친밀도가 감소하거나 소모되는 경우가 없습니다. 그래서 값은 누적 친밀도 하나뿐이고, 쓰담으로만 오릅니다. 소녀 변신 가능 여부도 이 값으로 판정합니다.

예전에는 "현재 친밀도"와 "누적 친밀도"를 따로 들고, Shift+우클릭으로 현재 친밀도를 0으로 되돌리는 기능이 있었습니다. 기획을 옮기는 과정에서 잘못 들어간 기능이라 근거가 없어 지웠습니다. 리셋이 없으면 두 값이 늘 같아지므로 하나로 합쳤습니다. **친밀도를 줄이는 경로를 다시 만들지 않습니다.** 기획 사본(`Docs/Planning/UserSettings.md`)의 "친밀도 리셋" 옵션도 같은 이유로 근거 없음 표시가 붙어 있습니다.

## 저장은 CharacterOwnership이 합니다

친밀도는 캐릭터별 기록(`CharacterOwnership`의 레코드)에 함께 저장됩니다. 캐릭터 쪽 `AffinityModule`이 직접 저장하지 않는 이유는 둘입니다.

- Character 레이어는 저장소(`Platform/Data/`)를 부르지 않습니다(`Scripts/CLAUDE.md`의 레이어 표).
- `AffinityModule`은 캐릭터마다 하나씩 있는 모듈이라, 저장 파일 하나에 대응하는 주체가 될 수 없습니다.

**쓰담할 때마다 바로 저장합니다.** 저장을 미루면 비정상 종료 때 친밀도가 하트를 받은 시점보다 뒤로 돌아가고, 같은 단계의 하트를 다시 받게 됩니다. 작은 파일 하나를 원자적으로 쓰는 것이라 비용은 작습니다.

## 복원은 이벤트를 울리지 않고, 하트 기준선은 Start에서 잡습니다

하트 지급(`BridgeAffinityHeart`)은 누적 친밀도가 일정 단위를 새로 넘을 때마다 하트를 줍니다. 이미 지급한 단위 수는 저장하지 않고, 그때그때 친밀도에서 다시 계산합니다. 친밀도가 줄지 않으므로 계산만으로 충분합니다.

여기에 하트 복제 함정이 있습니다. 재시작 후 친밀도를 복원하면서 하트 지급 쪽이 "지급한 단위 수 0"으로 시작하면, 첫 쓰담에 지난 실행에서 받은 하트를 전부 다시 받습니다. 그래서 두 가지를 같이 지킵니다.

- **복원(`AffinityModule.Restore`)은 친밀도 변경 이벤트를 울리지 않습니다.** 울리면 하트 지급 쪽이 복원된 값만큼의 하트를 그 자리에서 지급합니다.
- **하트 지급 쪽은 지급한 단위 수를 `Start`에서 현재 친밀도로 맞춥니다.** `CharacterOwnership`은 스폰 직후 같은 흐름에서 복원하고, 새 오브젝트의 `Start`는 그 뒤에 불립니다. `Awake`에서 맞추면 복원 전 값(0)을 읽어 함정이 그대로 남습니다.

복원이 이벤트를 울리지 않으므로, 친밀도를 화면에 보여주는 쪽(디버그 상태 글자 등)도 `Start`에서 현재 값을 한 번 읽어야 합니다.
