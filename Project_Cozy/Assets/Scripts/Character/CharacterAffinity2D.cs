using UnityEngine;

/// <summary>
/// 캐릭터: 랜덤 Idle/Walk(만점이면 Special_Idle/Special_Walk, 유지시간은 동일 범위), 호버마다 친밀도↑, Shift+우클릭으로 친밀도 0.
/// <see cref="InputInteractionManager"/> 사용 시 이 오브젝트에 <see cref="Collider2D"/> 필요.
/// Animator Int: 0 Idle, 1 Walk, 2 Special_Idle, 3 Special_Walk.
/// </summary>
public sealed class CharacterAffinity2D : MonoBehaviour, IHoverable, IShiftRightClickable
{
    private enum Phase
    {
        Idle,
        Walk,
    }

    private const int VisualIdle = 0;
    private const int VisualWalk = 1;
    private const int VisualSpecialIdle = 2;
    private const int VisualSpecialWalk = 3;

    [SerializeField] private Animator _animator;
    [SerializeField] private SpriteRenderer _spriteRenderer;
    [Tooltip("Animator Int. Controller: Idle=0, Walk=1, Special_Idle=2, Special_Walk=3")]
    [SerializeField] private string _visualStateParameter = "VisualState";
    [SerializeField] private float _walkSpeed = 1.5f;
    [Header("Phase durations (Idle / Walk, 만점 여부 동일)")]
    [SerializeField] private Vector2 _idleDurationRange = new Vector2(1f, 2f);
    [SerializeField] private Vector2 _walkDurationRange = new Vector2(3f, 5f);

    [Header("Affinity")]
    [SerializeField] private int _maxAffinity = 100;
    [Tooltip("커서가 캐릭터 위에 들어올 때마다(OnHoverEnter) 친밀도가 이 값만큼 증가합니다.")]
    [SerializeField] private int _affinityPerHoverEnter = 10;

    const float MIN_PHASE_SECONDS = 0.05f;

    private int _visualStateHash;
    private Phase _phase;
    private float _phaseEndsAt;
    private float _walkDirection;

    private int _affinity;

    private void Awake()
    {
        _visualStateHash = Animator.StringToHash(_visualStateParameter);

        if (_animator == null)
            _animator = GetComponent<Animator>();

        if (_spriteRenderer == null)
            _spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void Start()
    {
        _phase = Phase.Idle;
        _phaseEndsAt = Time.time + RandomInRange(_idleDurationRange);
        ApplyAnimatorVisualState();
    }

    private void Update()
    {
        if (Time.time >= _phaseEndsAt)
            AdvancePhase();

        if (_phase == Phase.Walk)
            transform.position += new Vector3(_walkDirection * _walkSpeed * Time.deltaTime, 0f, 0f);
    }

    private void AdvancePhase()
    {
        if (_phase == Phase.Idle)
        {
            _phase = Phase.Walk;
            _walkDirection = Random.value < 0.5f ? -1f : 1f;
            ApplyFacing();
            _phaseEndsAt = Time.time + RandomInRange(_walkDurationRange);
        }
        else
        {
            _phase = Phase.Idle;
            _phaseEndsAt = Time.time + RandomInRange(_idleDurationRange);
        }

        ApplyAnimatorVisualState();
    }

    private void ApplyFacing()
    {
        if (_spriteRenderer == null)
            return;

        _spriteRenderer.flipX = _walkDirection < 0f;
    }

    private void ApplyAnimatorVisualState()
    {
        if (_animator == null)
            return;

        var atOrAboveMax = IsAffinityMaxed();
        int value;
        if (_phase == Phase.Idle)
            value = atOrAboveMax ? VisualSpecialIdle : VisualIdle;
        else
            value = atOrAboveMax ? VisualSpecialWalk : VisualWalk;

        _animator.SetInteger(_visualStateHash, value);
    }

    public void OnHoverEnter()
    {
        var cap = Mathf.Max(1, _maxAffinity);
        if (_affinity >= cap)
            return;

        var before = _affinity;
        var gain = Mathf.Max(0, _affinityPerHoverEnter);
        _affinity = Mathf.Min(cap, _affinity + gain);
        ApplyAnimatorVisualState();

        if (before < cap && _affinity >= cap)
            ReschedulePhaseEndForCurrentVisual();
    }

    public void OnHoverExit()
    {
    }

    public void OnShiftRightClick()
    {
        var wasMax = IsAffinityMaxed();
        _affinity = 0;
        if (wasMax)
            ReschedulePhaseEndForCurrentVisual();
        ApplyAnimatorVisualState();
    }

    private bool IsAffinityMaxed()
    {
        return _affinity >= Mathf.Max(1, _maxAffinity);
    }

    /// <summary>지금 페이즈(Idle/Walk)에 맞는 남은 시간을 다시 뽑습니다. 만점 진입·해제 시 호출.</summary>
    private void ReschedulePhaseEndForCurrentVisual()
    {
        var range = _phase == Phase.Idle ? _idleDurationRange : _walkDurationRange;
        _phaseEndsAt = Time.time + RandomInRange(range);
    }

    private static float RandomInRange(Vector2 range)
    {
        var min = Mathf.Min(range.x, range.y);
        var max = Mathf.Max(range.x, range.y);
        return Mathf.Max(MIN_PHASE_SECONDS, Random.Range(min, max));
    }
}
