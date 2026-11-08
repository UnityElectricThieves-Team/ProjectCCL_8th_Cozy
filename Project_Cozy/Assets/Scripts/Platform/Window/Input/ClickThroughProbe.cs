using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 클릭 투과(WS_EX_TRANSPARENT)가 ON일 때 Unity의 마우스 위치가 계속 갱신되는지를 확인하는 테스트 컴포넌트.
///
/// 본격적인 ClickThroughController를 만들기 전, 한 가지 사실만 검증한다:
///   - WS_EX_TRANSPARENT ON 상태에서 Mouse.current.position이 계속 갱신되는가, 아니면 freeze되는가?
/// freeze된다면 OS-wide GetCursorPos 폴링 컴포넌트가 따로 필요하고, 갱신된다면 기존 Input을 그대로 쓸 수 있다.
///
/// 화면 좌상단에 두 값을 OnGUI로 실시간 표시하고, F12로 클릭 투과를 토글한다. 빌드에서만 동작.
/// </summary>
public class ClickThroughProbe : MonoBehaviour
{
    // BorderlessWindow와 동일 상수 (재선언 — 두 컴포넌트가 독립적으로 동작 가능하도록)
    const int GWL_EXSTYLE = -20;
    const uint WS_EX_TRANSPARENT = 0x00000020;

    [StructLayout(LayoutKind.Sequential)]
    struct POINT { public int x; public int y; }

    [DllImport("user32.dll")]
    static extern uint GetWindowLong(IntPtr hwnd, int nIndex);

    [DllImport("user32.dll")]
    static extern int SetWindowLong(IntPtr hwnd, int nIndex, uint dwNewLong);

    [DllImport("user32.dll")]
    static extern bool GetCursorPos(out POINT lpPoint);

    [Tooltip("HWND 획득용. 비워두면 자동 탐색.")]
    [SerializeField] private BorderlessWindow _borderlessWindow;

    [Tooltip("토글 키 입력 소스. 비워두면 자동 탐색.")]
    [SerializeField] private GlobalKeyInput _globalKeyInput;

    [Tooltip("클릭 투과 ON↔OFF 토글 키. 빌드에서 멈췄을 때 비상 탈출용.")]
    [SerializeField] private Key _toggleKey = Key.F12;

    [Tooltip("Start 시점에 클릭 투과를 ON으로 켤지 여부. 끄고 시작하면 F12로 켜서 차이 비교.")]
    [SerializeField] private bool _startWithClickThrough = true;

    bool _clickThroughOn;
    Vector2 _unityMousePos;
    Vector2 _osCursorPos;     // OS 데스크톱 좌표 (좌상단 원점, y-down)
    bool _osCursorValid;
    GUIStyle _labelStyle;
    Texture2D _bgTex;

    // BorderlessWindow처럼 한 씬에 둘 이상 두면 토글 상태가 꼬임 — 두 번째 인스턴스는 스스로 제거.
    static int _instanceCount;

    void Awake()
    {
        if (_instanceCount > 0)
        {
            Debug.LogWarning("[ClickThroughProbe] 두 번째 인스턴스 감지 — 자기 자신 제거.");
            Destroy(this);
            return;
        }
        _instanceCount++;
    }

    void Start()
    {
        if (_borderlessWindow == null)
            _borderlessWindow = FindFirstObjectByType<BorderlessWindow>();
        if (_globalKeyInput == null)
            _globalKeyInput = FindFirstObjectByType<GlobalKeyInput>();

        if (_globalKeyInput != null)
            _globalKeyInput.KeyPressed += OnKeyPressed;
        else
            Debug.LogWarning("[ClickThroughProbe] GlobalKeyInput 없음 — F12 토글 비활성. 시작 상태로 고정됨.");

#if !UNITY_EDITOR
        if (_startWithClickThrough)
            SetClickThrough(true);
        else
            _clickThroughOn = false; // 아무것도 안 했지만 상태 표시 일관성
#endif
    }

    void OnDestroy()
    {
        if (_globalKeyInput != null)
            _globalKeyInput.KeyPressed -= OnKeyPressed;

#if !UNITY_EDITOR
        // 잔여 상태 방지 — 클릭 투과를 무조건 OFF로 돌려놓고 빠진다.
        SetClickThrough(false);
#endif
        if (_instanceCount > 0) _instanceCount--;
        if (_bgTex != null) Destroy(_bgTex);
    }

