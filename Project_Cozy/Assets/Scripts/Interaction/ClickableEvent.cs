using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// <see cref="IClickable"/> 구현 + UnityEvent 노출만 하는 단순 어댑터.
/// 클릭 라우팅은 <see cref="InputInteractionManager"/>가 같은 GameObject의 Collider2D를 통해 수행하므로,
/// 이 컴포넌트는 콜라이더가 있는 GameObject(예: Visual + PolygonCollider2D)에 부착해야 한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class ClickableEvent : MonoBehaviour, IClickable
{
    [SerializeField] private UnityEvent _onClick;

    public void OnClick() => _onClick?.Invoke();

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (GetComponent<Collider2D>() == null)
        {
            Debug.LogWarning(
                $"[{nameof(ClickableEvent)}] '{name}' needs a Collider2D on this GameObject for InputInteractionManager to find it.",
                this);
        }
    }
#endif
}
