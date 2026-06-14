using UnityEngine;

/// <summary>
/// StarKK 클릭 시 <see cref="_characterPrefab"/>(예: Character_debug) 1개를 인근에 스폰한다.
/// 게이트: <see cref="_starThreshold"/>의 <see cref="StarInputThreshold.Counter"/>의 Count가
/// <see cref="StarInputThreshold.Threshold"/> 이상일 때만. 임계값을 새로 선언하지 않고
/// StarInputThreshold의 값을 단일 출처로 끌어온다.
/// 실제 생성·동시 존재 한도는 <see cref="CharacterManager"/>(씬 싱글톤)에 위임한다 —
/// 캡 도달 시 Spawn이 null을 반환하고, 그때는 기운을 차감하지 않는다.
/// <see cref="InputInteractionManager"/>가 잡으려면 이 GameObject에 <see cref="Collider2D"/> 필요.
/// </summary>
public sealed class StarClickCharacterSpawner : MonoBehaviour, IClickable
{
    [Tooltip("진행도(InputCounter)와 임계값을 단일 출처로 끌어올 StarInputThreshold. StarKK 루트 컴포넌트를 참조.")]
    [SerializeField] private StarInputThreshold _starThreshold;

    [Tooltip("클릭마다 1개 생성할 캐릭터 프리팹.")]
    [SerializeField] private GameObject _characterPrefab;

    [Tooltip("스폰 시 부모 Transform (선택). 비워두면 Hierarchy 루트에 스폰.")]
    [SerializeField] private Transform _spawnParent;

    [Tooltip("스폰 위치 = transform.position + Random(Min..Max). Y는 위쪽(+)이 자연 낙하에 적합.")]
    [SerializeField] private Vector2 _spawnOffsetMin = new Vector2(-0.5f, 1f);
    [SerializeField] private Vector2 _spawnOffsetMax = new Vector2(0.5f, 2f);

    public void OnClick()
    {
        if (_starThreshold == null || _characterPrefab == null) return;
        if (CharacterManager.Instance == null) return;

        var counter = _starThreshold.Counter;
        if (counter == null) return;
        if (counter.Count < _starThreshold.Threshold) return;

        var offset = new Vector3(
            Random.Range(_spawnOffsetMin.x, _spawnOffsetMax.x),
            Random.Range(_spawnOffsetMin.y, _spawnOffsetMax.y),
            0f);

        // 생성·캡 판정은 CharacterManager에 위임. null이면 캡 도달이라 기운 차감 없음.
        var instance = CharacterManager.Instance.Spawn(_characterPrefab, transform.position + offset, _spawnParent);
        if (instance != null)
            counter.ReduceSpawnEnergy(_starThreshold.Threshold);
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
