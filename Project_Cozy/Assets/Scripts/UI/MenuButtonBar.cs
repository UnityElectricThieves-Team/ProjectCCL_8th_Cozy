using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 화면 우하단에 세로로 쌓인 메뉴 버튼 바. 버튼↔패널 쌍을 들고, 각 버튼 클릭을
/// <see cref="UIManager.Toggle"/>에 연결한다(열려 있으면 닫고, 닫혀 있으면 연다).
/// 버튼의 hover/눌림 시각은 uGUI Button의 SpriteSwap이 담당하므로 여기서 건드리지 않는다.
///
/// 배선은 코드(AddListener)로 한다 — 인스펙터 드래그 연결은 씬 파일에 숨어 리뷰/머지에 안 보이므로.
/// 매 프레임 로직 없음.
/// </summary>
public class MenuButtonBar : MonoBehaviour
{
    [System.Serializable]
    public struct Entry
    {
        public Button button;
        public UIPanel panel;
    }

    [SerializeField] private Entry[] _entries;

    private void Awake()
    {
        foreach (var e in _entries)
            if (e.button != null)
                e.button.onClick.AddListener(() => UIManager.Instance?.Toggle(e.panel));
    }

#if UNITY_EDITOR
    private void OnValidate() // 셋업 실수(빈 배선) 조기 발견 — 코드베이스 컨벤션
    {
        if (_entries == null) return;
        foreach (var e in _entries)
            if (e.button == null || e.panel == null)
                Debug.LogWarning($"[MenuButtonBar] Entry에 빈 참조가 있습니다 ({name}).", this);
    }
#endif
}
