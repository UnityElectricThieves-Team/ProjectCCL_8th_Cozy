using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// "어떤 캐릭터를 가졌고, 그중 누가 화면에 나와 있어야 하는가"의 주인. 캐릭터별 기록(보유·배치·누적 친밀도)을
/// 파일에 들고, 시작할 때 화면에 나와 있어야 하는 캐릭터를 다시 등장시키며 친밀도를 되돌린다.
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
/// 규약과 이유는 .claude/rules/unity/character-ownership.md.
///
/// 기록이 바뀔 때마다 즉시 저장한다. 해금·배치는 가끔 일어나는 이산 이벤트라
/// <see cref="SaveScheduler"/>를 거치지 않는다(하트·상점과 같은 판단).
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

    // 등장시킨 캐릭터의 친밀도 구독. 파괴될 때 풀기 위해 들고 있다.
    private readonly List<(AffinityModule affinity, Action<int> handler)> _affinitySubscriptions = new();

    private void Awake()
    {
        _data = UserDataSaveIO.Load<CharacterOwnershipFileFormat>(GameDataPaths.CharacterOwnership);

        // 에디터 세이브는 사람이 열어 고칠 수 있는 평문 JSON이라, null 필드나 같은 id 레코드가 들어올 수 있다.
        // 같은 id가 둘이면 캐릭터가 두 마리 나오므로 앞의 것 하나만 남긴다.
        _data.records ??= new List<CharacterRecord>();
        var seen = new HashSet<string>();
        _data.records.RemoveAll(r => r == null || string.IsNullOrEmpty(r.id) || !seen.Add(r.id));
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
            SpawnAllPlaced();
            return;
        }
        _waitingForViewport = true;
        _viewportSettings.ViewportConfirmed += OnFirstViewportConfirmed;
    }

    private void OnDestroy()
    {
        if (_waitingForViewport && _viewportSettings != null)
            _viewportSettings.ViewportConfirmed -= OnFirstViewportConfirmed;

        for (int i = 0; i < _affinitySubscriptions.Count; i++)
            _affinitySubscriptions[i].affinity.AffinityChanged -= _affinitySubscriptions[i].handler;
        _affinitySubscriptions.Clear();
    }

    // ViewportConfirmed는 확정될 때마다 울리지만, 등장은 처음 한 번만 필요하다.
    private void OnFirstViewportConfirmed(RectInt _)
    {
        _waitingForViewport = false;
        _viewportSettings.ViewportConfirmed -= OnFirstViewportConfirmed;
        SpawnAllPlaced();
    }

    private bool IsViewportReady => !_waitingForViewport && _viewportSettings != null
        && _viewportSettings.IsReady && _cameraFitter != null && CharacterManager.Instance != null;

    private void SpawnAllPlaced()
    {
        for (int i = 0; i < _data.records.Count; i++)
            if (_data.records[i].placed) SpawnCharacter(_data.records[i].id);
    }

    /// <summary>
    /// 캐릭터를 해금한다. 보유 기록을 만들고 화면에 내보낸다 — 기획상 처음 해금된 캐릭터는 바로 떨어지며 등장한다.
    /// 대응표에 없는 id는 기록하지 않는다. 기록하면 지울 방법 없이 시작할 때마다 경고가 남는다.
    /// 이미 보유한 캐릭터면 배치만 시도한다.
    /// </summary>
    private void Unlock(string id)
    {
        if (FindPrefab(id) == null)
        {
            Debug.LogWarning($"[{nameof(CharacterOwnership)}] 대응표에 없는 캐릭터 id '{id}'라 해금하지 않았습니다.", this);
            return;
        }

        if (FindRecord(id) == null) _data.records.Add(new CharacterRecord { id = id, placed = false });
        Place(id);
    }

    /// <summary>보유한 캐릭터를 화면에 내보낸다. 이미 나와 있으면 아무 일도 하지 않는다 — 같은 캐릭터가 두 마리가 되지 않게.</summary>
    private void Place(string id)
    {
        var record = FindRecord(id);
        if (record == null || record.placed) return;

        record.placed = true;
        Save();

        // 뷰포트가 아직이면 기록만 해 둔다. 첫 확정 때 배치된 캐릭터 전부와 함께 등장한다.
        if (IsViewportReady) SpawnCharacter(id);
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

        var area = _cameraFitter.BaseRectToWorld(_viewportSettings.Viewport, _viewportSettings.BaseSpaceSize);
        var position = new Vector3(
            UnityEngine.Random.Range(area.xMin, area.xMax),
            Mathf.Min(area.yMin + _dropHeight, area.yMax),
            0f);

        var instance = CharacterManager.Instance.Spawn(prefab, position);
        if (instance == null)
        {
            Debug.LogWarning($"[{nameof(CharacterOwnership)}] 동시 존재 상한에 걸려 '{id}'를 등장시키지 못했습니다.", this);
            return;
        }

        if (!instance.TryGetComponent(out BaseCharacterController controller)) return;

        // 저장된 친밀도를 되돌리고, 오를 때마다 기록에 써서 바로 저장한다(쓰담마다 저장).
        // 복원은 이벤트를 울리지 않는다 — 이유는 AffinityModule.Restore 주석.
        var record = FindRecord(id);
        var affinity = controller.Affinity;
        affinity.Restore(record.cumulativeAffinity);
        Action<int> onChanged = value =>
        {
            record.cumulativeAffinity = value;
            Save();
        };
        affinity.AffinityChanged += onChanged;
        _affinitySubscriptions.Add((affinity, onChanged));
    }

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
    // 별의 캐릭터 소환이 막혀 있는 동안 캐릭터를 띄울 길이 이것뿐이다. 운영과 같은 해금 경로를 부르므로
    // 저장·재시작 동작이 그대로 검증된다. 처음 상태로 되돌리려면 저장 파일을 지운다.
    [ContextMenu("테스트: 모든 캐릭터 해금")]
    private void EditorUnlockAll()
    {
        // 편집 모드에서 누르면 Awake가 돌지 않은 상태로 실제 저장 파일을 덮어쓸 수 있다.
        if (!Application.isPlaying) return;
        if (_characterPrefabs == null) return;
        for (int i = 0; i < _characterPrefabs.Length; i++)
            if (_characterPrefabs[i] != null) Unlock(_characterPrefabs[i].id);
    }
#endif
}
