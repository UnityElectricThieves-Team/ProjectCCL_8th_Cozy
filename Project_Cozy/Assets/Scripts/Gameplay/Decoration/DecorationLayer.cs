using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 화면에 놓인 장식 하나를 그리는 데 필요한 것 — 어떤 프리팹을, 베이스 공간 px의 어느 가로 위치에.
/// 상점 데이터(<c>PlacedDecorationData</c>)를 직접 받지 않으려고 따로 둔다. 상품 id를 프리팹으로 바꾸는 일은 바인더가 한다.
///
/// <see cref="PlacedIndex"/>는 저장 목록에서의 칸 번호다. 배치 모드가 놓인 장식을 고르면 이 번호를 밖으로 돌려준다.
/// 바인더가 그릴 수 없는 장식을 건너뛰므로 레이어의 칸 번호와 저장 목록의 칸 번호가 다를 수 있어서 따로 싣는다.
/// </summary>
public readonly struct PlacedDecoration
{
    public readonly GameObject Prefab;
    public readonly float BaseX;
    public readonly int PlacedIndex;

    public PlacedDecoration(GameObject prefab, float baseX, int placedIndex)
    {
        Prefab = prefab;
        BaseX = baseX;
        PlacedIndex = placedIndex;
    }
}

/// <summary>
/// 화면에 놓인 장식들을 그린다. 받은 목록대로 장식 프리팹을 만들어, 그림의 가로 중앙을 저장된 x에,
/// 그림의 아래 끝을 지면(확정 뷰포트의 아래 변)에 맞춘다.
///
/// 이 컴포넌트는 <b>상점을 모른다</b> — 무엇을 어디에 놓을지는 밖에서 <see cref="SetDecorations"/>로 넣어 준다
/// (배경의 <see cref="BackgroundStage"/>와 같은 자리). 그래야 이동·회수 모드가 같은 레이어를 그대로 쓸 수 있다.
///
/// 목록이 바뀌면 인스턴스를 전부 지우고 다시 만든다. 바뀌는 순간이 배치 모드에서 무언가를 확정할 때뿐이라 드물고 개수도 적다.
/// 놓인 장식에는 고유 id가 없고, 저장 목록의 칸 번호(<see cref="PlacedDecoration.PlacedIndex"/>)로 가리킨다.
/// 모드 중에 저장 목록을 바꾸는 것은 모드 자신의 확정뿐이라, 고른 순간부터 확정까지 칸 번호가 바뀌지 않는다.
///
/// 배치 모드가 쓰는 렌더러 상태(호버 강조, 앞으로 당기기)는 이 레이어가 들고 있다. 다시 만들 때(<see cref="Rebuild"/>)
/// 렌더러가 새로 생기므로, 상태를 두 클래스가 나눠 가지면 재생성 때 어긋난다. 강조는 다시 만들면 비우고, 앞으로 당기기는 지킨다.
///
/// 배치는 뷰포트가 확정될 때(<see cref="ViewportScreenSettings.ViewportConfirmed"/>)와 목록이 바뀔 때만 다시 계산한다.
/// 매 프레임 하는 일은 없다. 편집 중 프리뷰에는 따라가지 않는다(배경과 같은 판단).
///
/// 지면 맞춤은 transform 위치가 아니라 그림의 아래 끝으로 한다. 장식마다 그림의 피벗이 달라서다(가구는 아래 끝, 임시 그림은 가운데).
/// 장식에는 콜라이더를 달지 않는다 — 이유는 .claude/rules/unity/decoration-placement.md.
///
/// 장식 레이어 프리팹 루트에 <see cref="DecorationBinder"/>와 함께 붙는다.
/// </summary>
[DisallowMultipleComponent]
public sealed class DecorationLayer : MonoBehaviour
{
    // 놓인 장식의 Order in Layer. 캐릭터(0 이상)보다 뒤, 배경(-100)보다 앞.
    // 장식끼리는 겹치게 놓을 수 없어 서로의 앞뒤가 드러나지 않으므로 값 하나로 둔다.
    private const int SORTING_ORDER = -50;

    // 이동·회수·제거 모드 동안의 Order in Layer. 캐릭터(지금 10~12)보다 앞, 들고 다니는 고스트(100)보다 뒤.
    // 캐릭터가 앞에서 알짱대 장식을 못 알아보는 일을 막는다(Figma 이동/회수/제거 모드 설명). 캐릭터 쪽 값은 건드리지 않는다.
    private const int BROUGHT_FORWARD_SORTING_ORDER = 50;

