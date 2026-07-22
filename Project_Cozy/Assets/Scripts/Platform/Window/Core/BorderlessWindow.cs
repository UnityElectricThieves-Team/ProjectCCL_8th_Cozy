using System;
using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// Always-on-Top + 투명 배경 borderless 창 설정.
/// 빌드 전용 (#if !UNITY_EDITOR) — Editor에서 실행하면 Unity Editor 창 자체가 망가진다.
///
/// 다른 창 컴포넌트가 HWND를 필요로 하는 초기 실험을 위해 Hwnd 프로퍼티로 노출한다.
/// 적용 시점을 Awake로 둬서, Start에서 등록되는 다른 Window 관련 컴포넌트보다 먼저 스타일이 잡히도록 한다.
/// </summary>
public class BorderlessWindow : MonoBehaviour
{
    // === GetWindowLong / SetWindowLong 의 nIndex 인자 ===
    // 어떤 속성을 읽고 쓸지 지정. 음수 상수들은 Win32 헤더(winuser.h)에 정의된 값.
    const int GWL_STYLE   = -16; // 일반 윈도우 스타일 (테두리/타이틀바 등 외형)
    const int GWL_EXSTYLE = -20; // 확장 윈도우 스타일 (Layered 등 합성 관련)

    // === Window Style 비트 플래그 ===
    // borderless(테두리/타이틀바 제거) 창을 만들기 위해 WS_POPUP만 켠다.
    // WS_OVERLAPPED(기본 창)에는 캡션/시스템 메뉴/리사이즈 보더가 포함되어 있어 우리에겐 부적합.
    const uint WS_POPUP    = 0x80000000;
    const uint WS_VISIBLE  = 0x10000000;

    // === Extended Window Style 비트 플래그 ===
    // WS_EX_LAYERED: DWM 합성에 참여 → DwmExtendFrameIntoClientArea 트릭으로 투명 배경을 만들 수 있게 한다.
    const uint WS_EX_LAYERED     = 0x00080000;
    // WS_EX_TRANSPARENT: 마우스 메시지를 통과시키는 click-through.
    // TODO: click-through 기능 도입 시 사용. 활성화하면 리사이즈와 양립 불가하므로 토글 메커니즘 필요.
    const uint WS_EX_TRANSPARENT = 0x00000020;

    // === SetWindowPos 플래그 ===
    // 한 번의 호출로 Z-order만 바꾸고 싶을 때 위치/크기 인자를 무시하도록 NOMOVE/NOSIZE를 사용.
    const uint SWP_NOMOVE       = 0x0002;
    const uint SWP_NOSIZE       = 0x0001;
    const uint SWP_SHOWWINDOW   = 0x0040;
    const uint SWP_FRAMECHANGED = 0x0020; // 스타일 변경 후 프레임 갱신을 강제

    // hwndInsertAfter 자리에 넣어 Always-on-Top을 지정하는 매직 핸들 값(-1).
    static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);

    // DwmExtendFrameIntoClientArea 인자.
    // 모든 변을 -1로 두면 클라이언트 전체 영역에 DWM 프레임이 확장되어, 검정(0,0,0) 픽셀이 투명으로 합성된다.
    [StructLayout(LayoutKind.Sequential)]
    struct MARGINS
    {
        public int cxLeftWidth;
        public int cxRightWidth;
        public int cyTopHeight;
        public int cyBottomHeight;
    }


    [DllImport("user32.dll")]
    static extern uint GetWindowLong(IntPtr hwnd, int nIndex);

    [DllImport("user32.dll")]
    static extern int SetWindowLong(IntPtr hwnd, int nIndex, uint dwNewLong);

    [DllImport("user32.dll")]
    static extern bool SetWindowPos(IntPtr hwnd, IntPtr hwndInsertAfter,
        int x, int y, int cx, int cy, uint uFlags);

    [DllImport("dwmapi.dll")]
    static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref MARGINS pMarInset);

    /// <summary>현재 Unity 프로세스의 메인 창 핸들. 다른 Window 관련 컴포넌트가 사용한다.</summary>
    public IntPtr Hwnd { get; private set; }

    void Awake()
    {
#if !UNITY_EDITOR
        Hwnd = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;

        // 1. 테두리/타이틀바 제거 (borderless)
        SetWindowLong(Hwnd, GWL_STYLE, WS_POPUP | WS_VISIBLE);

        // 2. Layered 스타일 추가 (DWM 투명화에 필요)
        uint exStyle = GetWindowLong(Hwnd, GWL_EXSTYLE);
        SetWindowLong(Hwnd, GWL_EXSTYLE, exStyle | WS_EX_LAYERED);

        // 3. DWM 프레임을 전체 클라이언트 영역으로 확장 → 검정 픽셀이 투명해짐
        MARGINS margins = new MARGINS { cxLeftWidth = -1, cxRightWidth = -1,
                                        cyTopHeight = -1, cyBottomHeight = -1 };
        DwmExtendFrameIntoClientArea(Hwnd, ref margins);

        // 4. Always-on-Top 설정
        SetWindowPos(Hwnd, HWND_TOPMOST, 0, 0, 0, 0,
            SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW | SWP_FRAMECHANGED);

        Debug.Log("[BorderlessWindow] Always-on-Top + 투명 창 설정 완료");
#else
        Debug.Log("[BorderlessWindow] Editor 모드: 창 설정 스킵");
#endif
    }
}
