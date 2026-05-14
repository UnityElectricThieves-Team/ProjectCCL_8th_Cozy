using UnityEngine;

/// <summary>
/// "쓰다듬" 시각 반응 (테스트용). <see cref="OpaquePixelHover"/>의 UnityEvent에서 호출되어
/// SpriteRenderer 색 틴트 + Transform 스케일을 토글한다. Tint / Scale 각각 인스펙터 체크박스로 끌 수 있다.
///
/// 실제 게임 캐릭터의 쓰다듬 반응(친밀도 누적, Animator 전이 등)은 별도 컴포넌트로 구현될 예정 —
/// 이 컴포넌트는 OpaquePixelHover의 동작 확인이 끝나면 폐기 후보.
/// </summary>
public sealed class PettingReactionTestProbe : MonoBehaviour
{
    [SerializeField] private SpriteRenderer _spriteRenderer;
    [SerializeField, Tooltip("스케일 변경 대상. 비우면 자기 transform.")]
    private Transform _scaleTarget;

    [Header("Tint")]
    [SerializeField] private bool _enableTint = true;
    [SerializeField, Tooltip("쓰다듬 받는 동안 SpriteRenderer.color에 적용. 흰색에 가까우면 미묘, 짙은 색이면 강한 변화.")]
    private Color _onColor = new Color(1f, 0.9f, 0.5f, 1f);
    [SerializeField, Tooltip("기본 상태 색 (원본 스프라이트 그대로 보이려면 흰색).")]
    private Color _offColor = Color.white;

    [Header("Scale")]
    [SerializeField] private bool _enableScale = true;
    [SerializeField, Tooltip("쓰다듬 받는 동안 적용할 스케일 배수. Awake 시 캡처된 기준 스케일에 곱해진다. 1.0이면 변화 없음.")]
    private float _onScale = 1.1f;
    [SerializeField, Tooltip("기본 상태의 스케일 배수. 보통 1.0 (씬에서 설정한 크기 그대로).")]
    private float _offScale = 1.0f;

    // 씬/프리팹에서 사용자가 설정한 크기. Awake 시 1회 캡처해 _onScale/_offScale의 *배수* 기준으로 삼는다.
    private Vector3 _baseScale;

    private void Awake()
    {
        if (_spriteRenderer == null) _spriteRenderer = GetComponent<SpriteRenderer>();
        if (_scaleTarget == null) _scaleTarget = transform;
        _baseScale = _scaleTarget.localScale;
    }

    /// <summary>OpaquePixelHover의 onOpaqueHoverEnter 이벤트에 연결.</summary>
    public void OnPetEnter()
    {
        if (_enableTint && _spriteRenderer != null) _spriteRenderer.color = _onColor;
        if (_enableScale && _scaleTarget != null) _scaleTarget.localScale = _baseScale * _onScale;
        Debug.Log($"[{name}] 쓰다듬 시작", this);
    }

    /// <summary>OpaquePixelHover의 onOpaqueHoverExit 이벤트에 연결.</summary>
    public void OnPetExit()
    {
        if (_enableTint && _spriteRenderer != null) _spriteRenderer.color = _offColor;
        if (_enableScale && _scaleTarget != null) _scaleTarget.localScale = _baseScale * _offScale;
        Debug.Log($"[{name}] 쓰다듬 끝", this);
    }
}
