using UnityEngine;

/// <summary>
/// 게임의 마우스 커서(Figma 생선 커서)를 Windows 하드웨어 커서로 건다. 그림은 4K(마스터 캔버스) 기준 1x로 받고,
/// 화면에서는 UI와 같은 배율(작업 영역 폭 / 마스터 캔버스 폭)로 줄여 건다 — 1080p에서는 절반 크기다.
///
/// 하드웨어 커서로 거는 이유: 커서를 UI 그림으로 그리면 창에 포커스가 없을 때(30fps) 마우스를 늦게 따라가고,
/// 클릭 통과 중인 곳에서는 OS 화살표를 숨길 수 없어 둘이 함께 보인다.
/// 그래서 생선은 게임이 마우스를 받는 곳(캐릭터·구름·UI 위)에서만 보이고, 클릭 통과 중인 곳은 OS 화살표다. 버그가 아니다.
///
/// 배율의 주인은 <see cref="ViewportScreenSettings"/>다. <c>Screen.width</c>로 따로 계산하지 않는다.
/// 배율은 뷰포트가 확정될 때만 바뀌므로 그때만 다시 만들고, 크기가 같으면 다시 만들지 않는다. 매 프레임 하는 일은 없다.
///
/// 커서 표시 여부(<see cref="Cursor.visible"/>)는 건드리지 않는다 — 설치 모드의 <see cref="PlacementGuide"/>가 쓴다.
/// </summary>
[DisallowMultipleComponent]
public sealed class GameCursor : MonoBehaviour
{
    [Tooltip("1x(4K 기준) 커서 그림. 임포트 Texture Type은 Cursor여야 한다(Read/Write가 켜진 무압축 RGBA).")]
    [SerializeField] private Texture2D _texture;

    [Tooltip("클릭 지점. 1x 그림의 왼쪽 위에서 잰 px. 화면 배율에 맞춰 함께 줄어든다.")]
    [SerializeField] private Vector2 _hotspot = new Vector2(20f, 22f);

    [SerializeField] private ViewportScreenSettings _viewportSettings;

    private Texture2D _scaled;          // 코드로 만든 축소본. 배율 1이면 원본을 그대로 쓰므로 null이다.
    private Vector2Int _appliedSize;    // 지금 걸린 커서 크기 — 같으면 다시 만들지 않는다

    private void Start()
    {
        if (_texture == null)
        {
            Debug.LogWarning($"[{nameof(GameCursor)}] 커서 그림이 없음 — OS 기본 커서를 씁니다.", this);
            enabled = false;
            return;
        }
        if (!_texture.isReadable)
        {
            Debug.LogError($"[{nameof(GameCursor)}] '{_texture.name}'을 읽을 수 없음 — 임포트 Texture Type을 Cursor로 바꿔 주세요.", this);
            enabled = false;
            return;
        }

        if (_viewportSettings == null) _viewportSettings = FindFirstObjectByType<ViewportScreenSettings>();
        if (_viewportSettings == null)
        {
            Debug.LogWarning($"[{nameof(GameCursor)}] ViewportScreenSettings 없음 — 커서를 1x 그대로 겁니다.", this);
            Apply(1f);
            return;
        }

        _viewportSettings.ViewportConfirmed += OnViewportConfirmed;

        // 첫 확정 신호가 이미 지나갔을 수 있다(다시 오지 않는다).
        if (_viewportSettings.IsReady) Apply(_viewportSettings.ScreenPxPerBasePx);
    }

    private void OnDestroy()
    {
        if (_viewportSettings != null) _viewportSettings.ViewportConfirmed -= OnViewportConfirmed;
        if (_appliedSize != Vector2Int.zero) Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        if (_scaled != null) Destroy(_scaled); // 코드로 만든 텍스처는 씬과 함께 정리되지 않는다
    }

    private void OnViewportConfirmed(RectInt _) => Apply(_viewportSettings.ScreenPxPerBasePx);

    private void Apply(float scale)
    {
        var size = new Vector2Int(
            Mathf.Max(1, Mathf.RoundToInt(_texture.width * scale)),
            Mathf.Max(1, Mathf.RoundToInt(_texture.height * scale)));
        if (size == _appliedSize) return;

        Texture2D previous = _scaled;
        bool isOriginalSize = size.x == _texture.width && size.y == _texture.height;
        _scaled = isOriginalSize ? null : Resample(_texture, size);

        float sx = size.x / (float)_texture.width;
        float sy = size.y / (float)_texture.height;
        var hotspot = new Vector2(
            Mathf.Clamp(Mathf.Round(_hotspot.x * sx), 0f, size.x - 1),
            Mathf.Clamp(Mathf.Round(_hotspot.y * sy), 0f, size.y - 1));

        Cursor.SetCursor(_scaled != null ? _scaled : _texture, hotspot, CursorMode.Auto);
        _appliedSize = size;

        // 새 커서를 건 다음에 옛 축소본을 지운다.
        if (previous != null) Destroy(previous);
    }

    // 넓이 평균으로 줄인다. 투명한 가장자리 색이 섞여 테두리가 어두워지지 않게 알파를 곱한 값으로 평균한다.
    // 시작할 때와 해상도가 바뀔 때만 도는 일이라 할당을 신경 쓰지 않는다.
    private static Texture2D Resample(Texture2D source, Vector2Int size)
    {
        int sw = source.width, sh = source.height;
        Color32[] src = source.GetPixels32();
        var dst = new Color32[size.x * size.y];
        float fx = sw / (float)size.x;
        float fy = sh / (float)size.y;

        for (int dy = 0; dy < size.y; dy++)
        {
            float y0 = dy * fy, y1 = y0 + fy;
            for (int dx = 0; dx < size.x; dx++)
            {
                float x0 = dx * fx, x1 = x0 + fx;
                float r = 0f, g = 0f, b = 0f, a = 0f, area = 0f;

                for (int y = (int)y0; y < Mathf.Min(sh, Mathf.CeilToInt(y1)); y++)
                {
                    float wy = Mathf.Min(y + 1, y1) - Mathf.Max(y, y0);
                    for (int x = (int)x0; x < Mathf.Min(sw, Mathf.CeilToInt(x1)); x++)
                    {
                        float w = wy * (Mathf.Min(x + 1, x1) - Mathf.Max(x, x0));
                        Color32 c = src[y * sw + x];
                        float wa = w * c.a;
                        r += c.r * wa; g += c.g * wa; b += c.b * wa;
                        a += wa;
                        area += w;
                    }
                }

                dst[dy * size.x + dx] = a <= 0f
                    ? new Color32(0, 0, 0, 0)
                    : new Color32((byte)Mathf.RoundToInt(r / a), (byte)Mathf.RoundToInt(g / a),
                                  (byte)Mathf.RoundToInt(b / a), (byte)Mathf.RoundToInt(a / area));
            }
        }

        var result = new Texture2D(size.x, size.y, TextureFormat.RGBA32, false)
        {
            name = source.name + "_scaled",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
        };
        result.SetPixels32(dst);
        result.Apply(false, false); // 커서는 CPU 쪽 픽셀을 읽어 만들어지므로 읽기 가능으로 남긴다
        return result;
    }
}
