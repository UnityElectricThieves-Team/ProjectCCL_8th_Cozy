using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;  // KeyControl
using UnityEngine.InputSystem.LowLevel;  // MouseButton
using UnityEngine.InputSystem.Utilities; // .Call() extension method

/// <summary>
/// 스폰 포인트의 '스폰 기운'을 관리한다. 포커스 무관 입력 4채널을 모아 스폰 기운을 누적하고, 스폰으로 차감한다.
/// 스폰 포인트(Star 오브젝트)의 <see cref="StarController"/>는 이 관리자를 참조해 활성 여부·소환을 판단한다.
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
public class SpawnPointManager : MonoBehaviour
{
    [SerializeField] private OutFocusKeyHook _outFocusKey;
    [SerializeField] private OutFocusMouseHook _outFocusMouse;

    private IDisposable _anyButtonSubscription;

    /// <summary>현재 스폰 기운 — 소비형. 스폰으로 차감된다.</summary>
    public int CurrentEnergy { get; private set; }

    /// <summary>줄어들지 않는 누적 스폰 기운. 스폰으로 <see cref="CurrentEnergy"/>가 깎여도 유지된다(디버그 표시·캐릭터 해금 진행도용).</summary>
    public int CumulativeEnergy { get; private set; }

    /// <summary>입력 1회 — 소비형 <see cref="CurrentEnergy"/>와 누적 <see cref="CumulativeEnergy"/>를 함께 올린다. 모든 입력 채널이 이 메서드를 통한다.</summary>
    private void Increment()
    {
        CurrentEnergy++;
        CumulativeEnergy++;
    }

    /// <summary>스폰 기운 차감. 음수로 가지 않도록 0에서 클램프.</summary>
    public void Spend(int amount)
    {
        if (amount <= 0) return;
        CurrentEnergy = Mathf.Max(0, CurrentEnergy - amount);
    }

    /// <summary>현재 스폰 기운 상태를 저장 데이터로 내보낸다(미래 저장 시스템용). <see cref="HeartSystem.ExportSave"/>와 같은 seam.</summary>
    public SpawnPointFileFormat ExportSave()
    {
        return new SpawnPointFileFormat { currentEnergy = CurrentEnergy, cumulativeEnergy = CumulativeEnergy };
    }

    /// <summary>저장 데이터를 스폰 기운 상태에 주입한다(미래 저장 시스템용). null은 무시.</summary>
    public void ImportSave(SpawnPointFileFormat data)
    {
        if (data == null) return;
        CurrentEnergy = data.currentEnergy;
        CumulativeEnergy = data.cumulativeEnergy;
    }

    private void OnEnable()
    {
        // 인스펙터 미연결 시 Singleton 폴백 — OutFocus 훅은 OS-wide라 씬당 1개. 훅의 [DefaultExecutionOrder(-100)]이
        // 먼저 Awake되도록 보장하므로 이 시점에 Instance가 잡혀 있다. (StateModule.Bind와 같은 방식)
        if (_outFocusKey == null) _outFocusKey = OutFocusKeyHook.Instance;
        if (_outFocusMouse == null) _outFocusMouse = OutFocusMouseHook.Instance;

        if (_outFocusKey != null) _outFocusKey.KeyPressed += OnOutFocusKey;
        else Debug.LogError($"[{nameof(SpawnPointManager)}] OutFocusKeyHook 참조가 없습니다.", this);

        if (_outFocusMouse != null) _outFocusMouse.ButtonPressed += OnOutFocusMouseButton;
        else Debug.LogError($"[{nameof(SpawnPointManager)}] OutFocusMouseHook 참조가 없습니다.", this);

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
