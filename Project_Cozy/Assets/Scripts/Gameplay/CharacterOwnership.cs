using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Assets.Scripts.Contents.CollectionSystem.Model;

/// <summary>
/// "어떤 캐릭터를 가졌고, 그중 누가 화면에 나와 있어야 하는가"의 주인. 캐릭터별 기록(보유·배치·누적 친밀도)을
/// 파일에 들고, 시작할 때 보유한 캐릭터를 모두 만들어(화면에 없어야 하는 것은 숨겨) 친밀도를 되돌린다.
/// 숨긴(도감으로 귀환한) 캐릭터는 꺼져 있어 시간 친밀도를 받지 않는다 — 국기님 결정(2026-11-08), .claude/rules/unity/character-affinity.md.
/// 친밀도가 오르면 기록에 저장한 뒤 저장돼 있던 값과 비교해 오른 만큼 하트를 준다(<see cref="AffinityHeartReward"/>).
/// 친밀도를 캐릭터(AffinityModule)가 직접 저장하지 않는 이유: Character 레이어는 저장소(Platform/Data)를
/// 부르지 않고, 캐릭터 모듈은 캐릭터마다 따로 있어 파일 하나에 대응하지 않는다.
///
/// <see cref="CharacterManager"/>와 나눈 이유: 그쪽은 "지금 살아 움직이는 캐릭터"(살아있는 목록,
/// 동시 존재 상한, 스폰 단일 지점, 유저 설정 적용)를 맡는다. 반면 이쪽은 재시작해도 남아야 하는 기록이다.
/// 둘은 수명이 다르다 — 캐릭터를 회수하면 살아있는 목록에서는 빠지지만 가졌다는 기록은 남는다.
/// 그래서 저장·해금·배치는 CharacterManager가 아니라 이 클래스에 둔다.
/// 실제 생성은 언제나 <see cref="CharacterManager.Spawn"/>을 거친다.
///
/// 캐릭터는 씬에 놓지 않는다. 화면에 나와 있는 캐릭터는 저장 기록이 정한다.
/// 첫 실행(저장 파일 없음)에는 아무 캐릭터도 보유하지 않은 상태라 화면이 비어 있는 것이 정상이다.
/// Development Build도 같다 — 대신 해금 기운이 테스트 값(<see cref="GameBalance.TestUnlockEnergy"/>)이라 금방 열린다.
/// 규약과 이유는 .claude/rules/unity/character-ownership.md.
///
/// 기록이 바뀔 때마다 즉시 저장한다. 해금·배치는 가끔 일어나는 이산 이벤트라
/// <see cref="SaveScheduler"/>를 거치지 않는다(하트·상점과 같은 판단).
///
/// 해금은 구름(<see cref="CloudController"/>)을 누를 때 일어난다. 어떤 캐릭터가 얼마의 누적 스폰 기운에서 열리는지는
/// 도감 정의(CollectionTool이 내보낸 collection.dat)의 <c>CharacterId</c>·<c>RequiredSpawnEnergy</c>가 정본이라,
/// 시작할 때 거기서 해금 순서만 뽑아 둔다. 도감 화면이 보여주는 필요 기운과 실제 해금 기준이 갈라지지 않게 하려는 것이다.
///
/// 등장은 뷰포트가 처음 확정된 뒤로 미룬다. 그 전에는 뷰포트 영역을 몰라서 위치를 정할 수 없다
/// (.claude/rules/unity/viewport-coordinates.md).
///
/// 실행 순서가 -99인 이유: CharacterManager(-100)의 Awake가 Instance를 잡은 뒤이고,
/// 그 Start가 씬에 놓인 캐릭터를 먼저 등록한 뒤에 스폰하도록 하려는 것이다.
/// </summary>
[DefaultExecutionOrder(-99)]
[DisallowMultipleComponent]
public class CharacterOwnership : MonoBehaviour
{
    public static CharacterOwnership Instance { get; private set; }

    /// <summary>
    /// 어떤 캐릭터의 기록(보유·배치·누적 친밀도)이 바뀌었는지 알린다. 인자는 캐릭터 id.
    /// 도감처럼 기록을 보여주는 쪽이 구독해 그 캐릭터만 다시 그린다.
    /// </summary>
    public event Action<string> RecordChanged;

