using UnityEngine;

/// <summary>
/// 달 등: Animator Int가 Active(1)일 때만 좌클릭으로 Idle(0)으로 바꾸고 로그를 남깁니다.
/// <see cref="SpriteRandomIdleWalk2D"/>와 같이 Int 파라미터로 상태를 맞춥니다.
/// <see cref="InputInteractionManager"/>에 잡히려면 이 오브젝트에 <see cref="Collider2D"/>가 있어야 합니다.
/// </summary>
public sealed class MoonClickIdle2D : MonoBehaviour, IClickable
{
    private const int StateIdle = 0;
    private const int StateActive = 1;

    [SerializeField] private Animator _animator;
    [Tooltip("Animator Int. 컨트롤러: Idle=0, Active=1")]
    [SerializeField] private string _stateParameter = "MoonState";

    private int _stateHash;

    private void Awake()
    {
        if (_animator == null)
            _animator = GetComponent<Animator>();

        _stateHash = Animator.StringToHash(_stateParameter);
    }

    public void OnClick()
    {
        if (_animator == null)
        {
            Debug.LogWarning($"[{nameof(MoonClickIdle2D)}] '{name}' has no Animator.", this);
            return;
        }

        if (_animator.GetInteger(_stateHash) != StateActive)
            return;

        _animator.SetInteger(_stateHash, StateIdle);
        Debug.Log($"[{name}] Active(1) → Idle(0) (click)", this);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (GetComponent<Collider2D>() == null)
        {
            Debug.LogWarning(
                $"[{nameof(MoonClickIdle2D)}] '{name}' needs a Collider2D on this GameObject for clicks to register.",
                this);
        }
    }
#endif
}
