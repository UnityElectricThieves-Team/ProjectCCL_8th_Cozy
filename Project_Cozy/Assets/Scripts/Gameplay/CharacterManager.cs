using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 씬에 살아있는 캐릭터를 한 곳에서 관리하는 중앙 객체. 글로벌 동시 존재 캡 + alive 추적 +
/// 스폰 단일 지점(<see cref="Spawn"/>)을 제공한다.
///
/// - 무엇을 스폰할지(prefab)는 호출자가 인자로 넘긴다. 총량 캡(<see cref="_maxCount"/>)은
///   prefab 종류와 무관하게 공통 적용된다.
/// - 캐릭터가 Destroy되면 슬롯이 비어 재스폰 가능.
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

    private readonly List<GameObject> _alive = new List<GameObject>();

    /// <summary>현재 살아있는 캐릭터 수.</summary>
    public int AliveCount { get { Prune(); return _alive.Count; } }

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

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
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
        _alive.Add(instance);
        return instance;
    }

    /// <summary>파괴된(null) 항목을 추적 목록에서 제거.</summary>
    private void Prune()
    {
        for (int i = _alive.Count - 1; i >= 0; i--)
            if (_alive[i] == null) _alive.RemoveAt(i);
    }
}
