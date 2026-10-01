using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// "화면 설정"(UserSettings.md §2.1.1)의 정책 레이어 — 정적 창 모델.
///
/// 창:          항상 현재 모니터의 **작업 영역**(작업표시줄을 뺀 영역)에 놓인다. 평상시에는 크기도
///              위치도 변하지 않고, ReadjustWindow()를 부를 때만 다시 잡는다.
/// 베이스 공간: 작업 영역을 **마스터 캔버스 기준 px**로 표현한 것. 폭은 항상 마스터 캔버스 폭이고,
///              높이는 작업 영역의 종횡비를 따른다. 카메라는 이 전체를 비추며, 재조정 때 말고는
///              건드리지 않는다. 화면에서는 베이스 px 하나가 (작업 영역 폭 / 마스터 캔버스 폭) 화면 px로
///              보인다 — UI의 CanvasScaler(기준 폭 일치, Match=Width)와 같은 배율이라, 어느 해상도에서든
///              월드와 UI가 같은 비율로 줄어든다. 이 클래스가 그 단위 정의의 유일한 원천이다
///              (RefreshBaseSpace). px→월드 환산(앵커·PPU)은 BaseSpaceCameraFitter가 든다.
/// 뷰포트:      베이스 공간 안의 논리적 사각형. **렌더링 파라미터가 아니다** — 창을 줄이지도 화면을
///              잘라내지도 않는다. 캐릭터가 살 수 있는 영역이자 지면·회수 판정의 기준이다.
/// 편집:        조정값은 프리뷰일 뿐이며(PreviewChanged로 UI가 경계·딤·회수 예정 시각화),
///              SaveEdit()로만 확정, CancelEdit()는 폐기(§2.1.1 "저장하지 않고 나가면 폐기").
///
/// 이 모델을 고른 이유는 Docs/Development/WindowViewportUIArchitecture.md.
///
/// Win32를 모른다 — 창 배치·클릭 통과는 WindowManager에 위임(HWND 접점은 그쪽 한 곳).
/// 영속화도 모른다 — 확정 뷰포트는 ViewportSaved 구독 측(ViewportSaveBinder)이 저장하고,
/// 로드 시 SetViewport()로 주입한다(베이스 공간 밖 값은 자동 클램프). 작업 영역 px 단위로 저장된
/// 옛 값은 SetViewportFromWorkAreaPx()로 넣으면 여기서 환산한다 — 파일 버전은 모르고 단위만 안다.
/// </summary>
[DisallowMultipleComponent]
public class ViewportScreenSettings : MonoBehaviour
{
    /// <summary>§2.1.1 제약 — 뷰포트 최소 크기(베이스 공간 px = 마스터 캔버스 기준 px).
    /// 기획서의 "절대 픽셀"을 마스터 캔버스 좌표계의 픽셀로 해석한 것이라, 4K에서만 화면 px와 같다.</summary>
    public static readonly Vector2Int MinViewportSize = new Vector2Int(720, 480);

    // 마스터 캔버스 폭(px). 베이스 공간의 폭이자 화면 배율의 분모다. GameScene의 UIRoot CanvasScaler
    // 기준 해상도 폭과 같아야 월드와 UI가 같은 배율로 줄어든다 — 씬 값은 인스펙터에서 바뀌므로 코드로
    // 강제하지 못한다. 규약은 .claude/rules/unity/viewport-coordinates.md.
    private const int MasterCanvasWidthPx = 3840;

    [Header("협력자")]
    [SerializeField] private WindowManager _windowManager;
    [SerializeField] private BaseSpaceCameraFitter _cameraFitter;

    [Header("뷰포트 (베이스 공간 px, 원점=좌하단)")]
    [SerializeField, Tooltip("시작 뷰포트. 크기가 0이면 베이스 공간 전체(기본값)로 시작")]
    private RectInt _viewport = new RectInt(0, 0, 0, 0);

