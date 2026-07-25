using UnityEngine;

/// <summary>
/// orthographic 카메라를 "베이스 공간의 지정 픽셀 영역"에 절대 픽셀 1:1로 프레이밍한다.
///
/// 좌표계 전제 (Docs/Planning/User_Settings.md §2.1.1):
///   - 마스터 캔버스(3840×2160)가 모든 에셋의 제작 기준 절대 좌표계 — 모니터 해상도와 무관하게 크기 불변.
///   - 베이스 공간 = 마스터 캔버스의 "우하단"을 모니터 해상도만큼 잘라낸 영역.
///   - 따라서 월드 앵커도 우하단: _masterCanvasBottomRight가 마스터 캔버스 우하단 모서리의 월드 좌표.
///
/// "창 크기(px) == 프레이밍 영역 크기(px)"가 항상 일치하는 구조(평시 창=뷰포트,
/// 편집 시 창=모니터·프레이밍=베이스 공간 전체) 전제라, orthoSize만 픽셀 높이에 맞추면
/// 화면상 오브젝트 크기가 자동으로 불변이고 camera.rect/aspect를 만질 필요가 없다.
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
    /// baseSpaceSize = 현재 모니터 해상도(px).
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
