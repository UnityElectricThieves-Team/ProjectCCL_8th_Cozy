using System;
using UnityEngine;

/// <summary>
/// 인벤토리 창의 [이동]/[회수]/[제거] 버튼으로 들어가는 장식 배치 모드를 맡는다. 버튼 OnClick(인스펙터)이 여기 public 메서드를 부르고,
/// 모드에서 장식을 고르면 <see cref="ShopSystem"/>을 불러 저장한다. 규약은 .claude/rules/unity/decoration-placement.md.
///
/// 모드의 골격(고르기·들고 다니기·잠금·종료)은 Gameplay의 <see cref="DecorationPlacementController"/>에 있고, 그쪽은 상점을 모른다.
/// 상품 id·개수를 아는 쪽은 이 UI 컴포넌트다 — 상점의 구매를 상점 컨트롤러가 처리하는 것과 같은 층이다.
///
/// 모드 동안 인벤토리 창은 남아 있지만(<see cref="UIPanel.VisibleDuringInventoryModes"/>) X·ESC·메뉴 버튼으로 닫히지 않는다.
/// 닫으면 인벤토리 장식을 고를 수단이 사라지기 때문이다. 세 닫기 경로가 모두 지나는 <see cref="UIPanel.CanClose"/>에 건다.
/// 모드 중 [이동]/[회수]/[제거]를 다시 눌러도 무반응이다.
///
/// 닫기를 막으려면 <see cref="UIPanel"/>과 같은 오브젝트여야 해서, 콘텐츠 컨트롤러와 달리 인벤토리 패널 루트에 붙는다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(UIPanel))]
public sealed class InventoryModeController : MonoBehaviour
{
    private UIPanel _panel;
    private DecorationPlacementController _placement;

    // 콜백은 한 번 만들어 둔다. 버튼을 누를 때마다 메서드 그룹이 새 대리자로 할당되지 않게.
    private Func<bool> _canClose;
    private Action<int> _onRecall;

    private bool IsInMode => _placement != null && _placement.IsActive;

    private void Awake()
    {
        _panel = GetComponent<UIPanel>();
        _canClose = CanClosePanel;
        _onRecall = RecallPlaced;
        _panel.CanClose = _canClose;
    }

    private void Start()
    {
        // 컨트롤러는 Awake에서 Instance를 세운다. 그 순서에 기대지 않게 Start에서 읽는다.
        _placement = DecorationPlacementController.Instance;
        if (_placement == null)
            Debug.LogWarning($"[{nameof(InventoryModeController)}] 씬에 {nameof(DecorationPlacementController)}가 없어 이동·회수·제거 모드에 들어갈 수 없습니다.", this);
    }

    private void OnDestroy()
    {
        if (_panel != null && _panel.CanClose == _canClose) _panel.CanClose = null;
    }

    /// <summary>[회수] 버튼 OnClick. 놓인 장식을 누를 때마다 인벤토리로 거둔다. 우클릭·ESC까지 모드가 이어진다.</summary>
    public void EnterRecallMode()
    {
        if (_placement == null || IsInMode) return;
        _placement.EnterRecall(_onRecall);
    }

    private bool CanClosePanel() => !IsInMode;

    private void RecallPlaced(int placedIndex)
    {
        // 콜백이 모드가 끝난 뒤에 도착하면 무시한다.
        if (_placement == null || _placement.Mode != DecorationMode.Recall) return;
        ShopSystem.Instance?.RecallPlacedAt(placedIndex);
    }
}
