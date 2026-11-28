using UnityEngine;

/// <summary>
/// 장식 나열 샘플 전용 카메라. 게임의 기본 줌과 같은 폭 기준 배율을 유지하고 가로 위치만 옮깁니다.
/// 본편 씬에는 사용하지 않습니다.
/// </summary>
[RequireComponent(typeof(Camera))]
public sealed class DecorationArtSampleCamera : MonoBehaviour
{
    // 게임의 마스터 캔버스 폭 3840px / 월드 PPU 100. 전체 나열 폭에 맞춰 축소하지 않습니다.
    private const float ViewWidth = 38.4f;
    private const float FloorMargin = 0.8f;
    private const float PageStep = ViewWidth * 0.8f;

    [SerializeField] private float leftEdge;
    [SerializeField] private float rightEdge;

    private Camera sampleCamera;
    private float lastAspect;

    public void Configure(float left, float right)
    {
        leftEdge = left;
        rightEdge = right;
        sampleCamera = GetComponent<Camera>();
        UpdateFraming();
        MoveTo(leftEdge);
    }

    private void Awake()
    {
        sampleCamera = GetComponent<Camera>();
        UpdateFraming();
    }

    private void LateUpdate()
    {
        if (!Mathf.Approximately(lastAspect, sampleCamera.aspect)) UpdateFraming();
    }

    private void UpdateFraming()
    {
        lastAspect = Mathf.Max(sampleCamera.aspect, 0.01f);
        sampleCamera.orthographic = true;
        sampleCamera.orthographicSize = ViewWidth / (2 * lastAspect);
        Vector3 position = transform.position;
        position.y = sampleCamera.orthographicSize - FloorMargin;
        position.z = -10;
        transform.position = position;
    }

    private void MoveTo(float left)
    {
        float maximum = Mathf.Max(leftEdge, rightEdge - ViewWidth);
        Vector3 position = transform.position;
        position.x = Mathf.Clamp(left, leftEdge, maximum) + ViewWidth / 2;
        transform.position = position;
    }

    private void OnGUI()
    {
        // 아트 확인용 조작부. 상품 UI나 저장 시스템을 만들지 않는 독립 샘플입니다.
        GUILayout.BeginArea(new Rect(12, 12, 460, 75), GUI.skin.box);
        GUILayout.Label("Art decorations | Game scale (zoom 1) | Scroll to browse");
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("First")) MoveTo(leftEdge);
        if (GUILayout.Button("Previous")) MoveTo(transform.position.x - ViewWidth / 2 - PageStep);
        if (GUILayout.Button("Next")) MoveTo(transform.position.x - ViewWidth / 2 + PageStep);
        if (GUILayout.Button("Last")) MoveTo(rightEdge - ViewWidth);
        GUILayout.EndHorizontal();
        GUILayout.EndArea();

        float currentLeft = transform.position.x - ViewWidth / 2;
        float end = Mathf.Max(rightEdge, leftEdge + ViewWidth);
        float newLeft = GUI.HorizontalScrollbar(new Rect(12, Screen.height - 30, Screen.width - 24, 18),
            currentLeft, ViewWidth, leftEdge, end);
        if (!Mathf.Approximately(currentLeft, newLeft)) MoveTo(newLeft);
    }
}