    /// <summary>캐릭터 id와 프리팹의 짝. id는 저장 키라 한번 정하면 바꾸지 않는다.</summary>
    [Serializable]
    public class CharacterPrefabEntry
    {
        public string id;
        public GameObject prefab;
    }

    [Tooltip("캐릭터 id와 프리팹의 대응표. 저장 기록은 id만 들고 있고, 등장할 때 이 표에서 프리팹을 찾는다. " +
        "id는 저장 키라 한번 정하면 바꾸지 않는다.")]
    [SerializeField] private CharacterPrefabEntry[] _characterPrefabs;

    [Tooltip("등장할 때 뷰포트 아래 변(지면)에서 몇 월드 단위 위에서 떨어뜨릴지. 0이면 바닥에서 바로 시작해 " +
        "낙하로 등장하지 않으므로 0보다 커야 한다. 뷰포트 위 변을 넘으면 위 변에서 떨어진다.")]
    [SerializeField, Min(0.01f)] private float _dropHeight = 1.08f;

    private CharacterOwnershipFileFormat _data = new();

    private ViewportScreenSettings _viewportSettings;
    private BaseSpaceCameraFitter _cameraFitter;

    // 뷰포트가 처음 확정되기를 기다리는 중인가. 첫 신호에서 등장시키고 구독을 푼다.
    private bool _waitingForViewport;

    /// <summary>이 클래스가 만든 캐릭터 하나. 화면에 없어도(숨겨도) 오브젝트와 친밀도 구독은 그대로 든다.</summary>
    private sealed class OwnedCharacter
    {
        public string id;
        public BaseCharacterController controller;
        public Action<int> onAffinityChanged;
    }

    // 만든 캐릭터 전부 — 화면에 나온 것과 숨긴 것 모두. 구독 해제·초기화·귀환·소환이 이 목록 하나를 돈다.
    // 캐릭터가 몇 종뿐이라 선형 탐색으로 충분하다(FindRecord와 같은 판단).
    private readonly List<OwnedCharacter> _characters = new();

    // 해금 순서 — 도감 정의에서 뽑은 (캐릭터 id, 필요 누적 스폰 기운)을 필요 기운 오름차순으로 든다.
    private readonly List<(string id, int requiredEnergy)> _unlockSteps = new();

    private void Awake()
    {
        Instance = this;

        _data = UserDataSaveIO.Load<CharacterOwnershipFileFormat>(GameDataPaths.CharacterOwnership);

        // 에디터 세이브는 사람이 열어 고칠 수 있는 평문 JSON이라, null 필드나 같은 id 레코드가 들어올 수 있다.
        // 같은 id가 둘이면 캐릭터가 두 마리 나오므로 앞의 것 하나만 남긴다.
        _data.records ??= new List<CharacterRecord>();
        var seen = new HashSet<string>();
        _data.records.RemoveAll(r => r == null || string.IsNullOrEmpty(r.id) || !seen.Add(r.id));

        LoadUnlockSteps();
    }

    private void Start()
    {
        _viewportSettings = FindFirstObjectByType<ViewportScreenSettings>();
        _cameraFitter = FindFirstObjectByType<BaseSpaceCameraFitter>();
        if (_viewportSettings == null || _cameraFitter == null || CharacterManager.Instance == null)
        {
            Debug.LogWarning($"[{nameof(CharacterOwnership)}] ViewportScreenSettings/BaseSpaceCameraFitter/CharacterManager 중 " +
                "씬에 없는 것이 있어 캐릭터를 등장시키지 않습니다.", this);
            return;
        }

        // 뷰포트가 이미 확정됐으면 바로, 아니면 첫 확정 신호를 기다린다.
        if (_viewportSettings.IsReady)
        {
            SpawnAllOwned();
            return;
        }
        _waitingForViewport = true;
        _viewportSettings.ViewportConfirmed += OnFirstViewportConfirmed;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;

        if (_waitingForViewport && _viewportSettings != null)
            _viewportSettings.ViewportConfirmed -= OnFirstViewportConfirmed;

        UnsubscribeAll();
    }

    // ViewportConfirmed는 확정될 때마다 울리지만, 등장은 처음 한 번만 필요하다.
    private void OnFirstViewportConfirmed(RectInt _)
    {
        _waitingForViewport = false;
        _viewportSettings.ViewportConfirmed -= OnFirstViewportConfirmed;
        SpawnAllOwned();
    }

