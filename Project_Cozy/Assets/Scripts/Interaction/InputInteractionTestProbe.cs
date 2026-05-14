using UnityEngine;

/// <summary>
/// <see cref="InputInteractionManager"/> 연동 확인용. 이 오브젝트에 <see cref="Collider2D"/>가 있어야 하며,
/// 상호작용 스크립트는 매니저와 동일한 <see cref="GameObject"/>에 있어야 합니다.
/// </summary>
public class InputInteractionTestProbe : MonoBehaviour, IHoverable, IClickable, IShiftRightClickable
{
    [SerializeField] private string logPrefix;

    private string Prefix => string.IsNullOrEmpty(logPrefix) ? name : logPrefix;

    public void OnHoverEnter()
    {
        Debug.Log($"[{Prefix}] OnHoverEnter", this);
    }

    public void OnHoverExit()
    {
        Debug.Log($"[{Prefix}] OnHoverExit", this);
    }

    public void OnClick()
    {
        Debug.Log($"[{Prefix}] OnClick (left)", this);
    }

    public void OnShiftRightClick()
    {
        Debug.Log($"[{Prefix}] OnShiftRightClick (Shift + right)", this);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (GetComponent<Collider2D>() == null)
        {
            Debug.LogWarning(
                $"[{nameof(InputInteractionTestProbe)}] '{name}' needs a Collider2D on this GameObject (same object as this script).",
                this);
        }
    }
#endif
}
