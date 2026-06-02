using UnityEngine;

/// <summary>
/// 쓰다듬 시각 반응. 자식 Visual GameObject에 부착. <see cref="OpaqueHoverable"/>의 UnityEvent에서 호출되어
/// Tint는 자체 처리, Scale은 부모 <see cref="BaseCharacterController.Scale"/>의 ExtraMultiplier에 위임.
/// </summary>
public sealed class PettingReaction : MonoBehaviour
{
    [SerializeField] private SpriteRenderer _spriteRenderer;
    [SerializeField] private BaseCharacterController _controller;

    [Header("Tint")]
    [SerializeField] private bool _enableTint = true;
    [SerializeField] private Color _onColor = new Color(1f, 0.9f, 0.5f, 1f);
    [SerializeField] private Color _offColor = Color.white;

    [Header("Scale")]
    [SerializeField] private bool _enableScale = true;
    [SerializeField] private float _onScale = 1.1f;
    [SerializeField] private float _offScale = 1.0f;

    private void Awake()
    {
        if (_spriteRenderer == null) _spriteRenderer = GetComponent<SpriteRenderer>();
        if (_controller == null) _controller = GetComponentInParent<BaseCharacterController>();
    }

    /// <summary>OpaqueHoverable._onOpaqueHoverEnter UnityEvent에 wiring.</summary>
    public void OnPetEnter()
    {
        if (_enableTint && _spriteRenderer != null) _spriteRenderer.color = _onColor;
        if (_enableScale && _controller != null) _controller.Scale.ExtraMultiplier = _onScale;
    }

    /// <summary>OpaqueHoverable._onOpaqueHoverExit UnityEvent에 wiring.</summary>
    public void OnPetExit()
    {
        if (_enableTint && _spriteRenderer != null) _spriteRenderer.color = _offColor;
        if (_enableScale && _controller != null) _controller.Scale.ExtraMultiplier = _offScale;
    }

    // 안전망: disable/destroy 직전에 호버 exit이 누락되어도 잔류 상태를 남기지 않는다.
    private void OnDisable()
    {
        if (_enableTint && _spriteRenderer != null) _spriteRenderer.color = _offColor;
        if (_enableScale && _controller != null) _controller.Scale.ExtraMultiplier = _offScale;
    }
}
