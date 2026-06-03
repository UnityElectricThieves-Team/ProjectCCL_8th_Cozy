using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// StarKK 클릭 시 <see cref="_characterPrefab"/>(예: CharacterKK_debug) 1개를 인근에 스폰한다.
/// 게이트: <see cref="_starThreshold"/>의 <see cref="StarInputThreshold.Counter"/>의 Count가
/// <see cref="StarInputThreshold.Threshold"/> 이상일 때만. 임계값을 새로 선언하지 않고
/// StarInputThreshold의 값을 단일 출처로 끌어온다.
/// 동시 존재 한도: <see cref="_maxCharacterCount"/>. 인스턴스가 Destroy되면 슬롯이 비어 재스폰 가능.
/// <see cref="InputInteractionManager"/>가 잡으려면 이 GameObject에 <see cref="Collider2D"/> 필요.
/// </summary>
public sealed class StarClickCharacterSpawner : MonoBehaviour, IClickable
{
    [Tooltip("진행도(InputCounter)와 임계값을 단일 출처로 끌어올 StarInputThreshold. StarKK 루트 컴포넌트를 참조.")]
    [SerializeField] private StarInputThreshold _starThreshold;

    [Tooltip("클릭마다 1개 Instantiate할 캐릭터 프리팹.")]
    [SerializeField] private GameObject _characterPrefab;

    [Tooltip("맵에 동시 존재 가능한 최대 인스턴스 수. Destroy되면 슬롯이 비어 재스폰 가능.")]
    [SerializeField, Min(1)] private int _maxCharacterCount = 10;

    [Tooltip("스폰 시 부모 Transform (선택). 비워두면 Hierarchy 루트에 스폰.")]
    [SerializeField] private Transform _spawnParent;

    [Tooltip("스폰 위치 = transform.position + Random(Min..Max). Y는 위쪽(+)이 자연 낙하에 적합.")]
    [SerializeField] private Vector2 _spawnOffsetMin = new Vector2(-0.5f, 1f);
    [SerializeField] private Vector2 _spawnOffsetMax = new Vector2(0.5f, 2f);

    private readonly List<GameObject> _aliveCharacters = new List<GameObject>();

    public void OnClick()
    {
        if (_starThreshold == null || _characterPrefab == null) return;

        var counter = _starThreshold.Counter;
        if (counter == null) return;
        if (counter.Count < _starThreshold.Threshold) return;

        PruneDestroyed();
        if (_aliveCharacters.Count >= _maxCharacterCount) return;

        var offset = new Vector3(
            Random.Range(_spawnOffsetMin.x, _spawnOffsetMax.x),
            Random.Range(_spawnOffsetMin.y, _spawnOffsetMax.y),
            0f);
        var pos = transform.position + offset;

        var instance = Object.Instantiate(_characterPrefab, pos, Quaternion.identity, _spawnParent);
        _aliveCharacters.Add(instance);

        counter.ReduceSpawnEnergy(_starThreshold.Threshold);
    }

    private void PruneDestroyed()
    {
        for (int i = _aliveCharacters.Count - 1; i >= 0; i--)
            if (_aliveCharacters[i] == null) _aliveCharacters.RemoveAt(i);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (GetComponent<Collider2D>() == null)
        {
            Debug.LogWarning(
                $"[{nameof(StarClickCharacterSpawner)}] '{name}' needs a Collider2D on this GameObject for InputInteractionManager to find it.",
                this);
        }
    }
#endif
}
