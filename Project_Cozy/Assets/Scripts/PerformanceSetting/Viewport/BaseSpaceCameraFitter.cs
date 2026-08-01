using UnityEngine;

/// <summary>
/// orthographic 카메라를 "베이스 공간의 지정 픽셀 영역"에 절대 픽셀 1:1로 프레이밍한다.
///
/// 좌표계 전제 (Docs/Planning/UserSettings.md §2.1.1):
///   - 마스터 캔버스(3840×2160)가 모든 에셋의 제작 기준 절대 좌표계 — 모니터 해상도와 무관하게 크기 불변.
///   - 베이스 공간 = 마스터 캔버스의 "우하단"을 **작업 영역**(모니터에서 작업표시줄을 뺀 영역)만큼
///     잘라낸 영역.
///   - 따라서 월드 앵커도 우하단: _masterCanvasBottomRight가 마스터 캔버스 우하단 모서리의 월드 좌표.
///
/// "창 크기(px) == 프레이밍 영역 크기(px)"가 성립해야 orthoSize만 픽셀 높이에 맞추는 것으로
/// 화면상 오브젝트 크기가 불변이 되고 camera.rect/aspect를 만질 필요가 없다.
/// 정적 창 모델에서는 **창 = 베이스 공간 = 프레이밍 영역**이라 이 등식이 항상 참이다.
///
/// 그래서 이 클래스가 보증하는 것은 정확히 이것이다 —
/// **화면상 위치·크기는 작업 영역의 우하단 모서리에 고정된다.**
/// 창 크기가 달라져도 무조건 불변인 것이 아니다. 작업표시줄이 아래에서 위로 옮겨가는 것처럼
/// 그 모서리 자체가 움직이면 화면상 위치도 따라 움직인다(지면이 작업 영역 바닥을 따라가는 것이
/// 이 모델의 정의이므로 의도된 동작이다). 유도는
/// Docs/Development/WindowViewportUIArchitecture.md §4.1.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public class BaseSpaceCameraFitter : MonoBehaviour
{
    [Header("Master Canvas (design)")]
    [SerializeField, Tooltip("1 월드 유닛이 몇 픽셀인가. 스프라이트 임포트 PPU와 동일해야 절대 픽셀 1:1이 성립")]
    private float _pixelsPerUnit = 100f;

    [SerializeField, Tooltip("마스터 캔버스 우하단 모서리의 월드 좌표. 모든 프레이밍의 앵커")]
    private Vector2 _masterCanvasBottomRight = Vector2.zero;

    private Camera _camera;

    private void Awake() => _camera = GetComponent<Camera>();

    /// <summary>
    /// 베이스 공간 px rect(원점=좌하단, Y 위)를 월드 좌표 Rect로 변환.
    /// Frame()과 동일한 앵커/PPU 규칙 — 뷰포트 안팎 판정(캐릭터 회수 등)에 사용.
    /// </summary>
    public Rect BaseRectToWorld(RectInt basePx, Vector2Int baseSpaceSize)
    {
        float baseLeft   = _masterCanvasBottomRight.x - baseSpaceSize.x / _pixelsPerUnit;
        float baseBottom = _masterCanvasBottomRight.y;
        return new Rect(
            baseLeft   + basePx.x / _pixelsPerUnit,
            baseBottom + basePx.y / _pixelsPerUnit,
            basePx.width  / _pixelsPerUnit,
            basePx.height / _pixelsPerUnit);
    }

    /// <summary>
    /// 베이스 공간 내 픽셀 rect(원점=베이스 공간 좌하단, Y 위 방향)를 화면에 꽉 차게 프레이밍.
    /// baseSpaceSize = 현재 모니터의 작업 영역 크기(px).
    /// </summary>
    public void Frame(RectInt viewportPx, Vector2Int baseSpaceSize)
    {
        if (_camera == null) _camera = GetComponent<Camera>();

        if (!_camera.orthographic)
        {
            Debug.LogWarning("[BaseSpaceCameraFitter] 카메라가 Orthographic 모드가 아님 — 적용 스킵");
            return;
        }

        if (_pixelsPerUnit <= 0f || viewportPx.width <= 0 || viewportPx.height <= 0)
        {
            Debug.LogWarning($"[BaseSpaceCameraFitter] 잘못된 파라미터(ppu {_pixelsPerUnit}, viewport {viewportPx}) — 적용 스킵");
            return;
        }

        // 베이스 공간은 마스터 캔버스 우하단 크롭 → 좌하단 월드 좌표는 앵커에서 모니터 폭만큼 왼쪽.
        float baseLeft   = _masterCanvasBottomRight.x - baseSpaceSize.x / _pixelsPerUnit;
        float baseBottom = _masterCanvasBottomRight.y;

        _camera.orthographicSize = viewportPx.height / _pixelsPerUnit * 0.5f;

        var pos = transform.position;
        pos.x = baseLeft   + (viewportPx.x + viewportPx.width  * 0.5f) / _pixelsPerUnit;
        pos.y = baseBottom + (viewportPx.y + viewportPx.height * 0.5f) / _pixelsPerUnit;
        transform.position = pos;
    }
}
