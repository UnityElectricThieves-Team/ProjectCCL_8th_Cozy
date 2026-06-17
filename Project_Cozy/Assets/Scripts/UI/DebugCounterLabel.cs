using TMPro;
using UnityEngine;

/// <summary>
/// <see cref="InputCounter.Count"/>를 매 프레임 폴링해 TMP 라벨에 표시한다. 디버그 표시 전용.
/// </summary>
public class DebugCounterLabel : MonoBehaviour
{
    [SerializeField] private InputCounter _counter;
    [SerializeField] private TMP_Text _label;

    private void Awake()
    {
        if (_label == null) _label = GetComponent<TMP_Text>();
    }

    private void OnEnable()
    {
        if (_counter == null || _label == null)
            Debug.LogError($"[{nameof(DebugCounterLabel)}] InputCounter 또는 TMP_Text 참조가 없습니다.", this);
    }

    private void Update()
    {
        if (_counter == null || _label == null) return;
        _label.text = $"Count: {_counter.Count}";
    }
}
