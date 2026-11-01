using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// SpawnPointManager의 누적 스폰 기운과 (선택) CloudController의 Activated 상태를 매 프레임 TMP 라벨에 표시한다. 디버그 표시 전용.
/// </summary>
public class DebugCounterLabel : MonoBehaviour
{
    [SerializeField, FormerlySerializedAs("_counter")] private SpawnPointManager _spawnPoint;
    [SerializeField] private TMP_Text _label;
    [Tooltip("선택 — 지정하면 'Activated: True/False' 줄을 함께 표시. 비우면 그 줄은 '-'.")]
    [SerializeField, FormerlySerializedAs("_star")] private CloudController _cloud;

    private void Awake()
    {
        if (_label == null) _label = GetComponent<TMP_Text>();
        if (_cloud == null) _cloud = GetComponentInParent<CloudController>();
    }

    private void OnEnable()
    {
        if (_spawnPoint == null || _label == null)
            Debug.LogError($"[{nameof(DebugCounterLabel)}] SpawnPointManager 또는 TMP_Text 참조가 없습니다.", this);
    }

    private void Update()
    {
        if (_spawnPoint == null || _label == null) return;
        string activated = _cloud != null ? (_cloud.IsActivated ? "True" : "False") : "-";
        _label.text = $"Cumulative: {_spawnPoint.CumulativeEnergy}\nActivated: {activated}";
    }
}
