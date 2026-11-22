using System;
using UnityEngine;

/// <summary>
/// 캐릭터 transform.localScale 정책. 유저 배율(설정 그래픽 탭의 캐릭터 크기) + 일시 ExtraMultiplier 슬롯.
/// 최종 스케일 = _baseScale * User * Extra. <see cref="BaseCharacterController"/>가 [SerializeField]로 nested 보유.
///
/// 유저 배율은 캐릭터가 설정을 몰라도 되게 밖에서 주입받는다(<see cref="UserMultiplier"/>) —
/// <see cref="CharacterManager"/>가 설정 값을 읽어 건다. 소녀 변신 금지와 같은 길이다.
/// </summary>
[Serializable]
public sealed class ScaleModule
{
    [Tooltip("캐릭터별 자연 크기. 디자이너가 인스펙터에서 명시. 디폴트 (1,1,1).")]
    [SerializeField] private Vector3 _baseScale = Vector3.one;

    private BaseCharacterController _owner;
    private float _user = 1f;
    private float _extra = 1f;

    /// <summary>유저 설정의 캐릭터 크기 배율. 1.0이 원래 크기.</summary>
    public float UserMultiplier
    {
        get => _user;
        set
        {
            if (Mathf.Approximately(_user, value)) return;
            _user = value;
            Apply();
        }
    }

    /// <summary>호버 강조 등 일시적 곱셈. 1.0이 무효.</summary>
    public float ExtraMultiplier
    {
        get => _extra;
        set
        {
            _extra = value;
            Apply();
        }
    }

    public void Bind(BaseCharacterController owner) => _owner = owner;

    public void Subscribe()
    {
        _extra = 1f;
        Apply();
    }

    private void Apply()
    {
        if (_owner == null) return;
        _owner.transform.localScale = _baseScale * _user * _extra;
    }
}
