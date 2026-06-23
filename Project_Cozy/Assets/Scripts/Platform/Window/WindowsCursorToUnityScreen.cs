// ============================================================
// WindowsCursorToUnityScreen
//
// "지금 커서가 유니티 스크린 좌표로 어디인가"의 단일 소스.
// 소비자(InputInteractionManager, OpaqueHoverable)는 UnityScreenPosition만 읽으면 된다.
//
// 왜 필요한가:
//   투명 클릭-통과(ColorKey) 창에선, 커서가 투명(빈) 픽셀 위에 있으면 마우스 이동이 뒤 창으로
//   통과해 우리 창엔 안 온다 → Unity의 Mouse.current.position이 "마지막 위치"에 멈춘다(freeze).
//   그래서 호버 이탈을 못 잡는다(예: Pet에서 안 빠져나옴). Win32 GetCursorPos는 포커스·투명과
//   무관하게 항상 진짜 커서 좌표를 주므로, 빌드에선 이걸 쓴다.
//
// 변환 단계 (각 단계가 코드에 그대로 드러남):
//   desktopPoint  : GetCursorPos            — 데스크톱 전역 좌표(좌상단 원점, y-down)
//   clientPoint   : ScreenToClient(hwnd)    — 현재 창 클라이언트 좌표(리사이즈/이동 자동 반영)
//   UnityScreen   : Y 뒤집기(Screen.height) — 유니티 스크린 좌표(좌하단 원점, y-up)
//   → 월드 좌표는 카메라가 필요하므로 소비자가 ScreenToWorldPoint로 처리.
//
// 폴백은 여기 한 곳에 모은다: 에디터, 또는 빌드에서 HWND가 아직 준비 안 됐으면 Mouse.current 사용.
// HWND는 OverlayWindow가 잡아둔 것을 재사용(EnsureHwnd 중복 금지).
// ============================================================
using System;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class WindowsCursorToUnityScreen : MonoBehaviour
{
    [SerializeField, Tooltip("HWND 소스. 비우면 Awake에서 씬에서 자동 탐색.")]
    private OverlayWindow _overlayWindow;

    /// <summary>현재 커서의 유니티 스크린 좌표(픽셀, 좌하단 원점). 빌드는 OS 커서 기반, 그 외엔 Mouse.current.</summary>
    public Vector2 UnityScreenPosition { get; private set; }

    private void Awake()
    {
        if (_overlayWindow == null) _overlayWindow = FindFirstObjectByType<OverlayWindow>();
    }

    private void Update()
    {
#if !UNITY_EDITOR
        // 빌드: OS 커서 우선. HWND가 아직 준비 안 됐으면(시작 직후) 아래 Mouse.current 폴백으로 떨어진다.
        IntPtr hwnd = _overlayWindow != null ? _overlayWindow.Hwnd : IntPtr.Zero;
        if (hwnd != IntPtr.Zero && Win32WindowApi.GetCursorPos(out var desktopPoint))
        {
            var clientPoint = desktopPoint;
            Win32WindowApi.ScreenToClient(hwnd, ref clientPoint);   // 데스크톱 → 현재 창 클라이언트
            UnityScreenPosition = new Vector2(clientPoint.x, Screen.height - clientPoint.y); // y-down → y-up
            return;
        }
#endif
        // 에디터, 또는 빌드에서 HWND 미준비 시 — Mouse.current 폴백 (한 곳에서만)
        var mouse = Mouse.current;
        if (mouse != null) UnityScreenPosition = mouse.position.ReadValue();
    }
}
