using UnityEngine;
using UnityEngine.Events;

// ============================================================
// <deprecated>
// Star 오브젝트에서는 더 이상 사용하지 않습니다. 임계값/Counter 단일 출처와 활성 표현은 StarController로
// 통합됐습니다(활성 표현은 Animator의 Idle/Activated 상태). 레거시 StarKK.prefab이 아직 참조하므로
// 남겨둡니다 — StarKK 정리 시 삭제 검토.
// </deprecated>
// ============================================================

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
