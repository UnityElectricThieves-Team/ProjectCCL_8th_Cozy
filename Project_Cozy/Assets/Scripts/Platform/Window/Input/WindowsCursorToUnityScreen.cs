// ============================================================
// WindowsCursorToUnityScreen
//
// "지금 커서가 유니티 스크린 좌표로 어디인가"의 단일 소스.
// 소비자(InputInteractionManager, OpaqueHoverable)는 UnityScreenPosition만 읽으면 된다.
//
// 왜 필요한가:
//   투명 클릭-통과 창에선, 커서가 빈 영역 위에 있으면 마우스 이동이 뒤 창으로
//   통과해 우리 창엔 안 온다 → Unity의 Mouse.current.position이 "마지막 위치"에 멈춘다(freeze).
//   그래서 호버 이탈을 못 잡는다(예: Pet에서 안 빠져나옴). Win32 GetCursorPos는 포커스·투명과
//   무관하게 항상 진짜 커서 좌표를 주므로, 빌드에선 이걸 쓴다.
//
// WindowManager가 데스크톱 전역 커서를 현재 창 기준 Unity 화면 좌표로 변환한다.
// 월드 좌표는 카메라가 필요하므로 소비자가 ScreenToWorldPoint로 처리한다.
//
// 폴백은 여기 한 곳에 모은다: 에디터, 또는 빌드에서 HWND가 아직 준비 안 됐으면 Mouse.current 사용.
// HWND 접근과 좌표 변환은 신 창 스택의 WindowManager에 위임한다.
// ============================================================
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class WindowsCursorToUnityScreen : MonoBehaviour
{
    [SerializeField, Tooltip("OS 커서 좌표 소스. 비우면 Awake에서 씬에서 자동 탐색.")]
    private WindowManager _windowManager;

    /// <summary>현재 커서의 유니티 스크린 좌표(픽셀, 좌하단 원점). 빌드는 OS 커서 기반, 그 외엔 Mouse.current.</summary>
    public Vector2 UnityScreenPosition { get; private set; }

    private void Awake()
    {
        if (_windowManager == null) _windowManager = FindFirstObjectByType<WindowManager>();
    }

    private void Update()
    {
#if !UNITY_EDITOR
        // 빌드: OS 커서 우선. WindowManager가 아직 준비되지 않았으면 아래 폴백으로 떨어진다.
        if (_windowManager != null && _windowManager.TryGetUnityScreenCursorPosition(out Vector2 position))
        {
            UnityScreenPosition = position;
            return;
        }
#endif
        // 에디터, 또는 빌드에서 HWND 미준비 시 — Mouse.current 폴백 (한 곳에서만)
        var mouse = Mouse.current;
        if (mouse != null) UnityScreenPosition = mouse.position.ReadValue();
    }
}
