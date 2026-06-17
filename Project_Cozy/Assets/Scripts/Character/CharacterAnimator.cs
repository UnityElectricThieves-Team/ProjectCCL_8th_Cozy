// <deprecated_for_develop_kk>
// develop-kk 시스템에서는 사용하지 않습니다. develop 머지 시 재논의.
// 새 시각 게이트는 ./Modules/VisualModule.cs 참조.
// namespace로 격리해 사용자 코드와의 컴파일 충돌 회피.
// </deprecated_for_develop_kk>

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Prototype.Minjun
{

/// <summary>
/// 캐릭터의 시각 상태(<see cref="VisualState"/>)와 폼(<see cref="CharacterForm"/>)을 관리.
/// 베이스 Animator Controller 1개 + 폼별 <see cref="AnimatorOverrideController"/> 2개 구조.
///
/// 사용 규칙:
/// - 외부는 <see cref="Play"/> / <see cref="PlayOneShotAsync"/> / <see cref="TransformToAsync"/> 같은 API만 호출한다.
/// - <see cref="Animator.SetInteger(int,int)"/>를 다른 시스템에서 직접 부르지 말 것.
/// - OneShot(<see cref="VisualState.Transform"/>, <see cref="VisualState.Landing"/> 등) 재생 중에는 <see cref="IsBusy"/>가 true가 되고 <see cref="Play"/> 호출은 무시된다.
/// - 새 OneShot/Transform 호출이 들어오면 이전 시퀀스는 자동 취소된다(<see cref="OperationCanceledException"/>).
///   호출자가 예외를 굳이 잡기 싫다면 <c>.SuppressCancellationThrow()</c>를 붙여라.
/// - 오브젝트 파괴 시점에 진행 중인 시퀀스는 자동 취소된다.
///
/// 베이스 컨트롤러 가정:
/// - Int 파라미터 <see cref="_visualStateParameter"/> 하나로 모든 상태를 표현.
/// - Any State → 각 상태(조건: VisualState == enum 값).
/// </summary>
[RequireComponent(typeof(Animator))]
public sealed class CharacterAnimator : MonoBehaviour
{
    [SerializeField] private Animator _animator;
    [SerializeField] private SpriteRenderer _spriteRenderer;
    [Tooltip("베이스 Animator Controller의 Int 파라미터 이름")]
    [SerializeField] private string _visualStateParameter = "VisualState";

    [Header("Form overrides")]
    [Tooltip("동물 폼 클립 세트. 베이스 컨트롤러의 모든 슬롯을 동물 클립으로 매핑한 Override.")]
    [SerializeField] private AnimatorOverrideController _animalOverride;
    [Tooltip("소녀 폼 클립 세트. 베이스 컨트롤러의 모든 슬롯을 소녀 클립으로 매핑한 Override.")]
    [SerializeField] private AnimatorOverrideController _girlOverride;

    [Header("Startup")]
    [SerializeField] private CharacterForm _startForm = CharacterForm.Animal;
    [SerializeField] private VisualState _startState = VisualState.Idle;

    public VisualState CurrentState { get; private set; }
    public CharacterForm CurrentForm { get; private set; }

    /// <summary>OneShot(Transform/Landing 등) 재생 중이면 true. true일 때 <see cref="Play"/>는 무시된다.</summary>
    public bool IsBusy { get; private set; }

    public event Action<VisualState> StateChanged;

    private int _stateHash;
    private CancellationTokenSource _oneShotCts;

    private void Awake()
    {
        if (_animator == null)
            _animator = GetComponent<Animator>();
        if (_spriteRenderer == null)
            _spriteRenderer = GetComponent<SpriteRenderer>();

        _stateHash = Animator.StringToHash(_visualStateParameter);
    }

    private void Start()
    {
        ApplyForm(_startForm);
        ApplyState(_startState);
    }

    private void OnDestroy()
    {
        CancelOneShot();
    }

    /// <summary>시각 상태 변경. <see cref="IsBusy"/>면 무시됨.</summary>
    public void Play(VisualState state)
    {
        if (IsBusy)
            return;
        ApplyState(state);
    }

    /// <summary>OneShot 클립을 재생하고 끝날 때까지 await. 새 OneShot 호출 시 이전 것은 자동 취소된다.</summary>
    public UniTask PlayOneShotAsync(VisualState state, CancellationToken cancellationToken = default)
    {
        var ct = BeginScope(cancellationToken);
        return RunOneShotAsync(state, ct);
    }

    /// <summary>폼 즉시 교체. 현재 <see cref="VisualState"/>는 유지되어 같은 상태의 다른 폼 클립이 재생됨.</summary>
    public void SetForm(CharacterForm form)
    {
        ApplyForm(form);
    }

    /// <summary>변신 시퀀스: <see cref="VisualState.Transform"/> 재생 → 폼 교체 → <see cref="VisualState.Idle"/>.</summary>
    public UniTask TransformToAsync(CharacterForm to, CancellationToken cancellationToken = default)
    {
        var ct = BeginScope(cancellationToken);
        return RunTransformAsync(to, ct);
    }

    /// <summary>좌우 반전. flipX 조작은 이 한 곳에 모은다.</summary>
    public void SetFacing(bool faceLeft)
    {
        if (_spriteRenderer != null)
            _spriteRenderer.flipX = faceLeft;
    }

    /// <summary>진행 중인 OneShot/Transform을 즉시 취소한다.</summary>
    public void CancelOneShot()
    {
        if (_oneShotCts == null)
            return;
        _oneShotCts.Cancel();
        _oneShotCts.Dispose();
        _oneShotCts = null;
    }

    private async UniTask RunOneShotAsync(VisualState state, CancellationToken ct)
    {
        IsBusy = true;
        try
        {
            ApplyState(state);

            await UniTask.NextFrame(ct);
            var len = _animator != null ? _animator.GetCurrentAnimatorStateInfo(0).length : 0f;
            if (len > 0f)
                await UniTask.Delay(TimeSpan.FromSeconds(len), cancellationToken: ct);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async UniTask RunTransformAsync(CharacterForm to, CancellationToken ct)
    {
        IsBusy = true;
        try
        {
            ApplyState(VisualState.Transform);

            await UniTask.NextFrame(ct);
            var len = _animator != null ? _animator.GetCurrentAnimatorStateInfo(0).length : 0f;
            if (len > 0f)
                await UniTask.Delay(TimeSpan.FromSeconds(len), cancellationToken: ct);

            ApplyForm(to);
            ApplyState(VisualState.Idle);
        }
        finally
        {
            IsBusy = false;
        }
    }

    // 외부 토큰 + 오브젝트 파괴 토큰을 묶고, 이전 시퀀스는 취소한다.
    private CancellationToken BeginScope(CancellationToken externalCt)
    {
        if (_oneShotCts != null)
        {
            _oneShotCts.Cancel();
            _oneShotCts.Dispose();
        }
        _oneShotCts = CancellationTokenSource.CreateLinkedTokenSource(
            externalCt, this.GetCancellationTokenOnDestroy());
        return _oneShotCts.Token;
    }

    private void ApplyState(VisualState state)
    {
        CurrentState = state;
        if (_animator != null)
            _animator.SetInteger(_stateHash, (int)state);
        StateChanged?.Invoke(state);
    }

    private void ApplyForm(CharacterForm form)
    {
        CurrentForm = form;
        if (_animator == null)
            return;

        var ov = form == CharacterForm.Animal ? _animalOverride : _girlOverride;
        if (ov != null)
            _animator.runtimeAnimatorController = ov;
    }
}

}

