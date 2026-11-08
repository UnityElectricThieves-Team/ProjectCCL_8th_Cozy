using System;
using TMPro;
using UnityEngine;

/// <summary>
/// 몇 개를 할지 숫자로 묻는 공용 팝업. 지금은 인벤토리 장식 제거("몇개를 삭제할까요?", Figma 인벤토리 장식 삭제 시도)만 쓴다.
/// 씬에 하나만 두고 <see cref="Instance"/>로 찾는다. 패널 루트(<see cref="UIPanel"/>, Is Modal 켬, "장식 이동·회수·제거 중에도 보임" 켬)에 같이 붙는다.
///
/// ◀▶ 줄은 설정의 <see cref="SettingsStepper"/>를 그대로 쓴다. 단계 수 = 최대 개수, 단계 번호 = 고른 개수 − 1이다.
/// 스테퍼가 끝 단계에서 버튼을 숨기므로 "1일 때 왼쪽 없음, max일 때 오른쪽 없음"(Figma 메모)이 저절로 맞는다. 시작 값은 1이다.
///
/// 대답 규칙은 <see cref="ConfirmPopup"/>과 같다 — 대답 없이 닫히면(ESC, X) 취소, 팝업을 먼저 닫고 콜백, 열리지 못하면 곧바로 취소.
/// [확인]·[취소]·◀▶ 버튼 OnClick과 스테퍼의 값 변경 이벤트는 인스펙터로 건다. 대답하면 할 일만 부른 쪽이 코드로 넘긴다
/// (인스펙터 배선 규칙의 예외, .claude/rules/unity/ui-panels.md 확인 팝업 절).
/// </summary>
[RequireComponent(typeof(UIPanel))]
[DisallowMultipleComponent]
public sealed class CountPopup : MonoBehaviour
{
    public static CountPopup Instance { get; private set; }

    [SerializeField, Tooltip("질문 문구를 채울 글자. 문구는 Show에 넘긴 번역 키로 채운다.")]
    private TMP_Text _message;

    [SerializeField, Tooltip("◀▶ 줄. 단계 번호 = 고른 개수 − 1.")]
    private SettingsStepper _stepper;

    [SerializeField, Tooltip("고른 개수를 적을 글자. 숫자는 번역하지 않는다.")]
    private TMP_Text _countText;

    private UIPanel _panel;
    private Action<int> _onConfirm;
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
    /// 1~<paramref name="max"/> 중 몇 개인지 묻는다. <paramref name="messageId"/>는 번역 표의 키다.
    /// 이미 다른 질문이 떠 있으면 그것은 취소로 끝내고 새 질문으로 바꾼다.
    /// </summary>
    public void Show(string messageId, int max, Action<int> onConfirm, Action onCancel = null)
    {
        if (_waiting) Answer(false);

        _onConfirm = onConfirm;
        _onCancel = onCancel;
        _waiting = true;
        if (_message != null) LocalizationManager.SetText(_message, messageId);

        // Setup은 값 변경 이벤트를 울리지 않으므로 시작 숫자는 여기서 직접 넣는다.
        if (_stepper != null) _stepper.Setup(Mathf.Max(1, max), 0);
        ShowCount(0);

        UIManager.Instance?.Open(_panel);

        // 패널을 숨기는 중(설치 모드·뷰포트 편집)이면 열리지 않는다. 대답을 영영 못 받게 두지 않고 취소로 돌려준다.
        if (!_panel.IsOpen) Answer(false);
    }

    /// <summary>스테퍼의 값 변경 이벤트에 인스펙터로 건다. 단계 번호를 받아 개수를 적는다.</summary>
    public void ShowCount(int stepIndex)
    {
        if (_countText != null) _countText.text = (stepIndex + 1).ToString();
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

        int count = _stepper != null ? _stepper.Index + 1 : 1;
        Action<int> confirm = _onConfirm;
        Action cancel = _onCancel;
        _onConfirm = null;
        _onCancel = null;

        UIManager.Instance?.Close(_panel);
        if (confirmed) confirm?.Invoke(count);
        else cancel?.Invoke();
    }
}
