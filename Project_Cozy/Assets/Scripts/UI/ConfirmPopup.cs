using System;
using TMPro;
using UnityEngine;

/// <summary>
/// 확인/취소 두 갈래로 묻는 공용 팝업. 그래픽 미저장·초기화, 소녀 변신 금지 등이 같이 쓴다.
/// 대답이 필요 없는 안내(<see cref="ShowNotice"/>)도 같은 팝업으로 띄운다 — [취소]를 숨기고 [확인]만 남긴다.
/// 씬에 하나만 두고 <see cref="Instance"/>로 찾는다 — 패널 프리팹은 씬 오브젝트를 참조할 수 없어서다.
///
/// 패널 루트(<see cref="UIPanel"/>, Is Modal 켬)에 같이 붙는다. 팝업 안의 [확인]·[취소] 버튼 OnClick은 인스펙터로
/// <see cref="Confirm"/>/<see cref="Cancel"/>에 건다. 다만 "대답하면 무엇을 할지"는 부른 쪽마다 달라서 <see cref="Show"/>로
/// 코드가 넘긴다 — 인스펙터 배선 규칙의 예외이고, 이유는 .claude/rules/unity/ui-panels.md.
///
/// 대답 없이 닫히면(ESC, 헤더의 X) 취소로 친다. 그래야 질문(<see cref="Show"/>)이면 부른 쪽이 늘 둘 중 하나의 대답을 받는다.
/// </summary>
[RequireComponent(typeof(UIPanel))]
[DisallowMultipleComponent]
public sealed class ConfirmPopup : MonoBehaviour
{
    public static ConfirmPopup Instance { get; private set; }

    [SerializeField, Tooltip("띄울 문구를 채울 글자. 문구는 Show·ShowNotice에 넘긴 번역 키로 채운다.")]
    private TMP_Text _message;

    [SerializeField, Tooltip("안내(ShowNotice)일 때 숨길 [취소] 버튼.")]
    private GameObject _cancelButton;

    private UIPanel _panel;
    private Action _onConfirm;
    private Action _onCancel;
    private bool _waiting;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        _panel = GetComponent<UIPanel>();
        _panel.Closed += OnPanelClosed;
    }

    private void OnDestroy()
    {
        if (_panel != null) _panel.Closed -= OnPanelClosed;
        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// 질문을 띄운다. <paramref name="messageId"/>는 번역 표의 키다.
    /// 이미 다른 질문이나 안내가 떠 있으면 그것은 취소로 끝내고 새 질문으로 바꾼다.
    /// </summary>
    public void Show(string messageId, Action onConfirm, Action onCancel = null)
    {
        if (_waiting) Answer(false);

        // ShowNotice가 숨긴 [취소]를 질문에서 되살린다. 앞 질문의 취소 콜백이 다시 팝업을 띄울 수 있어 Answer 뒤에 둔다.
        if (_cancelButton != null) _cancelButton.SetActive(true);

        _onConfirm = onConfirm;
        _onCancel = onCancel;
        _waiting = true;
        if (_message != null) LocalizationManager.SetText(_message, messageId);
        UIManager.Instance?.Open(_panel);

        // 패널을 잠시 숨기는 중(장식 설치 모드 등)이면 열리지 않는다. 대답을 영영 못 받게 두지 않고 취소로 돌려준다.
        if (!_panel.IsOpen) Answer(false);
    }

    /// <summary>
    /// 대답이 필요 없는 안내를 띄운다. [취소]를 숨기고 [확인]만 남긴다 — 버튼 배치는 프리팹의 레이아웃 그룹이 맞춘다.
    /// [확인]·ESC·X 어느 쪽으로 닫혀도 할 일이 없다.
    /// </summary>
    public void ShowNotice(string messageId)
    {
        Show(messageId, null);
        if (_cancelButton != null) _cancelButton.SetActive(false);
    }

    // [확인]·[취소] 버튼의 OnClick()에 인스펙터로 거는 진입점.
    public void Confirm() => Answer(true);
    public void Cancel() => Answer(false);

    // 대답 없이 닫혔다(ESC, X). 버튼으로 대답해 닫힌 경우는 이미 _waiting이 꺼져 있다.
    private void OnPanelClosed()
    {
        if (_waiting) Answer(false);
    }

    /// <summary>팝업을 먼저 닫고 나서 콜백을 부른다. 콜백이 다른 패널을 닫거나 열 때 모달이 이미 사라져 있어야 해서다.</summary>
    private void Answer(bool confirmed)
    {
        if (!_waiting) return;
        _waiting = false;
        Action callback = confirmed ? _onConfirm : _onCancel;
        _onConfirm = null;
        _onCancel = null;
        UIManager.Instance?.Close(_panel);
        callback?.Invoke();
    }

#if UNITY_EDITOR
    // 실제로 팝업을 띄우는 화면이 생기기 전에 모달 동작(뒤 패널 잠김, ESC = 취소)을 확인하는 길이다.
    // Play 중에 컴포넌트를 우클릭해 쓴다. 문구는 이미 있는 아무 키를 빌려 쓴다.
    [ContextMenu("테스트: 팝업 띄우기")]
    private void EditorShowTest()
    {
        if (!Application.isPlaying) return;
        Show("UISettings.save_notice",
            () => Debug.Log($"[{nameof(ConfirmPopup)}] 테스트 — 확인"),
            () => Debug.Log($"[{nameof(ConfirmPopup)}] 테스트 — 취소"));
    }
#endif
}
