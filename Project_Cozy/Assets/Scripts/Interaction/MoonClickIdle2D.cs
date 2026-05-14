using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 테스트용 달: <b>K</b>로 Animator Active(1). Active에서 좌클릭 시 리스트에서 <b>다음 한 개</b>만 생성(한 번 순서대로만, 끝나면 더 이상 스폰 없음) 후 Idle(0).
/// <see cref="InputInteractionManager"/>에 잡히려면 이 오브젝트에 <see cref="Collider2D"/>가 있어야 합니다.
/// </summary>
public sealed class MoonClickIdle2D : MonoBehaviour, IClickable
{
    private const int StateIdle = 0;
    private const int StateActive = 1;

    [SerializeField] private Animator _animator;
    [Tooltip("Animator Int. 컨트롤러: Idle=0, Active=1")]
    [SerializeField] private string _stateParameter = "MoonState";

    [Header("Spawn prefabs")]
    [Tooltip("클릭마다 리스트의 다음 비어 있지 않은 한 개만 Instantiate합니다. 리스트 끝까지 쓰면 이후 클릭은 스폰하지 않습니다.")]
    [SerializeField] private List<GameObject> _spawnPrefabs = new List<GameObject>();
    [SerializeField] private Transform _spawnParent;
    [Tooltip("모든 스폰 위치: 달 transform.position + 이 값 (여러 개면 같은 지점에 겹침)")]
    [SerializeField] private Vector3 _spawnBaseOffset;

    private int _stateHash;
    private int _spawnIndex;

    private void Awake()
    {
        if (_animator == null)
            _animator = GetComponent<Animator>();

        _stateHash = Animator.StringToHash(_stateParameter);
    }

    private void Update()
    {
        if (_animator == null)
            return;

        var kb = Keyboard.current;
        if (kb != null && kb.kKey.wasPressedThisFrame)
            _animator.SetInteger(_stateHash, StateActive);
    }

    public void OnClick()
    {
        if (_animator == null)
        {
            Debug.LogWarning($"[{nameof(MoonClickIdle2D)}] '{name}' has no Animator.", this);
            return;
        }

        if (_animator.GetInteger(_stateHash) != StateActive)
            return;

        SpawnNextPrefabInList();

        _animator.SetInteger(_stateHash, StateIdle);
        Debug.Log($"[{name}] Active(1) → Idle(0) (click)", this);
    }

    private void SpawnNextPrefabInList()
    {
        if (_spawnPrefabs == null || _spawnPrefabs.Count == 0)
            return;

        var n = _spawnPrefabs.Count;
        if (_spawnIndex >= n)
            return;

        var basePos = transform.position + _spawnBaseOffset;

        for (var i = _spawnIndex; i < n; i++)
        {
            var prefab = _spawnPrefabs[i];
            if (prefab == null)
                continue;

            Object.Instantiate(prefab, basePos, Quaternion.identity, _spawnParent);
            _spawnIndex = i + 1;
            return;
        }

        _spawnIndex = n;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (GetComponent<Collider2D>() == null)
        {
            Debug.LogWarning(
                $"[{nameof(MoonClickIdle2D)}] '{name}' needs a Collider2D on this GameObject for clicks to register.",
                this);
        }
    }
#endif
}
