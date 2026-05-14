/// <summary>
/// 2D 포인터(마우스) 라우터가 전달하는 상호작용 계약. <see cref="PointerHitRouter2D"/> 참고.
/// </summary>
public interface IClickable
{
    void OnClick();
}

public interface IHoverable
{
    void OnHoverEnter();
    void OnHoverExit();
}

public interface IShiftRightClickable
{
    void OnShiftRightClick();
}
