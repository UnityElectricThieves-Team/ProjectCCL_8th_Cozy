using UnityEngine;

/// <summary>
/// orthographic 카메라를 "베이스 공간의 지정 픽셀 영역"에 화면 가득 프레이밍한다.
///
/// 좌표계 전제 (Docs/Planning/UserSettings.md §2.1.1을 개발자가 해석한 것 — .claude/rules/unity/viewport-coordinates.md):
///   - 마스터 캔버스(3840×2160)가 모든 에셋의 제작 기준 좌표계. 여기 px 값은 **마스터 캔버스 기준 px**다.
///   - 베이스 공간 = 마스터 캔버스의 "우하단"을 작업 영역(모니터에서 작업표시줄을 뺀 영역)의 종횡비만큼
///     잘라낸 영역. 폭은 항상 마스터 캔버스 폭이고, 단위의 정의는 ViewportScreenSettings가 든다.
///   - 따라서 월드 앵커도 우하단: _masterCanvasBottomRight가 마스터 캔버스 우하단 모서리의 월드 좌표.
///
/// 이 클래스의 식은 px가 어떤 단위인지 모른다 — 앵커와 PPU로 px를 월드 길이로 바꿀 뿐이다.
/// 프레이밍 영역의 높이(베이스 px)를 orthoSize에 맞추면, 화면 높이가 작업 영역 높이이므로
/// 베이스 px 하나는 자동으로 (작업 영역 폭 / 마스터 캔버스 폭) 화면 px가 된다. camera.rect/aspect를 만질
/// 필요가 없다. 정적 창 모델에서는 **창 = 작업 영역, 프레이밍 영역 = 베이스 공간 전체**다.
///
/// 그래서 이 클래스가 보증하는 것은 정확히 이것이다 —
/// **화면상 위치·크기는 작업 영역의 우하단 모서리에 고정되고, 작업 영역 폭에 비례해 줄어든다.**
/// 창 크기가 달라져도 무조건 불변인 것이 아니다. 작업표시줄이 아래에서 위로 옮겨가는 것처럼
/// 그 모서리 자체가 움직이면 화면상 위치도 따라 움직인다(지면이 작업 영역 바닥을 따라가는 것이
/// 이 모델의 정의이므로 의도된 동작이다). 유도는
/// Docs/Development/WindowViewportUIArchitecture.md §4.1.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public class BaseSpaceCameraFitter : MonoBehaviour
{
    // 1 월드 유닛이 몇 베이스 공간 픽셀인가. Unity 기본 임포트 PPU(100)와 같아서,
    // 임포트 PPU를 그대로 둔 스프라이트는 그림 1px = 베이스(마스터 캔버스) 1px가 된다.
    private const float PIXELS_PER_UNIT = 100f;

    [Header("Master Canvas (design)")]
    [SerializeField, Tooltip("마스터 캔버스 우하단 모서리의 월드 좌표. 모든 프레이밍의 앵커")]
    private Vector2 _masterCanvasBottomRight = Vector2.zero;

    private Camera _camera;

    /// <summary>1 월드 유닛이 몇 베이스 공간 픽셀인가. 픽셀 단위 값을 월드 길이로 바꿀 때 쓴다.</summary>
    public float PixelsPerUnit => PIXELS_PER_UNIT;

    private void Awake() => _camera = GetComponent<Camera>();

    /// <summary>
    /// 베이스 공간 px rect(원점=좌하단, Y 위)를 월드 좌표 Rect로 변환.
    /// Frame()과 동일한 앵커/PPU 규칙 — 뷰포트 안팎 판정(캐릭터 회수 등)에 사용.
    /// </summary>
    public Rect BaseRectToWorld(RectInt basePx, Vector2Int baseSpaceSize)
    {
        float baseLeft   = _masterCanvasBottomRight.x - baseSpaceSize.x / PIXELS_PER_UNIT;
        float baseBottom = _masterCanvasBottomRight.y;
        return new Rect(
            baseLeft   + basePx.x / PIXELS_PER_UNIT,
            baseBottom + basePx.y / PIXELS_PER_UNIT,
            basePx.width  / PIXELS_PER_UNIT,
            basePx.height / PIXELS_PER_UNIT);
    }

    /// <summary>
    /// 베이스 공간 내 픽셀 rect(원점=베이스 공간 좌하단, Y 위 방향)를 화면에 꽉 차게 프레이밍.
    /// baseSpaceSize = 베이스 공간 크기(마스터 캔버스 기준 px) — ViewportScreenSettings.BaseSpaceSize.
    /// </summary>
    public void Frame(RectInt viewportPx, Vector2Int baseSpaceSize)
    {
        if (_camera == null) _camera = GetComponent<Camera>();

        if (!_camera.orthographic)
        {
            Debug.LogWarning("[BaseSpaceCameraFitter] 카메라가 Orthographic 모드가 아님 — 적용 스킵");
            return;
        }

        if (viewportPx.width <= 0 || viewportPx.height <= 0)
        {
            Debug.LogWarning($"[BaseSpaceCameraFitter] 잘못된 파라미터(viewport {viewportPx}) — 적용 스킵");
            return;
        }

        // 베이스 공간은 마스터 캔버스 우하단 크롭 → 좌하단 월드 좌표는 앵커에서 모니터 폭만큼 왼쪽.
        float baseLeft   = _masterCanvasBottomRight.x - baseSpaceSize.x / PIXELS_PER_UNIT;
        float baseBottom = _masterCanvasBottomRight.y;

        _camera.orthographicSize = viewportPx.height / PIXELS_PER_UNIT * 0.5f;

        var pos = transform.position;
        pos.x = baseLeft   + (viewportPx.x + viewportPx.width  * 0.5f) / PIXELS_PER_UNIT;
        pos.y = baseBottom + (viewportPx.y + viewportPx.height * 0.5f) / PIXELS_PER_UNIT;
        transform.position = pos;
    }
}
