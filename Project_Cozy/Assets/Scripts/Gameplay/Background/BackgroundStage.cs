using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 배경 프리팹 하나를 뷰포트 아래 변에 깐다. 아트가 만든 배경 프리팹을 크기를 바꾸지 않고 그대로 쓰며,
/// 그림 전체의 아래 끝을 뷰포트 아래 변에, 가로 가운데를 <b>방 가운데</b>(<see cref="ViewportScreenSettings.RoomCenterX"/>)에 맞춘다.
/// 방 가운데는 뷰포트를 통째로 옮길 때만 따라가므로, 변을 끌어 크기를 바꾸면 그림은 제자리이고 마스크(창)만 바뀌어
/// 보이는 범위가 넓어지거나 좁아진다. 놓인 장식도 같은 방 가운데를 기준으로 놓여 함께 움직인다.
///
/// 예외는 아트가 Draw Mode를 <b>Sliced</b>로 고른 층이다. 아트가 9-slice 테두리로 늘일 곳을 정해 둔 그림이라,
/// 그 층의 가로 크기(<see cref="SpriteRenderer.size"/>의 x)만 뷰포트 폭에 맞춰 양 끝을 뷰포트 변에 붙인다. 세로는 아트 값 그대로다.
/// Sliced 층이 있는 프리팹은 가로 가운데를 방 가운데가 아니라 뷰포트 가운데에 맞춘다.
///
/// 이 컴포넌트는 <b>배치만</b> 책임진다 — 어떤 프리팹을 깔지는 밖에서 <see cref="SetPrefab"/>으로 넣어 준다(상점·배경 시스템을 모른다).
/// 프리팹이 없으면 아무것도 그리지 않아 바탕화면이 그대로 비친다.
///
/// 아트 프리팹은 타일 개수가 고정이라 뷰포트보다 넓게 뻗어 있다. 창은 작업 영역 전체를 덮으므로 그대로 두면
/// 뷰포트 밖 바탕화면 위에도 배경이 그려진다. 그래서 뷰포트 크기의 SpriteMask를 하나 두고, 프리팹의 렌더러(파티클 포함)를
/// 그 안에서만 보이게 한다. 마스크 그림은 에셋으로 두지 않고 흰 텍스처로 만든다.
///
/// 배치는 뷰포트가 적용될 때(<see cref="ViewportScreenSettings.ViewportConfirmed"/>), 편집 중 프리뷰가 바뀔 때
/// (<see cref="ViewportScreenSettings.PreviewChanged"/>), 프리팹이 바뀔 때만 다시 계산한다. 프리뷰를 따라가는 것은
/// 기획 요청이다("뷰포트를 옮기는 즉시 배경도 같이") — 끄는 동안 저장했을 때의 배경을 미리 보여 준다.
/// 마스크는 프리뷰 뷰포트를, 그림의 가로는 프리뷰 방 가운데(<see cref="ViewportScreenSettings.PreviewRoomCenterX"/>)를 따른다.
/// 평소에 매 프레임 하는 일은 없다(프리팹 안의 파티클은 제 갈 길을 간다).
///
/// 콜라이더를 붙이지 않는다. <see cref="WindowManager"/>가 커서 아래 콜라이더 유무로 클릭 통과를 끄기 때문에,
/// 콜라이더가 있으면 배경 전체에서 바탕화면 클릭이 막힌다.
///
/// 배경 프리팹 루트에 붙는다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BackgroundStage : MonoBehaviour
{
    // 마스크용 흰 사각형 스프라이트의 PPU. 텍스처 한 변과 같게 두면 스프라이트 한 장이 월드 1칸이라, 배율이 곧 크기가 된다.
    private const int MASK_SPRITE_SIZE = 4;

    private readonly List<SpriteRenderer> _spriteRenderers = new();
    private readonly List<SpriteRenderer> _slicedRenderers = new(); // 가로를 뷰포트 폭에 맞출 층(_spriteRenderers에도 들어 있다)

    private ViewportScreenSettings _viewportSettings;
    private BaseSpaceCameraFitter _cameraFitter;
    private SpriteMask _mask;
    private Transform _inactiveHolder; // 프리팹을 꺼진 채로 만들기 위한 꺼진 부모
    private GameObject _instance;

    private Rect _area;      // 지금 따르는 뷰포트(적용된 뷰포트 또는 편집 프리뷰)의 월드 사각형 — 마스크와 아래 끝
    private float _roomCenterWorldX; // 지금 따르는 방 가운데의 월드 x — 그림의 가로 가운데
    private bool _hasArea;

    private void Awake()
    {
        var maskObject = new GameObject("ViewportMask");
        maskObject.transform.SetParent(transform, false);
        _mask = maskObject.AddComponent<SpriteMask>();
        _mask.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, MASK_SPRITE_SIZE, MASK_SPRITE_SIZE),
            new Vector2(0.5f, 0.5f), MASK_SPRITE_SIZE);
        // 범위를 두지 않으면 씬의 모든 VisibleInsideMask 렌더러가 이 마스크를 탄다. 배경의 정렬 범위만 자르게 한다(SetPrefab에서 채움).
        _mask.isCustomRangeActive = true;
        _mask.enabled = false;

        var holderObject = new GameObject("InactiveHolder");
        holderObject.transform.SetParent(transform, false);
        holderObject.SetActive(false);
        _inactiveHolder = holderObject.transform;
    }

    private void Start()
    {
        // 프리팹은 씬 오브젝트를 인스펙터로 참조할 수 없고, 인스턴스에서 걸면 씬 override가 남는다.
        // 그래서 Start에서 한 번만 찾는다.
        _viewportSettings = FindFirstObjectByType<ViewportScreenSettings>();
        _cameraFitter = FindFirstObjectByType<BaseSpaceCameraFitter>();

        if (_viewportSettings == null || _cameraFitter == null)
        {
            Debug.LogWarning($"[{nameof(BackgroundStage)}] ViewportScreenSettings/BaseSpaceCameraFitter 없음 — 배경을 그리지 않습니다.", this);
            enabled = false;
            return;
        }

        _viewportSettings.ViewportConfirmed += OnViewportChanged;
        _viewportSettings.PreviewChanged += OnViewportChanged;

        // 초기 적용이 이미 끝난 뒤에 이 컴포넌트가 붙었을 수 있다(첫 확정 신호는 이미 지나갔으면 다시 오지 않는다).
        if (_viewportSettings.IsReady) OnViewportChanged(_viewportSettings.PreviewViewport);
    }

    private void OnDestroy()
    {
        if (_viewportSettings != null)
        {
            _viewportSettings.ViewportConfirmed -= OnViewportChanged;
            _viewportSettings.PreviewChanged -= OnViewportChanged;
        }
        if (_mask != null) Destroy(_mask.sprite); // 코드로 만든 스프라이트는 씬과 함께 정리되지 않는다
    }

    /// <summary>깔 배경 프리팹을 바꾼다. null이면 배경을 지운다.</summary>
    public void SetPrefab(GameObject prefab)
    {
        if (_instance != null) Destroy(_instance);
        _instance = null;
        _spriteRenderers.Clear();
        _slicedRenderers.Clear();

        if (prefab != null)
        {
            // 첫 배치가 끝나기 전에는 그리지 않는다. 카메라가 프레이밍되기 전 몇 프레임 동안 엉뚱한 자리에 보이는 깜빡임을 막는다.
            // 꺼진 부모 아래에서 만들어 끈 뒤 옮긴다 — 켜진 채로 만들면 파티클의 Awake·자동 재생이 한 번 돌고 나서 꺼진다.
            _instance = Instantiate(prefab, _inactiveHolder);
            _instance.SetActive(false);
            _instance.transform.SetParent(transform, false);

            // 마스크가 배경의 정렬 범위만 자르도록 렌더러들의 Order in Layer 범위를 모은다. 배경은 Default 레이어만 쓴다(background.md).
            int minOrder = int.MaxValue, maxOrder = int.MinValue;
            var renderers = _instance.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                minOrder = Mathf.Min(minOrder, renderers[i].sortingOrder);
                maxOrder = Mathf.Max(maxOrder, renderers[i].sortingOrder);

                if (renderers[i] is SpriteRenderer sprite)
                {
                    sprite.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
                    _spriteRenderers.Add(sprite);
                    if (sprite.drawMode == SpriteDrawMode.Sliced) _slicedRenderers.Add(sprite);
                }
                else if (renderers[i] is ParticleSystemRenderer particle)
                {
                    particle.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
                }
            }

            if (renderers.Length > 0)
            {
                // 마스크는 back보다 크고 front 이하인 Order를 자른다. back은 범위에 들지 않으므로 하나 아래로 둔다.
                int defaultLayer = SortingLayer.NameToID("Default");
                _mask.frontSortingLayerID = defaultLayer;
                _mask.backSortingLayerID = defaultLayer;
                _mask.frontSortingOrder = maxOrder;
                _mask.backSortingOrder = minOrder - 1;
            }

            if (_spriteRenderers.Count == 0)
                Debug.LogWarning($"[{nameof(BackgroundStage)}] 배경 프리팹 '{prefab.name}'에 SpriteRenderer가 없어 바닥을 맞출 수 없습니다.", prefab);
        }

        Relayout();
    }

    private void OnViewportChanged(RectInt viewportPx)
    {
        _area = _cameraFitter.BaseRectToWorld(viewportPx, _viewportSettings.BaseSpaceSize);
        // 방 가운데는 위치라 앵커를 거친다. 신호가 울릴 때는 이미 새 값이다(ViewportScreenSettings). 장식 레이어도 같은 속성을 읽는다.
        _roomCenterWorldX = _cameraFitter.BaseXToWorldX(_viewportSettings.PreviewRoomCenterX, _viewportSettings.BaseSpaceSize);
        _hasArea = true;
        Relayout();
    }

    // 뷰포트와 프리팹이 모두 있을 때만 그린다.
    private void Relayout()
    {
        _mask.enabled = _hasArea && _instance != null;
        if (!_mask.enabled)
        {
            if (_instance != null) _instance.SetActive(false);
            return;
        }

        _mask.transform.position = new Vector3(_area.center.x, _area.center.y, _mask.transform.position.z);
        // 마스크는 이 오브젝트의 자식이라, 부모 배율로 나눠야 월드 크기가 뷰포트와 같아진다.
        Vector3 parentScale = transform.lossyScale;
        _mask.transform.localScale = new Vector3(_area.width / parentScale.x, _area.height / parentScale.y, 1f);

        // Sliced 층은 월드 폭이 뷰포트 폭과 같아지도록 가로 크기만 바꾼다. 배율로 나눠야 월드 폭이 된다.
        for (int i = 0; i < _slicedRenderers.Count; i++)
        {
            SpriteRenderer sliced = _slicedRenderers[i];
            float scaleX = Mathf.Abs(sliced.transform.lossyScale.x);
            if (scaleX <= 0f) continue;
            sliced.size = new Vector2(_area.width / scaleX, sliced.size.y);
        }

        // 그림 전체(스프라이트들의 합)의 가로 범위와 아래 끝이 지금 월드 어디에 있는지 구해, 그만큼 인스턴스 루트를 옮긴다.
        // 파티클은 범위에 넣지 않는다 — 떠다니는 것이라 바닥의 기준이 될 수 없다.
        // Sliced 층이 있으면 가로는 그 층들의 범위를 뷰포트 가운데에 맞춘다(폭이 뷰포트와 같으므로 양 끝이 뷰포트 변에 붙는다).
        // renderer.bounds 대신 로컬 사각형을 직접 구하는 이유: 꺼진 오브젝트에서도 같은 값을 얻기 위해서다.
        if (_spriteRenderers.Count > 0)
        {
            bool alignToViewport = _slicedRenderers.Count > 0;
            float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue;
            for (int i = 0; i < _spriteRenderers.Count; i++)
            {
                SpriteRenderer renderer = _spriteRenderers[i];
                if (renderer.sprite == null) continue;
                bool countsForX = !alignToViewport || renderer.drawMode == SpriteDrawMode.Sliced;

                // 층이 돌아가 있으면 min·max 두 점만으로는 범위가 틀리므로 네 모서리를 모두 옮긴다.
                Rect local = LocalRect(renderer);
                for (int c = 0; c < 4; c++)
                {
                    var corner = new Vector3((c & 1) == 0 ? local.xMin : local.xMax, (c & 2) == 0 ? local.yMin : local.yMax, 0f);
                    Vector3 p = renderer.transform.TransformPoint(corner);
                    if (countsForX)
                    {
                        minX = Mathf.Min(minX, p.x);
                        maxX = Mathf.Max(maxX, p.x);
                    }
                    minY = Mathf.Min(minY, p.y);
                }
            }

            if (minY != float.MaxValue)
            {
                float targetCenterX = alignToViewport ? _area.center.x : _roomCenterWorldX;
                Vector3 pos = _instance.transform.position;
                pos.x += targetCenterX - (minX + maxX) * 0.5f;
                pos.y += _area.yMin - minY;
                _instance.transform.position = pos;
            }
        }

        _instance.SetActive(true);
    }

    // 렌더러가 실제로 그리는 사각형(렌더러 로컬 좌표). Simple은 스프라이트 크기 그대로이고,
    // Sliced·Tiled는 renderer.size만큼 그리므로 sprite.bounds를 쓰면 원본 그림 크기로 잘못 잡힌다.
    private static Rect LocalRect(SpriteRenderer renderer)
    {
        Sprite sprite = renderer.sprite;
        if (renderer.drawMode == SpriteDrawMode.Simple)
        {
            Bounds b = sprite.bounds;
            return new Rect(b.min.x, b.min.y, b.size.x, b.size.y);
        }

        // 피벗은 그림 안의 비율 위치라, 늘린 크기에도 같은 비율로 걸린다.
        Vector2 size = renderer.size;
        Vector2 pivot = new Vector2(sprite.pivot.x / sprite.rect.width, sprite.pivot.y / sprite.rect.height);
        return new Rect(-pivot.x * size.x, -pivot.y * size.y, size.x, size.y);
    }
}
