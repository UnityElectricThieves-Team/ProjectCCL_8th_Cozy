/// <summary>
/// 장식 배치 모드마다 UI가 어떻게 달라지는지의 판정을 한 곳에 모은다.
/// <see cref="UIManager"/>(어떤 패널을 남길지)와 <see cref="InventoryModeController"/>(인벤토리 슬롯을 받을지)가 이것만 부른다 —
/// "어떤 모드가 인벤토리를 쓰는가"를 여러 클래스가 따로 판단하면 모드를 고칠 때 고칠 곳이 흩어진다.
/// 인벤토리 창·슬롯은 UI 개념이라 Gameplay의 <see cref="DecorationMode"/> 옆이 아니라 여기 둔다.
/// </summary>
public static class DecorationModeUi
{
    /// <summary>
    /// 이 모드 동안 "장식 이동·회수·제거 중에도 보임"(<see cref="UIPanel.VisibleDuringInventoryModes"/>)이 켜진 패널을 남기는가.
    /// 이동·회수·제거는 인벤토리 창에서 시작하고 그 창을 계속 쓴다(Figma 이동/회수/제거 모드). 설치는 전부 숨긴다.
    /// </summary>
    public static bool KeepsMarkedPanelsVisible(DecorationMode mode)
        => mode == DecorationMode.Move || mode == DecorationMode.Recall || mode == DecorationMode.Remove;

    /// <summary>이 모드에서 인벤토리 슬롯을 눌러 대상으로 삼는가. 이동(꺼내 놓기)과 제거만 그렇다 — 회수는 놓인 장식만 대상이다.</summary>
    public static bool TargetsInventorySlots(DecorationMode mode)
        => mode == DecorationMode.Move || mode == DecorationMode.Remove;
}
