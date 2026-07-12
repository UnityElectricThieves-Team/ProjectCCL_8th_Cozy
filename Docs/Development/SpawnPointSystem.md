# 스폰 기운 시스템

바탕화면에서 유저의 키보드·마우스 입력이 쌓이면 별(Star)에 "스폰 기운"이 차오르고, 기운이 임계값을 넘으면 별을 클릭해 캐릭터를 소환할 수 있다. 이 문서는 그 데이터가 어디서 만들어지고, 어떻게 소비되며, 저장 시스템이 어떻게 값을 주고받는지 정리한다.

관련 코드는 모두 `Project_Cozy/Assets/Scripts/Gameplay/`에 있다.

---

## 1. 두 개의 수치

스폰 기운의 실제 데이터는 `SpawnPointManager` 컴포넌트가 들고 있다. 이름 그대로 스폰 포인트(별)의 스폰 기운을 관리하는 컴포넌트다. 서로 다른 두 값을 함께 관리한다.

| 프로퍼티 | 기획 용어 | 성격 | 용도 |
|---|---|---|---|
| `SpawnPointManager.CurrentEnergy` | **스폰 기운** | 소비형 — 스폰할 때마다 차감 | 별 활성화·클릭 소환의 진행도 |
| `SpawnPointManager.CumulativeEnergy` | 누적 스폰 기운 | 줄지 않음 — 계속 쌓이기만 함 | 해금 진행도, 재파밍 방지 |

`CurrentEnergy`가 우리가 흔히 말하는 "스폰 기운"이다. 스폰으로 깎이면 다시 임계값 아래로 내려가 별이 잠긴다. `CumulativeEnergy`는 스폰으로 `CurrentEnergy`가 깎여도 유지되므로, "지금까지 이 유저가 얼마나 입력했나"를 나타낸다.

> 기획(`Docs/Planning/Progress_Numeric_Balance.md` §2)에서 캐릭터 해금 게이트는 **누적 스폰 기운**(`CumulativeEnergy`)이 임계(10 → 500 → 1,500 …)를 넘는 방식으로 정의돼 있다. 지금 코드가 `CurrentEnergy`를 스폰마다 차감하는 소비 모델은 프로토타입 단계의 동작이며, 기획의 누적 게이트와는 별개다. 이 문서는 현재 코드 동작을 기술한다.

---

## 2. 기운은 어떻게 차오르나 — 입력 4채널

`SpawnPointManager`는 게임 창의 포커스 여부와 무관하게 입력을 센다. 데스크톱 펫이라 창이 비활성 상태여도 바탕화면에서 친 키가 기운으로 쌓여야 하기 때문이다. 네 개의 경로를 하나의 `Increment()`로 합산한다.

| 채널 | 소스 |
|---|---|
| InFocus 키 | `InputSystem.onAnyButtonPress`의 `KeyControl` |
| OutFocus 키 | `OutFocusKeyHook.KeyPressed` (OS 전역 훅) |
| InFocus 마우스 | `Mouse.current` 버튼 폴링 (좌/우/휠 클릭) |
| OutFocus 마우스 | `OutFocusMouseHook.ButtonPressed` (OS 전역 훅) |

`Increment()`는 호출될 때마다 `CurrentEnergy`와 `CumulativeEnergy`를 **함께** 1씩 올린다. 모든 입력이 이 한 지점을 통과한다.

> InFocus 경로와 OutFocus 경로는 자연스럽게 상호 배타적이다. Unity의 InputSystem은 창이 비활성일 때 fire하지 않고, OutFocus 훅은 `Application.isFocused == false`일 때만 통과시킨다. 그래서 같은 입력이 두 번 세지지 않는다.

---

## 3. 기운은 어떻게 소비되나 — 별과 소환

`StarController`가 기운을 읽어 별의 상태와 소환을 관리한다.

- **상태 전환**: `CurrentEnergy >= _threshold`이면 `Activated`, 아니면 `Idle`인 2상태 머신. 상태가 바뀔 때만 Animator의 `StarState` Int 파라미터를 갱신한다.
- **클릭 소환**: 별을 클릭(드래그가 아닌 경우)하면 캐릭터 1개 소환을 `CharacterManager`에 요청한다.
  - 생성·동시 존재 캡 판정은 `CharacterManager`에 위임한다.
  - 캡에 막혀 생성에 실패하면 **기운을 차감하지 않는다**.
  - 성공하면 `SpawnPointManager.Spend(_threshold)`로 임계값만큼 `CurrentEnergy`를 깎는다. `CumulativeEnergy`는 그대로다.