    private RectInt _previewViewport;
    private RectInt _baseSpaceScreenRect;      // 작업 영역을 스크린 좌표(화면 px, Y 아래)로 표현한 것 — 창 배치용
    private Vector2Int _baseSpaceSize;         // 베이스 공간 크기(마스터 캔버스 기준 px) — 폭은 항상 MasterCanvasWidthPx
    private bool _isEditing;
    private bool _ready;                       // 초기 적용 완료 전 API 호출 가드

    // ready 전에 작업 영역 px 단위로 들어온 뷰포트. 환산에 필요한 배율은 Start에서 작업 영역을 읽은 뒤에야
    // 알 수 있어 그때까지 따로 들고 있는다. _viewport에 섞어 두면 어느 단위인지 알 수 없어진다.
    private RectInt _pendingWorkAreaPx;
    private bool _hasPendingWorkAreaPx;

    /// <summary>확정된 뷰포트(베이스 공간 px, 원점=좌하단).</summary>
    public RectInt Viewport => _viewport;

    /// <summary>편집 중 프리뷰 뷰포트. 편집 중이 아니면 Viewport와 동일.</summary>
    public RectInt PreviewViewport => _isEditing ? _previewViewport : _viewport;

    public bool IsEditing => _isEditing;

    /// <summary>초기 적용 완료 여부. false 동안 EnterEdit/ReadjustWindow는 거부된다 — UI는 이걸로 버튼을 잠글 것.</summary>
    public bool IsReady => _ready;

    /// <summary>베이스 공간 크기(마스터 캔버스 기준 px). 폭은 항상 마스터 캔버스 폭이고 높이는 작업 영역의
    /// 종횡비를 따른다. 화면 px가 아니다 — 화면 px로 바꾸려면 Screen 크기와의 비율을 곱한다.</summary>
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

        // 창 배치가 Screen 크기에 반영되는 것은 다음 프레임이라, 진단 로그는 한 프레임 뒤에 찍는다.
        // 부팅 1회뿐이라 영구로 둔다 — 해상도·DPI 문제 보고를 받았을 때 Player.log에서 바로 읽는 값이다.
        yield return null;
        Debug.Log($"[ViewportScreenSettings] 작업 영역 {_baseSpaceScreenRect} / 화면 {Screen.width}x{Screen.height} / " +
                  $"베이스 공간 {_baseSpaceSize} / 베이스 px당 화면 px {ScreenPxPerBasePx:F4}");

