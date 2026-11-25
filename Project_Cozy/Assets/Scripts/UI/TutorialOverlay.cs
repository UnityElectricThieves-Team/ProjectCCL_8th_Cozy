using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 튜토리얼의 화면 UI — 대사창(초상화 + 대사 글자), "화면 어디든 클릭"을 받는 전면 투명 버튼, 메뉴 바를 덮는 딤.
/// <see cref="TutorialDirector"/>가 아는 UI는 이것 하나다. 진행 판단은 지휘자가 하고, 여기는 보여 달라는 것을 그린다.
/// 월드 쪽 안내 그림(<see cref="TutorialClickHint"/>)과 월드 딤은 여기 없다.
///
/// - <see cref="UIPanel"/>이 아니다. 패널 스택·ESC와 무관하고, 모드 동안 패널을 숨기는 대상도 아니다(ViewportEditOverlay와 같은 판단).
///   UI 크기 설정도 따르지 않는다.
/// - 전체 표시는 루트 CanvasGroup의 알파와 blocksRaycasts를 함께 바꾼다. 프리팹에는 알파 0으로 저장하고 Awake에서 숨긴다 —
///   튜토리얼이 아닐 때 레이캐스트에 걸리면 창이 바탕화면 클릭을 먹고 캐릭터·구름도 반응하지 않는다.
/// - 전면 버튼은 대사 장면(S1·S3·S5)에만 켠다. 켜 둔 채면 커서가 늘 UI 위로 판정돼 구름·치즈가 눌리지 않는다.
///   OnClick은 인스펙터로 <see cref="OnScreenClicked"/>에 건다. Button은 뗄 때 울리므로, 장면이 바뀌며 잠금이 풀리는 프레임에
///   그 클릭이 뒤의 구름·치즈로 새지 않는다.
/// - 메뉴 바 딤은 이 프리팹 안에 두고 Awake에서 메뉴 바의 마지막 자식으로 옮겨 붙인다. 메뉴 바의 자식이어야 UI 크기 배율을
///   따라 늘 정확히 덮는데, 메뉴 바는 씬에 직접 만든 오브젝트라 씬에서 자식을 더하면 씬 오브젝트가 는다.
///   옮긴 뒤에는 프리팹의 앵커·offset이 그대로 메뉴 바 기준이 된다(값은 계산하지 않는다).
/// - 대사창의 Raycast Target은 프리팹에서 끈다 — 구름·치즈와 겹칠 때 그 자리 클릭을 먹지 않게.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CanvasGroup))]
public sealed class TutorialOverlay : MonoBehaviour
{
    [Tooltip("화면 전체를 덮는 투명 버튼. 프리팹에는 꺼 둔다. OnClick → OnScreenClicked.")]
    [SerializeField] private GameObject _clickCatcher;
    [Tooltip("대사 글자.")]
    [SerializeField] private TMP_Text _lineText;
    [Tooltip("초상화.")]
    [SerializeField] private Image _portrait;
    [SerializeField] private Sprite _shockFace;
    [SerializeField] private Sprite _happyFace;
    [Tooltip("메뉴 바를 덮는 딤. Awake에서 메뉴 바 아래로 옮겨 붙는다. 프리팹에는 꺼 둔다.")]
    [SerializeField] private GameObject _menuBarDim;
    [Tooltip("씬의 메뉴 바. 씬 인스턴스에서 연결한다.")]
    [SerializeField] private RectTransform _menuBar;

    /// <summary>대사 장면에서 화면 어디든 눌렀다 뗐을 때 울린다.</summary>
    public event Action ScreenClicked;

    private CanvasGroup _group;
    private LocalizationManager _localization;
    private string _lineId;

    /// <summary>튜토리얼에 필요한 배선이 모두 연결됐는가. 지휘자가 시작 전에 확인한다.</summary>
    public bool IsWired => _clickCatcher != null && _lineText != null && _menuBarDim != null && _menuBar != null;

    private void Awake()
    {
        _group = GetComponent<CanvasGroup>();
        if (_menuBarDim != null)
        {
            if (_menuBar != null)
            {
                _menuBarDim.transform.SetParent(_menuBar, false);
                _menuBarDim.transform.SetAsLastSibling();
            }
            _menuBarDim.SetActive(false);
        }
        if (_clickCatcher != null) _clickCatcher.SetActive(false);
        Hide();
    }

    private void Start()
    {
        _localization = LocalizationManager.Instance;
        if (_localization != null) _localization.LanguageChanged += RedrawLine;
    }

    private void OnDisable()
    {
        // 딤은 메뉴 바 아래에 살아서 이 오브젝트가 꺼져도 남는다.
        SetMenuBarDim(false);
    }

    private void OnDestroy()
    {
        if (_localization != null) _localization.LanguageChanged -= RedrawLine;
    }

    public void Show() => SetVisible(true);

    public void Hide()
    {
        SetVisible(false);
        SetClickCatcher(false);
        SetMenuBarDim(false);
    }

    /// <summary>화면 전체를 덮는 클릭 판을 켜고 끈다. 대사 장면에만 켠다.</summary>
    public void SetClickCatcher(bool on)
    {
        if (_clickCatcher != null) _clickCatcher.SetActive(on);
    }

    /// <summary>메뉴 바를 덮는 딤을 켜고 끈다. 메뉴 버튼 클릭도 막는다.</summary>
    public void SetMenuBarDim(bool on)
    {
        if (_menuBarDim != null) _menuBarDim.SetActive(on);
    }

    /// <summary>대사 한 줄과 표정을 보인다. 언어가 바뀌면 같은 줄을 다시 채운다.</summary>
    public void ShowLine(string stringId, TutorialFace face)
    {
        _lineId = stringId;
        RedrawLine();
        if (_portrait != null)
        {
            var sprite = face == TutorialFace.Happy ? _happyFace : _shockFace;
            if (sprite != null) _portrait.sprite = sprite;
        }
    }

    /// <summary>전면 클릭 버튼의 OnClick. 인스펙터에서 건다.</summary>
    public void OnScreenClicked() => ScreenClicked?.Invoke();

    private void RedrawLine()
    {
        if (_lineText == null || string.IsNullOrEmpty(_lineId)) return;
        LocalizationManager.SetText(_lineText, _lineId);
    }

    private void SetVisible(bool visible)
    {
        if (_group == null) return;
        _group.alpha = visible ? 1f : 0f;
        _group.interactable = visible;
        _group.blocksRaycasts = visible;
    }
}
