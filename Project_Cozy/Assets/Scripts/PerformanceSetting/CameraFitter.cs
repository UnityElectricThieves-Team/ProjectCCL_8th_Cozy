using System.Collections;
using UnityEngine;

/// <summary>
/// 카메라 viewport를 디자인 좌표계(y=_minY ~ y=_maxY)에 맞춘다.
/// x는 0이 가운데로 오고, 좌우 폭은 Fit() 시점의 화면 종횡비로 맞춰진다.
///
/// 게임 로딩 시 자동 1회 적용. WindowResizeHandler(koko 윈도우)가 씬에 있으면 그
/// Resized 이벤트를 구독해 리사이즈마다 자동 재Fit한다(세로 _minY~_maxY 유지 + 가로 자동
/// 핏 → 비율 유지). WindowResizeHandler가 없는 씬(예: MainScene_KK)에서는 1회 Fit만.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public class CameraFitter : MonoBehaviour
{
    [Header("Viewport Y Range (world units)")]
    [SerializeField] private float _minY = 0f;
    [SerializeField] private float _maxY = 100f;

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

        _camera.orthographicSize = (_maxY - _minY) * 0.5f;

        var pos = transform.position;
        pos.x = 0f;
        pos.y = (_minY + _maxY) * 0.5f;
        transform.position = pos;

        // 가로 aspect를 현재 화면 종횡비로 설정. WindowResizeHandler.Resized 구독 시
        // 리사이즈마다 Fit()이 재호출되어 종횡비가 다시 맞춰진다(세로 _minY~_maxY 유지 + 가로 자동 핏).
        _camera.aspect = (float)Screen.width / Screen.height;
    }
}
