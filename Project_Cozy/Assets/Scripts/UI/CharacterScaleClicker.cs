using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 클릭 시 <see cref="ScaleMultiplierSettings"/>의 Character 배수를 <see cref="_value"/>로 set.
/// 자식 GameObject 하나당 하나의 사이즈 옵션. 5개 모이면 기존 <c>CharacterSizeSelector</c>의 역할을 대체.
/// </summary>
[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(BoxCollider2D))]
public sealed class CharacterScaleClicker : MonoBehaviour, IClickable
{
    [SerializeField] private ScaleMultiplierSettings _settings;
    [SerializeField] private float _value = 1f;

    private BoxCollider2D _collider;
    private RectTransform _rect;

    private void Awake()
    {
        _collider = GetComponent<BoxCollider2D>();
        _rect = GetComponent<RectTransform>();
    }

    private void Start()
    {
        // 부모 HorizontalLayoutGroup의 child-force-expand가 결정한 실제 rect 폭이
        // 인스펙터의 sizeDelta와 다를 수 있어, rebuild 한 번 강제한 뒤 콜라이더 사이즈 동기화.
        if (_rect.parent is RectTransform parentRect)
            LayoutRebuilder.ForceRebuildLayoutImmediate(parentRect);
        _collider.size = _rect.rect.size;
    }

    public void OnClick()
    {
        if (_settings != null) _settings.Character.Value = _value;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (_settings == null)
            Debug.LogWarning($"[{nameof(CharacterScaleClicker)}] _settings가 비어 있음.", this);
    }
#endif
}
