/// <summary>
/// 마우스가 창의 어느 영역에 위치하는지 나타내는 enum (리사이즈 핫존 전용).
/// HitTestCalculator가 좌표 → ResizeHitZone 판정을 수행하고,
/// WindowManager는 이 값을 Win32의 NCHITTEST 반환값(HT*)으로 변환해 OS에 돌려준다.
/// </summary>
public enum ResizeHitZone
{
    /// <summary>리사이즈 핫존 밖 (= 클라이언트 영역). NCHITTEST에서 HTCLIENT(1) 반환.</summary>
    None,

    // 4개의 변
    Left,
    Right,
    Top,
    Bottom,

    // 4개의 모서리. 변보다 우선순위가 높다 (변끼리 겹치는 영역이므로).
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight,

    /// <summary>이동(드래그) 영역 — NCHITTEST에서 HTCAPTION(2) 반환. 편집 모드 상단 바.</summary>
    Caption,
}

/// <summary>
/// ResizeHitZone → Win32 NCHITTEST 반환 코드 변환.
///
/// HTLEFT/HTRIGHT/HTTOP/... 정수값들은 winuser.h에 정의된 OS 약속이라 임의로 바꾸면 안 된다.
/// OS는 이 값을 보고 어떤 시스템 커서를 그릴지, 어느 방향으로 리사이즈할지를 결정한다.
/// </summary>
public static class ResizeHitZoneExtensions
{
    // === Win32 NCHITTEST 반환 코드 (winuser.h) ===
    // OS 정의값이라 변경 불가. 우리는 단순히 매핑만 한다.
    public const int HTCLIENT      = 1;
    public const int HTCAPTION     = 2;
    public const int HTLEFT        = 10;
    public const int HTRIGHT       = 11;
    public const int HTTOP         = 12;
    public const int HTTOPLEFT     = 13;
    public const int HTTOPRIGHT    = 14;
    public const int HTBOTTOM      = 15;
    public const int HTBOTTOMLEFT  = 16;
    public const int HTBOTTOMRIGHT = 17;

    public static int ToHitTestCode(this ResizeHitZone zone)
    {
        switch (zone)
        {
            case ResizeHitZone.Left:        return HTLEFT;
            case ResizeHitZone.Right:       return HTRIGHT;
            case ResizeHitZone.Top:         return HTTOP;
            case ResizeHitZone.Bottom:      return HTBOTTOM;
            case ResizeHitZone.TopLeft:     return HTTOPLEFT;
            case ResizeHitZone.TopRight:    return HTTOPRIGHT;
            case ResizeHitZone.BottomLeft:  return HTBOTTOMLEFT;
            case ResizeHitZone.BottomRight: return HTBOTTOMRIGHT;
            case ResizeHitZone.Caption:     return HTCAPTION;
            default:                        return HTCLIENT;
        }
    }
}
