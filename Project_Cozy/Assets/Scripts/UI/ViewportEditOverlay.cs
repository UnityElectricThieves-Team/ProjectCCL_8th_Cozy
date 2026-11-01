using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 뷰포트 편집 화면의 안내 문구와 [완료] 버튼. 설정 그래픽 탭의 화면 크기 [조정]으로 들어온 편집을 끝내는 길이다.
/// 핸들과 딤은 <see cref="ViewportEditHandles"/>가 IMGUI로 그리고, 번역해야 하는 글자와 버튼만 여기 UGUI로 둔다.
///
/// [완료]와 ESC는 편집을 끝내되 확정하지 않는다(<see cref="ViewportScreenSettings.EndEditKeepPreview"/>).
/// 확정은 그래픽 탭의 [저장]이 한다. 편집 동안 열린 패널은 UIManager가 숨기므로, 이 오버레이는 패널(UIPanel)이 아니다 —
/// 패널이면 자기도 숨는다.
///
/// <see cref="ViewportScreenSettings.EditModeChanged"/>를 듣고 켜고 끈다 — 편집 쪽이 이 컴포넌트를 부르지 않는다.
/// 글자는 프리팹 안의 <see cref="LocalizedText"/>가 채운다(<c>UISettings.viewport_edit.*</c>).
/// 버튼 OnClick은 인스펙터로 <see cref="Done"/>에 건다.
/// 범용 UI 위젯 폴더(Prefabs/UIPanels/UIObjects)의 프리팹 루트에 붙는다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CanvasGroup))]
public sealed class ViewportEditOverlay : MonoBehaviour
{
    private CanvasGroup _group;
    private ViewportScreenSettings _viewport;
    private bool _isShown;

    private void Awake()
    {
        _group = GetComponent<CanvasGroup>();
        Show(false);
    }

    private void Start()
    {
        // 프리팹은 씬 오브젝트를 참조할 수 없어 한 번 찾아 둔다.
        _viewport = FindFirstObjectByType<ViewportScreenSettings>();
        if (_viewport == null)
        {
            Debug.LogWarning($"[{nameof(ViewportEditOverlay)}] 씬에 {nameof(ViewportScreenSettings)}가 없어 편집 화면을 띄우지 않습니다.", this);
            enabled = false;
            return;
        }
        _viewport.EditModeChanged += Show;
        if (_viewport.IsEditing) Show(true);
    }

    private void OnDestroy()
    {
        if (_viewport != null) _viewport.EditModeChanged -= Show;
    }

    // [완료] 버튼.
    public void Done()
    {
        if (_viewport != null) _viewport.EndEditKeepPreview();
    }

    private void Show(bool show)
    {
        _isShown = show;
        _group.alpha = show ? 1f : 0f;
        _group.interactable = show;
        _group.blocksRaycasts = show;
    }

    private void Update()
    {
        // ESC는 뗄 때 받는다. UIManager는 누른 순간에 패널을 닫으므로, 누를 때는 아직 편집 중(패널 숨김)이라
        // 같은 ESC로 되살아난 설정 패널까지 닫히는 일이 없다. 다른 앱에서 누른 ESC는 가로채지 않는다.
        if (!_isShown || !Application.isFocused) return;
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasReleasedThisFrame) Done();
    }
}
