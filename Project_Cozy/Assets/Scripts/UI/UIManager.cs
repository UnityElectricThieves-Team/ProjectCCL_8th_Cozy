using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 씬에 하나 존재하는 UI 매니저. 패널 열기/닫기를 관리한다.
/// 여러 패널이 동시에 열릴 수 있다 — 열린 패널을 리스트로 들고, 리스트 끝이
/// 가장 앞(위)에 그려지는 최상단 패널이다. 패널을 열거나 다시 누르면 맨 앞으로 온다.
/// ESC 닫기는 우리 창이 포커스일 때만 — 데스크톱에서 다른 앱의 ESC를 가로채지 않게.
///
/// 장식 설치 모드 동안에는 열린 패널을 닫지 않고 잠시 숨긴다(<see cref="UIPanel.SetSuspended"/>).
/// 설치를 마치거나 취소하면 그대로 되살아나서 상점에서 이어 살 수 있다. 숨긴 동안에는 ESC로 패널을 닫지 않고,
/// 메뉴 버튼의 열기·토글도 무시한다 — 그 ESC와 클릭은 설치 모드의 것이다.
/// 모드가 이 매니저를 부르는 것이 아니라 이 매니저가 모드의 시작·종료 신호를 듣는다. 그래야 모드가 어떻게 끝나든
/// (강제 종료 포함) 패널이 되살아난다.
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

    private DecorationPlacementController _placement;
    private bool _isSuspended;

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
        // 설치 모드 컨트롤러는 Awake에서 Instance를 세우므로 이 시점엔 있다. 없는 씬이면 숨김 기능만 없는 것이다.
        _placement = DecorationPlacementController.Instance;
        if (_placement != null) _placement.ActiveChanged += OnPlacementActiveChanged;
    }

    private void OnDestroy()
    {
        if (_placement != null) _placement.ActiveChanged -= OnPlacementActiveChanged;
        if (Instance == this) Instance = null;
    }

    /// <summary>패널을 연다. 다른 패널은 닫지 않는다. 이미 열려 있으면 맨 앞으로 가져온다. 설치 모드 중에는 무시한다.</summary>
    public void Open(UIPanel panel)
    {
        if (panel == null || _isSuspended || IsBlockedByModal(panel)) return;
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

    /// <summary>열려 있으면 닫고, 닫혀 있으면 연다. (메뉴 버튼용) 설치 모드 중과 모달이 떠 있는 동안에는 무시한다.</summary>
    public void Toggle(UIPanel panel)
    {
        if (panel == null || _isSuspended || IsBlockedByModal(panel)) return;
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

    private void OnPlacementActiveChanged(bool active)
    {
        _isSuspended = active;
        for (int i = 0; i < _open.Count; i++)
        {
            // 플레이 종료 중에는 패널이 먼저 파괴됐을 수 있다.
            if (_open[i] != null) _open[i].SetSuspended(active);
        }
    }

    private void Update()
    {
        if (_isSuspended || _open.Count == 0 || !Application.isFocused) return;

        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
        {
            Close(_open[_open.Count - 1]); // 최상단 패널 하나만 닫는다
        }
    }
}
