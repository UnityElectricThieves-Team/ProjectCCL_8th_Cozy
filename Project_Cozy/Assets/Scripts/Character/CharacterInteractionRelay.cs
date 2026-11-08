using UnityEngine;

/// <summary>
/// 자식 Visual GameObject에 부착해 마우스 인터랙션을 부모 <see cref="BaseCharacterController"/>로 위임한다.
/// <see cref="InputInteractionManager"/>는 collider GameObject에서 <c>GetComponent&lt;I*&gt;</c>로 첫 컴포넌트만 잡으므로,
/// <see cref="IHoverable"/>·<see cref="IClickable"/>은 같은 GameObject의 OpaqueHoverable / ClickableEvent에 양보하고
/// 본 컴포넌트는 변신 두 방향을 책임진다 — 우클릭은 동물 → 소녀(<see cref="IRightClickable"/>),
/// Shift+우클릭은 소녀 → 동물(<see cref="IShiftRightClickable"/>). 매니저는 Shift가 눌려 있으면 둘 중 뒤쪽만 부른다.
/// </summary>
public class CharacterInteractionRelay : MonoBehaviour, IRightClickable, IShiftRightClickable
{
    [SerializeField] private BaseCharacterController _owner;

    private void Awake()
    {
        if (_owner == null)
            _owner = GetComponentInParent<BaseCharacterController>();
    }

    void IRightClickable.OnRightClick()
    {
        if (_owner != null)
            _owner.RequestTransform();
    }

    void IShiftRightClickable.OnShiftRightClick()
    {
        if (_owner != null)
            _owner.RequestRevertToAnimal();
    }
}
