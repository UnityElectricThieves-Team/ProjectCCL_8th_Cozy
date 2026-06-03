using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Star의 <see cref="InputCounter.Count"/>가 <c>_threshold</c>에 도달하는 순간 <see cref="_onThresholdReached"/>를 1회 발사.
/// 구체적 반응(애니메이션 전환, 색감 변경, 파티클 등)은 인스펙터에서 UnityEvent에 연결.
/// </summary>
public class StarInputThreshold : MonoBehaviour
{
    [SerializeField] private InputCounter _counter;
    [SerializeField, Min(1)] private int _threshold = 100;
    [SerializeField] private UnityEvent _onThresholdReached;

    public InputCounter Counter => _counter;
    public int Threshold => _threshold;

    private bool _fired;

    private void Awake()
    {
        if (_counter == null) _counter = GetComponent<InputCounter>();
    }

    private void OnEnable()
    {
        if (_counter == null)
            Debug.LogError($"[{nameof(StarInputThreshold)}] InputCounter 참조가 없습니다.", this);
    }

    private void Update()
    {
        if (_fired || _counter == null) return;
        if (_counter.Count >= _threshold)
        {
            _fired = true;
            _onThresholdReached?.Invoke();
        }
    }
}