    void OnApplicationQuit()
    {
#if !UNITY_EDITOR
        SetClickThrough(false);
#endif
    }

    void Update()
    {
        // (1) Unity가 보는 마우스 위치 — 이게 freeze되는지 보는 게 본 테스트의 핵심.
        var mouse = Mouse.current;
        if (mouse != null)
            _unityMousePos = mouse.position.ReadValue();

#if !UNITY_EDITOR
        // (2) OS-wide 데스크톱 커서 위치 — 클릭 투과와 무관하게 *반드시* 갱신되는 기준점.
        _osCursorValid = GetCursorPos(out POINT p);
        if (_osCursorValid)
            _osCursorPos = new Vector2(p.x, p.y);
#endif
    }

    void OnKeyPressed(Key key)
    {
#if !UNITY_EDITOR
        if (key == _toggleKey)
            SetClickThrough(!_clickThroughOn);
#endif
    }

#if !UNITY_EDITOR
    void SetClickThrough(bool on)
    {
        if (_borderlessWindow == null || _borderlessWindow.Hwnd == IntPtr.Zero)
        {
            Debug.LogWarning("[ClickThroughProbe] BorderlessWindow.Hwnd 미준비 — 토글 무시.");
            return;
        }

        uint exStyle = GetWindowLong(_borderlessWindow.Hwnd, GWL_EXSTYLE);
        uint newStyle = on
            ? (exStyle | WS_EX_TRANSPARENT)
            : (exStyle & ~WS_EX_TRANSPARENT);
        SetWindowLong(_borderlessWindow.Hwnd, GWL_EXSTYLE, newStyle);
        _clickThroughOn = on;
        Debug.Log("[ClickThroughProbe] ClickThrough = " + on);
    }
#endif

    void OnGUI()
    {
        EnsureGuiResources();

        // 배경 박스 — DWM 키컬러가 (0,0,0)이라 완전한 검정은 투명 처리됨. 짙은 회색으로 회피.
        GUI.DrawTexture(new Rect(10, 10, 640, 220), _bgTex);

        float y = 20f;
        GUI.Label(new Rect(20, y, 620, 30),
            $"ClickThrough: {(_clickThroughOn ? "ON" : "OFF")}   (toggle: {_toggleKey})", _labelStyle);
        y += 36;
        GUI.Label(new Rect(20, y, 620, 30),
            $"Mouse.current.position : {_unityMousePos.x:F0}, {_unityMousePos.y:F0}", _labelStyle);
        y += 36;
        GUI.Label(new Rect(20, y, 620, 30),
            $"GetCursorPos (OS)      : {(_osCursorValid ? $"{_osCursorPos.x:F0}, {_osCursorPos.y:F0}" : "(invalid)")}",
            _labelStyle);
        y += 36;
        // 좌표계 보정: OS는 좌상단 원점 y-down, Unity는 좌하단 원점 y-up.
        // 윈도우가 화면 (0,0)에 있다고 가정한 단순 변환 — 절대값이 어긋나도 마우스를 움직였을 때 변화량(delta)이 같이 따라가는지를 본다.
        Vector2 unityFromOs = new Vector2(_osCursorPos.x, Screen.height - _osCursorPos.y);
        GUI.Label(new Rect(20, y, 620, 30),
            $"OS → Unity 변환         : {unityFromOs.x:F0}, {unityFromOs.y:F0}", _labelStyle);
        y += 36;
        Vector2 delta = unityFromOs - _unityMousePos;
        GUI.Label(new Rect(20, y, 620, 30),
            $"Diff (변환 - Unity)     : {delta.x:F0}, {delta.y:F0}   (마우스 움직일 때 *변화*하지 않으면 Unity가 freeze)",
            _labelStyle);
    }

    void EnsureGuiResources()
    {
        if (_labelStyle == null)
        {
            _labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 22 };
            _labelStyle.normal.textColor = Color.white;
        }
        if (_bgTex == null)
        {
            _bgTex = new Texture2D(1, 1);
            // (0,0,0)은 DWM 트릭에서 투명이 되므로 살짝 띄운 회색.
            _bgTex.SetPixel(0, 0, new Color(0.08f, 0.08f, 0.08f, 0.85f));
            _bgTex.Apply();
        }
    }
}
