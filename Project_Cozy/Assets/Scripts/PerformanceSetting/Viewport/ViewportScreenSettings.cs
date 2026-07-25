using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// "화면 설정"(User_Settings.md §2.1.1)의 정책 레이어 — 평시/편집 절충안.
///
/// 평시(Normal):   OS 창 자체를 뷰포트 rect로 배치 → 뷰포트 밖 픽셀은 렌더링·DWM 합성 비용 0.
/// 편집(Editing):  창을 모니터 전체로 확장하고 카메라는 베이스 공간 전체를 프레이밍.
///                 조정값은 프리뷰일 뿐이며(PreviewChanged로 UI가 경계·딤·회수 예정 시각화),
///                 SaveEdit()로만 확정, CancelEdit()는 폐기(§2.1.1 "저장하지 않고 나가면 폐기").
/// 저장/취소:      확정(또는 기존) 뷰포트로 창을 재계산·재적용. 배치가 결정론적이라
///                 "이전 상태 백업"이 필요 없다.
///
/// Win32를 모른다 — 창 배치·클릭 통과는 WindowManager에 위임(HWND 접점은 그쪽 한 곳).
/// 영속화도 모른다 — 확정 뷰포트는 ViewportSaved 구독 측(SaveSystem)이 저장하고,
/// 로드 시 SetViewport()로 주입한다(베이스 공간 밖 값은 자동 클램프).
/// </summary>
[DisallowMultipleComponent]
public class ViewportScreenSettings : MonoBehaviour
{
    /// <summary>§2.1.1 제약 — 뷰포트 최소 크기(절대 픽셀).</summary>
    public static readonly Vector2Int MinViewportSize = new Vector2Int(720, 480);

    [Header("협력자")]
    [SerializeField] private WindowManager _windowManager;
    [SerializeField] private BaseSpaceCameraFitter _cameraFitter;

    [Header("뷰포트 (베이스 공간 px, 원점=좌하단)")]
    [SerializeField, Tooltip("시작 뷰포트. 크기가 0이면 베이스 공간 전체(기본값)로 시작")]
    private RectInt _viewport = new RectInt(0, 0, 0, 0);

    private RectInt _previewViewport;
    private RectInt _monitorRect;              // 스크린 좌표(Y 아래 방향) — 창 배치 계산용
    private Vector2Int _baseSpaceSize;         // = 현재 모니터 해상도
    private bool _isEditing;
    private bool _ready;                       // 초기 적용 완료 전 API 호출 가드

    /// <summary>확정된 뷰포트(베이스 공간 px, 원점=좌하단).</summary>
    public RectInt Viewport => _viewport;

    /// <summary>편집 중 프리뷰 뷰포트. 편집 중이 아니면 Viewport와 동일.</summary>
    public RectInt PreviewViewport => _isEditing ? _previewViewport : _viewport;

    public bool IsEditing => _isEditing;

    /// <summary>초기 적용 완료 여부. false 동안 EnterEdit/SetViewport는 거부된다 — UI는 이걸로 버튼을 잠글 것.</summary>
    public bool IsReady => _ready;

    /// <summary>베이스 공간 크기(px) = 현재 모니터 해상도.</summary>
    public Vector2Int BaseSpaceSize => _baseSpaceSize;

    /// <summary>편집 중 프리뷰 변경 — UI가 경계 핸들·바깥 딤·회수 예정 표시를 갱신하는 지점.</summary>
    public event Action<RectInt> PreviewChanged;

    /// <summary>저장 확정 — SaveSystem이 구독해 영속화하는 지점.</summary>
    public event Action<RectInt> ViewportSaved;

    /// <summary>편집 모드 진입(true)/이탈(false) — 편집 UI 표시 토글 지점.</summary>
    public event Action<bool> EditModeChanged;

    /// <summary>확정 뷰포트가 실제로 화면에 적용된 직후 — 초기 적용, SetViewport, 저장/취소 복귀,
    /// OS 창 드래그 역동기화 전부 포함. 뷰포트 밖 캐릭터 회수(ViewportResidencyEnforcer) 등이 구독.</summary>
    public event Action<RectInt> ViewportApplied;

