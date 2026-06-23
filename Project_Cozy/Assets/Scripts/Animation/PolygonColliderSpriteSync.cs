using System.Collections.Generic;
using UnityEngine;

// ============================================================
// <deprecated>
// 캐릭터 파이프라인에선 더 이상 사용하지 않습니다 (2026-06부터).
//
// 마우스 판정 영역을 보이는 모양과 일치시키는 목적은, 이제
//   "고정 BoxCollider2D(매니저 라우팅용) + OpaqueHoverable의 픽셀 알파 검사(정밀 판정)"
// 조합으로 처리합니다. 그게 픽셀 단위로 정확하고, 프레임마다 physics shape를
// 일일이 생성/유지해야 하는 이 컴포넌트의 fragile함을 없앱니다.
//
// 주의: Character.prefab에서는 제거 예정이지만, Star/StarKK.prefab은 아직 이 컴포넌트를
// 참조합니다. 그래서 파일은 삭제하지 말 것 — 그쪽까지 정리된 뒤에 삭제 검토.
// 새 캐릭터 작업에는 사용하지 마세요.
// </deprecated>
// ============================================================

/// <summary>
/// [DEPRECATED — 위 주석 참고] SpriteRenderer의 sprite가 바뀔 때마다 같은 GameObject의 PolygonCollider2D의 path를
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
