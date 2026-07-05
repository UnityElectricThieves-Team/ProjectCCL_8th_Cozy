using UnityEngine;

/// <summary>
/// 캐릭터의 친밀도 누적을 하트 적립으로 잇는 어댑터. 캐릭터(Character)와 지갑(HeartSystem)이 서로를
/// 모르게 두고, 이 컴포넌트만 둘을 안다.
///
/// 누적 친밀도가 <see cref="_affinityStepSize"/>의 배수를 새로 넘길 때마다, 넘긴 스텝 수 × <see cref="_heartsPerStep"/>
/// 만큼 <see cref="HeartSystem.Add"/>를 호출한다. '언제(스텝 크기)'와 '얼마(환율)'라는 보상 수치를 모두 이곳이 보유한다 —
/// 친밀도(AffinityModule)도 지갑(HeartSystem)도 이 환산을 모른 채 순수하게 남는다.
///
/// 부착 위치: 캐릭터의 자식 Visual GameObject(루트엔 BaseCharacterController 하나만 두는 컨벤션 때문).
/// <see cref="Component.GetComponentInParent"/>로 컨트롤러를 찾으므로 자식 어디에 붙여도 동작한다.
/// </summary>
public class BridgeAffinityHeart : MonoBehaviour
{
    [Tooltip("누적 친밀도가 이만큼 오를 때마다 하트를 지급한다(스텝 1개).")]
    [SerializeField] private int _affinityStepSize = 100;
    [Tooltip("스텝 1개당 지급할 하트 수.")]
    [SerializeField, Min(0)] private int _heartsPerStep = 10;

    private BaseCharacterController _controller;
    private int _rewardedSteps;

    private void Awake()
    {
        _controller = GetComponentInParent<BaseCharacterController>();
    }

    private void OnEnable()
    {
        if (_controller != null) _controller.Affinity.AffinityChanged += OnAffinityChanged;
        else Debug.LogError($"[{nameof(BridgeAffinityHeart)}] BaseCharacterController 참조가 없습니다.", this);
    }

    private void OnDisable()
    {
        if (_controller != null) _controller.Affinity.AffinityChanged -= OnAffinityChanged;
    }

    // 친밀도가 바뀔 때마다 누적값 기준으로 새로 넘긴 스텝 수를 계산해 그만큼 하트를 적립.
    // 누적 친밀도는 Reset으로 줄지 않으므로, 리셋 후 재획득(파밍)은 발생하지 않는다.
    private void OnAffinityChanged(int _)
    {
        var step = Mathf.Max(1, _affinityStepSize);
        var steps = _controller.Affinity.CumulativeAffinity / step;
        if (steps <= _rewardedSteps) return;

        HeartSystem.Instance?.Add((steps - _rewardedSteps) * _heartsPerStep);
        _rewardedSteps = steps;
    }
}
