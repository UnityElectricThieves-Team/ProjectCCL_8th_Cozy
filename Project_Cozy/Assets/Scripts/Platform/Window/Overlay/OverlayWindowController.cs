// ============================================================
// OverlayWindowController
//
// "무엇을 원하는가" 정책 레이어. WindowMode를 WindowState로 매핑해
// OverlayWindow에 전달하고, 전체화면/영역 사이징과 리사이즈 토글을 조율한다. Win32를 모른다.
//
// 모드:
//   Normal      — 전체화면(또는 사용자가 정한 영역) 투명 오버레이. 캐릭터 클릭 O, 빈 공간 통과.
//   PassThrough — 잠금/방해금지. 전부 클릭 통과.
//   EditRegion  — 화면 영역 수정. 불투명 + 가장자리 리사이즈 + 상단바 이동. (WindowResizeHandler 필요)
//
// 인앱 토글: SetMode / ToggleMode / EnterEditRegion / ExitEditRegion / ToggleEditRegion / SetFullscreen.
//   예) 옵션 UI "화면 영역 수정하기" 버튼 OnClick → ToggleEditRegion()
// ============================================================
using UnityEngine;

[RequireComponent(typeof(OverlayWindow))]
[RequireComponent(typeof(WindowResizeHandler))]  // 리사이즈/이동 게이트. 없으면 EditRegion에서 크기·이동 불가.
[RequireComponent(typeof(RegionEditChrome))]     // 편집 모드 시각 핸들/테두리. 없으면 핸들이 안 보임.
[DisallowMultipleComponent]
public class OverlayWindowController : MonoBehaviour
{
    [Header("베이스 (상수)")]
    [SerializeField, Tooltip("투명 처리할 색. 카메라 BackgroundColor와 반드시 동일하게")]
    private Color _colorKey = Color.black;

    [Header("시작 모드")]
    [SerializeField] private eWindowMode _mode = eWindowMode.Normal;

    [Header("영역 수정")]
    [SerializeField, Tooltip("편집 진입 시 전체화면이면 줄어들 기본 영역 크기(px)")]
    private Vector2Int _editDefaultSize = new Vector2Int(700, 500);

    private OverlayWindow _window;
    private WindowResizeHandler _resize;   // 없으면 EditRegion 리사이즈는 비활성 (state만 적용)
    private bool _useFullscreen = true;    // 현재 영역이 화면 전체인지

    public eWindowMode Mode => _mode;

    private void Awake()
    {
        _window = GetComponent<OverlayWindow>();
        _resize = GetComponent<WindowResizeHandler>();
    }

    private void Start() => ApplyMode(_mode);

    /// <summary>모드 설정. 같은 모드면 무시(차분).</summary>
    public void SetMode(eWindowMode mode)
    {
        if (mode == _mode) return;
        ApplyMode(mode);
    }

    /// <summary>Normal ↔ PassThrough 토글.</summary>
    public void ToggleMode() =>
        SetMode(_mode == eWindowMode.Normal ? eWindowMode.PassThrough : eWindowMode.Normal);

    public void EnterEditRegion() => SetMode(eWindowMode.EditRegion);
    public void ExitEditRegion() => SetMode(eWindowMode.Normal);

    /// <summary>옵션 UI "화면 영역 수정하기" 버튼용 — EditRegion ↔ Normal 토글.</summary>
    public void ToggleEditRegion() =>
        SetMode(_mode == eWindowMode.EditRegion ? eWindowMode.Normal : eWindowMode.EditRegion);

    /// <summary>현재 영역을 화면 전체로 되돌린다.</summary>
    public void SetFullscreen()
    {
        _useFullscreen = true;
        _window.SetFullscreen();
    }

    private void ApplyMode(eWindowMode mode)
    {
        bool edit = mode == eWindowMode.EditRegion;

        // 편집 진입 시 전체화면이면 중앙 편집 영역으로 축소 (가장자리를 잡을 수 있게)
        if (edit && _useFullscreen)
        {
            _window.SetRegionCentered(_editDefaultSize.x, _editDefaultSize.y);
            _useFullscreen = false;
        }

        _mode = mode;
        _window.Apply(BuildState(mode));

        if (_resize != null) _resize.SetEditEnabled(edit);
        else if (edit) Debug.LogWarning("[OverlayWindowController] WindowResizeHandler가 없어 리사이즈/이동이 비활성입니다.");

        // 비편집 모드인데 전체화면 플래그면 화면 전체로 (시작 시 + SetFullscreen 경로)
        if (!edit && _useFullscreen) _window.SetFullscreen();
    }

    private WindowState BuildState(eWindowMode mode) => new WindowState
    {
        Borderless = true,
        ColorKey = _colorKey,
        Transparent = mode != eWindowMode.EditRegion,   // 편집 모드만 불투명(영역을 잡을 수 있게)
        ClickThrough = mode == eWindowMode.PassThrough,  // 잠금/방해금지만 전체 통과
        TopMost = mode != eWindowMode.PassThrough,        // PassThrough만 항상 위 해제
    };
}
