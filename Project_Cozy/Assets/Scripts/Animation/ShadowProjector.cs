using UnityEngine;

/// <summary>
/// 캐릭터 Visual 아래로 수직 raycast를 쏴서, ground hit 위치에 그림자를 배치한다.
/// 그림자의 X폭은 Visual 폭 × (거리에 따라 작아지는 팩터)로 계산.
/// 거리 ≥ <see cref="_maxVisibleDistance"/>면 SpriteRenderer를 끄는 방식으로 숨김.
///
/// 사용 조건: 같은 GameObject에 <see cref="SpriteRenderer"/> 필요, Draw Mode = Sliced/Tiled
/// (size를 코드로 갱신하려면 Simple이 아니어야 한다).
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public sealed class ShadowProjector : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField, Tooltip("따라갈 Visual의 SpriteRenderer. 비우면 부모의 'Visual' 자식 자동 탐색.")]
    private SpriteRenderer _visualToFollow;
    [SerializeField, Tooltip("ground로 간주할 레이어.")]
    private LayerMask _groundLayerMask;

    [Header("Probe")]
    [SerializeField, Tooltip("raycast 최대 거리. 이 거리 안에 ground 없으면 그림자 숨김.")]
    private float _maxProbeDistance = 100f;

    [Header("Visibility & Size")]
    [SerializeField, Tooltip("Visual 발 ~ ground 거리가 이 값 이상이면 그림자 숨김.")]
    private float _maxVisibleDistance = 3f;
    [SerializeField, Tooltip("그림자 Y 두께 (월드 단위).")]
    private float _height = 0.1f;
    [SerializeField, Tooltip("ground 위/아래 미세 조정 (월드 단위, 양수=위).")]
    private float _yOffset = 0f;

    private SpriteRenderer _sr;

    private void Awake()
    {
        _sr = GetComponent<SpriteRenderer>();

        // _visualToFollow 자동 탐색 — 부모의 "Visual" 자식
        if (_visualToFollow == null && transform.parent != null)
        {
            var visualTransform = transform.parent.Find("Visual");
            if (visualTransform != null)
                _visualToFollow = visualTransform.GetComponent<SpriteRenderer>();
        }
    }

    private void LateUpdate()
    {
        if (_visualToFollow == null)
        {
            SetVisible(false);
            return;
        }

        // Visual의 중심 X에서 아래로 raycast
        var visualBounds = _visualToFollow.bounds;
        var origin = new Vector2(visualBounds.center.x, visualBounds.center.y);
        var hit = Physics2D.Raycast(origin, Vector2.down, _maxProbeDistance, _groundLayerMask);
        if (hit.collider == null)
        {
            SetVisible(false);
            return;
        }

        // 거리 = Visual 발(bounds 하단) ~ ground hit
        var distance = Mathf.Max(0f, visualBounds.min.y - hit.point.y);
        if (distance >= _maxVisibleDistance)
        {
            SetVisible(false);
            return;
        }

        SetVisible(true);

        // 위치 — X는 Visual 중심, Y는 ground hit (+ offset). Z 보존.
        var p = transform.position;
        transform.position = new Vector3(visualBounds.center.x, hit.point.y + _yOffset, p.z);

        // X 폭 — Visual 폭 × (1 - distance/maxDist). 발 끝에 붙으면 1.0, 멀어질수록 0.
        var widthFactor = 1f - distance / _maxVisibleDistance;
        var worldWidth = visualBounds.size.x * widthFactor;

        // SpriteRenderer.size는 local 단위 — 부모 lossyScale로 보정해 결과 월드 크기 일정.
        var lossy = transform.lossyScale;
        var localWidth = worldWidth / Mathf.Max(0.0001f, lossy.x);
        var localHeight = _height / Mathf.Max(0.0001f, lossy.y);
        _sr.size = new Vector2(localWidth, localHeight);
    }

    private void SetVisible(bool visible)
    {
        if (_sr.enabled != visible) _sr.enabled = visible;
    }
}
