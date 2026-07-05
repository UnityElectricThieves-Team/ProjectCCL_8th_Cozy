// ============================================================
// RegionEditChrome  (편집 모드 시각 크롬 — 선택 컴포넌트)
//
// OverlayWindowController가 EditRegion 모드일 때만 OnGUI로:
//   - 테두리 + 모서리 핸들 (리사이즈 가장자리 표시)
//   - 상단 중앙 "이동 핸들" (제목표시줄 형태, 그립 아이콘 + 라벨)
//   - 모드 배너 + "완료" 버튼
// 을 그린다. 실제 리사이즈/이동은 OS가 NCHITTEST로 처리하고, 이 크롬은
// "어디를 잡으면 되는지" 보여주는 시각 안내 + 종료 버튼 역할만 한다.
//
// ⚠️ 핸들 위치/크기(edge/corner/caption)는 WindowResizeHandler의 핫존 값과
//    동일하게 맞춰야 보이는 곳 = 잡히는 곳이 된다.
// ============================================================
using UnityEngine;

[RequireComponent(typeof(OverlayWindowController))]
public class RegionEditChrome : MonoBehaviour
{
    [SerializeField] private int edgeThicknessPx = 6;    // WindowResizeHandler.edgeThicknessPx와 일치
    [SerializeField] private int cornerSizePx    = 12;   // 〃 cornerSizePx
    [SerializeField] private int captionHeightPx = 32;   // 〃 captionHeightPx (이동 핸들 높이)
    [SerializeField] private int captionWidthPx  = 260;  // 〃 captionWidthPx  (이동 핸들 너비)
    [SerializeField] private Color accent = new Color(0.40f, 0.80f, 1f, 0.95f);

    private OverlayWindowController _controller;
    private static Texture2D _tex;
    private GUIStyle _handleLabel;
    private GUIStyle _bannerLabel;

    private void Awake() => _controller = GetComponent<OverlayWindowController>();

    private void OnGUI()
    {
        if (_controller.Mode != eWindowMode.EditRegion) return;

        int w = Screen.width;
        int h = Screen.height;
        EnsureStyles();

        // 리사이즈 테두리
        Fill(0, 0, w, edgeThicknessPx, accent);
        Fill(0, h - edgeThicknessPx, w, edgeThicknessPx, accent);
        Fill(0, 0, edgeThicknessPx, h, accent);
        Fill(w - edgeThicknessPx, 0, edgeThicknessPx, h, accent);

        // 모서리 핸들
        Fill(0, 0, cornerSizePx, cornerSizePx, accent);
        Fill(w - cornerSizePx, 0, cornerSizePx, cornerSizePx, accent);
        Fill(0, h - cornerSizePx, cornerSizePx, cornerSizePx, accent);
        Fill(w - cornerSizePx, h - cornerSizePx, cornerSizePx, cornerSizePx, accent);

        // 상단 중앙 "이동 핸들" — 솔리드 바 + 그립 아이콘 + 라벨 (히트존과 동일 영역)
        int hx = (w - captionWidthPx) / 2;
        Fill(hx, 0, captionWidthPx, captionHeightPx, accent);

        // 그립 아이콘 (가로 3선) — 폰트와 무관하게 "잡는 곳"임을 표시
        int gripCx = hx + 22;
        int gripCy = captionHeightPx / 2;
        for (int i = -1; i <= 1; i++)
            Fill(gripCx - 9, gripCy + i * 5 - 1, 18, 2, Color.white);

        GUI.Label(new Rect(hx, 0, captionWidthPx, captionHeightPx), "드래그하여 이동", _handleLabel);

        // 모드 배너 (핸들 아래)
        GUI.Label(new Rect(0, captionHeightPx + 6, w, 22),
            "영역 수정 모드 · 가장자리 = 크기 조절", _bannerLabel);

        // 완료 버튼 (중앙)
        if (GUI.Button(new Rect(w / 2 - 70, h / 2 - 22, 140, 44), "완료"))
            _controller.ExitEditRegion();
    }

    private void EnsureStyles()
    {
        if (_handleLabel == null)
            _handleLabel = new GUIStyle(GUI.skin.label)
            { alignment = TextAnchor.MiddleCenter, fontSize = 13, fontStyle = FontStyle.Bold };
        _handleLabel.normal.textColor = Color.white;

        if (_bannerLabel == null)
            _bannerLabel = new GUIStyle(GUI.skin.label)
            { alignment = TextAnchor.MiddleCenter, fontSize = 12 };
        _bannerLabel.normal.textColor = new Color(accent.r, accent.g, accent.b, 1f);
    }

    private static void Fill(float x, float y, float w, float h, Color color)
    {
        if (_tex == null)
        {
            _tex = new Texture2D(1, 1);
            _tex.SetPixel(0, 0, Color.white);
            _tex.Apply();
        }
        Color prev = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(new Rect(x, y, w, h), _tex);
        GUI.color = prev;
    }
}
