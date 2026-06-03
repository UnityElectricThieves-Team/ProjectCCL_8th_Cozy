// ============================================================
// WindowDebugOverlay
//
// 브링업/디버그용 OnGUI 오버레이. 핵심 로직과 분리된 선택적 컴포넌트로,
// 릴리스에서는 빼거나 GameObject를 비활성화하면 된다.
//
// OverlayWindow / OverlayWindowController의 공개 상태만 읽는다 — 창 조작은 안 한다.
// ============================================================
using UnityEngine;

[RequireComponent(typeof(OverlayWindow))]
public class WindowDebugOverlay : MonoBehaviour
{
    [SerializeField] private bool _show = true;

    private OverlayWindow _window;
    private OverlayWindowController _controller;

    private void Awake()
    {
        _window = GetComponent<OverlayWindow>();
        _controller = GetComponent<OverlayWindowController>();
    }

    private void OnGUI()
    {
        if (!_show) return;

        GUILayout.BeginArea(new Rect(10, 10, 460, 90));
        var box = new GUIStyle(GUI.skin.box) { fontSize = 11 };
        box.normal.textColor = _window.HasWindow ? Color.green : Color.red;

        string mode = _controller != null ? _controller.Mode.ToString() : "(no controller)";
        GUILayout.Box($"HasWindow={_window.HasWindow}  HWND=0x{(long)_window.Hwnd:X}", box, GUILayout.Width(440));
        GUILayout.Box($"Mode={mode}", box, GUILayout.Width(440));
        GUILayout.EndArea();
    }
}
