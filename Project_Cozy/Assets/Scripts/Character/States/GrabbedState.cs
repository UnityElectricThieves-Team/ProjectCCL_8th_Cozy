using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>잡힘. 잡은 지점을 유지한 채 마우스를 따라간다. 좌클릭을 떼면 낙하한다.</summary>
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

        // 기준은 **누른 순간**의 커서다. 홀드가 완료된 순간으로 재면, 누른 채 커서를 옮긴 거리가
        // 그대로 오프셋에 굳어 캐릭터가 커서에서 떨어진 채 끌려다닌다. 누른 순간을 기준으로 하면
        // 잡히는 순간 캐릭터가 커서 쪽으로 따라붙고, 그 뒤로는 누른 지점이 커서 아래 유지된다.
        if (owner.TryGetPressAnchor(out var anchor))
        {
            _grabOffset = owner.WorldPosition - anchor;
            return;
        }

        // 누르기를 거치지 않고 들어온 경우(코드가 RequestGrab을 직접 호출) — 지금 커서로 잰다.
        if (TryGetMouseWorld(out var world))
            _grabOffset = owner.WorldPosition - world;
    }

    public override void Tick(IStateOwner owner, float dt)
    {
        var mouse = Mouse.current;
        if (mouse == null || !TryGetMouseWorld(out var world)) return;

        owner.SetWorldPosition(world + _grabOffset);

        // 놓으면 무조건 낙하다 — 발이 이미 바닥에 있었는지 따지지 않는다(확정안).
        // 바닥에 붙은 채 놓으면 Fall이 같은 프레임에 접지를 보고 Land로 넘어간다.
        if (mouse.leftButton.wasReleasedThisFrame)
            owner.ChangeState(CharacterState.Fall);
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
