using System;
using UnityEngine;

/// <summary>
/// 직렬화 가능한 스케일 배수 단위. <see cref="ScaleMultiplierSettings"/>가 종류별(Character/UI/Background 등)로 이 인스턴스를 보유한다.
/// 범위(<see cref="_min"/>/<see cref="_max"/>)는 인스턴스 단위로 인스펙터에서 분류별로 조정 — 캐릭터는 넉넉히, UI는 좁게 같은 식.
/// setter에서 클램프 + 값 변경 시 <see cref="Changed"/> 발화.
/// </summary>
[Serializable]
public class ScaleMultiplier
{
    [Tooltip("이 분류의 허용 최소 배수. 디폴트는 넉넉한 0.1.")]
    [SerializeField] private float _min = 0.1f;
    [Tooltip("이 분류의 허용 최대 배수. 디폴트는 넉넉한 4.")]
    [SerializeField] private float _max = 4f;
    [SerializeField] private float _value = 1f;

    public float Value
    {
        get => _value;
        set
        {
            var clamped = Mathf.Clamp(value, _min, _max);
            if (Mathf.Approximately(_value, clamped)) return;
            _value = clamped;
            Changed?.Invoke(_value);
        }
    }

    public event Action<float> Changed;
}
