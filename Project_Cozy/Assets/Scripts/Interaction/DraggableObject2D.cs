using System;
using UnityEngine;
using UnityEngine.InputSystem;

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
    }

    private void Update()
    {
        var mouse = Mouse.current;
        if (mouse == null || _camera == null) return;

        var mouseScreen = mouse.position.ReadValue();
        var mouseWorld = _camera.ScreenToWorldPoint(mouseScreen);

        if (mouse.leftButton.wasPressedThisFrame && _collider.OverlapPoint(mouseWorld))
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
}
