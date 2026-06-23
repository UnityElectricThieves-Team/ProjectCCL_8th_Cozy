using TMPro;
using UnityEngine;

/// <summary>
/// InputCounter의 누적/현재 값과 (선택) StarController의 Activated 상태를 매 프레임 TMP 라벨에 표시한다. 디버그 표시 전용.
/// </summary>
public class DebugCounterLabel : MonoBehaviour
{
    [SerializeField] private InputCounter _counter;
    [SerializeField] private TMP_Text _label;
    [Tooltip("선택 — 지정하면 'Activated: True/False' 줄을 함께 표시. 비우면 그 줄은 '-'.")]
    [SerializeField] private StarController _star;

    private void Awake()
    {
        if (_label == null) _label = GetComponent<TMP_Text>();
        if (_star == null) _star = GetComponentInParent<StarController>();
    }

    private void OnEnable()
    {
        if (_counter == null || _label == null)
            Debug.LogError($"[{nameof(DebugCounterLabel)}] InputCounter 또는 TMP_Text 참조가 없습니다.", this);
    }

    private void Update()
    {
        if (_counter == null || _label == null) return;
        string activated = _star != null ? (_star.IsActivated ? "True" : "False") : "-";
        _label.text = $"Cumulative: {_counter.CumulativeCount}\nCurrent: {_counter.Count}\nActivated: {activated}";
    }
}
