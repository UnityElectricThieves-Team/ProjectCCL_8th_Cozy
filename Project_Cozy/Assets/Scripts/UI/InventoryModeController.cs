using System;
using UnityEngine;

/// <summary>
/// 인벤토리 창의 [이동]/[회수]/[제거] 버튼으로 들어가는 장식 배치 모드를 맡는다. 버튼 OnClick(인스펙터)이 여기 public 메서드를 부르고,
/// 모드에서 장식을 고르면 <see cref="ShopSystem"/>을 불러 저장한다. 규약은 .claude/rules/unity/decoration-placement.md.
///
/// 모드의 골격(고르기·들고 다니기·잠금·종료)은 Gameplay의 <see cref="DecorationPlacementController"/>에 있고, 그쪽은 상점을 모른다.
/// 상품 id·개수를 아는 쪽은 이 UI 컴포넌트다 — 상점의 구매를 상점 컨트롤러가 처리하는 것과 같은 층이다.
/// 인벤토리 슬롯 클릭도 여기서 받는다. 콘텐츠 컨트롤러는 "칸이 눌렸다"만 알린다.
///
/// 슬롯의 호버 색을 켜도 되는지(슬롯 입력 허용)는 여기 한 곳에서 정해 알린다 —
/// 슬롯을 받는 모드이고, 수량 팝업으로 입력을 멈추지 않았을 때다. 이 값이 바뀔 수 있는 순간마다 <see cref="RefreshSlotInput"/>을 부른다.
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
    private const string REMOVE_COUNT_QUESTION_ID = "UIPlacement.remove_count.question";

    [Tooltip("이 패널의 Area_Content에 붙은 콘텐츠 컨트롤러. 슬롯 클릭을 받고 호버 허용을 알린다.")]
    [SerializeField] private InventoryPanelContentController _content;

    private UIPanel _panel;
    private DecorationPlacementController _placement;
    private DecorationMode _mode;   // ModeChanged로 받은 값

    // 콜백은 한 번 만들어 둔다. 누를 때마다 메서드 그룹이 새 대리자로 할당되지 않게.
    private Func<bool> _canClose;
    private Action<int> _onRecall;
    private Action<int> _onRemovePlaced;
    private Action<ShopItemDefinition> _onSlotClicked;
    private Action<DecorationMode> _onModeChanged;
    private Action<int, float> _onMovePlaced;
    private Action<bool> _onCarryingChanged;

    private bool IsInMode => _mode != DecorationMode.None;

    private void Awake()
    {
        _panel = GetComponent<UIPanel>();
        _canClose = CanClosePanel;
        _onRecall = RecallPlaced;
        _onRemovePlaced = RemovePlaced;
        _onSlotClicked = OnSlotClicked;
        _onModeChanged = OnModeChanged;
        _onMovePlaced = MovePlaced;
        _onCarryingChanged = OnCarryingChanged;
        _panel.CanClose = _canClose;
        if (_content != null) _content.SlotClicked += _onSlotClicked;
    }

    private void Start()
    {
        // 컨트롤러는 Awake에서 Instance를 세운다. 그 순서에 기대지 않게 Start에서 읽는다.
        _placement = DecorationPlacementController.Instance;
        if (_placement == null)
        {
            Debug.LogWarning($"[{nameof(InventoryModeController)}] 씬에 {nameof(DecorationPlacementController)}가 없어 이동·회수·제거 모드에 들어갈 수 없습니다.", this);
            return;
        }
        _placement.ModeChanged += _onModeChanged;
        _placement.CarryingChanged += _onCarryingChanged;
        _mode = _placement.Mode;
        RefreshSlotInput();
    }

    private void OnDestroy()
    {
        if (_panel != null && _panel.CanClose == _canClose) _panel.CanClose = null;
        if (_content != null) _content.SlotClicked -= _onSlotClicked;
        if (_placement != null)
        {
            _placement.ModeChanged -= _onModeChanged;
            _placement.CarryingChanged -= _onCarryingChanged;
        }
    }

    /// <summary>
    /// [이동] 버튼 OnClick. 놓인 장식을 누르면 들고 다니다 새 자리에 놓고, 인벤토리 장식을 누르면 꺼내 화면에 놓는다.
    /// 놓을 때마다 다시 고르는 단계로 돌아가고, 우클릭·ESC까지 모드가 이어진다.
    /// </summary>
    public void EnterMoveMode()
    {
        if (_placement == null || IsInMode) return;
        _placement.EnterMove(_onMovePlaced);
    }

    /// <summary>[회수] 버튼 OnClick. 놓인 장식을 누를 때마다 인벤토리로 거둔다. 우클릭·ESC까지 모드가 이어진다.</summary>
    public void EnterRecallMode()
    {
        if (_placement == null || IsInMode) return;
        _placement.EnterRecall(_onRecall);
    }

    /// <summary>[제거] 버튼 OnClick. 놓인 장식이나 인벤토리 장식을 누를 때마다 영구히 없앤다. 하트는 돌려주지 않는다.</summary>
    public void EnterRemoveMode()
    {
        if (_placement == null || IsInMode) return;
        _placement.EnterRemove(_onRemovePlaced);
    }

    private bool CanClosePanel() => !IsInMode;

    private void OnModeChanged(DecorationMode mode)
    {
        _mode = mode;
        RefreshSlotInput();
    }

    private void OnCarryingChanged(bool carrying) => RefreshSlotInput();

    // 들고 다니는 동안에는 슬롯을 받지 않는다 — 그 클릭은 "여기에 놓기"이고, 인벤토리 위는 놓을 수 없는 자리다.
    private bool IsSlotInputAllowed()
        => DecorationModeUi.TargetsInventorySlots(_mode) && _placement != null
           && !_placement.IsInputPaused && !_placement.IsCarrying;

    // 슬롯 입력 허용이 바뀔 수 있는 순간(모드 바뀜, 팝업 열고 닫음, 들기 시작·끝)마다 부른다.
    private void RefreshSlotInput()
    {
        if (_content != null) _content.SetSlotHoverAllowed(IsSlotInputAllowed());
    }

    // 콜백이 모드가 끝난 뒤에 도착하면 무시한다.
    private void RecallPlaced(int placedIndex)
    {
        if (_mode != DecorationMode.Recall) return;
        ShopSystem.Instance?.RecallPlacedAt(placedIndex);
    }

    private void RemovePlaced(int placedIndex)
    {
        if (_mode != DecorationMode.Remove) return;
        ShopSystem.Instance?.RemovePlacedAt(placedIndex);
    }

    private void OnSlotClicked(ShopItemDefinition item)
    {
        if (item == null || !IsSlotInputAllowed()) return;

        if (_mode == DecorationMode.Remove) RemoveFromInventory(item);
        else if (_mode == DecorationMode.Move) CarryFromInventory(item);
    }

    private void MovePlaced(int placedIndex, float baseX)
    {
        if (_mode != DecorationMode.Move) return;
        ShopSystem.Instance?.MovePlacedAt(placedIndex, baseX);
    }

    // 인벤토리 장식을 들어 화면에 놓는다. 하트 없이 놓인 목록에만 더한다. 칸은 놓는 순간에 줄어든다.
    private void CarryFromInventory(ShopItemDefinition item)
    {
        if (item.decorationPrefab == null)
        {
            Debug.LogWarning($"[{nameof(InventoryModeController)}] '{item.id}'에 장식 프리팹이 없어 들 수 없습니다.", this);
            return;
        }

        string id = item.id;
        _placement.CarryFromInventory(item.decorationPrefab, baseX =>
        {
            if (_mode == DecorationMode.Move) ShopSystem.Instance?.PlaceFromInventory(id, baseX);
        });
    }

    // 한 개면 바로 지우고, 두 개 이상이면 몇 개를 지울지 묻는다(Figma 인벤토리 장식 삭제 시도).
    private void RemoveFromInventory(ShopItemDefinition item)
    {
        var shop = ShopSystem.Instance;
        if (shop == null) return;

        int count = shop.GetUnplacedCount(item.id);
        if (count <= 0) return;
        if (count == 1)
        {
            shop.RemoveUnplaced(item.id, 1);
            return;
        }

        var popup = CountPopup.Instance;
        if (popup == null)
        {
            Debug.LogWarning($"[{nameof(InventoryModeController)}] 씬에 {nameof(CountPopup)}이 없어 몇 개를 지울지 물을 수 없습니다.", this);
            return;
        }

        // 팝업이 떠 있는 동안 모드는 클릭·ESC를 받지 않는다. 팝업 버튼 클릭이 뒤의 장식으로 새지 않게 하려는 것이다.
        _placement.SetInputPaused(true);
        RefreshSlotInput();
        popup.Show(REMOVE_COUNT_QUESTION_ID, count,
            n => OnRemoveCountAnswered(item.id, n),
            () => OnRemoveCountAnswered(item.id, 0));
    }

    // 대답(확인 n개, 취소 0) 뒤 모드 입력을 되살린다. 대답이 모드가 끝난 뒤 오면 확인이라도 아무것도 하지 않는다.
    private void OnRemoveCountAnswered(string id, int count)
    {
        if (_mode != DecorationMode.Remove) return;

        _placement.SetInputPaused(false);
        RefreshSlotInput();
        if (count > 0) ShopSystem.Instance?.RemoveUnplaced(id, count);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (_content == null)
            Debug.LogWarning($"[{nameof(InventoryModeController)}] _content(InventoryPanelContentController)가 비어 있음.", this);
    }
#endif
}