    private void UnsubscribeAll()
    {
        for (int i = 0; i < _characters.Count; i++)
        {
            var owned = _characters[i];
            if (owned.controller != null) owned.controller.Affinity.AffinityChanged -= owned.onAffinityChanged;
        }
    }

    private bool IsViewportReady => !_waitingForViewport && _viewportSettings != null
        && _viewportSettings.IsReady && _cameraFitter != null && CharacterManager.Instance != null;

    /// <summary>
    /// 보유한 캐릭터를 모두 만들고, 화면에 나와 있지 않아야 하는 것은 바로 숨긴다.
    /// 숨긴 캐릭터도 오브젝트를 두는 이유: 변신 카운터·소녀 상태·1분 미만 나머지 시간이 오브젝트 안에만 있어서, 파괴하면 잃는다.
    /// 꺼진 오브젝트는 Update·렌더·판정이 멈추고(그래서 시간 친밀도도 오르지 않는다), 그림은 대응표가 프리팹을 참조해
    /// 원래부터 메모리에 있으므로 비용이 거의 없다.
    /// </summary>
    private void SpawnAllOwned()
    {
        for (int i = 0; i < _data.records.Count; i++)
        {
            var record = _data.records[i];
            SpawnCharacter(record.id);
            if (!record.placed) HideFromField(record.id);
        }
    }

    /// <summary>캐릭터를 파괴하지 않고 화면에서 숨긴다. 살아있는 목록에서 빼고(<see cref="CharacterManager.Unregister"/>) 끈다.
    /// 목록에서 빼는 이유: 그 목록과 동시 존재 상한은 "화면에 나온" 캐릭터를 뜻한다.</summary>
    private void HideFromField(string id)
    {
        var owned = FindCharacter(id);
        if (owned == null || owned.controller == null) return;

        var go = owned.controller.gameObject;
        if (CharacterManager.Instance != null) CharacterManager.Instance.Unregister(go);
        go.SetActive(false);
    }

    private OwnedCharacter FindCharacter(string id)
    {
        for (int i = 0; i < _characters.Count; i++)
            if (_characters[i].id == id) return _characters[i];
        return null;
    }

    /// <summary>
    /// 캐릭터를 해금한다. 보유 기록을 만들고 화면에 내보낸다 — 기획상 처음 해금된 캐릭터는 바로 떨어지며 등장한다.
    /// 대응표에 없는 id는 기록하지 않는다. 기록하면 지울 방법 없이 시작할 때마다 경고가 남는다.
    /// 이미 보유한 캐릭터면 아무 일도 하지 않는다 — 도감으로 귀환해 둔 캐릭터를 해금 경로(테스트 메뉴 등)가 다시 불러내지 않게.
    /// </summary>
    private void Unlock(string id)
    {
        if (FindPrefab(id) == null)
        {
            Debug.LogWarning($"[{nameof(CharacterOwnership)}] 대응표에 없는 캐릭터 id '{id}'라 해금하지 않았습니다.", this);
            return;
        }

        if (FindRecord(id) != null) return;
        _data.records.Add(new CharacterRecord { id = id, placed = false });
        if (Summon(id)) return;

        // 화면 상한에 걸려 소환이 거절돼도 해금 기록은 남긴다(Summon은 거절하면 저장하지 않는다).
        // 그 캐릭터는 다음 실행에 숨은 채로 만들어지고, 도감에서 소환할 수 있다.
        Save();
        RecordChanged?.Invoke(id);
    }

    /// <summary>
    /// 보유했지만 화면에 없는 캐릭터를 화면에 내보낸다(도감 [소환]). 이미 나와 있으면 아무 일도 하지 않는다 — 같은 캐릭터가 두 마리가 되지 않게.
    /// 화면에 나온 캐릭터의 동시 상한에 걸리면 기록을 바꾸지 않고 거절한다 — 기록과 화면이 어긋나지 않게.
    /// 바뀌었으면 true.
    /// </summary>
    public bool Summon(string id)
    {
        var record = FindRecord(id);
        if (record == null || record.placed) return false;
        var manager = CharacterManager.Instance;
        if (manager != null && !manager.CanSpawn)
        {
            Debug.LogWarning($"[{nameof(CharacterOwnership)}] 화면에 나온 캐릭터가 상한이라 '{id}'를 내보내지 않았습니다.", this);
            return false;
        }

        record.placed = true;
        Save();
        RecordChanged?.Invoke(id);

        // 뷰포트가 아직이면 기록만 해 둔다. 첫 확정 때 보유한 캐릭터 전부와 함께 만들어진다.
        if (IsViewportReady) ShowOnField(id);
        return true;
    }