    private IEnumerator Start()
    {
        // 인스펙터 미할당 배선 실수가 EnterEdit/ApplyNormal 전체를 죽이지 않게 자동 탐색으로 보강.
        if (_windowManager == null) _windowManager = FindFirstObjectByType<WindowManager>();
        if (_cameraFitter == null)  _cameraFitter  = FindFirstObjectByType<BaseSpaceCameraFitter>();
        if (_cameraFitter == null)
            Debug.LogError("[ViewportScreenSettings] BaseSpaceCameraFitter 없음 — 카메라 프레이밍 불가. " +
                           "메인 카메라에 BaseSpaceCameraFitter를 붙여주세요.");

        // WindowManager가 창 스타일·표시를 잡은 뒤에 모니터를 읽어야 안정적
        // (WindowManager.ApplyMaximizeAfterReady와 같은 이유의 지연).
        for (int i = 0; i < 10; i++) yield return null;

        RefreshBaseSpace();

        // 크기 0 = "베이스 공간 전체" 기본값 (§2.1.1 뷰포트 기본값)
        if (_viewport.width <= 0 || _viewport.height <= 0)
            _viewport = new RectInt(0, 0, _baseSpaceSize.x, _baseSpaceSize.y);

        _viewport = ClampToBaseSpace(_viewport);
        _ready = true;
        ApplyNormal();

        // 사용자가 캡션 드래그(이동)나 가장자리 드래그(리사이즈)로 창을 직접 바꾸면
        // 창 rect가 곧 새 뷰포트다 — 역동기화해서 카메라도 그 영역을 비추게 한다.
        if (_windowManager != null)
            _windowManager.WindowRectChangedByUser += OnWindowRectChangedByUser;
    }

    private void OnDestroy()
    {
        if (_windowManager != null)
            _windowManager.WindowRectChangedByUser -= OnWindowRectChangedByUser;
    }

    private void OnWindowRectChangedByUser()
    {
        if (!_ready || _isEditing) return;
        if (!_windowManager.TryGetWindowRect(out RectInt win)) return;

        // 창이 다른 모니터로 드래그됐을 수 있음 — stale 모니터 기준으로 계산하면
        // 옛 모니터로 스냅백하는 버그가 되므로 반드시 먼저 갱신 (리뷰 합의).
        RefreshBaseSpace();

        // 스크린 좌표(모니터 좌상단 원점, Y 아래) → 베이스 공간 px(좌하단 원점, Y 위)
        RectInt v = new RectInt(
            win.x - _monitorRect.x,
            _baseSpaceSize.y - (win.y - _monitorRect.y) - win.height,
            win.width, win.height);

        _viewport = ClampToBaseSpace(v);
        // 창은 사용자가 이미 놓은 자리 그대로 두고(클램프로 달라졌을 때만 재배치), 카메라만 따라간다.
        if (_viewport != v) ApplyNormal();
        else
        {
            if (_cameraFitter != null) _cameraFitter.Frame(_viewport, _baseSpaceSize);
            ViewportApplied?.Invoke(_viewport);
        }
        ViewportSaved?.Invoke(_viewport); // 사용자가 직접 확정한 배치 — 영속화 대상
    }

    // ===== 외부 API =====

    /// <summary>확정 뷰포트를 직접 설정(로드 경로). 베이스 공간 밖 값은 클램프.
    /// 편집 중이면 확정 값만 갱신하고 화면 적용은 편집을 벗어날 때까지 미룬다.</summary>
    public void SetViewport(RectInt viewport)
    {
        // ready 전엔 베이스 공간 크기를 아직 모르므로 클램프할 수 없다 — Start가 클램프·적용을 맡는다.
        if (!_ready) { _viewport = viewport; return; }

        _viewport = ClampToBaseSpace(viewport);

        // 편집 중에는 진행 중인 프리뷰를 건드리지 않는다. 저장/취소로 빠져나올 때
        // ExitEdit → ApplyNormal이 이 확정 값을 화면에 반영한다.
        if (_isEditing) return;

        ApplyNormal();
    }

