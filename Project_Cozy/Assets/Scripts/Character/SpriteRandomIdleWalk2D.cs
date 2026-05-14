using UnityEngine;

/// <summary>랜덤 Idle/Walk.</summary>
public sealed class SpriteRandomIdleWalk2D : MonoBehaviour
{
    private enum Phase
    {
        Idle,
        Walk,
    }

    [SerializeField] private Animator _animator;
    [SerializeField] private SpriteRenderer _spriteRenderer;
    [Tooltip("Animator Int 파라미터. Controller에서 Idle=0, Walk=1로 맞춥니다.")]
    [SerializeField] private string _visualStateParameter = "VisualState";
    [SerializeField] private float _walkSpeed = 1.5f;
    [SerializeField] private Vector2 _idleDurationRange = new Vector2(1f, 2f);
    [SerializeField] private Vector2 _walkDurationRange = new Vector2(3f, 5f);

    const float MIN_PHASE_SECONDS = 0.05f;

    private int _visualStateHash;
    private Phase _phase;
    private float _phaseEndsAt;
    private float _walkDirection;

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
        SetAnimatorVisualState(0);
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
            SetAnimatorVisualState(1);
            _phaseEndsAt = Time.time + RandomInRange(_walkDurationRange);
        }
        else
        {
            _phase = Phase.Idle;
            SetAnimatorVisualState(0);
            _phaseEndsAt = Time.time + RandomInRange(_idleDurationRange);
        }
    }

    private void ApplyFacing()
    {
        if (_spriteRenderer == null)
            return;

        _spriteRenderer.flipX = _walkDirection < 0f;
    }

    private void SetAnimatorVisualState(int value)
    {
        if (_animator != null)
            _animator.SetInteger(_visualStateHash, value);
    }

    private static float RandomInRange(Vector2 range)
    {
        var min = Mathf.Min(range.x, range.y);
        var max = Mathf.Max(range.x, range.y);
        return Mathf.Max(MIN_PHASE_SECONDS, Random.Range(min, max));
    }
}
