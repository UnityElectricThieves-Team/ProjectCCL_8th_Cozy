using System;
using UnityEngine;

/// <summary>
/// 캐릭터 시각의 단일 진입점. <see cref="UnityEngine.Animator"/>를 외부가 직접 만지지 못하게 막고
/// <c>Play</c> / <c>PlayOneShot</c> / <c>SetFacing</c> / <c>SetForm</c> API만 노출한다.
/// 순수 C# <see cref="SerializableAttribute"/> 클래스 — <see cref="BaseCharacterController"/>가 <c>[SerializeField]</c>로 nested 보유한다.
/// OneShot은 float timer 기반(UniTask 미사용). 길이는 다음 Tick에 Animator state info에서 측정.
/// </summary>
[Serializable]
public sealed class VisualModule
{
    [Tooltip("베이스 Animator Controller의 Int 파라미터 이름.")]
    [SerializeField] private string _visualStateParameter = "VisualState";

    [Header("Form overrides")]
    [Tooltip("동물 폼 클립 세트. Phase 8에서 활용.")]
    [SerializeField] private AnimatorOverrideController _animalOverride;
    [Tooltip("소녀 폼 클립 세트. Phase 8에서 활용.")]
    [SerializeField] private AnimatorOverrideController _girlOverride;

    [Header("Startup")]
    [SerializeField] private CharacterForm _startForm = CharacterForm.Animal;
    [SerializeField] private CharacterState _startState = CharacterState.Idle;

    private BaseCharacterController _owner;
    private Animator _animator;
    private SpriteRenderer _spriteRenderer;
    private int _stateHash;

    private bool _isBusy;
    private float _oneShotEndsAt;
    private bool _oneShotPendingLengthMeasure;

    public CharacterState CurrentState { get; private set; }
    public CharacterForm CurrentForm { get; private set; }
    public bool IsBusy => _isBusy;

    public event Action<CharacterState> StateChanged;

    public void Bind(BaseCharacterController owner)
    {
        _owner = owner;
        _animator = owner.Animator;
        _spriteRenderer = owner.SpriteRenderer;
        _stateHash = Animator.StringToHash(_visualStateParameter);
    }

    /// <summary>Start에서 한 번 호출 — 시작 폼·상태 적용.</summary>
    public void ApplyStartup()
    {
        ApplyForm(_startForm);
        ApplyState(_startState);
    }

    /// <summary>시각 상태 변경. <see cref="IsBusy"/>면 무시.</summary>
    public void Play(CharacterState state)
    {
        if (_isBusy) return;
        ApplyState(state);
    }

    /// <summary>OneShot 클립 재생. 길이는 다음 Tick에서 자동 측정. 이전 OneShot이 있으면 즉시 종료된다.</summary>
    public void PlayOneShot(CharacterState state)
    {
        _isBusy = false;
        ApplyState(state);
        _isBusy = true;
        _oneShotPendingLengthMeasure = true;
        _oneShotEndsAt = 0f;
    }

    /// <summary>폼 즉시 교체. 현재 state는 유지되어 같은 state의 다른 폼 클립이 재생됨.</summary>
    public void SetForm(CharacterForm form)
    {
        ApplyForm(form);
    }

    /// <summary>좌우 반전. flipX 조작은 이 한 곳에 모은다.</summary>
    public void SetFacing(bool faceLeft)
    {
        if (_spriteRenderer != null)
            _spriteRenderer.flipX = faceLeft;
    }

    /// <summary>진행 중인 OneShot을 즉시 취소.</summary>
    public void CancelOneShot()
    {
        _isBusy = false;
        _oneShotPendingLengthMeasure = false;
    }

    public void Tick(float dt)
    {
        if (!_isBusy) return;

        if (_oneShotPendingLengthMeasure)
        {
            if (_animator != null)
            {
                var len = _animator.GetCurrentAnimatorStateInfo(0).length;
                if (len > 0f)
                {
                    _oneShotEndsAt = Time.time + len;
                    _oneShotPendingLengthMeasure = false;
                    return;
                }
            }
            // length 측정 실패(Animator 미연결 또는 클립 길이 0) — 안전 폴백 1초
            _oneShotEndsAt = Time.time + 1f;
            _oneShotPendingLengthMeasure = false;
            return;
        }

        if (Time.time >= _oneShotEndsAt)
            _isBusy = false;
    }

    private void ApplyState(CharacterState state)
    {
        CurrentState = state;
        if (_animator != null)
            _animator.SetInteger(_stateHash, (int)state);
        StateChanged?.Invoke(state);
    }

    private void ApplyForm(CharacterForm form)
    {
        CurrentForm = form;
        if (_animator == null) return;
        var ov = form == CharacterForm.Animal ? _animalOverride : _girlOverride;
        if (ov != null)
            _animator.runtimeAnimatorController = ov;
    }
}
