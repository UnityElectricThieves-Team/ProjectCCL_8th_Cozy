using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 씬에 살아있는 캐릭터를 한 곳에서 관리하는 중앙 객체. 글로벌 동시 존재 캡 + alive 추적 +
/// 스폰 단일 지점(<see cref="Spawn"/>)을 제공한다.
///
/// - 무엇을 스폰할지(prefab)는 호출자가 인자로 넘긴다. 총량 캡(<see cref="_maxCount"/>)은
///   prefab 종류와 무관하게 공통 적용된다.
/// - 캐릭터가 Destroy되면 슬롯이 비어 재스폰 가능.
/// - **추적 목록에 들어오는 문은 <see cref="Register"/> 하나다.** 스폰이든 씬 배치든 여기를 지나므로,
///   "살아있는 캐릭터 전부에 거는 규칙"(뷰포트 거주 영역 등)이 새는 경로가 없다.
/// - 시작 시 캐릭터가 하나도 없으면 최초 캐릭터를 한 마리 스폰한다. 게임에는 항상 캐릭터가
///   최소 한 마리 있어야 하고, 그 보장을 스폰 지점이 함께 갖는 편이 씬에 캐릭터를 직접
///   놓는 것보다 안전하다(씬 배치는 추적 목록을 지나지 않아 규칙에서 빠졌던 전례가 있다).
/// - 씬 단일 인스턴스(Singleton). 스폰 호출자(StarClickCharacterSpawner / 데모 버튼 등)는
///   <see cref="Instance"/>.Spawn(...) 만 호출하면 되며 별도 참조 wiring이 필요 없다.
/// </summary>
[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
public class CharacterManager : MonoBehaviour
{
    public static CharacterManager Instance { get; private set; }

    [Tooltip("맵에 동시 존재 가능한 최대 캐릭터 수(prefab 무관 총량). 파괴되면 슬롯이 빈다.")]
    [SerializeField, Min(1)] private int _maxCount = 10;

    [Header("최초 캐릭터")]
    [SerializeField, Tooltip("시작 시 캐릭터가 하나도 없으면 이 프리팹으로 한 마리 스폰한다. 비우면 스폰하지 않는다.")]
    private GameObject _initialCharacterPrefab;

    [SerializeField, Tooltip("최초 캐릭터를 놓을 위치(월드). 바닥보다 위면 떨어져서 착지한다. " +
        "뷰포트 밖이어도 ViewportLivingAreaBinder가 안으로 끌어들이므로 정확할 필요는 없다.")]
    private Vector3 _initialSpawnPosition = new Vector3(0f, 10f, 0f);

    private readonly List<GameObject> _alive = new List<GameObject>();

    /// <summary>현재 살아있는 캐릭터 수.</summary>
    public int AliveCount { get { Prune(); return _alive.Count; } }

    /// <summary>살아있는 캐릭터 목록(읽기 전용). 파괴된 항목을 걸러낸 뒤 돌려준다.
    /// 내부 리스트를 그대로 노출하므로 순회에 할당이 생기지 않는다 — 받는 쪽은 읽기만 할 것.
    /// "살아있는 모두에게 같은 규칙을 건다"는 작업(뷰포트 거주 영역 등)의 진입점이다.</summary>
    public IReadOnlyList<GameObject> Alive { get { Prune(); return _alive; } }

    /// <summary>캐릭터가 추적 목록에 등록된 직후 발행(스폰·씬 배치 both). 살아있는 캐릭터 전체에 거는
    /// 규칙을 새 캐릭터에도 즉시 걸기 위한 지점이다 — 뷰포트 같은 전역 상태는 등록 시점에 다시
    /// 알려주지 않으므로, 이 신호가 없으면 나중에 등록된 캐릭터만 규칙 없이 돌아다니게 된다.</summary>
    public event Action<GameObject> Registered;

    /// <summary>최대 동시 수. 인스펙터 노출 + 런타임 변경 가능.</summary>
    public int MaxCount
    {
        get => _maxCount;
        set => _maxCount = Mathf.Max(1, value);
    }

    /// <summary>지금 스폰 가능한가(캡 미만).</summary>
    public bool CanSpawn { get { Prune(); return _alive.Count < _maxCount; } }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        // 1) 씬에 미리 배치된 캐릭터는 Spawn을 거치지 않아 추적 목록에 없다. 먼저 찾아서 등록한다.
        //    빠뜨리면 "살아있는 캐릭터 전부에 거는 규칙"(뷰포트 거주 영역 등)이 그 캐릭터만 비껴가고,
        //    증상은 "그 캐릭터만 제한이 안 걸린다"로 나타난다. 동시 존재 캡도 그만큼 헐거워진다.
        var placed = FindObjectsByType<BaseCharacterController>(FindObjectsSortMode.None);
        for (int i = 0; i < placed.Length; i++) Register(placed[i].gameObject);

        // 2) 그래도 한 마리도 없으면 최초 캐릭터를 스폰한다. 순서가 이렇게 되어야
        //    씬에 배치된 캐릭터가 남아 있을 때 두 마리가 되지 않는다.
        if (_initialCharacterPrefab != null && _alive.Count == 0)
            Spawn(_initialCharacterPrefab, _initialSpawnPosition);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>추적 목록에 등록하고 <see cref="Registered"/>를 발행한다. 이미 있으면 아무 일도 하지 않는다.
    /// 스폰 경로와 씬 배치 캐릭터가 같은 문을 쓰게 해서, 등록 신호를 놓치는 경로가 생기지 않게 한다.</summary>
    public void Register(GameObject character)
    {
        if (character == null) return;
        Prune();
        if (_alive.Contains(character)) return;

        _alive.Add(character);
        Registered?.Invoke(character);
    }

    /// <summary>
    /// <paramref name="prefab"/>을 <paramref name="position"/>에 1개 생성하고 추적 목록에 등록한다.
    /// 캡(<see cref="MaxCount"/>) 도달 시 생성하지 않고 null 반환.
    /// </summary>
    public GameObject Spawn(GameObject prefab, Vector3 position, Transform parent = null)
    {
        if (prefab == null) return null;
        Prune();
        if (_alive.Count >= _maxCount) return null;

        var instance = Instantiate(prefab, position, Quaternion.identity, parent);
        Register(instance);
        return instance;
    }

    /// <summary>파괴된(null) 항목을 추적 목록에서 제거.</summary>
    private void Prune()
    {
        for (int i = _alive.Count - 1; i >= 0; i--)
            if (_alive[i] == null) _alive.RemoveAt(i);
    }
}
