using UnityEngine;

/// <summary>
/// 변신(동물 ↔ 소녀) 상태. 기획서 §🛡️ "상태 잠금"의 중단 불가 액션이라, <see cref="StateModule.IsLockedState"/>에
/// 이미 포함되어 진행 중 외부 Request*가 무시된다(모션 캔슬 방지).
/// <see cref="IStateOwner.TransformDuration"/> 동안 Transform 이펙트가 재생되고, 중간 지점에서 폼을 스왑
/// (파티클이 가리는 동안)한 뒤 Idle로 복귀한다.
/// </summary>
public sealed class TransformState : BaseCharacterState
{
    public override CharacterState Id => CharacterState.Transform;
    public override string Name => "Transform";

    private float _endsAt;
    private float _swapAt;
    private bool _swapped;

    public override void OnEnter(IStateOwner owner)
    {
        var dur = owner.TransformDuration;
        _endsAt = Time.time + dur;
        _swapAt = Time.time + dur * 0.5f; // 이펙트 중간에 폼 스왑
        _swapped = false;
    }

    public override void Tick(IStateOwner owner, float dt)
    {
        if (!_swapped && Time.time >= _swapAt)
        {
            var next = owner.CurrentForm == CharacterForm.Animal ? CharacterForm.Girl : CharacterForm.Animal;
            owner.SetForm(next);
            _swapped = true;
        }

        if (Time.time >= _endsAt)
            owner.ChangeState(CharacterState.Idle);
    }
}
