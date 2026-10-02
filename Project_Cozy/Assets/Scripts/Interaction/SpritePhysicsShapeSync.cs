using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 지금 보이는 스프라이트 프레임의 외곽선(physics shape)을 같은 GameObject의 <see cref="PolygonCollider2D"/>에 옮겨 담는다.
/// 판정 크기를 숫자로 들지 않고 그림에서 유도하기 위한 것이다 — 그림이 바뀌면(애니메이션 프레임, 좌우 반전) 판정도 따라 바뀐다.
///
/// 이 콜라이더는 1차 거름이다. 바탕화면 클릭 통과와 마우스 라우팅이 이 모양을 본다.
/// 호버·꾹 누르기·쓰담의 최종 판정은 <see cref="OpaqueHoverable"/>의 픽셀 알파 검사가 한다.
/// 둘은 기준이 다르므로(외곽선 근사 vs 알파 임계값) 서로 맞추려고 튜닝하지 않는다.
/// 외곽선의 정밀도는 그림 임포트 설정(Sprite Editor의 Custom Physics Shape / Outline Tolerance)에서 정한다.
///
/// 콜라이더는 인터랙터블(IClickable 등)과 같은 GameObject에 있어야 하므로, 그림이 자식에 있는 구조(별)도 있다.
/// 그 경우 <see cref="_spriteRenderer"/>를 인스펙터에서 연결한다.
/// 전제: 실행 중 그림과 콜라이더 사이의 상대 위치·배율은 변하지 않는다 — 외곽선은 프레임·반전이 바뀔 때만 다시 담는다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PolygonCollider2D))]
public sealed class SpritePhysicsShapeSync : MonoBehaviour
{
    [SerializeField, Tooltip("외곽선을 가져올 그림. 비우면 같은 GameObject에서 찾는다. 그림이 다른 GameObject(예: 자식 Visual)에 있으면 반드시 연결.")]
    private SpriteRenderer _spriteRenderer;

    private PolygonCollider2D _collider;
    private readonly List<Vector2> _points = new List<Vector2>(32);
    private Sprite _lastSprite;
    private bool _lastFlipX;
    private bool _lastFlipY;
    private bool _dirty;

    private void Awake()
    {
        _collider = GetComponent<PolygonCollider2D>();
        _spriteRenderer = ResolveRenderer();
    }

    private void OnEnable()
    {
        // 다시 켜질 때 그 사이 바뀐 프레임을 놓치지 않도록, 그리고 첫 프레임에 빈 콜라이더가 없도록 즉시 담는다.
        _dirty = true;
        Sync();
    }

    // Animator·SpriteAnimator가 프레임을 바꾼 뒤에 본다. 바뀐 게 없으면 비교 몇 번으로 끝난다.
    private void LateUpdate() => Sync();

    private void Sync()
    {
        if (_spriteRenderer == null) return;

        Sprite sprite = _spriteRenderer.sprite;
        bool flipX = _spriteRenderer.flipX;
        bool flipY = _spriteRenderer.flipY;
        if (!_dirty && sprite == _lastSprite && flipX == _lastFlipX && flipY == _lastFlipY) return;

        _dirty = false;
        _lastSprite = sprite;
        _lastFlipX = flipX;
        _lastFlipY = flipY;

        int shapeCount = sprite != null ? sprite.GetPhysicsShapeCount() : 0;
        _collider.pathCount = shapeCount;
        if (shapeCount == 0) return;

        // 외곽선은 그림(렌더러) 로컬 좌표다. 반전을 먼저 적용하고, 콜라이더 로컬로 옮긴다.
        // 같은 GameObject면 단위 행렬이다.
        Matrix4x4 rendererToCollider = transform.worldToLocalMatrix * _spriteRenderer.transform.localToWorldMatrix;
        for (int i = 0; i < shapeCount; i++)
        {
            sprite.GetPhysicsShape(i, _points);
            for (int j = 0; j < _points.Count; j++)
            {
                Vector2 flipped = SpriteFlip.Apply(_points[j], flipX, flipY);
                _points[j] = rendererToCollider.MultiplyPoint3x4(flipped);
            }
            _collider.SetPath(i, _points);
        }
    }

    private SpriteRenderer ResolveRenderer() =>
        _spriteRenderer != null ? _spriteRenderer : GetComponent<SpriteRenderer>();

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (ResolveRenderer() == null)
        {
            Debug.LogWarning(
                $"[{nameof(SpritePhysicsShapeSync)}] '{name}' has no SpriteRenderer. Assign _spriteRenderer when the sprite is on another GameObject.",
                this);
        }
    }
#endif
}
