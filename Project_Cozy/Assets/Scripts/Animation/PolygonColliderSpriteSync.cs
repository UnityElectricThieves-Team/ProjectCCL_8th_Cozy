using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// SpriteRenderer의 sprite가 바뀔 때마다 같은 GameObject의 PolygonCollider2D의 path를
/// 현재 sprite의 Custom Physics Shape로 갱신한다. 애니메이션 프레임별 폴리곤이 다를 때 사용.
/// LateUpdate에서 sprite 변경을 감지 — Animator의 sprite 갱신(Update 단계) 직후에 동기화.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(PolygonCollider2D))]
public sealed class PolygonColliderSpriteSync : MonoBehaviour
{
    private SpriteRenderer _spriteRenderer;
    private PolygonCollider2D _polygonCollider;
    private Sprite _lastSprite;
    private readonly List<Vector2> _pointsBuffer = new List<Vector2>(64);

    private void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _polygonCollider = GetComponent<PolygonCollider2D>();
    }

    private void LateUpdate()
    {
        var current = _spriteRenderer.sprite;
        if (current == _lastSprite) return;
        _lastSprite = current;
        SyncCollider(current);
    }

    private void SyncCollider(Sprite sprite)
    {
        if (sprite == null)
        {
            _polygonCollider.pathCount = 0;
            return;
        }

        int pathCount = sprite.GetPhysicsShapeCount();
        _polygonCollider.pathCount = pathCount;

        for (int i = 0; i < pathCount; i++)
        {
            _pointsBuffer.Clear();
            sprite.GetPhysicsShape(i, _pointsBuffer);
            _polygonCollider.SetPath(i, _pointsBuffer);
        }
    }
}
