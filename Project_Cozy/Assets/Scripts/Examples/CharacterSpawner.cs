// ============================================================
// CharacterSpawner  (예시 데모 — 콘텐츠/게임 로직 레이어)
//
// 화면에 버튼을 코드로 생성하고, 누르면 캐릭터 프리팹을 씬에 Instantiate 한다.
// 윈도우 베이스(Platform/Window)와 완전히 분리 — Win32를 전혀 모른다.
//
// 사용:
//   1) 씬의 빈 GameObject에 이 컴포넌트를 붙인다.
//   2) Inspector의 Character Prefab 슬롯에 Assets/Prefabs/Character.prefab 할당.
//   3) Play → 좌하단 "Spawn Character" 버튼 클릭 시마다 캐릭터가 스폰됨.
//
// 버튼은 ColorKey 창의 Normal 모드에서 클릭된다(핑크=비검정 픽셀이라 통과 안 함).
// 신 Input System 기준 EventSystem(InputSystemUIInputModule)을 코드로 보장한다.
// ============================================================
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

public class CharacterSpawner : MonoBehaviour
{
    [Header("스폰 대상")]
    [SerializeField, Tooltip("Assets/Prefabs/Character.prefab 할당")]
    private GameObject characterPrefab;

    [Header("스폰 위치")]
    [SerializeField] private Vector3 spawnPoint = Vector3.zero;
    [SerializeField, Tooltip("매 스폰마다 흩뿌리는 반경 — 겹침 방지")]
    private float spawnSpread = 1.5f;

    private void Start()
    {
        EnsureEventSystem();
        BuildButton();
    }

    /// <summary>버튼 OnClick에 연결되는 스폰 동작.</summary>
    public void Spawn()
    {
        if (characterPrefab == null)
        {
            Debug.LogWarning("[CharacterSpawner] characterPrefab 미할당 — Inspector에 Character.prefab을 넣어주세요.");
            return;
        }
        Vector2 jitter = Random.insideUnitCircle * spawnSpread;
        Vector3 pos = spawnPoint + new Vector3(jitter.x, jitter.y, 0f);
        Instantiate(characterPrefab, pos, Quaternion.identity);
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;
        new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
    }

    private void BuildButton()
    {
        // Canvas (Screen Space - Overlay)
        var canvasGo = new GameObject("DemoCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

        // Button 배경
        var btnGo = new GameObject("SpawnButton", typeof(Image), typeof(Button));
        btnGo.transform.SetParent(canvasGo.transform, false);
        btnGo.GetComponent<Image>().color = new Color(0.96f, 0.65f, 0.75f, 1f); // 핑크 (비검정)

        var rt = btnGo.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 0f); // 좌하단 기준
        rt.anchoredPosition = new Vector2(20f, 90f); // 작업 표시줄 위로
        rt.sizeDelta = new Vector2(170f, 48f);

        btnGo.GetComponent<Button>().onClick.AddListener(Spawn);

        // 라벨
        var txtGo = new GameObject("Label", typeof(Text));
        txtGo.transform.SetParent(btnGo.transform, false);
        var txt = txtGo.GetComponent<Text>();
        txt.text = "Spawn Character";
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color = Color.white;
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        var trt = txtGo.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = trt.offsetMax = Vector2.zero;
    }
}
