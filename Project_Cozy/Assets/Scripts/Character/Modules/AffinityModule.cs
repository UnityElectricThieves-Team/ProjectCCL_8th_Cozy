using System;
using UnityEngine;

/// <summary>
/// 친밀도 수치 + 이벤트만. <see cref="UnityEngine.Animator"/>/<see cref="UnityEngine.SpriteRenderer"/> 직접 참조 금지 —
/// 시각 전환은 <c>SpecialActivated</c>/<c>SpecialReleased</c>/<c>HumanTransformAvailable</c> 이벤트로 위임한다.
/// 순수 C# <see cref="SerializableAttribute"/> 클래스 — <see cref="BaseCharacterController"/>가 <c>[SerializeField]</c>로 nested 보유.
/// </summary>
[Serializable]
public sealed class AffinityModule
{
    [Header("Affinity")]
    [SerializeField] private int _maxAffinity = 100;
    [Tooltip("호버 진입 시(OnHoverEnter) 누적 친밀도.")]
    [SerializeField] private int _affinityPerHoverEnter = 10;
    [Tooltip("인간 변신 가능 임계. Phase 8에서 활용.")]
    [SerializeField] private int _humanTransformThreshold = 1000;

    private BaseCharacterController _owner;
    private int _affinity;

    public int Current => _affinity;
    public int Max => _maxAffinity;
    public bool IsMaxed => _affinity >= Mathf.Max(1, _maxAffinity);
    public bool CanHumanTransform => _affinity >= _humanTransformThreshold;

    /// <summary>친밀도 값이 변할 때마다 새 값으로 호출.</summary>
    public event Action<int> AffinityChanged;
    /// <summary>친밀도가 Max에 진입한 순간 1회 발사. <see cref="StateModule.SpecialMode"/> ON 신호.</summary>
    public event Action SpecialActivated;
    /// <summary>친밀도가 Max에서 해제된 순간 1회 발사.</summary>
    public event Action SpecialReleased;
    /// <summary>인간 변신 임계 도달 시 1회 발사. Phase 8에서 변신 시퀀스 트리거.</summary>
    public event Action HumanTransformAvailable;

    public void Bind(BaseCharacterController owner)
    {
        _owner = owner;
    }

    /// <summary>호버 진입 시 누적. Cap 도달 시 SpecialActivated 발사, 변신 임계 도달 시 HumanTransformAvailable 발사.</summary>
    public void AddOnHoverEnter()
    {
        var cap = Mathf.Max(1, _maxAffinity);
        if (_affinity >= cap) return;

        var before = _affinity;
        var gain = Mathf.Max(0, _affinityPerHoverEnter);
        _affinity = Mathf.Min(cap, _affinity + gain);

        AffinityChanged?.Invoke(_affinity);

        if (before < cap && _affinity >= cap)
            SpecialActivated?.Invoke();

        if (before < _humanTransformThreshold && _affinity >= _humanTransformThreshold)
            HumanTransformAvailable?.Invoke();
    }

    /// <summary>친밀도 0 리셋. Max에서 해제 시 SpecialReleased 발사.</summary>
    public void Reset()
    {
        var wasMax = IsMaxed;
        _affinity = 0;
        AffinityChanged?.Invoke(_affinity);
        if (wasMax) SpecialReleased?.Invoke();
    }
}
