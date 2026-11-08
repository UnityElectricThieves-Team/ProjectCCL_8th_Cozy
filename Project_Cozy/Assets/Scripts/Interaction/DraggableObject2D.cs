using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

/// <summary>
/// 마우스 좌클릭으로 transform 위치를 드래그한다. <see cref="InputInteractionManager"/>의
/// 라우팅을 거치지 않고 자체 폴링 — 매니저는 mouse down에서 <see cref="IClickable.OnClick"/>을
/// 즉시 호출하므로, 드래그/단순클릭을 mouse up 시점에 분리해 알려주는 게 본 컴포넌트의 역할이다.
///
/// <see cref="SetBounds"/>로 영역을 받으면 transform 위치가 그 밖으로 나가지 않는다 — 끌던 대상이 경계에 걸려 멈춘다.
/// 영역이 무엇에서 왔는지는 모른다(구름은 뷰포트에서 환산해 넣어 준다).
///
/// 누르고 있는 동안은 창의 클릭 통과를 멈춘다. 경계에 걸리면 커서가 대상 밖 투명한 곳으로 나가는데,
/// 통과가 켜져 있으면 거기서 뗀 버튼이 뒤 창으로 가서 드래그가 끝나지 않기 때문이다.
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
    private WindowManager _windowManager;
    private bool _pressActive;
    private bool _isDragging;
    private Vector2 _pressStartScreen;
    private Vector3 _grabOffset;
    private Rect _bounds;
    private bool _hasBounds;

    /// <summary>이 press가 드래그였는지를 mouse up 시점에 알리는 신호 (true=드래그, false=정지 클릭).</summary>
    public event Action<bool> PressEnded;

    public bool IsDragging => _isDragging;

    /// <summary>드래그로 transform 위치가 갈 수 있는 월드 영역. 부를 때마다 바꾼다. 부르지 않으면 제한이 없다.</summary>
    public void SetBounds(Rect worldArea)
    {
        _bounds = worldArea;
        _hasBounds = true;
    }

    private void Awake()
    {
        _collider = GetComponent<Collider2D>();
        if (_camera == null) _camera = Camera.main;
        if (_cursorSource == null) _cursorSource = FindFirstObjectByType<WindowsCursorToUnityScreen>();
        _windowManager = FindFirstObjectByType<WindowManager>();
    }

    private void OnDisable()
    {
        // 누르던 중에 꺼지면 클릭 통과 정지가 남아 창이 바탕화면 클릭을 영영 흡수한다.
        if (_windowManager != null) _windowManager.ReleaseClickThroughSuspend(this);
        _pressActive = false;
        _isDragging = false;
    }

    private void Update()
    {
        var mouse = Mouse.current;
        if (mouse == null || _camera == null) return;

        var mouseScreen = ReadMouseScreen(mouse);
        var mouseWorld = _camera.ScreenToWorldPoint(mouseScreen);

        // UI(패널·버튼) 위에서 누른 press는 UI가 먹는다 — 뒤 캐릭터가 드래그로 끌려오지 않게 가드.
        // 진행 중인 드래그는 첫 프레임에만 판정하므로 끊기지 않는다. 월드 입력 잠금도 같은 자리에서 본다.
        if (mouse.leftButton.wasPressedThisFrame && !WorldInputLock.IsLocked && _collider.OverlapPoint(mouseWorld)
            && (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject()))
        {
            _pressActive = true;
            _isDragging = false;
            _pressStartScreen = mouseScreen;
            _grabOffset = transform.position - mouseWorld;
            if (_windowManager != null) _windowManager.AcquireClickThroughSuspend(this);
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
            if (_hasBounds)
            {
                target.x = Mathf.Clamp(target.x, _bounds.xMin, _bounds.xMax);
                target.y = Mathf.Clamp(target.y, _bounds.yMin, _bounds.yMax);
            }
            transform.position = target;
        }

        if (mouse.leftButton.wasReleasedThisFrame)
        {
            var wasDrag = _isDragging;
            _pressActive = false;
            _isDragging = false;
            if (_windowManager != null) _windowManager.ReleaseClickThroughSuspend(this);
            PressEnded?.Invoke(wasDrag);
        }
    }

    // 위치 소스: 빌드의 투명 클릭-통과 창에선 Mouse.current.position이 투명 픽셀 위에서 freeze되므로
    // OS 커서 기반(WindowsCursorToUnityScreen)을 우선 사용. 없거나 에디터면 Mouse.current 폴백.
    // 누르는 동안 클릭 통과를 멈추므로, 경계에 걸려 커서가 투명한 곳에서 버튼을 떼도 mouse up을 받는다.
    private Vector2 ReadMouseScreen(Mouse mouse)
    {
        if (_cursorSource != null) return _cursorSource.UnityScreenPosition;
        return mouse.position.ReadValue();
    }
}
