using System;
using UnityEngine;

/// <summary>
/// 캐릭터 transform.localScale 정책. 글로벌 multiplier(<see cref="ScaleMultiplierSettings"/>) 구독 + 일시 ExtraMultiplier 슬롯.
/// 최종 스케일 = _baseScale * User * Extra. <see cref="BaseCharacterController"/>가 [SerializeField]로 nested 보유.
/// </summary>
[Serializable]
public sealed class ScaleModule
{
    [SerializeField] private ScaleMultiplierSettings _settings;
    [Tooltip("캐릭터별 자연 크기. 디자이너가 인스펙터에서 명시. 디폴트 (1,1,1).")]
    [SerializeField] private Vector3 _baseScale = Vector3.one;

    private BaseCharacterController _owner;
    private float _extra = 1f;

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

    public void Bind(BaseCharacterController owner)
    {
        _owner = owner;
        if (_settings == null)
            Debug.LogWarning($"[{nameof(ScaleModule)}] '{owner.name}' has no ScaleMultiplierSettings assigned. User multiplier will fall back to 1.", owner);
    }

    public void Subscribe()
    {
        _extra = 1f;
        if (_settings != null) _settings.Character.Changed += OnUserChanged;
        Apply();
    }

    public void Unsubscribe()
    {
        if (_settings != null) _settings.Character.Changed -= OnUserChanged;
    }

    private void OnUserChanged(float _) => Apply();

    private void Apply()
    {
        if (_owner == null) return;
        float user = _settings != null ? _settings.Character.Value : 1f;
        _owner.transform.localScale = _baseScale * user * _extra;
    }
}
