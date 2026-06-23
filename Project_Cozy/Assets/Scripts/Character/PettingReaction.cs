using UnityEngine;

// ============================================================
// <deprecated>
// 이 컴포넌트는 더 이상 사용하지 않습니다 (2026-06부터).
//
// 원래 Pet 전용 애니메이션이 없던 시절, 쓰다듬을 때 노란색 tint + 크기 확대로
// 임시 시각 피드백을 주던 placeholder였습니다. 이제 Pet 상태(CharacterState.Pet)에
// 실제 애니메이션이 연결되어, 호버 시 BaseCharacterController.OnHover → RequestPet 으로
// Pet 상태에 진입하면 그 애니메이션이 재생됩니다. 따라서 이 컴포넌트의 역할은 사라졌습니다.
//
// 사람 · AI · 다른 프로젝트 모두: 새로 부착하거나 사용하지 마세요. 참고용으로만 남겨둔 코드입니다.
// (Character.prefab의 Visual에서 이미 제거됨.)
// </deprecated>
// ============================================================

/// <summary>
/// [DEPRECATED — 위 주석 참고] 쓰다듬 시각 반응. 자식 Visual GameObject에 부착. <see cref="OpaqueHoverable"/>의 UnityEvent에서 호출되어
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
