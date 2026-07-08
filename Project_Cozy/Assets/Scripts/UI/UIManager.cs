using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 씬에 하나 존재하는 UI 매니저. 패널 열기/닫기를 관리한다.
/// 여러 패널이 동시에 열릴 수 있다 — 열린 패널을 리스트로 들고, 리스트 끝이
/// 가장 앞(위)에 그려지는 최상단 패널이다. 패널을 열거나 다시 누르면 맨 앞으로 온다.
/// ESC 닫기는 우리 창이 포커스일 때만 — 데스크톱에서 다른 앱의 ESC를 가로채지 않게.
/// </summary>
public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    // 동시에 열려 있는 패널들. 리스트 끝(마지막)이 최상단 — 가장 앞에 그려지고 ESC로 닫히는 대상.
    private readonly List<UIPanel> _open = new();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>패널을 연다. 다른 패널은 닫지 않는다. 이미 열려 있으면 맨 앞으로 가져온다.</summary>
    public void Open(UIPanel panel)
    {
        if (panel == null) return;
        bool wasOpen = _open.Remove(panel); // 열려 있었으면 순서만 뺐다가
        _open.Add(panel);                   // 리스트 끝(최상단)으로 다시 넣는다
        if (!wasOpen) panel.Open();
        panel.transform.SetAsLastSibling(); // 형제 순서 = 그리기 순서. 맨 뒤 형제 = 맨 앞에 그려짐.
    }

    /// <summary>패널을 닫는다.</summary>
    public void Close(UIPanel panel)
    {
        if (panel == null) return;
        if (_open.Remove(panel)) panel.Close();
    }

    /// <summary>열려 있으면 닫고, 닫혀 있으면 연다. (메뉴 버튼용)</summary>
    public void Toggle(UIPanel panel)
    {
        if (panel == null) return;
        if (_open.Contains(panel)) Close(panel);
        else Open(panel);
    }

    private void Update()
    {
        if (_open.Count == 0 || !Application.isFocused) return;

        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
        {
            Close(_open[_open.Count - 1]); // 최상단 패널 하나만 닫는다
        }
    }
}
