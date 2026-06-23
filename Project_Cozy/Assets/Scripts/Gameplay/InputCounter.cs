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

    /// <summary>지금까지 누적된 입력 횟수 — 기획 용어로는 '스폰 기운'. 스폰으로 차감된다.</summary>
    public int Count { get; private set; }

    /// <summary>줄어들지 않는 총 입력 누적. 스폰으로 Count가 깎여도 유지된다(디버그 표시·향후 해금용).</summary>
    public int CumulativeCount { get; private set; }

    /// <summary>입력 1회 — 소비형 Count와 누적 CumulativeCount를 함께 올린다. 모든 입력 채널이 이 메서드를 통한다.</summary>
    private void Increment()
    {
        Count++;
        CumulativeCount++;
    }

    /// <summary>스폰 기운 차감. 음수로 가지 않도록 0에서 클램프.</summary>
    public void ReduceSpawnEnergy(int amount)
    {
        if (amount <= 0) return;
        Count = Mathf.Max(0, Count - amount);
    }

    private void OnEnable()
    {
        // 인스펙터 미연결 시 Singleton 폴백 — OutFocus 훅은 OS-wide라 씬당 1개. 훅의 [DefaultExecutionOrder(-100)]이
        // 먼저 Awake되도록 보장하므로 이 시점에 Instance가 잡혀 있다. (StateModule.Bind와 같은 방식)
        if (_outFocusKey == null) _outFocusKey = OutFocusKeyHook.Instance;
        if (_outFocusMouse == null) _outFocusMouse = OutFocusMouseHook.Instance;

        if (_outFocusKey != null) _outFocusKey.KeyPressed += OnOutFocusKey;
        else Debug.LogError($"[{nameof(InputCounter)}] OutFocusKeyHook 참조가 없습니다.", this);

        if (_outFocusMouse != null) _outFocusMouse.ButtonPressed += OnOutFocusMouseButton;
        else Debug.LogError($"[{nameof(InputCounter)}] OutFocusMouseHook 참조가 없습니다.", this);

        // InFocus 키 — KeyControl만 통과시켜 마우스/게임패드 등 다른 ButtonControl 제외.
        _anyButtonSubscription = InputSystem.onAnyButtonPress.Call(ctrl =>
        {
            if (ctrl is KeyControl) Increment();
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

        if (mouse.leftButton.wasPressedThisFrame)   Increment();
        if (mouse.rightButton.wasPressedThisFrame)  Increment();
        if (mouse.middleButton.wasPressedThisFrame) Increment();
    }

    private void OnOutFocusKey(Key _)              => Increment();
    private void OnOutFocusMouseButton(MouseButton _) => Increment();
}