기운을 깎을 때는 `Spend(int)`를 쓴다. 음수로 내려가지 않도록 0에서 클램프한다.

---

## 4. 별의 디버그 문 — 진행도 표시

`DebugCounterLabel`이 매 프레임 폴링해 TMP 라벨에 세 줄을 찍는다. 별의 디버그 문에 뜨는 "진행도"가 바로 이 값들이다.

```
Cumulative: {CumulativeEnergy}
Current:    {CurrentEnergy}     ← 스폰 기운
Activated:  {True/False}        ← StarController.IsActivated
```

---

## 5. 저장 API — 저장 시스템과의 seam

스폰 기운은 세션이 끝나면 사라지면 안 된다. 모아둔 기운도, 누적 진행도도 재접속 시 이어져야 한다. 그래서 `SpawnPointManager`는 저장 시스템이 값을 가져가고 되돌려 넣을 수 있는 seam을 노출한다. 재화 하트(`HeartSystem`)와 **같은 패턴**이다.

### 데이터 컨테이너 — `SpawnPointFileFormat`

```csharp
[Serializable]
public class SpawnPointFileFormat
{
    public int currentEnergy;    // = SpawnPointManager.CurrentEnergy    (소비형 스폰 기운)
    public int cumulativeEnergy; // = SpawnPointManager.CumulativeEnergy (줄지 않는 누적)
}
```

두 값을 모두 저장하는 이유: `currentEnergy`만 저장하면 재접속 시 누적 진행도가 사라지고, `cumulativeEnergy`만 저장하면 모아둔 기운이 날아간다. `SpawnPointManager`의 상태 두 개가 곧 저장 대상 전부다.

### API

```csharp
// 내보내기 — 현재 상태를 저장 데이터로 만든다.
SpawnPointFileFormat data = spawnPoint.ExportSave();

// 되돌려 넣기 — 저장 데이터를 상태에 주입한다. null은 무시.
spawnPoint.ImportSave(data);
```

`ImportSave`는 값만 세팅한다. `StarController`·`DebugCounterLabel`이 매 프레임 폴링하는 구조라 별도의 변경 이벤트를 발사하지 않는다.

### 파일 입출력과 연결하기

실제 파일 저장/로드는 공용 유틸리티 `GameDataStore`(`Platform/Data/`)가 담당한다. 저장 시스템은 이렇게 잇는다.

```csharp
// 저장
GameDataStore.SaveEncrypted("경로/spawnEnergy.dat", spawnPoint.ExportSave());

// 로드
var data = GameDataStore.LoadEncrypted<SpawnPointFileFormat>("경로/spawnEnergy.dat");
spawnPoint.ImportSave(data);
```

`GameDataStore`는 평문 JSON(`SavePlain`/`LoadPlain`)과 AES 암호화(`SaveEncrypted`/`LoadEncrypted`) 두 방식을 제공한다. 평문은 사람이 읽고 수정하는 작업용 원본, 암호화는 배포용 산출물이다.

> 현재 `ExportSave`/`ImportSave`의 호출자는 아직 없다. 통합 저장 시스템이 합류할 때를 위한 seam이다.

---

## 관련 파일

| 파일 | 역할 |
|---|---|
| `Gameplay/SpawnPointManager.cs` | 스폰 기운 데이터 소스 (4채널 합산, 소비, 저장 seam) |
| `Gameplay/SpawnPointFileFormat.cs` | 저장 데이터 컨테이너 |
| `Gameplay/StarController.cs` | 별 상태 머신 + 클릭 소환 + 기운 차감 |
| `Gameplay/CharacterManager.cs` | 소환·동시 존재 캡 판정 |
| `UI/DebugCounterLabel.cs` | 디버그 문 진행도 표시 |
| `Platform/Data/GameDataStore.cs` | 공용 파일 저장/로드 유틸리티 |
| `Platform/Input/OutFocusKeyHook.cs`, `OutFocusMouseHook.cs` | OutFocus 입력 소스 |
