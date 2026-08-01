using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// "화면 설정"(UserSettings.md §2.1.1)의 정책 레이어 — 정적 창 모델.
///
/// 창:          항상 현재 모니터의 **작업 영역**(작업표시줄을 뺀 영역)에 놓인다. 평상시에는 크기도
///              위치도 변하지 않고, ReadjustWindow()를 부를 때만 다시 잡는다.
/// 베이스 공간: = 작업 영역. 카메라는 이 전체를 절대 픽셀 1:1로 비추며, 재조정 때 말고는 건드리지 않는다.
/// 뷰포트:      베이스 공간 안의 논리적 사각형. **렌더링 파라미터가 아니다** — 창을 줄이지도 화면을
///              잘라내지도 않는다. 캐릭터가 살 수 있는 영역이자 지면·회수 판정의 기준이다.
/// 편집:        조정값은 프리뷰일 뿐이며(PreviewChanged로 UI가 경계·딤·회수 예정 시각화),
///              SaveEdit()로만 확정, CancelEdit()는 폐기(§2.1.1 "저장하지 않고 나가면 폐기").
///
/// 이 모델을 고른 이유는 Docs/Development/WindowViewportUIArchitecture.md.
///
/// Win32를 모른다 — 창 배치·클릭 통과는 WindowManager에 위임(HWND 접점은 그쪽 한 곳).
/// 영속화도 모른다 — 확정 뷰포트는 ViewportSaved 구독 측(ViewportSaveBinder)이 저장하고,
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
    private RectInt _baseSpaceScreenRect;      // 베이스 공간의 원점·크기를 스크린 좌표(Y 아래)로 표현한 것 = 작업 영역
    private Vector2Int _baseSpaceSize;         // = 작업 영역 크기
    private bool _isEditing;
    private bool _ready;                       // 초기 적용 완료 전 API 호출 가드

    /// <summary>확정된 뷰포트(베이스 공간 px, 원점=좌하단).</summary>
    public RectInt Viewport => _viewport;

    /// <summary>편집 중 프리뷰 뷰포트. 편집 중이 아니면 Viewport와 동일.</summary>
    public RectInt PreviewViewport => _isEditing ? _previewViewport : _viewport;

    public bool IsEditing => _isEditing;

    /// <summary>초기 적용 완료 여부. false 동안 EnterEdit/ReadjustWindow는 거부된다 — UI는 이걸로 버튼을 잠글 것.</summary>
    public bool IsReady => _ready;

    /// <summary>베이스 공간 크기(px) = 현재 모니터의 작업 영역 크기.</summary>
    public Vector2Int BaseSpaceSize => _baseSpaceSize;

    /// <summary>편집 중 프리뷰 변경 — UI가 경계 핸들·바깥 딤·회수 예정 표시를 갱신하는 지점.</summary>
    public event Action<RectInt> PreviewChanged;

    /// <summary>저장 확정 — ViewportSaveBinder가 구독해 영속화하는 지점.
    /// 사용자가 편집 모드에서 명시적으로 저장했을 때만 발행한다. 클램프로 값이 줄어든 것은
    /// 사용자의 뜻이 아니므로 발행하지 않는다 — 작은 화면에 한 번 열었다고 설정이 깎이면 안 된다.</summary>
    public event Action<RectInt> ViewportSaved;

    /// <summary>편집 모드 진입(true)/이탈(false) — 편집 UI 표시 토글 지점.</summary>
    public event Action<bool> EditModeChanged;

    /// <summary>확정 뷰포트가 적용된 직후 — 초기 적용, SetViewport, 저장/취소 복귀, 창 재조정 전부 포함.
    /// 뷰포트 밖 캐릭터 회수(ViewportResidencyEnforcer) 등이 구독.</summary>
    public event Action<RectInt> ViewportApplied;

    private IEnumerator Start()
    {
        // 인스펙터 미할당 배선 실수가 초기 적용 전체를 죽이지 않게 자동 탐색으로 보강.
        if (_windowManager == null) _windowManager = FindFirstObjectByType<WindowManager>();
        if (_cameraFitter == null)  _cameraFitter  = FindFirstObjectByType<BaseSpaceCameraFitter>();
        if (_cameraFitter == null)
            Debug.LogError("[ViewportScreenSettings] BaseSpaceCameraFitter 없음 — 카메라 프레이밍 불가. " +
                           "메인 카메라에 BaseSpaceCameraFitter를 붙여주세요.");

        // WindowManager가 창 스타일·표시를 잡은 뒤에 작업 영역을 읽어야 안정적
        // (WindowManager.ApplyMaximizeAfterReady와 같은 이유의 지연).
        for (int i = 0; i < 10; i++) yield return null;

        ApplyScreenLayout();

        // 크기 0 = "베이스 공간 전체" 기본값 (§2.1.1 뷰포트 기본값).
        // 저장된 값이 Awake에 주입돼 있으면(ViewportSaveBinder) 그 값이 살아남는다.
        if (_viewport.width <= 0 || _viewport.height <= 0)
            _viewport = new RectInt(0, 0, _baseSpaceSize.x, _baseSpaceSize.y);

        _viewport = ClampToBaseSpace(_viewport);
        _ready = true;
        PublishViewportApplied();
    }

    private void OnDisable()
    {
        // 편집 중에 이 컴포넌트가 비활성화·파괴되면 클릭 통과가 정지된 채로 남아, 창이 화면 위 모든
        // 클릭을 영구히 흡수한다(사용자에게는 바탕화면이 잠긴 것과 같고 복구 수단은 강제 종료뿐이다).
        // 여기서 무조건 되돌린다. Unity는 파괴 시에도 OnDisable을 먼저 부르므로 이 한 곳으로 두 경우가 덮인다.
        //
        // _isEditing은 건드리지 않는다 — 이벤트 없이 조용히 내리면 편집 UI가 상태를 잘못 알게 된다.
        if (_windowManager == null) return;
        _windowManager.SetClickThroughSuspended(false);
        _windowManager.SetResizeSuspended(false);
    }

    // ===== 외부 API =====

    /// <summary>확정 뷰포트를 직접 설정(로드 경로). 베이스 공간 밖 값은 클램프.
    /// 편집 중이면 확정 값만 갱신하고 반영은 편집을 벗어날 때까지 미룬다.</summary>
    public void SetViewport(RectInt viewport)
    {
        // ready 전엔 베이스 공간 크기를 아직 모르므로 클램프할 수 없다 — Start가 클램프·적용을 맡는다.
        if (!_ready) { _viewport = viewport; return; }

        _viewport = ClampToBaseSpace(viewport);

        // 편집 중에는 진행 중인 프리뷰를 건드리지 않는다. 저장/취소로 빠져나올 때
        // ExitEdit이 이 확정 값을 반영한다.
        if (_isEditing) return;

        PublishViewportApplied();
    }

    /// <summary>
    /// "윈도우 크기 재조정" — 작업 영역을 다시 읽어 창과 카메라를 맞춘다.
    /// 모니터 해상도가 바뀌었거나 작업표시줄을 옮겼을 때 사용자가 직접 부르는 유일한 경로다.
    /// 평상시에는 창이 저절로 바뀌지 않는다.
    ///
    /// 창 rect는 저장하지 않는다 — 작업 영역에서 언제든 다시 유도할 수 있는 값이라 저장하면
    /// 원본과 어긋날 위험만 생긴다. 부팅 때마다 Start가 같은 계산을 한다.
    /// </summary>
    public void ReadjustWindow()
    {
        if (!_ready)
        {
            Debug.LogWarning("[ViewportScreenSettings] 초기화 전 ReadjustWindow 호출 — 무시. IsReady로 버튼을 잠그세요.");
            return;
        }

        ApplyScreenLayout();

        // 작업 영역이 줄었으면 뷰포트가 베이스 공간을 넘칠 수 있다. 줄여서 맞추되 저장하지는 않는다
        // (큰 모니터로 돌아가면 저장된 원래 크기가 복원되어야 한다).
        _viewport = ClampToBaseSpace(_viewport);
        PublishViewportApplied();
    }

    /// <summary>화면 설정 진입 — 뷰포트 조정을 시작한다.
    /// 창과 카메라는 그대로다(이미 작업 영역 전체를 차지하고 비추고 있다).</summary>
    public void EnterEdit()
    {
        if (_isEditing) return;
        if (!_ready)
        {
            // 무음 무시 금지 (리뷰 합의: 큐잉보다 로그+거부 — 10프레임 뒤 갑자기 상태가 바뀌는 UX가 더 나쁨)
            Debug.LogWarning("[ViewportScreenSettings] 초기화 전 EnterEdit 호출 — 무시. IsReady로 버튼을 잠그세요.");
            return;
        }
        _isEditing = true;
        _previewViewport = _viewport;

        if (_windowManager != null)
        {
            _windowManager.SetClickThroughSuspended(true); // 빈 공간에서도 핸들 드래그가 잡히게
            _windowManager.SetResizeSuspended(true);       // OS 가장자리 리사이즈가 켜져 있다면 핸들 UI와 충돌 방지
        }

        EditModeChanged?.Invoke(true);
        PreviewChanged?.Invoke(_previewViewport);
    }

    /// <summary>편집 중 프리뷰 갱신(핸들 드래그·슬라이더). 이벤트만 발행한다.</summary>
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
        PublishViewportApplied();
        EditModeChanged?.Invoke(false);
    }

    /// <summary>창을 작업 영역에 놓고 카메라를 베이스 공간 전체에 프레이밍한다.
    /// 부팅 시 1회와 ReadjustWindow에서만 부른다 — 뷰포트가 바뀌어도 창·카메라는 그대로다.</summary>
    private void ApplyScreenLayout()
    {
        RefreshBaseSpace();

        if (_windowManager != null)
        {
            _windowManager.ApplyRegion(
                _baseSpaceScreenRect.x, _baseSpaceScreenRect.y,
                _baseSpaceScreenRect.width, _baseSpaceScreenRect.height);
        }
        if (_cameraFitter != null)
            _cameraFitter.Frame(new RectInt(0, 0, _baseSpaceSize.x, _baseSpaceSize.y), _baseSpaceSize);
    }

    /// <summary>확정 뷰포트가 적용됐음을 알린다. 창·카메라는 건드리지 않는다 —
    /// 뷰포트는 렌더링이 아니라 게임플레이 규칙의 기준이기 때문이다.</summary>
    private void PublishViewportApplied() => ViewportApplied?.Invoke(_viewport);

    private void RefreshBaseSpace()
    {
        if (_windowManager != null && _windowManager.TryGetWorkAreaRect(out RectInt workArea))
        {
            _baseSpaceScreenRect = workArea;
        }
        else
        {
            // Editor 등 Win32 불가 환경 — 현재 화면 크기를 베이스 공간으로 간주(카메라 프레이밍은 검증 가능)
            _baseSpaceScreenRect = new RectInt(0, 0, Mathf.Max(Screen.width, 1), Mathf.Max(Screen.height, 1));
        }
        _baseSpaceSize = new Vector2Int(_baseSpaceScreenRect.width, _baseSpaceScreenRect.height);
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
