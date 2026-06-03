// ============================================================
// WindowState
//
// 마스코트 창이 "어떤 상태이길 원하는가"를 표현하는 평범한 데이터.
// 정책 레이어(OverlayWindowController)가 생성하고,
// 적용 레이어(OverlayWindow)가 Win32로 실현한다.
//
// 이 타입 자체는 Win32를 모른다 — 의도만 담는다.
// ============================================================
using System;
using UnityEngine;

[Serializable]
public class WindowState : IEquatable<WindowState>
{
    [Tooltip("WS_POPUP — 타이틀바/테두리 제거")]
    public bool Borderless;

    [Tooltip("WS_EX_LAYERED + ColorKey — ColorKey 픽셀이 투명 + 클릭 통과")]
    public bool Transparent;

    [Tooltip("WS_EX_TRANSPARENT — 픽셀 무관하게 창 전체 클릭 통과")]
    public bool ClickThrough;

    [Tooltip("HWND_TOPMOST — 항상 위")]
    public bool TopMost;

    [Tooltip("이 색 픽셀이 투명 + 클릭 통과. 카메라 BackgroundColor와 동일해야 함")]
    public Color ColorKey;

    public bool Equals(WindowState o)
    {
        return Borderless == o.Borderless
            && Transparent == o.Transparent
            && ClickThrough == o.ClickThrough
            && TopMost == o.TopMost
            && ColorKey == o.ColorKey;
    }

    public override bool Equals(object obj) => obj is WindowState o && Equals(o);

    public override int GetHashCode() =>
        (Borderless, Transparent, ClickThrough, TopMost, ColorKey).GetHashCode();
}