    /// <summary>화면에 나와 있는 캐릭터를 도감으로 들여보낸다(도감 [귀환]). 파괴하지 않고 숨긴다 — 이유는 <see cref="SpawnAllOwned"/>.
    /// 연출 없이 바로 사라진다(Figma "캐릭터가 사라짐"). 바뀌었으면 true.</summary>
    public bool Recall(string id)
    {
        var record = FindRecord(id);
        if (record == null || !record.placed) return false;

        record.placed = false;
        Save();
        RecordChanged?.Invoke(id);

        HideFromField(id);
        return true;
    }

    /// <summary>이 캐릭터가 화면에 나와 있어야 하는가. 보유하지 않았으면 false.</summary>
    public bool IsPlaced(string id) => FindRecord(id)?.placed ?? false;

    /// <summary>
    /// 숨겨 둔 캐릭터를 뷰포트 안 무작위 가로 위치 위에서 떨어뜨리며 보인다. 오브젝트가 없으면(스폰이 실패했던 경우) 새로 만든다.
    ///
    /// **켜기가 등록보다 먼저여야 한다.** 등록(<see cref="CharacterManager.Register"/>)이 거주 영역을 걸면서 접지를 강제하는데,
    /// 꺼진 채로는 숨기 전 상태(대기 등)라 바닥으로 끌어내려 낙하가 사라진다. 켜는 순간 캐릭터가 스스로 낙하로 다시 들어가고
    /// (BaseCharacterController.OnEnable), 낙하는 세로를 스스로 쥐어 끌려 내려가지 않는다.
    /// </summary>
    private void ShowOnField(string id)
    {
        var owned = FindCharacter(id);
        if (owned == null || owned.controller == null)
        {
            SpawnCharacter(id);
            return;
        }

        var go = owned.controller.gameObject;
        go.transform.position = RandomDropPosition();
        go.SetActive(true);
        CharacterManager.Instance.Register(go);
    }

    // 뷰포트 안 무작위 가로 위치, 지면(뷰포트 아래 변)에서 _dropHeight 위. 뷰포트 위 변을 넘지 않는다.
    private Vector3 RandomDropPosition()
    {
        var area = _cameraFitter.BaseRectToWorld(_viewportSettings.Viewport, _viewportSettings.BaseSpaceSize);
        return new Vector3(
            UnityEngine.Random.Range(area.xMin, area.xMax),
            Mathf.Min(area.yMin + _dropHeight, area.yMax),
            0f);
    }

    private void SpawnCharacter(string id)
    {
        var prefab = FindPrefab(id);
        if (prefab == null)
        {
            // 저장 기록은 지우지 않는다. 대응표에 다시 등록되면 저절로 다시 나온다.
            Debug.LogWarning($"[{nameof(CharacterOwnership)}] 대응표에 없는 캐릭터 id '{id}'라 등장시키지 않았습니다.", this);
            return;
        }

        var instance = CharacterManager.Instance.Spawn(prefab, RandomDropPosition());
        if (instance == null)
        {
            Debug.LogWarning($"[{nameof(CharacterOwnership)}] 동시 존재 상한에 걸려 '{id}'를 등장시키지 못했습니다.", this);
            return;
        }

        if (!instance.TryGetComponent(out BaseCharacterController controller)) return;

        // 저장된 친밀도를 되돌리고, 오를 때마다 기록에 써서 바로 저장한 뒤 오른 만큼 하트를 준다.
        // 하트의 기준은 저장돼 있던 값이다 — 재시작해도 지난 하트를 다시 주지 않는다. 저장이 먼저인 이유는 AffinityHeartReward.Grant.
        // 복원은 이벤트를 울리지 않는다 — 이유는 AffinityModule.Restore 주석.
        var record = FindRecord(id);
        var affinity = controller.Affinity;
        affinity.Restore(record.cumulativeAffinity);
        Action<int> onChanged = value =>
        {
            var before = record.cumulativeAffinity;
            record.cumulativeAffinity = value;
            Save();
            AffinityHeartReward.Grant(before, value);
            RecordChanged?.Invoke(id);
        };
        affinity.AffinityChanged += onChanged;
        _characters.Add(new OwnedCharacter { id = id, controller = controller, onAffinityChanged = onChanged });
    }

