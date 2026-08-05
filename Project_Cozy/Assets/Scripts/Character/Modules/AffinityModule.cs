using System;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 친밀도 수치 + 이벤트만. <see cref="UnityEngine.Animator"/>/<see cref="UnityEngine.SpriteRenderer"/> 직접 참조 금지 —
/// 값이 바뀌었다는 사실만 <c>AffinityChanged</c>로 알리고, 그걸로 무엇을 할지는 구독자가 정한다.
/// 순수 C# <see cref="SerializableAttribute"/> 클래스 — <see cref="BaseCharacterController"/>가 <c>[SerializeField]</c>로 nested 보유.
/// </summary>
[Serializable]
public sealed class AffinityModule
{
    [Header("Affinity")]
    [Tooltip("이 친밀도에 도달하면 소녀로 변신할 수 있다.")]
    [FormerlySerializedAs("_maxAffinity")]
    [SerializeField] private int _humanTransformThreshold = 100;
    [Tooltip("쓰담 1회당 오르는 친밀도.\n" +
             "쓰담은 캐릭터를 좌클릭할 때 시작한다 — 마우스를 올려두기만 해서는 오르지 않는다.")]
    [FormerlySerializedAs("_affinityPerHoverEnter")]
    [SerializeField] private int _affinityPerPet = 10;
    [Tooltip("친밀도 최대치. 이 값을 넘으면 더 오르지 않는다(오버플로우 방지).")]
    [SerializeField] private int _affinityHardCap = 100_000_000;

    private BaseCharacterController _owner;
    private int _affinity;
    private int _cumulativeAffinity;

    /// <summary>현재 친밀도. <see cref="Reset"/>로 0으로 돌아간다.</summary>
    public int Current => _affinity;
    /// <summary>줄어들지 않는 누적 친밀도. <see cref="Reset"/>에도 유지된다(디버그 표시·향후 활용용).</summary>
    public int CumulativeAffinity => _cumulativeAffinity;
    /// <summary>친밀도 최대치(하드 상한).</summary>
    public int Max => _affinityHardCap;
    /// <summary>소녀 변신 가능 여부 — 친밀도가 변신 임계 이상인가.</summary>
    public bool CanHumanTransform => _affinity >= Mathf.Max(1, _humanTransformThreshold);

    /// <summary>친밀도 값이 변할 때마다 새 값으로 호출.</summary>
    public event Action<int> AffinityChanged;

    public void Bind(BaseCharacterController owner)
    {
        _owner = owner;
    }

    /// <summary>쓰담 1회당 누적. 하드 상한에서 멈춘다.</summary>
    public void AddOnPet()
    {
        var cap = Mathf.Max(1, _affinityHardCap);
        if (_affinity >= cap) return;

        var before = _affinity;
        var gain = Mathf.Max(0, _affinityPerPet);
        _affinity = Mathf.Min(cap, _affinity + gain);
        _cumulativeAffinity = Mathf.Min(cap, _cumulativeAffinity + (_affinity - before));

        AffinityChanged?.Invoke(_affinity);
    }

    /// <summary>현재 친밀도 0 리셋. 누적 친밀도는 유지.</summary>
    public void Reset()
    {
        _affinity = 0;
        AffinityChanged?.Invoke(_affinity);
    }
}
