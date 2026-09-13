using UnityEngine;

/// <summary>
/// 뷰포트 아래 변에 붙는 배경 띠. 스프라이트를 고정 높이(베이스 공간 px)에 맞춰 키운 뒤,
/// 뷰포트 폭을 꽉 채울 때까지 가로로 반복한다. 이미지의 원래 크기와 무관하게 같은 높이로 보인다.
///
/// 이 컴포넌트는 <b>기하만</b> 책임진다 — 어떤 스프라이트를 보일지와 띠 높이는 밖에서 <see cref="SetSprite"/>·<see cref="SetHeight"/>로
/// 넣어 준다(상점·배경 시스템을 모른다). 스프라이트나 높이가 없으면 아무것도 그리지 않아 바탕화면이 그대로 비친다.
///
/// 반복은 SpriteRenderer의 Tiled 드로우 모드로 한다. 그래서 배경 스프라이트는 Mesh Type을 Full Rect로
/// 임포트해야 한다 — Tight 메시로는 타일이 제대로 그려지지 않는다. 아트 샘플 씬처럼 오브젝트 여러 개를
/// 나란히 놓는 방식은 쓰지 않는다. 뷰포트 폭이 바뀔 때마다 개수를 다시 맞춰야 해서다.
///
/// 배치는 뷰포트가 확정될 때(<see cref="ViewportScreenSettings.ViewportApplied"/>)와 스프라이트가 바뀔 때만
/// 다시 계산한다. 편집 중 프리뷰에는 따라가지 않는다(<see cref="ViewportLivingAreaBinder"/>와 같은 판단).
/// 매 프레임 하는 일은 없다.
///
/// 콜라이더를 붙이지 않는다. <see cref="WindowManager"/>가 커서 아래 콜라이더 유무로 클릭 통과를 끄기 때문에,
/// 콜라이더가 있으면 배경 띠 전체에서 바탕화면 클릭이 막힌다.
///
/// 배경 프리팹 루트에 붙는다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class BackgroundStrip : MonoBehaviour
{
    // 띠 높이(베이스 공간 px). 인스펙터 필드가 아니다 — 값의 소유자는 BackgroundSystem 하나이고,
    // 이 컴포넌트는 SetHeight로 받기만 한다. 두 곳에서 각자 값을 들면 어느 쪽이 맞는지 알 수 없어서다.
    private int _heightBasePx;

    private SpriteRenderer _renderer;
    private ViewportScreenSettings _viewportSettings;
    private BaseSpaceCameraFitter _cameraFitter;

    private Rect _area;      // 확정 뷰포트의 월드 사각형
    private bool _hasArea;

    private void Awake()
    {
        _renderer = GetComponent<SpriteRenderer>();
        _renderer.drawMode = SpriteDrawMode.Tiled;

        // 첫 배치가 끝나기 전에는 그리지 않는다. 카메라가 프레이밍되기 전 몇 프레임 동안
        // 프리팹 기본 크기의 스프라이트가 창을 가득 채우는 깜빡임을 막는다.
        _renderer.enabled = false;
    }

    private void Start()
    {
        // 프리팹은 씬 오브젝트를 인스펙터로 참조할 수 없고, 인스턴스에서 걸면 씬 override가 남는다.
        // 그래서 Start에서 한 번만 찾는다.
        _viewportSettings = FindFirstObjectByType<ViewportScreenSettings>();
        _cameraFitter = FindFirstObjectByType<BaseSpaceCameraFitter>();

        if (_viewportSettings == null || _cameraFitter == null)
        {
            Debug.LogWarning($"[{nameof(BackgroundStrip)}] ViewportScreenSettings/BaseSpaceCameraFitter 없음 — 배경을 그리지 않습니다.", this);
            enabled = false;
            return;
        }

        _viewportSettings.ViewportApplied += OnViewportApplied;

        // 초기 적용이 이미 끝난 뒤에 이 컴포넌트가 붙었을 수 있다(ViewportApplied는 다시 오지 않는다).
        if (_viewportSettings.IsReady) OnViewportApplied(_viewportSettings.Viewport);
    }

    private void OnDestroy()
    {
        if (_viewportSettings != null) _viewportSettings.ViewportApplied -= OnViewportApplied;
    }

    /// <summary>띠 높이(베이스 공간 px)를 정한다. 0 이하이면 띠를 숨긴다.</summary>
    public void SetHeight(int heightBasePx)
    {
        _heightBasePx = heightBasePx;
        Relayout();
    }

    /// <summary>보일 스프라이트를 바꾼다. null이면 띠를 숨긴다.</summary>
    public void SetSprite(Sprite sprite)
    {
        _renderer.sprite = sprite;
        Relayout();
    }

    private void OnViewportApplied(RectInt viewportPx)
    {
        _area = _cameraFitter.BaseRectToWorld(viewportPx, _viewportSettings.BaseSpaceSize);
        _hasArea = true;
        Relayout();
    }

    // 뷰포트·스프라이트·높이가 모두 있을 때만 그린다. 하나라도 없으면 숨긴다.
    private void Relayout()
    {
        Sprite sprite = _renderer.sprite;
        if (!_hasArea || sprite == null || _heightBasePx <= 0)
        {
            _renderer.enabled = false;
            return;
        }

        // 스프라이트 높이(월드)를 고정 높이(월드)에 맞추는 배율. 가로도 같은 배율이라 비율이 유지된다.
        float heightWorld = _heightBasePx / _cameraFitter.PixelsPerUnit;
        float scale = heightWorld / sprite.bounds.size.y;
        transform.localScale = new Vector3(scale, scale, 1f);

        // Tiled 크기는 배율을 곱하기 전 로컬 단위다. 세로는 정확히 한 장.
        _renderer.size = new Vector2(_area.width / scale, sprite.bounds.size.y);

        // 배경 스프라이트의 pivot은 가운데(0.5, 0.5)를 전제한다. 띠의 아래 변을 뷰포트 아래 변에 맞춘다.
        Vector3 pos = transform.position;
        pos.x = _area.center.x;
        pos.y = _area.yMin + heightWorld * 0.5f;
        transform.position = pos;

        _renderer.enabled = true;
    }
}
