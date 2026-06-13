// ============================================================
// OverlayModeDemoButtons  (예시 데모 — 콘텐츠/UI 레이어)
//
// 화면 좌하단에 코드로 버튼을 만들어 OverlayWindowController를 토글한다:
//   [영역 수정]  → ToggleEditRegion()  (잠금 토글과 같은 패턴)
//   [전체화면]   → SetFullscreen()      (편집한 영역을 화면 전체로 복귀)
//
// 윈도우 베이스와 분리된 데모 UI. 씬의 아무 활성 GameObject에 붙이면 됨
// (controller 미할당 시 씬에서 자동 탐색).
//
// 신 Input System 기준 EventSystem을 코드로 보장한다.
// 버튼은 EditRegion 모드(불투명·HTCLIENT 내부)에서도 클릭된다.
// ============================================================
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

public class OverlayModeDemoButtons : MonoBehaviour
{
    [SerializeField, Tooltip("미할당 시 씬에서 자동 탐색")]
    private OverlayWindowController controller;

    private void Start()
    {
        if (controller == null) controller = FindFirstObjectByType<OverlayWindowController>();
        if (controller == null)
        {
            Debug.LogWarning("[OverlayModeDemoButtons] OverlayWindowController를 찾지 못함 — 버튼 비활성.");
            return;
        }

        EnsureEventSystem();

        var canvasGo = new GameObject("DemoModeCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

        // 좌하단 Spawn 버튼(20..190) 오른쪽에 나란히. y=90 → 작업 표시줄 위로.
        AddButton(canvasGo.transform, "영역 수정", new Vector2(200f, 90f),
            () => controller.ToggleEditRegion());
        AddButton(canvasGo.transform, "전체화면", new Vector2(360f, 90f),
            () => controller.SetFullscreen());
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;
        new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
    }

    private static void AddButton(Transform parent, string label, Vector2 anchoredPos, UnityAction onClick)
    {
        var btnGo = new GameObject("Btn_" + label, typeof(Image), typeof(Button));
        btnGo.transform.SetParent(parent, false);
        btnGo.GetComponent<Image>().color = new Color(0.55f, 0.75f, 1f, 1f); // 파랑 (비검정)

        var rt = btnGo.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 0f); // 좌하단 기준
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = new Vector2(150f, 48f);

        btnGo.GetComponent<Button>().onClick.AddListener(onClick);

        var txtGo = new GameObject("Label", typeof(Text));
        txtGo.transform.SetParent(btnGo.transform, false);
        var txt = txtGo.GetComponent<Text>();
        txt.text = label;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color = Color.white;
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        var trt = txtGo.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = trt.offsetMax = Vector2.zero;
    }
}
