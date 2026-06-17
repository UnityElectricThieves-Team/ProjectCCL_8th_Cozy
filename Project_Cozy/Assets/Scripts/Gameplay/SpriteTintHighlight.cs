using UnityEngine;

/// <summary>
/// <see cref="Apply"/> 호출 시 지정된 <see cref="SpriteRenderer"/>의 tint 색을 <see cref="_color"/>로 바꿔 강조하고 info 로그를 찍는다.
/// UnityEvent 핸들러로 어디서든 사용 가능 — 현재는 <see cref="StarInputThreshold"/>의 임계 도달 반응(테스트용)으로 연결.
/// </summary>
public class SpriteTintHighlight : MonoBehaviour
{
    [SerializeField] private SpriteRenderer _spriteRenderer;
    [SerializeField] private Color _color = new Color(1f, 0.8f, 0.2f, 1f);

    private void Awake()
    {
        if (_spriteRenderer == null) _spriteRenderer = GetComponent<SpriteRenderer>();
    }

    public void Apply()
    {
        if (_spriteRenderer == null)
        {
            Debug.LogError($"[{nameof(SpriteTintHighlight)}] SpriteRenderer 참조가 없습니다.", this);
            return;
        }
        _spriteRenderer.color = _color;
        Debug.Log($"[{name}] SpriteRenderer 색을 {_color}로 변경", this);
    }
}
