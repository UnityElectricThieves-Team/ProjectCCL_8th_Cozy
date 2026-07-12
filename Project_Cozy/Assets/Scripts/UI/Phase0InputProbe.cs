using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// [일회용 — Phase 0 검증 후 삭제] 투명 오버레이에서 uGUI 입력이 도달하는지 확인하는 디버그 프로브.
/// 빌드 실행 후, 커서를 빈 바탕화면을 거쳐 버튼 위로 옮겨 클릭하며 라벨 숫자를 본다:
///   - Clicks가 오르면  → (a) 버튼 클릭(=버튼 down) 수신 OK
///   - OverUI가 true면   → (b) EventSystem.IsPointerOverGameObject OK
/// 둘 다 OK면 Branch A(표준 uGUI) 확정.
/// </summary>
public class Phase0InputProbe : MonoBehaviour
{
    [SerializeField] private Button _button;   // 테스트 버튼
    [SerializeField] private TMP_Text _label;  // 결과 표시 라벨

    private int _clicks;

    private void Awake()
    {
        if (_button != null) _button.onClick.AddListener(() => _clicks++);
    }

    private void Update()
    {
        if (_label == null) return;
        bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        _label.text = $"Clicks: {_clicks}\nOverUI: {overUI}";
    }
}
