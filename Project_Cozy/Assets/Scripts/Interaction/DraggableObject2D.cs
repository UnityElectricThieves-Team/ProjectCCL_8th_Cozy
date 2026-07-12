using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

/// <summary>
/// 마우스 좌클릭으로 transform 위치를 드래그한다. <see cref="InputInteractionManager"/>의
/// 라우팅을 거치지 않고 자체 폴링 — 매니저는 mouse down에서 <see cref="IClickable.OnClick"/>을
/// 즉시 호출하므로, 드래그/단순클릭을 mouse up 시점에 분리해 알려주는 게 본 컴포넌트의 역할이다.
///
/// 필요: 같은 GameObject의 <see cref="Collider2D"/>.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public sealed class DraggableObject2D : MonoBehaviour
{
    [SerializeField] private Camera _camera;
    [Tooltip("이만큼 픽셀 이상 마우스가 움직이면 드래그로 간주.")]
    [SerializeField] private float _dragThresholdPixels = 5f;
    [Tooltip("드래그 좌표 소스. 빌드의 투명 클릭-통과 창에선 Mouse.current가 freeze되므로 OS 커서 기반 좌표를 쓴다. 비우면 Awake에서 자동 탐색, 없으면 Mouse.current 폴백.")]
    [SerializeField] private WindowsCursorToUnityScreen _cursorSource;

    private Collider2D _collider;
    private bool _pressActive;
    private bool _isDragging;
    private Vector2 _pressStartScreen;
    private Vector3 _grabOffset;

    /// <summary>이 press가 드래그였는지를 mouse up 시점에 알리는 신호 (true=드래그, false=정지 클릭).</summary>
    public event Action<bool> PressEnded;

    public bool IsDragging => _isDragging;

    private void Awake()
    {
        _collider = GetComponent<Collider2D>();
        if (_camera == null) _camera = Camera.main;
        if (_cursorSource == null) _cursorSource = FindFirstObjectByType<WindowsCursorToUnityScreen>();
    }

    private void Update()
    {
        var mouse = Mouse.current;
        if (mouse == null || _camera == null) return;

        var mouseScreen = ReadMouseScreen(mouse);
        var mouseWorld = _camera.ScreenToWorldPoint(mouseScreen);

        // UI(패널·버튼) 위에서 누른 press는 UI가 먹는다 — 뒤 캐릭터가 드래그로 끌려오지 않게 가드.
        // 진행 중인 드래그는 첫 프레임에만 판정하므로 끊기지 않는다.
        if (mouse.leftButton.wasPressedThisFrame && _collider.OverlapPoint(mouseWorld)
            && (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject()))
        {
            _pressActive = true;
            _isDragging = false;
            _pressStartScreen = mouseScreen;
            _grabOffset = transform.position - mouseWorld;
        }

        if (!_pressActive) return;

        if (!_isDragging && Vector2.Distance(mouseScreen, _pressStartScreen) > _dragThresholdPixels)
        {
            _isDragging = true;
        }

        if (_isDragging)
        {
            var target = mouseWorld + _grabOffset;
            target.z = transform.position.z;
            transform.position = target;
        }

        if (mouse.leftButton.wasReleasedThisFrame)
        {
            var wasDrag = _isDragging;
            _pressActive = false;
            _isDragging = false;
            PressEnded?.Invoke(wasDrag);
        }
    }

    // 위치 소스: 빌드의 투명 클릭-통과 창에선 Mouse.current.position이 투명 픽셀 위에서 freeze되므로
    // OS 커서 기반(WindowsCursorToUnityScreen)을 우선 사용. 없거나 에디터면 Mouse.current 폴백.
    // 알려진 한계: 버튼을 '투명 픽셀 위'에서 떼면 WM_LBUTTONUP이 뒤 창으로 가서 mouse up을 못 받아
    // 드래그가 안 끝날 수 있다. 현재는 화면경계 클램프가 없어 대상이 늘 커서를 따라오므로 release가
    // 불투명 픽셀 위에서 일어나 거의 발생하지 않는다. 클램프 도입 시 전역 버튼-업 신호가 필요.
    private Vector2 ReadMouseScreen(Mouse mouse)
    {
        if (_cursorSource != null) return _cursorSource.UnityScreenPosition;
        return mouse.position.ReadValue();
    }
}
