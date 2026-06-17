// <deprecated_for_develop_kk>
// develop-kk 시스템에서는 사용하지 않습니다. develop 머지 시 재논의.
// 새 자율 거동은 ./Modules/StateModule.cs 참조.
// namespace로 격리해 사용자 코드와의 컴파일 충돌 회피.
// </deprecated_for_develop_kk>

using UnityEngine;

namespace Prototype.Minjun
{

/// <summary>
/// 자율 AI 레이어. Idle ↔ Walk/Run 사이클만 담당.
/// <para>책임</para>
/// - 일정 시간 Idle → 다음 행동(Walk/Run) 확률 결정 → 목표 X 도착 시 Idle 복귀.
/// - 카메라 viewport X 범위 안쪽으로 위치 클램프.
/// <para>비책임 (다른 컴포넌트가 처리)</para>
/// - 시각 상태/폼 적용 → <see cref="CharacterAnimator"/>.
/// - 마우스 Grab/Petting/낙하 → 추후 <c>CharacterInteraction</c>.
/// - 친밀도/별 시스템 → 별도.
/// <para>잠금 규칙</para>
/// - <see cref="CharacterAnimator.IsBusy"/> 동안 본 스크립트는 <see cref="Update"/>를 스킵한다.
///   (OneShot/Transform 재생 중 자율 행동이 끼어들지 않게)
/// </summary>
[RequireComponent(typeof(CharacterAnimator))]
public sealed class CharacterBrain : MonoBehaviour
{
    private enum Phase
    {
        Idle,
        Walk,
        Run,
    }

    [SerializeField] private CharacterAnimator _animator;
    [SerializeField] private Camera _targetCamera;

    [Header("Durations (sec)")]
    [SerializeField] private Vector2 _idleDurationRange = new Vector2(2f, 4f);
    [Tooltip("Walk/Run의 안전 최대 지속 시간. 도착 전에 이 시간을 넘기면 강제로 Idle 복귀.")]
    [SerializeField] private float _moveTimeoutSeconds = 8f;

    [Header("Speeds (world units / sec)")]
    [SerializeField] private float _walkSpeed = 1.5f;
    [SerializeField] private float _runSpeed = 3.5f;

    [Header("Action choice")]
    [Range(0f, 1f)]
    [Tooltip("Idle 종료 후 Run을 선택할 확률. 나머지는 Walk.")]
    [SerializeField] private float _runProbability = 0.25f;

    [Header("Bounds")]
    [Tooltip("카메라 viewport 좌우에서 안쪽으로 밀어둘 여백 비율 (0~0.5).")]
    [Range(0f, 0.5f)]
    [SerializeField] private float _viewportMargin = 0.05f;

    [Tooltip("이 거리 이내로 들어오면 도착으로 판정한다.")]
    [SerializeField] private float _arrivalThreshold = 0.05f;

    private const float MIN_PHASE_SECONDS = 0.05f;

    private Phase _phase;
    private float _phaseEndsAt;
    private float _targetX;
    private float _currentSpeed;

    private void Awake()
    {
        if (_animator == null)
            _animator = GetComponent<CharacterAnimator>();
        if (_targetCamera == null)
            _targetCamera = Camera.main;
    }

    private void Start()
    {
        EnterIdle();
    }

    private void Update()
    {
        if (_animator != null && _animator.IsBusy)
            return;

        switch (_phase)
        {
            case Phase.Idle:
                if (Time.time >= _phaseEndsAt)
                    DecideNextAction();
                break;

            case Phase.Walk:
            case Phase.Run:
                TickMove();
                break;
        }
    }

    private void EnterIdle()
    {
        _phase = Phase.Idle;
        _phaseEndsAt = Time.time + RandomInRange(_idleDurationRange);
        _animator?.Play(VisualState.Idle);
    }

    private void DecideNextAction()
    {
        var goRun = Random.value < _runProbability;
        _phase = goRun ? Phase.Run : Phase.Walk;
        _currentSpeed = goRun ? _runSpeed : _walkSpeed;

        _targetX = PickRandomTargetX();
        _phaseEndsAt = Time.time + Mathf.Max(MIN_PHASE_SECONDS, _moveTimeoutSeconds);

        var faceLeft = _targetX < transform.position.x;
        _animator?.SetFacing(faceLeft);
        _animator?.Play(goRun ? VisualState.Run : VisualState.Walk);
    }

    private void TickMove()
    {
        var pos = transform.position;
        var dx = _targetX - pos.x;

        if (Mathf.Abs(dx) <= _arrivalThreshold || Time.time >= _phaseEndsAt)
        {
            EnterIdle();
            return;
        }

        var step = Mathf.Sign(dx) * _currentSpeed * Time.deltaTime;
        if (Mathf.Abs(step) > Mathf.Abs(dx))
            step = dx;

        pos.x += step;
        pos.x = ClampToViewportX(pos.x, pos.z);
        transform.position = pos;
    }

    private float PickRandomTargetX()
    {
        if (_targetCamera == null)
            return transform.position.x;

        var z = transform.position.z;
        var lo = _targetCamera.ViewportToWorldPoint(new Vector3(_viewportMargin, 0.5f, z - _targetCamera.transform.position.z)).x;
        var hi = _targetCamera.ViewportToWorldPoint(new Vector3(1f - _viewportMargin, 0.5f, z - _targetCamera.transform.position.z)).x;
        if (lo > hi) (lo, hi) = (hi, lo);
        return Random.Range(lo, hi);
    }

    private float ClampToViewportX(float x, float z)
    {
        if (_targetCamera == null)
            return x;

        var lo = _targetCamera.ViewportToWorldPoint(new Vector3(_viewportMargin, 0.5f, z - _targetCamera.transform.position.z)).x;
        var hi = _targetCamera.ViewportToWorldPoint(new Vector3(1f - _viewportMargin, 0.5f, z - _targetCamera.transform.position.z)).x;
        if (lo > hi) (lo, hi) = (hi, lo);
        return Mathf.Clamp(x, lo, hi);
    }

    private static float RandomInRange(Vector2 range)
    {
        var min = Mathf.Min(range.x, range.y);
        var max = Mathf.Max(range.x, range.y);
        return Mathf.Max(MIN_PHASE_SECONDS, Random.Range(min, max));
    }
}

}

