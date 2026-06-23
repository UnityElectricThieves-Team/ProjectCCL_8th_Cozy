using UnityEngine;

/// <summary>
/// 자식 Visual GameObject에 부착해 마우스 인터랙션을 부모 <see cref="BaseCharacterController"/>로 위임한다.
/// <see cref="InputInteractionManager"/>는 collider GameObject에서 <c>GetComponent&lt;I*&gt;</c>로 첫 컴포넌트만 잡으므로,
/// <see cref="IHoverable"/>·<see cref="IClickable"/>은 같은 GameObject의 OpaqueHoverable / ClickableEvent에 양보하고
/// 본 컴포넌트는 <see cref="IShiftRightClickable"/>(Shift+우클릭 → 친밀도 리셋)와 <see cref="IRightClickable"/>(우클릭 → 변신 토글)을 책임진다.
/// </summary>
public class CharacterInteractionRelay : MonoBehaviour, IShiftRightClickable, IRightClickable
{
    [SerializeField] private BaseCharacterController _owner;

    private void Awake()
    {
        if (_owner == null)
            _owner = GetComponentInParent<BaseCharacterController>();
    }

    void IShiftRightClickable.OnShiftRightClick()
    {
        if (_owner != null)
            _owner.Affinity.Reset();
    }

    void IRightClickable.OnRightClick()
    {
        if (_owner != null)
            _owner.RequestTransform();
    }
}