    /// <summary>지금 누적 스폰 기운으로 해금할 수 있는데 아직 보유하지 않은 캐릭터가 있는가.
    /// 구름이 활성 상태를 정하려고 매 프레임 묻는다 — 몇 개뿐인 목록을 할당 없이 훑는다.</summary>
    public bool HasUnlockable(int spawnEnergy) => FindNextUnlockable(spawnEnergy) != null;

    /// <summary>구름을 눌렀을 때 부른다. 해금할 수 있는 캐릭터 중 필요 기운이 가장 낮은 하나만 해금한다.
    /// 여러 단계를 한꺼번에 넘겼어도 누를 때마다 하나씩 나온다. 해금했으면 true.</summary>
    public bool TryUnlockNext(int spawnEnergy)
    {
        var id = FindNextUnlockable(spawnEnergy);
        if (id == null) return false;
        Unlock(id);
        return true;
    }

    /// <summary>이 캐릭터가 실제로 해금되는 누적 스폰 기운. 테스트 값(<see cref="GameBalance.TestUnlockEnergy"/>)이 반영된다.
    /// 도감의 미해금 안내가 이 값을 보여줘야 화면과 실제 해금이 갈라지지 않는다. 해금 순서에 없는 캐릭터면 fallback.</summary>
    public int GetRequiredSpawnEnergy(string id, int fallback)
    {
        for (int i = 0; i < _unlockSteps.Count; i++)
            if (_unlockSteps[i].id == id) return _unlockSteps[i].requiredEnergy;
        return fallback;
    }

    // 필요 기운 오름차순이라, 기운이 모자란 단계를 만나면 뒤는 볼 필요가 없다.
    private string FindNextUnlockable(int spawnEnergy)
    {
        for (int i = 0; i < _unlockSteps.Count; i++)
        {
            var step = _unlockSteps[i];
            if (step.requiredEnergy > spawnEnergy) return null;
            if (FindRecord(step.id) == null) return step.id;
        }
        return null;
    }

    // 도감 정의에서 해금 순서만 뽑는다. 사진까지 든 정의 전체는 들고 있지 않는다 — 시작할 때 한 번 읽고 버린다.
    // CharacterId가 비었거나 대응표에 없는 항목은 해금할 수 없으므로 뺀다(도감 쪽도 빈 id는 잠그지 않는다).
    private void LoadUnlockSteps()
    {
        CollectionBoolData collection;
        try
        {
            collection = CollectionDataRuntime.Load();
        }
        catch (Exception e)
        {
            // collection.dat은 배포 데이터라 깨져 있으면 빌드 문제다. 캐릭터 기록과 등장까지 멈추지 않게 로그만 남긴다.
            Debug.LogError($"[{nameof(CharacterOwnership)}] 도감 정의를 읽지 못해 구름 해금을 쓸 수 없습니다: {e.Message}", this);
            return;
        }

        var entries = collection?.collectionDataList;
        if (entries == null) return;

        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            if (entry == null || string.IsNullOrEmpty(entry.CharacterId)) continue;
            if (FindPrefab(entry.CharacterId) == null)
            {
                Debug.LogWarning($"[{nameof(CharacterOwnership)}] 도감 항목의 캐릭터 id '{entry.CharacterId}'가 대응표에 없어 해금 순서에서 뺐습니다.", this);
                continue;
            }
            _unlockSteps.Add((entry.CharacterId, Mathf.Max(0, entry.RequiredSpawnEnergy)));
        }

        _unlockSteps.Sort((a, b) => a.requiredEnergy.CompareTo(b.requiredEnergy));

