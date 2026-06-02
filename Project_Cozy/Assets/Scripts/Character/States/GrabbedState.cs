using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>잡힘. 마우스 추종 + 좌클릭 릴리즈 시 ground 위/아래면 Idle(스냅), 아니면 Fall.</summary>
public sealed class GrabbedState : BaseCharacterState
{
    public override CharacterState Id => CharacterState.Grabbed;
    public override string Name => "Grabbed";

    private Camera _camera;

    public override void OnEnter(IStateOwner owner)
    {
        _camera = Camera.main;
    }

    public override void Tick(IStateOwner owner, float dt)
    {
        var mouse = Mouse.current;
        if (mouse == null || _camera == null) return;

        var screen = mouse.position.ReadValue();
        var world = _camera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, 0f));
        owner.SetWorldPosition(new Vector2(world.x, world.y));

        if (mouse.leftButton.wasReleasedThisFrame)
        {
            if (owner.IsFootBelowGround(out var groundTop))
            {
                owner.SnapToGround(groundTop);
                owner.ChangeState(CharacterState.Idle);
            }
            else
            {
                owner.ChangeState(CharacterState.Fall);
            }
        }
    }
}
