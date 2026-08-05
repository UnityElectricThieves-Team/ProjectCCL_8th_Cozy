using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// 좌클릭을 "누른 순간"과 "누른 채 일정 시간에 도달한 순간" 두 신호로 갈라 UnityEvent로 쏜다.
///
/// <see cref="InputInteractionManager"/>의 라우팅을 쓰지 않고 자체 폴링한다. 매니저는 매 프레임
/// 커서 아래 승자를 다시 고르고 마우스 down에서 <see cref="IClickable.OnClick"/>을 한 번 쏘고 끝이라,
/// **누르고 있는 대상을 붙잡아 두지 못한다.** 홀드는 커서가 도중에 벗어나도 유지돼야 하므로
/// press 시작 시점에 고정해야 한다. <see cref="DraggableObject2D"/>가 같은 이유로 같은 방식을 쓴다.
///
/// 필요: 같은 GameObject의 <see cref="Collider2D"/>.
/// </summary>
[RequireComponent(typeof(Collider2D))]
[DisallowMultipleComponent]
public sealed class HoldClickEvent : MonoBehaviour
{
    [Tooltip("좌클릭을 이만큼 누르고 있으면 홀드로 판정해 On Hold Reached를 1회 쏜다(초).\n" +
             "이 시간 전에 떼면 On Press Start만 발생한 채로 끝난다.\n" +
             "기획 확정값은 2초지만 잡기까지가 길게 느껴져 1초로 줄였다.")]
    [SerializeField] private float _holdSeconds = 1f;

    [Tooltip("알파 검사를 맡길 컴포넌트. 지정하면 불투명 픽셀 위에서 누른 것만 인정한다.\n" +
             "비우면 같은 GameObject에서 자동 탐색하고, 그래도 없으면 콜라이더 안이기만 하면 인정한다.")]
    [SerializeField] private OpaqueHoverable _opaqueGate;

    [Tooltip("클릭 좌표 소스. 빌드의 투명 클릭-통과 창에선 Mouse.current가 얼어붙으므로\n" +
             "OS 커서 기반 좌표를 쓴다. 비우면 Awake에서 자동 탐색, 없으면 Mouse.current 폴백.")]
    [SerializeField] private WindowsCursorToUnityScreen _cursorSource;

    [Tooltip("화면 좌표를 월드로 바꿀 카메라. 비우면 Awake에서 Camera.main.")]
    [SerializeField] private Camera _camera;

    [Header("Events")]
    [Tooltip("캐릭터 위에서 좌클릭을 누른 순간 1회 발사. 쓰담은 여기서 시작한다.")]
    [SerializeField] private UnityEvent _onPressStart;

    [Tooltip("누른 채로 Hold Seconds에 도달한 순간 1회 발사. 잡기는 여기서 시작한다.")]
    [SerializeField] private UnityEvent _onHoldReached;

    private Collider2D _collider;
    private bool _pressActive;
    private bool _holdFired;
    private float _pressStartedAt;
    private Vector2 _pressWorld;

    /// <summary>지금 누르고 있는 press가 **시작된 순간**의 커서 월드 좌표. 누르고 있지 않으면 false.
    ///
    /// 잡기 오프셋을 이 좌표로 재라고 내주는 것이다. 홀드가 완료된 순간의 커서로 재면,
    /// 누른 채 커서를 옮긴 거리가 그대로 굳어 캐릭터가 커서에서 떨어진 채 끌려다닌다.</summary>
    public bool TryGetPressWorld(out Vector2 world)
    {
        world = _pressWorld;
        return _pressActive;
    }

    private void Awake()
    {
        _collider = GetComponent<Collider2D>();
        if (_camera == null) _camera = Camera.main;
        if (_opaqueGate == null) _opaqueGate = GetComponent<OpaqueHoverable>();
        if (_cursorSource == null) _cursorSource = FindFirstObjectByType<WindowsCursorToUnityScreen>();
    }

    private void Update()
    {
        var mouse = Mouse.current;
        if (mouse == null || _camera == null) return;

        if (!_pressActive && mouse.leftButton.wasPressedThisFrame && TryGetPressPoint(mouse, out var pressWorld))
        {
            _pressActive = true;
            _holdFired = false;
            _pressStartedAt = Time.time;
            _pressWorld = pressWorld;
            _onPressStart?.Invoke();
        }

        if (!_pressActive) return;

        // 커서가 도중에 캐릭터를 벗어나도 홀드는 유지된다 — 잡으려고 누른 손을 놓지 않았기 때문이다.
        if (!_holdFired && Time.time - _pressStartedAt >= _holdSeconds)
        {
            _holdFired = true;
            _onHoldReached?.Invoke();
        }

        if (mouse.leftButton.wasReleasedThisFrame)
        {
            _pressActive = false;
            _holdFired = false;
        }
    }

    /// <summary>지금 커서가 이 오브젝트 위에 있으면 true와 함께 그 월드 좌표를 돌려준다.</summary>
    private bool TryGetPressPoint(Mouse mouse, out Vector2 world)
    {
        world = Vector2.zero;

        // UI(패널·버튼) 위에서 누른 press는 UI가 먹는다 — 뒤에 있는 캐릭터가 반응하지 않게 막는다.
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return false;

        // 투명 픽셀 위에서 눌러도 반응하면 캐릭터 옆 빈 공간을 눌렀는데 쓰담이 뜬다.
        if (_opaqueGate != null && !_opaqueGate.IsOpaqueHovered) return false;

        var screen = _cursorSource != null ? _cursorSource.UnityScreenPosition : mouse.position.ReadValue();
        var point = _camera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, 0f));
        world = new Vector2(point.x, point.y);

        return _collider.OverlapPoint(world);
    }
}
