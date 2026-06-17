using System.Collections;
using UnityEngine;

/// <summary>
/// 카메라 viewport의 "픽셀당 월드 크기"를 _referenceHeight 기준으로 고정한다.
/// 즉 _referenceHeight(px) 높이일 때 세로로 정확히 _minY~_maxY가 보이며, 그때의
/// 스케일을 모든 창 크기에서 유지한다. → 창을 리사이즈해도 화면상 오브젝트 크기는 불변.
/// 대신 카메라가 보는 월드 범위가 창 크기에 비례해 함께 변동한다(바닥 _minY는 하단에 고정).
/// x는 0이 가운데, 좌우 폭은 현재 종횡비로 맞춰져 픽셀이 정사각을 유지한다.
///
/// 게임 로딩 시 자동 1회 적용. WindowResizeHandler(koko 윈도우)가 씬에 있으면 그
/// Resized 이벤트를 구독해 리사이즈마다 자동 재Fit한다. WindowResizeHandler가 없는
/// 씬(예: MainScene_KK)에서는 1회 Fit만.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public class CameraFitter : MonoBehaviour
{
    [Header("Viewport Y Range (world units)")]
    [SerializeField] private float _minY = 0f;
    [SerializeField] private float _maxY = 100f;

    [Header("Reference (design)")]
    [Tooltip("이 화면 높이(px)에서 _minY~_maxY가 정확히 보인다. 오브젝트의 화면 크기는 이 기준으로 고정된다.")]
    [SerializeField] private float _referenceHeight = 1080f;

    [Header("Resize 연동 (선택)")]
    [Tooltip("창 리사이즈 시 자동 재Fit할 핸들러. 비우면 Awake에서 씬에서 자동 탐색. 없으면 1회 Fit만.")]
    [SerializeField] private WindowResizeHandler _resizeHandler;

    private Camera _camera;

    void Awake()
    {
        _camera = GetComponent<Camera>();
        if (_resizeHandler == null) _resizeHandler = FindFirstObjectByType<WindowResizeHandler>();
        if (_resizeHandler != null) _resizeHandler.Resized += Fit;
        StartCoroutine(FitAfterReady());
    }

    void OnDestroy()
    {
        if (_resizeHandler != null) _resizeHandler.Resized -= Fit;
    }

    // OverlayWindow/WindowResizeHandler가 초기 창 배치를 잡는 데 몇 프레임 걸리므로,
    // 그보다 살짝 늦게 측정해야 Screen 크기·종횡비가 안정화된 상태에서 Fit된다.
    private IEnumerator FitAfterReady()
    {
        for (int i = 0; i < 15; i++) yield return null;
        Fit();
    }

    /// <summary>현재 화면 최상단 월드 y값(_maxY). 디버그 UI 표시용.</summary>
    public float MaxY => _maxY;

    /// <summary>화면 최상단 월드 y값(_maxY)을 바꾸고 즉시 재Fit. 테스트 UI 등 런타임 조절용.</summary>
    public void SetMaxY(float maxY)
    {
        _maxY = maxY;
        Fit();
    }

    [ContextMenu("Fit Now")]
    public void Fit()
    {
        if (_camera == null) _camera = GetComponent<Camera>();

        if (!_camera.orthographic)
        {
            Debug.LogWarning("[CameraFitter] 카메라가 Orthographic 모드가 아님 — 적용 스킵");
            return;
        }

        if (_maxY <= _minY)
        {
            Debug.LogWarning($"[CameraFitter] _maxY({_maxY}) <= _minY({_minY}) — 적용 스킵");
            return;
        }

        if (_referenceHeight <= 0f)
        {
            Debug.LogWarning($"[CameraFitter] _referenceHeight({_referenceHeight}) <= 0 — 적용 스킵");
            return;
        }

        // 픽셀당 월드 크기를 _referenceHeight 기준으로 고정 → 리사이즈해도 오브젝트 화면 크기 불변.
        // 창이 커지면 orthoSize가 비례해 커져 더 넓은 월드를 보여준다(범위가 함께 변동).
        float worldPerPixel = (_maxY - _minY) / _referenceHeight;
        _camera.orthographicSize = worldPerPixel * Screen.height * 0.5f;

        // 바닥 고정: _minY가 항상 뷰포트 하단에 오도록 중심을 올린다.
        var pos = transform.position;
        pos.x = 0f;
        pos.y = _minY + _camera.orthographicSize;
        transform.position = pos;

        // 가로 aspect를 현재 화면 종횡비로 설정해 픽셀이 정사각을 유지한다. WindowResizeHandler.Resized
        // 구독 시 리사이즈마다 Fit()이 재호출되어 위 스케일·종횡비가 다시 맞춰진다.
        _camera.aspect = (float)Screen.width / Screen.height;
    }
}
