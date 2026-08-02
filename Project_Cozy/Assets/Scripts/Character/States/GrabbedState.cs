using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>잡힘. 잡은 지점을 유지한 채 마우스를 따라간다. 좌클릭 릴리즈 시 바닥이면 Idle, 아니면 Fall.</summary>
public sealed class GrabbedState : BaseCharacterState
{
    public override CharacterState Id => CharacterState.Grabbed;
    public override string Name => "Grabbed";

    private Camera _camera;

    // 잡은 순간의 커서→루트 오프셋. 루트를 커서에 그대로 붙이면, 루트가 발이므로 커서에 발바닥이 붙고
    // 몸 전체가 커서 위로 튀어오른다. 잡은 자리를 그대로 들고 다니게 하려면 이 오프셋이 필요하다.
    private Vector2 _grabOffset;

    public override void OnEnter(IStateOwner owner)
    {
        _camera = Camera.main;
        _grabOffset = Vector2.zero;

        if (!TryGetMouseWorld(out var world)) return;
        _grabOffset = owner.WorldPosition - world;
    }

    public override void Tick(IStateOwner owner, float dt)
    {
        var mouse = Mouse.current;
        if (mouse == null || !TryGetMouseWorld(out var world)) return;

        owner.SetWorldPosition(world + _grabOffset);

        if (mouse.leftButton.wasReleasedThisFrame)
            owner.ChangeState(owner.IsFootOnGround() ? CharacterState.Idle : CharacterState.Fall);
    }

    private bool TryGetMouseWorld(out Vector2 world)
    {
        world = Vector2.zero;
        var mouse = Mouse.current;
        if (mouse == null || _camera == null) return false;

        var screen = mouse.position.ReadValue();
        var p = _camera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, 0f));
        world = new Vector2(p.x, p.y);
        return true;
    }
}
