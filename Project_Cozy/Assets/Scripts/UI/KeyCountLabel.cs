using TMPro;
using UnityEngine;

/// <summary>
/// <see cref="KeyCounter"/>의 카운트를 TextMeshPro 라벨에 표시한다.
/// </summary>
public class KeyCountLabel : MonoBehaviour
{
    [SerializeField] private KeyCounter _counter;
    [SerializeField] private TMP_Text _label;

    private void Awake()
    {
        // 같은 GameObject의 TMP_Text가 있으면 자동 연결, 없으면 인스펙터에서 지정해야 한다.
        if (_label == null) _label = GetComponent<TMP_Text>();
    }

    private void OnEnable()
    {
        if (_counter == null || _label == null)
        {
            Debug.LogError($"[{nameof(KeyCountLabel)}] KeyCounter 또는 TMP_Text 참조가 없습니다.", this);
            return;
        }
        _counter.CountChanged += Refresh;
        Refresh(_counter.Count);
    }

    private void OnDisable()
    {
        if (_counter != null) _counter.CountChanged -= Refresh;
    }

    private void Refresh(int count) => _label.text = $"Key Count: {count}";
}