    private readonly List<PlacedDecoration> _decorations = new();
    private readonly List<SpriteRenderer> _instances = new();

    private Material _highlightMaterial;     // 배치 모드 컨트롤러가 한 번 넘겨준다(만들고 지우는 것도 그쪽)
    private SpriteRenderer _highlighted;     // 지금 강조 중인 렌더러
    private int _highlightedIndex = -1;      // 그 렌더러의 저장 목록 칸 번호
    private Material _highlightOriginal;     // 강조 전 머티리얼. 강조를 풀 때 돌려준다
    private bool _broughtForward;

    private ViewportScreenSettings _viewportSettings;
    private BaseSpaceCameraFitter _cameraFitter;

    private Rect _area;      // 확정 뷰포트의 월드 사각형
    private bool _hasArea;

    private void Start()
    {
        // 프리팹은 씬 오브젝트를 인스펙터로 참조할 수 없고, 인스턴스에서 걸면 씬 override가 남는다.
        // 그래서 Start에서 한 번만 찾는다(BackgroundStage와 같은 방식).
        _viewportSettings = FindFirstObjectByType<ViewportScreenSettings>();
        _cameraFitter = FindFirstObjectByType<BaseSpaceCameraFitter>();

        if (_viewportSettings == null || _cameraFitter == null)
        {
            Debug.LogWarning($"[{nameof(DecorationLayer)}] ViewportScreenSettings/BaseSpaceCameraFitter 없음 — 장식을 그리지 않습니다.", this);
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
    }

    /// <summary>놓인 장식 목록을 통째로 바꾼다. 넘긴 목록은 복사해 두므로 호출한 쪽이 다시 써도 된다.</summary>
    public void SetDecorations(IReadOnlyList<PlacedDecoration> decorations)
    {
        _decorations.Clear();
        for (int i = 0; i < decorations.Count; i++) _decorations.Add(decorations[i]);
        Rebuild();
    }

    /// <summary>
    /// 지금 화면에 그려진 장식들의 월드 사각형을 results에 채운다(먼저 비운다). 배치 모드의 겹침 판정용.
    /// 매 프레임 할당을 피하려고 호출한 쪽의 리스트를 받는다.
    /// </summary>
    public void CollectBounds(List<Bounds> results)
    {
        results.Clear();
        for (int i = 0; i < _instances.Count; i++)
        {
            SpriteRenderer renderer = _instances[i];
            if (renderer != null && renderer.enabled) results.Add(renderer.bounds);
        }
    }

    /// <summary>
    /// 월드 좌표 world가 어느 놓인 장식의 그림 사각형(투명 여백 포함) 안에 있는지 찾아 그 저장 목록 칸 번호를 돌려준다.
    /// 여러 개면 목록의 마지막 것. 그려지지 않은 장식은 건너뛴다. 배치 모드가 매 프레임 부르므로 할당하지 않는다.
    /// </summary>
    public bool TryFindAt(Vector2 world, out int placedIndex)
    {
        for (int i = _instances.Count - 1; i >= 0; i--)
        {
            SpriteRenderer renderer = _instances[i];
            if (renderer == null || !renderer.enabled) continue;

            Bounds b = renderer.bounds;
            if (world.x >= b.min.x && world.x <= b.max.x && world.y >= b.min.y && world.y <= b.max.y)
            {
                placedIndex = _decorations[i].PlacedIndex;
                return true;
            }
        }
        placedIndex = -1;
        return false;
    }

    /// <summary>호버 강조에 쓸 머티리얼을 넘긴다. 배치 모드 컨트롤러가 시작할 때 한 번 부른다.</summary>
    public void SetHighlightMaterial(Material material) => _highlightMaterial = material;

    /// <summary>
    /// 저장 목록 칸 번호 placedIndex의 장식을 강조한다(-1이면 강조를 푼다). 배치 모드가 매 프레임 부르므로,
    /// 지금과 같으면 아무것도 하지 않고 바뀔 때만 머티리얼을 갈아 끼운다.
    /// </summary>
    public void SetHighlighted(int placedIndex)
    {
        if (placedIndex == _highlightedIndex) return;

        ClearHighlight();
        if (placedIndex < 0 || _highlightMaterial == null) return;

        int i = FindInstanceIndex(placedIndex);
        if (i < 0 || _instances[i] == null) return;

        _highlighted = _instances[i];
        _highlightedIndex = placedIndex;
        _highlightOriginal = _highlighted.sharedMaterial;
        _highlighted.sharedMaterial = _highlightMaterial;
    }

    /// <summary>
    /// 놓인 장식을 모두 캐릭터 앞으로 당기거나(true) 평소 자리로 돌린다(false). 이동·회수·제거 모드 동안 켠다.
    /// 다시 만들어도(<see cref="Rebuild"/>) 이 상태를 지킨다.
    /// </summary>
    public void SetBroughtForward(bool forward)
    {
        if (_broughtForward == forward) return;
        _broughtForward = forward;

        int order = CurrentSortingOrder;
        for (int i = 0; i < _instances.Count; i++)
        {
            if (_instances[i] != null) _instances[i].sortingOrder = order;
        }
    }

    private int CurrentSortingOrder => _broughtForward ? BROUGHT_FORWARD_SORTING_ORDER : SORTING_ORDER;

    private void ClearHighlight()
    {
        if (_highlighted != null) _highlighted.sharedMaterial = _highlightOriginal;
        _highlighted = null;
        _highlightedIndex = -1;
        _highlightOriginal = null;
    }

    // 저장 목록 칸 번호 → 레이어 칸 번호. 장식은 수십 개 수준이고 바뀔 때만 불리므로 선형 탐색으로 충분하다.
    private int FindInstanceIndex(int placedIndex)
    {
        for (int i = 0; i < _decorations.Count; i++)
        {
            if (_decorations[i].PlacedIndex == placedIndex) return i;
        }
        return -1;
    }

    private void OnViewportConfirmed(RectInt viewportPx)
    {
        _area = _cameraFitter.BaseRectToWorld(viewportPx, _viewportSettings.BaseSpaceSize);
        _hasArea = true;
        Relayout();
    }

    private void Rebuild()
    {
        // 강조한 렌더러는 곧 파괴된다. 머티리얼을 돌려줄 대상이 없어지므로 기록만 비운다.
        _highlighted = null;
        _highlightedIndex = -1;
        _highlightOriginal = null;

        for (int i = 0; i < _instances.Count; i++)
        {
            if (_instances[i] != null) Destroy(_instances[i].gameObject);
        }
        _instances.Clear();

        for (int i = 0; i < _decorations.Count; i++)
        {
            GameObject prefab = _decorations[i].Prefab;
            SpriteRenderer renderer = null;
            if (prefab != null)
            {
                GameObject go = Instantiate(prefab, transform);
                renderer = go.GetComponentInChildren<SpriteRenderer>();
                if (renderer == null)
                {
                    Debug.LogWarning($"[{nameof(DecorationLayer)}] 장식 프리팹 '{prefab.name}'에 SpriteRenderer가 없습니다.", prefab);
                    Destroy(go);
                }
                else
                {
                    renderer.sortingOrder = CurrentSortingOrder;
                    renderer.enabled = false; // 첫 배치가 끝나기 전에는 그리지 않는다
                }
            }
            _instances.Add(renderer); // 목록과 같은 순서를 지키려고 실패한 칸도 null로 둔다
        }

        Relayout();
    }

    // 뷰포트와 목록이 모두 있을 때만 그린다.
    private void Relayout()
    {
        for (int i = 0; i < _instances.Count; i++)
        {
            SpriteRenderer renderer = _instances[i];
            if (renderer == null) continue;

            if (!_hasArea || renderer.sprite == null)
            {
                renderer.enabled = false;
                continue;
            }

            // 그림의 가로 중앙과 아래 끝이 지금 월드 어디에 있는지 구해, 그만큼 장식 루트를 옮긴다.
            // renderer.bounds 대신 스프라이트의 로컬 bounds를 쓰는 이유: 꺼진 렌더러도 같은 값을 얻기 위해서다.
            Bounds local = renderer.sprite.bounds;
            Vector3 center = renderer.transform.TransformPoint(local.center);
            Vector3 bottom = renderer.transform.TransformPoint(new Vector3(local.center.x, local.min.y, 0f));

            float targetX = _cameraFitter.BaseXToWorldX(_decorations[i].BaseX, _viewportSettings.BaseSpaceSize);
            Transform root = FindInstanceRoot(renderer.transform);
            Vector3 pos = root.position;
            pos.x += targetX - center.x;
            pos.y += _area.yMin - bottom.y;
            root.position = pos;

            renderer.enabled = true;
        }
    }

    // 렌더러가 프리팹 루트의 자식에 있을 수도 있으니, 이 레이어 바로 아래의 오브젝트(=인스턴스 루트)를 찾는다.
    private Transform FindInstanceRoot(Transform t)
    {
        while (t.parent != null && t.parent != transform) t = t.parent;
        return t;
    }
}
