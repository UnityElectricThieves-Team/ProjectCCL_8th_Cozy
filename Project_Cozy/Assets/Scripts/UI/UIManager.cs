using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// 씬에 하나 존재하는 UI 매니저. 패널 열기/닫기를 관리한다.
/// 여러 패널이 동시에 열릴 수 있다 — 열린 패널을 리스트로 들고, 리스트 끝이
/// 가장 앞(위)에 그려지는 최상단 패널이다. 패널을 열거나, 패널 어디든 누르면 맨 앞으로 온다.
/// ESC 닫기는 우리 창이 포커스일 때만 — 데스크톱에서 다른 앱의 ESC를 가로채지 않게.
///
/// 장식 배치 모드와 뷰포트 편집 동안에는 열린 패널을 닫지 않고 잠시 숨긴다(<see cref="UIPanel.SetSuspended"/>).
/// 모드가 끝나면 그대로 되살아나서 상점에서 이어 사거나 설정 그래픽 탭으로 돌아온다. 숨긴 동안에는 ESC로 패널을 닫지 않고,
/// 메뉴 버튼의 열기·토글도 무시한다 — 그 ESC와 클릭은 모드의 것이다.
/// 예외는 장식 이동·회수·제거 모드다. 그 입력 대상이 들어 있는 패널(<see cref="UIPanel.VisibleDuringInventoryModes"/>)은 남긴다.
/// 패널마다 숨길지는 <see cref="IsHidden"/> 한 곳에서 계산한다.
/// 모드가 이 매니저를 부르는 것이 아니라 이 매니저가 모드의 시작·종료 신호를 듣는다. 그래야 모드가 어떻게 끝나든
/// (강제 종료 포함) 패널이 되살아난다. 숨김은 모드별로 걸고 풀어서, 한쪽이 끝났다고 다른 쪽의 숨김이 풀리지 않는다.
///
/// 닫기는 X 버튼(<see cref="UIPanel.RequestClose"/>)·ESC·메뉴 버튼(<see cref="Toggle"/>) 모두 <see cref="Close"/> 하나를 지난다.
/// 그래서 닫기를 막을 수 있는 질문(<see cref="UIPanel.CanClose"/>)도 여기서 한 번만 묻는다.
///
/// 모달 패널(확인 팝업)이 열려 있는 동안에는 다른 패널을 열거나 토글할 수 없고, 열린 패널은 보이되 조작되지 않는다.
/// ESC는 맨 위인 모달을 닫는다.
/// </summary>
public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    // 동시에 열려 있는 패널들. 리스트 끝(마지막)이 최상단 — 가장 앞에 그려지고 ESC로 닫히는 대상.
    private readonly List<UIPanel> _open = new();

    // 패널을 숨기고 있는 모드들. 하나라도 있으면 숨긴다.
    private readonly HashSet<object> _suspendOwners = new();

    private DecorationPlacementController _placement;
    private DecorationMode _placementMode;   // ModeChanged로 받은 값. 컨트롤러의 Mode를 따로 읽지 않는다
    private ViewportScreenSettings _viewport;

    // 누른 패널을 찾는 레이캐스트용. 클릭마다 새로 만들지 않고 재사용한다.
    private PointerEventData _pressData;
    private readonly List<RaycastResult> _pressHits = new();

    private bool IsSuspended => _suspendOwners.Count > 0;

    /// <summary>모달 패널(확인 팝업)이 떠 있는가. 패널에 속하지 않은 메뉴 바 항목의 호버 말풍선이 팝업 앞에 겹치지 않게 볼 때 쓴다.</summary>
    public bool HasModal => TopModal() != null;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        // 배치 모드 컨트롤러는 Awake에서 Instance를 세우므로 이 시점엔 있다. 없는 씬이면 숨김 기능만 없는 것이다.
        _placement = DecorationPlacementController.Instance;
        if (_placement != null) _placement.ModeChanged += OnPlacementModeChanged;

        // 뷰포트 정책은 싱글톤이 아니라 한 번 찾아 둔다. 없는 씬이면 편집 중 숨김만 없는 것이다.
        _viewport = FindFirstObjectByType<ViewportScreenSettings>();
        if (_viewport != null) _viewport.EditModeChanged += OnViewportEditModeChanged;
    }

    private void OnDestroy()
    {
        if (_placement != null) _placement.ModeChanged -= OnPlacementModeChanged;
        if (_viewport != null) _viewport.EditModeChanged -= OnViewportEditModeChanged;
        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// 패널을 연다. 다른 패널은 닫지 않는다. 이미 열려 있으면 맨 앞으로 가져온다.
    /// 패널을 숨기는 모드 중에는 무시한다 — 그 모드에서도 남는 패널(<see cref="IsHidden"/>이 false)만 연다.
    /// </summary>
    public void Open(UIPanel panel)
    {
        if (panel == null || IsHidden(panel) || IsBlockedByModal(panel)) return;
        bool wasOpen = _open.Remove(panel); // 열려 있었으면 순서만 뺐다가
        _open.Add(panel);                   // 리스트 끝(최상단)으로 다시 넣는다
        if (!wasOpen) panel.Open();
        panel.transform.SetAsLastSibling(); // 형제 순서 = 그리기 순서. 맨 뒤 형제 = 맨 앞에 그려짐.
        RefreshModalBlock();
    }

    /// <summary>패널을 닫는다. 패널이 <see cref="UIPanel.CanClose"/>로 거절하면 닫지 않는다.</summary>
    public void Close(UIPanel panel)
    {
        if (panel == null || !_open.Contains(panel)) return;
        if (panel.CanClose != null && !panel.CanClose()) return;
        _open.Remove(panel);
        RefreshModalBlock(); // 닫힘 알림을 받은 쪽이 다른 패널을 다룰 수 있게, 막힘부터 먼저 푼다
        panel.Close();
    }

    /// <summary>
    /// 열려 있으면 닫고, 닫혀 있으면 연다. (메뉴 버튼용) 그 패널을 숨기는 모드 중과 모달이 떠 있는 동안에는 무시한다.
    /// 모드 중에도 남는 패널을 닫히지 않게 하는 것은 그 패널의 <see cref="UIPanel.CanClose"/>다.
    /// </summary>
    public void Toggle(UIPanel panel)
    {
        if (panel == null || IsHidden(panel) || IsBlockedByModal(panel)) return;
        if (_open.Contains(panel)) Close(panel);
        else Open(panel);
    }

    // 다른 모달이 떠 있으면 그 패널은 열거나 토글할 수 없다. 모달 자신은 막지 않는다.
    private bool IsBlockedByModal(UIPanel panel)
    {
        UIPanel modal = TopModal();
        return modal != null && modal != panel;
    }

    private UIPanel TopModal()
    {
        for (int i = _open.Count - 1; i >= 0; i--)
            if (_open[i] != null && _open[i].IsModal) return _open[i];
        return null;
    }

    // 모달이 있으면 그 밖의 열린 패널을 조작 못 하게, 없으면 모두 푼다.
    private void RefreshModalBlock()
    {
        UIPanel modal = TopModal();
        for (int i = 0; i < _open.Count; i++)
            if (_open[i] != null) _open[i].SetBlocked(modal != null && _open[i] != modal);
    }

    private void OnPlacementModeChanged(DecorationMode mode)
    {
        _placementMode = mode;
        SetSuspendedBy(_placement, mode != DecorationMode.None);
    }

    private void OnViewportEditModeChanged(bool editing) => SetSuspendedBy(_viewport, editing);

    // 숨김 소유자가 같아도 모드 종류가 바뀌면 남길 패널이 달라지므로, 바뀔 때마다 열린 패널 전부를 다시 계산한다.
    private void SetSuspendedBy(object owner, bool suspend)
    {
        if (suspend) _suspendOwners.Add(owner);
        else _suspendOwners.Remove(owner);

        for (int i = 0; i < _open.Count; i++)
        {
            // 플레이 종료 중에는 패널이 먼저 파괴됐을 수 있다.
            if (_open[i] != null) _open[i].SetSuspended(IsHidden(_open[i]));
        }
    }

    /// <summary>
    /// 이 패널을 지금 숨겨야 하는가. 숨김 소유자가 있으면 숨기되, 숨기는 것이 장식 배치 모드뿐이고
    /// 그 모드가 표시된 패널을 남기는 모드(<see cref="DecorationModeUi.KeepsMarkedPanelsVisible"/>)이고
    /// 패널이 표시돼 있으면(<see cref="UIPanel.VisibleDuringInventoryModes"/>) 남긴다.
    /// 숨김 적용·열기·토글·ESC가 모두 이 한 곳을 쓴다.
    /// </summary>
    private bool IsHidden(UIPanel panel)
    {
        if (!IsSuspended) return false;

        bool onlyPlacement = _suspendOwners.Count == 1 && _placement != null && _suspendOwners.Contains(_placement);
        bool kept = onlyPlacement
            && DecorationModeUi.KeepsMarkedPanelsVisible(_placementMode)
            && panel.VisibleDuringInventoryModes;
        return !kept;
    }

    private void Update()
    {
        if (IsSuspended || _open.Count == 0) return;
        BringPressedPanelToFront();
        CloseTopOnEscape();
    }

    // 왼쪽 버튼을 누른 프레임에만, 커서 아래 맨 앞 UI가 속한 열린 패널을 맨 앞으로 가져온다(ESC 대상이 된다).
    // 패널 루트에 IPointerDownHandler를 달면 버튼·탭이 이벤트를 먼저 가져가 본문 클릭을 놓친다.
    // 포커스는 보지 않는다 — 창 밖에서 들어온 첫 클릭 프레임엔 아직 포커스가 없을 수 있다.
    private void BringPressedPanelToFront()
    {
        var mouse = Mouse.current;
        var eventSystem = EventSystem.current;
        if (mouse == null || eventSystem == null || !mouse.leftButton.wasPressedThisFrame) return;

        _pressData ??= new PointerEventData(eventSystem);
        _pressData.position = mouse.position.ReadValue();
        _pressHits.Clear();
        eventSystem.RaycastAll(_pressData, _pressHits);
        if (_pressHits.Count == 0) return;

        // 모달이 떠 있으면 Open이 거절하므로 팝업이 맨 위로 남는다.
        UIPanel pressed = FindOpenPanelContaining(_pressHits[0].gameObject.transform);
        if (pressed != null) Open(pressed);
    }

    // 열린 패널 중 hit을 자식으로 가진 것. 앞(리스트 끝)부터 본다.
    private UIPanel FindOpenPanelContaining(Transform hit)
    {
        for (int i = _open.Count - 1; i >= 0; i--)
        {
            if (_open[i] != null && hit.IsChildOf(_open[i].transform)) return _open[i];
        }
        return null;
    }

    // ESC는 우리 창에 포커스가 있을 때만 — 다른 앱에서 누른 ESC를 가로채지 않게.
    private void CloseTopOnEscape()
    {
        if (!Application.isFocused) return;

        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
        {
            Close(_open[_open.Count - 1]); // 최상단 패널 하나만 닫는다
        }
    }
}