        // 작업 영역 px 단위로 들어온 옛 값은 배율을 안 지금 환산한다. 클램프보다 먼저여야 한다 —
        // 클램프 뒤에 환산하면 다른 해상도에서 저장한 값이 베이스 공간을 넘칠 수 있다.
        if (_hasPendingWorkAreaPx)
        {
            _viewport = WorkAreaPxToBasePx(_pendingWorkAreaPx);
            _hasPendingWorkAreaPx = false;
        }

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
        // 여기서 무조건 자기 정지를 푼다. Unity는 파괴 시에도 OnDisable을 먼저 부르므로 이 한 곳으로 두 경우가 덮인다.
        // 정지는 소유자별이라 다른 컴포넌트가 건 정지는 풀지 않는다. 걸지 않았을 때 풀어도 안전하다.
        //
        // _isEditing은 건드리지 않는다 — 이벤트 없이 조용히 내리면 편집 UI가 상태를 잘못 알게 된다.
        // 월드 입력 잠금도 같은 이유로 푼다 — 남으면 캐릭터·별이 영영 반응하지 않는다.
        WorldInputLock.Release(this);
        if (_windowManager == null) return;
        _windowManager.ReleaseClickThroughSuspend(this);
        _windowManager.ReleaseResizeSuspend(this);
    }

    // ===== 외부 API =====

    /// <summary>확정 뷰포트를 직접 설정(로드 경로). 베이스 공간 밖 값은 클램프.
    /// 편집 중이면 확정 값만 갱신하고 반영은 편집을 벗어날 때까지 미룬다.</summary>
    public void SetViewport(RectInt viewport)
    {
        // 뒤에 온 베이스 px 값이 이긴다 — 앞서 보류된 작업 영역 px 값을 Start가 또 환산해 덮지 않게.
        _hasPendingWorkAreaPx = false;

        // ready 전엔 베이스 공간 크기를 아직 모르므로 클램프할 수 없다 — Start가 클램프·적용을 맡는다.
        if (!_ready) { _viewport = viewport; return; }

        _viewport = ClampToBaseSpace(viewport);

        // 편집 중에는 진행 중인 프리뷰를 건드리지 않는다. 저장/취소로 빠져나올 때
        // ExitEdit이 이 확정 값을 반영한다.
        if (_isEditing) return;

        PublishViewportApplied();
    }

    /// <summary>작업 영역 px(옛 단위) 뷰포트를 설정한다. 베이스 px로 환산해 SetViewport와 같은 경로를 탄다.
    /// 환산 배율은 작업 영역을 읽은 뒤에야 알 수 있으므로 ready 전에는 보류했다가 Start에서 환산한다.
    /// 옛 파일이 저장된 기기의 해상도는 알 수 없어 현재 배율을 쓴다 — 같은 기기에서 이어 쓰는 경우 정확히 복원된다.</summary>
    public void SetViewportFromWorkAreaPx(RectInt workAreaPx)
    {
        if (!_ready)
        {
            _pendingWorkAreaPx = workAreaPx;
            _hasPendingWorkAreaPx = true;
            return;
        }
        SetViewport(WorkAreaPxToBasePx(workAreaPx));
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
            _windowManager.AcquireClickThroughSuspend(this); // 빈 공간에서도 핸들 드래그가 잡히게
            _windowManager.AcquireResizeSuspend(this);       // OS 가장자리 리사이즈가 켜져 있다면 핸들 UI와 충돌 방지
        }
        // 핸들은 IMGUI라 월드 폴러의 "UI 위" 가드에 잡히지 않는다 — 핸들을 잡는 클릭이 뒤의 캐릭터로 새지 않게 막는다.
        WorldInputLock.Acquire(this);

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
            _windowManager.ReleaseClickThroughSuspend(this);
            _windowManager.ReleaseResizeSuspend(this);
        }
        WorldInputLock.Release(this);
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
            // Editor 등 Win32 불가 환경 — 현재 화면 크기를 작업 영역으로 간주(카메라 프레이밍은 검증 가능)
            _baseSpaceScreenRect = new RectInt(0, 0, Mathf.Max(Screen.width, 1), Mathf.Max(Screen.height, 1));
        }

        // 베이스 공간은 작업 영역을 마스터 캔버스 폭에 맞춰 늘인 것 — 폭 기준이라 작업표시줄 유무로
        // 배율이 흔들리지 않고, UI CanvasScaler(Match=Width)와 같은 배율이 된다. 높이는 종횡비를 따른다.
        _baseSpaceSize = new Vector2Int(
            MasterCanvasWidthPx,
            Mathf.RoundToInt(_baseSpaceScreenRect.height * (float)MasterCanvasWidthPx / _baseSpaceScreenRect.width));
    }

    /// <summary>베이스 px 하나가 화면에서 몇 px인가 = 작업 영역 폭 / 마스터 캔버스 폭. 4K에서 1.</summary>
    private float ScreenPxPerBasePx => _baseSpaceScreenRect.width / (float)MasterCanvasWidthPx;

    private RectInt WorkAreaPxToBasePx(RectInt r)
    {
        float s = ScreenPxPerBasePx;
        return new RectInt(
            Mathf.RoundToInt(r.x / s), Mathf.RoundToInt(r.y / s),
            Mathf.RoundToInt(r.width / s), Mathf.RoundToInt(r.height / s));
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
