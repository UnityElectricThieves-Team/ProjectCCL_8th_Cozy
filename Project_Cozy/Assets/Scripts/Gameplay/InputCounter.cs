using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;  // KeyControl
using UnityEngine.InputSystem.LowLevel;  // MouseButton
using UnityEngine.InputSystem.Utilities; // .Call() extension method

/// <summary>
/// 포커스 무관 입력 4채널을 단일 <see cref="Count"/>로 합산한다.
///
/// <list type="bullet">
///   <item>InFocus 키 : <c>InputSystem.onAnyButtonPress</c>의 <see cref="KeyControl"/></item>
///   <item>OutFocus 키 : <see cref="OutFocusKeyHook"/>.KeyPressed</item>
///   <item>InFocus 마우스 : <c>Mouse.current</c> 버튼 폴링</item>
///   <item>OutFocus 마우스 : <see cref="OutFocusMouseHook"/>.ButtonPressed</item>
/// </list>
///
/// 중복 카운트 안전: InputSystem은 창 비활성 시 자체적으로 fire 안 하고, OutFocus 훅은 <c>Application.isFocused</c>로 게이트 → 두 경로가 자연 배타적.
/// </summary>
public class InputCounter : MonoBehaviour
{
    [SerializeField] private OutFocusKeyHook _outFocusKey;
    [SerializeField] private OutFocusMouseHook _outFocusMouse;

    private IDisposable _anyButtonSubscription;

    /// <summary>지금까지 누적된 입력 횟수 — 기획 용어로는 '스폰 기운'.</summary>
    public int Count { get; private set; }

    /// <summary>스폰 기운 차감. 음수로 가지 않도록 0에서 클램프.</summary>
    public void ReduceSpawnEnergy(int amount)
    {
        if (amount <= 0) return;
        Count = Mathf.Max(0, Count - amount);
    }

    private void OnEnable()
    {
        if (_outFocusKey != null) _outFocusKey.KeyPressed += OnOutFocusKey;
        else Debug.LogError($"[{nameof(InputCounter)}] OutFocusKeyHook 참조가 없습니다.", this);

        if (_outFocusMouse != null) _outFocusMouse.ButtonPressed += OnOutFocusMouseButton;
        else Debug.LogError($"[{nameof(InputCounter)}] OutFocusMouseHook 참조가 없습니다.", this);

        // InFocus 키 — KeyControl만 통과시켜 마우스/게임패드 등 다른 ButtonControl 제외.
        _anyButtonSubscription = InputSystem.onAnyButtonPress.Call(ctrl =>
        {
            if (ctrl is KeyControl) Count++;
        });
    }

    private void OnDisable()
    {
        if (_outFocusKey != null) _outFocusKey.KeyPressed -= OnOutFocusKey;
        if (_outFocusMouse != null) _outFocusMouse.ButtonPressed -= OnOutFocusMouseButton;
        _anyButtonSubscription?.Dispose();
        _anyButtonSubscription = null;
    }

    private void Update()
    {
        // InFocus 마우스 — OutFocus 시 Mouse.current는 자체적으로 클릭을 받지 못함.
        var mouse = Mouse.current;
        if (mouse == null) return;

        if (mouse.leftButton.wasPressedThisFrame)   Count++;
        if (mouse.rightButton.wasPressedThisFrame)  Count++;
        if (mouse.middleButton.wasPressedThisFrame) Count++;
    }

    private void OnOutFocusKey(Key _)              => Count++;
    private void OnOutFocusMouseButton(MouseButton _) => Count++;
}
