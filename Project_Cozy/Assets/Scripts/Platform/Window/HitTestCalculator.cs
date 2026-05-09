/// <summary>
/// 마우스 좌표 + 창 RECT → ResizeHitZone 판정.
///
/// 순수 C# (UnityEngine 의존 없음) — EditMode 단위 테스트가 가능하다.
/// 좌표계는 Win32 스크린 좌표(top-left 원점, Y는 아래로 증가)에 통일.
/// Unity의 Input.mousePosition(클라이언트 좌표, bottom-left 원점)을 사용하려면 호출자가 변환해야 한다.
/// </summary>
public static class HitTestCalculator
{
    /// <summary>
    /// (mouseX, mouseY)가 창 RECT의 어느 핫존에 속하는지 판정.
    /// </summary>
    /// <param name="mouseX">마우스 X 스크린 좌표</param>
    /// <param name="mouseY">마우스 Y 스크린 좌표</param>
    /// <param name="winLeft">창의 왼쪽 스크린 X</param>
    /// <param name="winTop">창의 위쪽 스크린 Y</param>
    /// <param name="winRight">창의 오른쪽 스크린 X (= Left + Width)</param>
    /// <param name="winBottom">창의 아래쪽 스크린 Y (= Top + Height)</param>
    /// <param name="edgeThicknessPx">변 핫존의 두께. 이 거리 이내로 마우스가 들어오면 변/모서리 후보.</param>
    /// <param name="cornerSizePx">모서리 핫존의 한 변 길이. 보통 edgeThicknessPx보다 크게 둬서 모서리 잡기를 쉽게 한다.</param>
    public static ResizeHitZone Calculate(
        int mouseX, int mouseY,
        int winLeft, int winTop, int winRight, int winBottom,
        int edgeThicknessPx, int cornerSizePx)
    {
        // Guard: 창 밖이면 즉시 None
        if (mouseX < winLeft || mouseX >= winRight || mouseY < winTop || mouseY >= winBottom)
            return ResizeHitZone.None;

        bool nearLeft   = mouseX <  winLeft   + edgeThicknessPx;
        bool nearRight  = mouseX >= winRight  - edgeThicknessPx;
        bool nearTop    = mouseY <  winTop    + edgeThicknessPx;
        bool nearBottom = mouseY >= winBottom - edgeThicknessPx;

        bool inCornerLeft   = mouseX <  winLeft   + cornerSizePx;
        bool inCornerRight  = mouseX >= winRight  - cornerSizePx;
        bool inCornerTop    = mouseY <  winTop    + cornerSizePx;
        bool inCornerBottom = mouseY >= winBottom - cornerSizePx;

        // 모서리 우선 — 변 핫존 안에 있더라도 모서리 정사각형에 들어왔으면 모서리로 판정한다.
        // (그렇지 않으면 좌상 모서리에서도 "왼쪽 변"으로만 잡혀 가로 리사이즈만 가능해진다.)
        if (nearTop && inCornerLeft)     return ResizeHitZone.TopLeft;
        if (nearTop && inCornerRight)    return ResizeHitZone.TopRight;
        if (nearBottom && inCornerLeft)  return ResizeHitZone.BottomLeft;
        if (nearBottom && inCornerRight) return ResizeHitZone.BottomRight;
        if (nearLeft && inCornerTop)     return ResizeHitZone.TopLeft;
        if (nearLeft && inCornerBottom)  return ResizeHitZone.BottomLeft;
        if (nearRight && inCornerTop)    return ResizeHitZone.TopRight;
        if (nearRight && inCornerBottom) return ResizeHitZone.BottomRight;

        if (nearLeft)   return ResizeHitZone.Left;
        if (nearRight)  return ResizeHitZone.Right;
        if (nearTop)    return ResizeHitZone.Top;
        if (nearBottom) return ResizeHitZone.Bottom;

        return ResizeHitZone.None;
    }
}
