using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// <see cref="GlobalKeyboardHook"/>이 보고하는 키 입력 횟수를 누적한다.
/// 어떤 키든 1회로 친다 (매핑되지 않아 <see cref="Key.None"/>으로 온 키도 포함).
/// 포커스 유무와 무관 — Platform/Input 의 인프라(<see cref="GlobalKeyboardHook"/>)에 의존한다.
///
/// 주의: README §2의 "별 클릭 수" 진척 메커니즘과는 별개. 이건 키보드 입력 횟수 카운터다.
/// </summary>
public class KeyCounter : MonoBehaviour
{
    [SerializeField] private GlobalKeyboardHook _hook;

    /// <summary>지금까지 감지된 키 입력 횟수.</summary>
    public int Count { get; private set; }

    /// <summary><see cref="Count"/>가 바뀔 때마다 새 값과 함께 호출된다.</summary>
    public event Action<int> CountChanged;

    private void Awake()
    {
        // 같은 GameObject에 GlobalKeyboardHook이 있으면 자동 연결, 없으면 인스펙터에서 지정해야 한다.
        if (_hook == null) _hook = GetComponent<GlobalKeyboardHook>();
    }

    private void OnEnable()
    {
        if (_hook != null) _hook.KeyPressed += OnKeyPressed;
        else Debug.LogError($"[{nameof(KeyCounter)}] GlobalKeyboardHook 참조가 없습니다.", this);
    }

    private void OnDisable()
    {
        if (_hook != null) _hook.KeyPressed -= OnKeyPressed;
    }

    private void OnKeyPressed(Key key)
    {
        Count++;
        CountChanged?.Invoke(Count);
    }
}