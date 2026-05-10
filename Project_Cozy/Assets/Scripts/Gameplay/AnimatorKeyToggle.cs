using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 지정한 키가 눌릴 때마다 <see cref="SpriteAnimator"/>의 재생/정지를 토글한다.
/// 키 지정은 <c>_toggleKey</c> 한 곳에서만 — 추후 InputActions 에셋이나 설정 ScriptableObject로 승격하기 쉬운 형태.
/// 입력은 포커스 무관 — Platform/Input 의 <see cref="GlobalKeyboardHook"/>에 의존한다.
/// </summary>
public class AnimatorKeyToggle : MonoBehaviour
{
    [SerializeField] private GlobalKeyboardHook _hook;
    [SerializeField] private SpriteAnimator _animator;
    [Tooltip("이 키가 눌릴 때마다 애니메이션 재생/정지 토글")]
    [SerializeField] private Key _toggleKey = Key.Space;

    private void Awake()
    {
        // 같은 GameObject에 GlobalKeyboardHook이 있으면 자동 연결, 없으면 인스펙터에서 지정해야 한다.
        if (_hook == null) _hook = GetComponent<GlobalKeyboardHook>();
    }

    private void OnEnable()
    {
        if (_hook != null) _hook.KeyPressed += OnKeyPressed;
        else Debug.LogError($"[{nameof(AnimatorKeyToggle)}] GlobalKeyboardHook 참조가 없습니다.", this);
    }

    private void OnDisable()
    {
        if (_hook != null) _hook.KeyPressed -= OnKeyPressed;
    }

    private void OnKeyPressed(Key key)
    {
        if (key != _toggleKey) return;

        if (_animator != null) _animator.Toggle();
        else Debug.LogError($"[{nameof(AnimatorKeyToggle)}] SpriteAnimator 참조가 없습니다.", this);
    }
}