using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 배경 프리팹 하나를 뷰포트 아래 변에 깐다. 아트가 만든 배경 프리팹을 크기를 바꾸지 않고 그대로 쓰며,
/// 그림 전체의 아래 끝을 뷰포트 아래 변에, 가로 가운데를 뷰포트 가운데에 맞춘다.
///
/// 이 컴포넌트는 <b>배치만</b> 책임진다 — 어떤 프리팹을 깔지는 밖에서 <see cref="SetPrefab"/>으로 넣어 준다(상점·배경 시스템을 모른다).
/// 프리팹이 없으면 아무것도 그리지 않아 바탕화면이 그대로 비친다.
///
/// 아트 프리팹은 타일 개수가 고정이라 뷰포트보다 넓게 뻗어 있다. 창은 작업 영역 전체를 덮으므로 그대로 두면
/// 뷰포트 밖 바탕화면 위에도 배경이 그려진다. 그래서 뷰포트 크기의 SpriteMask를 하나 두고, 프리팹의 렌더러(파티클 포함)를
/// 그 안에서만 보이게 한다. 마스크 그림은 에셋으로 두지 않고 흰 텍스처로 만든다.
///
/// 배치는 뷰포트가 확정될 때(<see cref="ViewportScreenSettings.ViewportConfirmed"/>)와 프리팹이 바뀔 때만
/// 다시 계산한다. 편집 중 프리뷰에는 따라가지 않는다(<see cref="ViewportLivingAreaBinder"/>와 같은 판단).
/// 이 컴포넌트가 매 프레임 하는 일은 없다(프리팹 안의 파티클은 제 갈 길을 간다).
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

    private ViewportScreenSettings _viewportSettings;
    private BaseSpaceCameraFitter _cameraFitter;
    private SpriteMask _mask;
    private GameObject _instance;

    private Rect _area;      // 확정 뷰포트의 월드 사각형
    private bool _hasArea;

    private void Awake()
    {
        var maskObject = new GameObject("ViewportMask");
        maskObject.transform.SetParent(transform, false);
        _mask = maskObject.AddComponent<SpriteMask>();
        _mask.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, MASK_SPRITE_SIZE, MASK_SPRITE_SIZE),
            new Vector2(0.5f, 0.5f), MASK_SPRITE_SIZE);
        _mask.enabled = false;
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

        _viewportSettings.ViewportConfirmed += OnViewportConfirmed;

        // 초기 적용이 이미 끝난 뒤에 이 컴포넌트가 붙었을 수 있다(첫 확정 신호는 이미 지나갔으면 다시 오지 않는다).
        if (_viewportSettings.IsReady) OnViewportConfirmed(_viewportSettings.Viewport);
    }

    private void OnDestroy()
    {
        if (_viewportSettings != null) _viewportSettings.ViewportConfirmed -= OnViewportConfirmed;
        if (_mask != null) Destroy(_mask.sprite); // 코드로 만든 스프라이트는 씬과 함께 정리되지 않는다
    }

    /// <summary>깔 배경 프리팹을 바꾼다. null이면 배경을 지운다.</summary>
    public void SetPrefab(GameObject prefab)
    {
        if (_instance != null) Destroy(_instance);
        _instance = null;
        _spriteRenderers.Clear();

        if (prefab != null)
        {
            _instance = Instantiate(prefab, transform);

            // 첫 배치가 끝나기 전에는 그리지 않는다. 카메라가 프레이밍되기 전 몇 프레임 동안 엉뚱한 자리에 보이는 깜빡임을 막는다.
            _instance.SetActive(false);

            var renderers = _instance.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] is SpriteRenderer sprite)
                {
                    sprite.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
                    _spriteRenderers.Add(sprite);
                }
                else if (renderers[i] is ParticleSystemRenderer particle)
                {
                    particle.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
                }
            }

            if (_spriteRenderers.Count == 0)
                Debug.LogWarning($"[{nameof(BackgroundStage)}] 배경 프리팹 '{prefab.name}'에 SpriteRenderer가 없어 바닥을 맞출 수 없습니다.", prefab);
        }

        Relayout();
    }

    private void OnViewportConfirmed(RectInt viewportPx)
    {
        _area = _cameraFitter.BaseRectToWorld(viewportPx, _viewportSettings.BaseSpaceSize);
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
        _mask.transform.localScale = new Vector3(_area.width, _area.height, 1f);

        // 그림 전체(스프라이트들의 합)의 가로 범위와 아래 끝이 지금 월드 어디에 있는지 구해, 그만큼 인스턴스 루트를 옮긴다.
        // 파티클은 범위에 넣지 않는다 — 떠다니는 것이라 바닥의 기준이 될 수 없다.
        // renderer.bounds 대신 스프라이트의 로컬 bounds를 쓰는 이유: 꺼진 오브젝트에서도 같은 값을 얻기 위해서다.
        if (_spriteRenderers.Count > 0)
        {
            float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue;
            for (int i = 0; i < _spriteRenderers.Count; i++)
            {
                SpriteRenderer renderer = _spriteRenderers[i];
                if (renderer.sprite == null) continue;

                Bounds local = renderer.sprite.bounds;
                Vector3 a = renderer.transform.TransformPoint(local.min);
                Vector3 b = renderer.transform.TransformPoint(local.max);
                minX = Mathf.Min(minX, Mathf.Min(a.x, b.x));
                maxX = Mathf.Max(maxX, Mathf.Max(a.x, b.x));
                minY = Mathf.Min(minY, Mathf.Min(a.y, b.y));
            }

            if (minY != float.MaxValue)
            {
                Vector3 pos = _instance.transform.position;
                pos.x += _area.center.x - (minX + maxX) * 0.5f;
                pos.y += _area.yMin - minY;
                _instance.transform.position = pos;
            }
        }

        _instance.SetActive(true);
    }
}
