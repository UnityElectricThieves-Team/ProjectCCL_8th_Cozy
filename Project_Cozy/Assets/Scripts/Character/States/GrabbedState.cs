using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 잡힘 상태. <see cref="CharacterBasicAI2D.RequestGrab"/> 진입 후 마우스 왼쪽이 떼질 때까지 마우스 위치를 따라간다.
/// 릴리즈 시 발이 ground 안/아래면 <see cref="CharacterBasicAI2D.SnapToGround"/> 후 Idle 복귀, 아니면 Fall 진입.
/// </summary>
public sealed class GrabbedState : BaseCharacterState
{
    public override CharacterStateId Id => CharacterStateId.Grabbed;
    public override string Name => "Grabbed";

    private Camera _camera;

    public override void OnEnter(CharacterBasicAI2D owner)
    {
        _camera = Camera.main;
    }

    public override void Tick(CharacterBasicAI2D owner, float dt)
    {
        var mouse = Mouse.current;
        if (mouse == null || _camera == null) return;

        // 마우스 추종 — 캐릭터 transform.position을 마우스의 월드 좌표로 매 프레임 갱신.
        var screen = mouse.position.ReadValue();
        var world = _camera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, 0f));
        owner.SetWorldPosition(new Vector2(world.x, world.y));

        // 릴리즈 — ground 안/아래면 끌어올려 Idle, 아니면 Fall.
        if (mouse.leftButton.wasReleasedThisFrame)
        {
            if (owner.IsFootBelowGround(out var groundTop))
            {
                owner.SnapToGround(groundTop);
                owner.ChangeState(CharacterStateId.Idle);
            }
            else
            {
                owner.ChangeState(CharacterStateId.Fall);
            }
        }
    }
}