    /// <summary>화면 설정 진입 — 창을 모니터 전체로 확장, 카메라는 베이스 공간 전체 프레이밍.</summary>
    public void EnterEdit()
    {
        if (_isEditing) return;
        if (!_ready)
        {
            // 무음 무시 금지 (리뷰 합의: 큐잉보다 로그+거부 — 10프레임 뒤 갑자기 전체화면 전환되는 UX가 더 나쁨)
            Debug.LogWarning("[ViewportScreenSettings] 초기화 전 EnterEdit 호출 — 무시. IsReady로 버튼을 잠그세요.");
            return;
        }
        _isEditing = true;
        _previewViewport = _viewport;

        RefreshBaseSpace();
        if (_windowManager != null)
        {
            // suspend를 풀스크린 확장보다 먼저 — 확장 직후 큐잉된 클릭이 통과 상태로 새는 창을 차단 (리뷰 합의)
            _windowManager.SetClickThroughSuspended(true); // 빈 공간에서도 핸들 드래그가 잡히게
            _windowManager.SetResizeSuspended(true);       // OS 가장자리 리사이즈는 편집 중 무의미 — 핸들 UI와 충돌 방지
            _windowManager.ApplyMonitorFullscreen();
        }
        if (_cameraFitter != null) _cameraFitter.Frame(new RectInt(0, 0, _baseSpaceSize.x, _baseSpaceSize.y), _baseSpaceSize);

        EditModeChanged?.Invoke(true);
        PreviewChanged?.Invoke(_previewViewport);
    }

    /// <summary>편집 중 프리뷰 갱신(핸들 드래그·슬라이더). 창·카메라는 그대로, 이벤트만 발행.</summary>
    public void SetPreviewViewport(RectInt viewport)
    {
        if (!_isEditing) return;
        _previewViewport = ClampToBaseSpace(viewport);
        PreviewChanged?.Invoke(_previewViewport);
    }

    /// <summary>"화면 설정 저장" — 프리뷰를 확정하고 평시 상태로 복귀. §2.1.1의 유일한 확정 경로.</summary>
    public void SaveEdit()
    {
        if (!_isEditing) return;
        _viewport = _previewViewport;
        ViewportSaved?.Invoke(_viewport);
        ExitEdit();
    }

    /// <summary>저장 없이 이탈 — 프리뷰 폐기, 기존 뷰포트로 복귀.</summary>
    public void CancelEdit()
    {
        if (!_isEditing) return;
        ExitEdit();
    }

    // ===== 내부 =====

    private void ExitEdit()
    {
        _isEditing = false;
        if (_windowManager != null)
        {
            _windowManager.SetClickThroughSuspended(false);
            _windowManager.SetResizeSuspended(false);
        }
        ApplyNormal();
        EditModeChanged?.Invoke(false);
    }

    /// <summary>평시 상태 적용: 창 = 뷰포트 rect, 카메라 = 뷰포트 영역 프레이밍.</summary>
    private void ApplyNormal()
    {
        // 베이스 공간 px(좌하단 원점, Y 위) → 스크린 좌표(모니터 좌상단 원점, Y 아래)
        int x = _monitorRect.x + _viewport.x;
        int y = _monitorRect.y + (_baseSpaceSize.y - _viewport.y - _viewport.height);

        if (_windowManager != null) _windowManager.ApplyRegion(x, y, _viewport.width, _viewport.height);
        if (_cameraFitter != null) _cameraFitter.Frame(_viewport, _baseSpaceSize);
        ViewportApplied?.Invoke(_viewport);
    }

    private void RefreshBaseSpace()
    {
        if (_windowManager != null && _windowManager.TryGetMonitorRect(out RectInt monitor))
        {
            _monitorRect = monitor;
        }
        else
        {
            // Editor 등 Win32 불가 환경 — 현재 화면 크기를 베이스 공간으로 간주(카메라 프레이밍은 검증 가능)
            _monitorRect = new RectInt(0, 0, Mathf.Max(Screen.width, 1), Mathf.Max(Screen.height, 1));
        }
        _baseSpaceSize = new Vector2Int(_monitorRect.width, _monitorRect.height);
    }

    private RectInt ClampToBaseSpace(RectInt r)
    {
        r.width  = Mathf.Clamp(r.width,  MinViewportSize.x, _baseSpaceSize.x);
        r.height = Mathf.Clamp(r.height, MinViewportSize.y, _baseSpaceSize.y);
        r.x = Mathf.Clamp(r.x, 0, _baseSpaceSize.x - r.width);
        r.y = Mathf.Clamp(r.y, 0, _baseSpaceSize.y - r.height);
        return r;
    }
}
