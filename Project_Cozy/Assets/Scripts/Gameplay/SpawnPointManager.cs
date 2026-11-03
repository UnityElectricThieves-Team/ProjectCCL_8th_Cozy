using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;  // KeyControl
using UnityEngine.InputSystem.LowLevel;  // MouseButton
using UnityEngine.InputSystem.Utilities; // .Call() extension method

/// <summary>
/// 스폰 포인트의 '스폰 기운'을 관리한다. 포커스 무관 입력 4채널을 모아 스폰 기운을 누적한다. 스폰해도 차감하지 않는다.
/// 스폰 포인트(Cloud 오브젝트)의 <see cref="CloudController"/>는 이 관리자를 참조해 활성 여부·소환을 판단한다.
///
/// <list type="bullet">
///   <item>InFocus 키 : <c>InputSystem.onAnyButtonPress</c>의 <see cref="KeyControl"/></item>
///   <item>OutFocus 키 : <see cref="OutFocusKeyHook"/>.KeyPressed</item>
///   <item>InFocus 마우스 : <c>Mouse.current</c> 버튼 폴링</item>
///   <item>OutFocus 마우스 : <see cref="OutFocusMouseHook"/>.ButtonPressed</item>
/// </list>
///
/// 중복 카운트 안전: InputSystem은 창 비활성 시 자체적으로 fire 안 하고, OutFocus 훅은 <c>Application.isFocused</c>로 게이트 → 두 경로가 자연 배타적.
///
/// OutFocus 두 채널은 유저 설정 '관리자 모드'가 켜져 있을 때만 센다(<see cref="SettingsManager.AdministratorMode"/>).
/// </summary>
public class SpawnPointManager : MonoBehaviour
{
    private IDisposable _anyButtonSubscription;

    /// <summary>누적 스폰 기운의 최대치. 기획이 "최대 수치 1,000,000,000"으로 정했다.</summary>
    public const int MaxEnergy = 1_000_000_000;

    /// <summary>누적 스폰 기운. 쌓이기만 하고 스폰해도 차감되지 않는다.</summary>
    public int CumulativeEnergy { get; private set; }

    /// <summary>입력 1회 — 누적 <see cref="CumulativeEnergy"/>를 올린다. 모든 입력 채널이 이 메서드를 통한다.</summary>
    private void Increment()
    {
        if (CumulativeEnergy >= MaxEnergy) return;
        CumulativeEnergy++;
    }

    /// <summary>게임 초기화(<see cref="GameProgressReset"/>). 누적 스폰 기운을 0으로 되돌린다.
    /// 구름(<see cref="CloudController"/>)은 다음 프레임에 이 값을 보고 스스로 Idle로 돌아간다.</summary>
    public void ResetProgress() => CumulativeEnergy = 0;

    private void OnEnable()
    {
        // OutFocus 훅은 static 이벤트를 방송하므로 인스턴스 참조 없이 바로 구독한다.
        OutFocusKeyHook.KeyPressed += OnOutFocusKey;
        OutFocusMouseHook.ButtonPressed += OnOutFocusMouseButton;

        // InFocus 키 — KeyControl만 통과시켜 마우스/게임패드 등 다른 ButtonControl 제외.
        _anyButtonSubscription = InputSystem.onAnyButtonPress.Call(ctrl =>
        {
            if (ctrl is KeyControl) Increment();
        });
    }

    private void OnDisable()
    {
        OutFocusKeyHook.KeyPressed -= OnOutFocusKey;
        OutFocusMouseHook.ButtonPressed -= OnOutFocusMouseButton;
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

    private void OnOutFocusKey(Key _)              { if (CountsOutFocusInput) Increment(); }
    private void OnOutFocusMouseButton(MouseButton _) { if (CountsOutFocusInput) Increment(); }

    // 유저 설정 '관리자 모드' = 다른 앱을 쓰는 중에 들어온 입력도 스폰 기운으로 셀지. Windows 관리자 권한과는 무관하다.
    // 매니저가 없는 씬(테스트 등)에서는 예전처럼 센다. 캐릭터 수면 판정은 이 설정과 상관없이 포커스 밖 입력을 센다.
    private static bool CountsOutFocusInput => SettingsManager.Instance == null || SettingsManager.Instance.AdministratorMode;
}
