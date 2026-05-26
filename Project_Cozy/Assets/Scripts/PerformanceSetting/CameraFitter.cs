using System.Collections;
using UnityEngine;

/// <summary>
/// 카메라 viewport를 디자인 좌표계(y=_minY ~ y=_maxY)에 맞춘다.
/// x는 0이 가운데로 오고, 좌우 폭은 Fit() 시점의 화면 종횡비로 락된다(이후 자동 추적 X).
///
/// 게임 로딩 시 자동 1회 적용. 모니터/해상도 변경 시엔 외부에서 Fit() 재호출 필요.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public class CameraFitter : MonoBehaviour
{
    [Header("Viewport Y Range (world units)")]
    [SerializeField] private float _minY = 0f;
    [SerializeField] private float _maxY = 100f;

    private Camera _camera;

    void Awake()
    {
        _camera = GetComponent<Camera>();
        StartCoroutine(FitAfterReady());
    }

    // WindowManager._maximizeToWorkArea 코루틴이 10프레임 후 윈도우를 작업영역에 맞추므로,
    // 그보다 살짝 늦게 측정해야 Screen 크기·종횡비가 안정화된 상태에서 락된다.
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

        // 가로 aspect 락 — 윈도우 크기 변해도 자동 추적 안 함. 갱신은 Fit() 재호출 필요.
        _camera.aspect = (float)Screen.width / Screen.height;
    }
}