        // 테스트 값이면 순서는 도감 정의대로 두고 기운만 갈아 끼운다. 목록보다 캐릭터가 많으면 남는 캐릭터는 마지막 값.
        var testEnergy = GameBalance.TestUnlockEnergy;
        if (testEnergy == null || testEnergy.Length == 0) return;
        for (int i = 0; i < _unlockSteps.Count; i++)
            _unlockSteps[i] = (_unlockSteps[i].id, testEnergy[Mathf.Min(i, testEnergy.Length - 1)]);
    }

    /// <summary>이 캐릭터를 보유했는가(해금됐는가). 화면에 나와 있는지와는 별개다.</summary>
    public bool IsOwned(string id) => FindRecord(id) != null;

    /// <summary>저장된 누적 친밀도. 보유하지 않은 캐릭터면 0.</summary>
    public int GetCumulativeAffinity(string id) => FindRecord(id)?.cumulativeAffinity ?? 0;

    private CharacterRecord FindRecord(string id)
    {
        for (int i = 0; i < _data.records.Count; i++)
            if (_data.records[i].id == id) return _data.records[i];
        return null;
    }

    // 캐릭터가 몇 종류뿐이고 해금·등장 때만 불리므로 선형 탐색으로 충분하다.
    private GameObject FindPrefab(string id)
    {
        if (_characterPrefabs == null) return null;
        for (int i = 0; i < _characterPrefabs.Length; i++)
        {
            var entry = _characterPrefabs[i];
            if (entry != null && entry.id == id) return entry.prefab;
        }
        return null;
    }

    /// <summary>
    /// 게임 초기화(<see cref="GameProgressReset"/>). 모든 기록(해금·배치·친밀도)을 지우고 저장한 뒤,
    /// 화면에 나와 있는 캐릭터를 모두 내린다. 친밀도를 줄이는 경로를 만들지 않는다는 규약
    /// (.claude/rules/unity/character-affinity.md)의 예외로, 사용자가 확인 팝업에서 직접 고른 경우에만 불린다.
    /// </summary>
    public void ResetProgress()
    {
        // 파괴할 캐릭터의 친밀도 모듈을 붙든 구독을 먼저 끊는다.
        UnsubscribeAll();

        // 숨긴 캐릭터는 살아있는 목록에 없으므로 자기 목록으로도 파괴한다. 씬에 직접 놓은 테스트 캐릭터는
        // 자기 목록에 없으므로 살아있는 목록으로도 파괴한다. 둘에 같이 든 것을 두 번 Destroy해도 괜찮다.
        for (int i = 0; i < _characters.Count; i++)
            if (_characters[i].controller != null) Destroy(_characters[i].controller.gameObject);
        _characters.Clear();

        if (CharacterManager.Instance != null)
        {
            var alive = CharacterManager.Instance.Alive;
            for (int i = 0; i < alive.Count; i++) Destroy(alive[i]);
        }

        var ids = new List<string>(_data.records.Count);
        for (int i = 0; i < _data.records.Count; i++) ids.Add(_data.records[i].id);
        _data.records.Clear();
        Save();
        for (int i = 0; i < ids.Count; i++) RecordChanged?.Invoke(ids[i]);
    }

    /// <summary>
    /// 현재 기록을 파일에 쓴다. 쓰기 실패는 로그만 남기고 삼킨다 — 저장이 안 됐다고 해금을 되돌리면
    /// 화면에 나온 캐릭터와 기록이 어긋나 더 이상해진다(ShopSystem과 같은 판단).
    /// </summary>
    private void Save()
    {
        try
        {
            UserDataSaveIO.Save(GameDataPaths.CharacterOwnership, _data);
        }
        catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
        {
            Debug.LogError($"[{nameof(CharacterOwnership)}] 캐릭터 기록 저장 실패: {e.Message}", this);
        }
    }

#if UNITY_EDITOR
    // 대응표의 모든 캐릭터를 해금한다. 이미 보유·배치된 캐릭터는 Unlock이 건너뛰므로 여러 번 불러도 늘지 않는다.
    private void UnlockAll()
    {
        if (_characterPrefabs == null) return;
        for (int i = 0; i < _characterPrefabs.Length; i++)
            if (_characterPrefabs[i] != null) Unlock(_characterPrefabs[i].id);
    }

    // 스폰 기운을 모으지 않고 에디터에서 캐릭터를 띄우는 길이다. 운영과 같은 해금 경로를 부르므로
    // 저장·재시작 동작이 그대로 검증된다. 처음 상태로 되돌리려면 저장 파일을 지운다.
    [ContextMenu("테스트: 모든 캐릭터 해금")]
    private void EditorUnlockAll()
    {
        // 편집 모드에서 누르면 Awake가 돌지 않은 상태로 실제 저장 파일을 덮어쓸 수 있다.
        if (!Application.isPlaying) return;
        UnlockAll();
    }
#endif
}
