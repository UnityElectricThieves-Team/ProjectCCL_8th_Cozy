using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// <see cref="ShopSystem"/>에 저장된 "놓인 장식" 목록을 <see cref="DecorationLayer"/>에 이어 주는 얇은 바인더.
/// 상품 id를 장식 프리팹으로 바꿔 넘긴다. 지금 상점 목록에 없는 id나 프리팹이 비어 있는 상품은 건너뛴다 —
/// 저장 데이터는 지우지 않으므로, 그 상품이 다시 생기면 저절로 다시 보인다.
///
/// 레이어(<see cref="DecorationLayer"/>)는 상점을 모르고 그리기만 책임진다. 상점 쪽 상태를 아는 것은 이 컴포넌트뿐이라,
/// 배경의 <see cref="BackgroundBinder"/>와 같은 자리다.
///
/// 부팅 복원은 <see cref="ShopSystem"/>이 이벤트를 쏘지 않으므로, Start에서 현재 목록을 한 번 직접 읽는다.
/// 장식 레이어 프리팹 루트에 <see cref="DecorationLayer"/>와 함께 붙는다.
///
/// 그래픽 탭의 [저장]으로 화면이 확정되면(<see cref="ViewportScreenSettings.Committed"/>) 지금 뷰포트에 온전히 들어가지 않는 장식을
/// 인벤토리로 회수한다. 뷰포트를 줄인 경우와 전체 크기(줌)를 키운 경우가 모두 여기로 온다. 판정에 장식 그림의 크기가 필요한데, 상품 id를 프리팹으로 바꾸는 곳이 여기라 여기서 한다.
/// 판정 상자는 설치 모드와 같은 그림 전체(투명 여백 포함)다. 확인 팝업은 띄우지 않는다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(DecorationLayer))]
public sealed class DecorationBinder : MonoBehaviour
{
    private readonly List<PlacedDecoration> _buffer = new();

    private DecorationLayer _layer;
    private ShopSystem _system;
    private ViewportScreenSettings _viewportSettings;
    private BaseSpaceCameraFitter _cameraFitter;

    private RectInt _savedViewport;   // 회수 판정 중인 새 뷰포트(베이스 공간 px)
    private Predicate<PlacedDecorationData> _isOutsideSavedViewport;

    private void Awake()
    {
        _layer = GetComponent<DecorationLayer>();
        _isOutsideSavedViewport = IsOutsideSavedViewport;
    }

    private void Start()
    {
        // ShopSystem은 실행 순서 -100이라 이 시점엔 Awake(저장 파일 복원)를 마쳤다.
        _system = ShopSystem.Instance;
        if (_system == null)
        {
            Debug.LogWarning($"[{nameof(DecorationBinder)}] 씬에 {nameof(ShopSystem)}이 없어 장식을 연결하지 못했습니다.", this);
            enabled = false;
            return;
        }

        _system.OwnedChanged += Apply;
        Apply();

        // 프리팹이라 씬 오브젝트를 인스펙터로 잇지 못한다 — DecorationLayer와 같이 Start에서 한 번 찾는다.
        _viewportSettings = FindFirstObjectByType<ViewportScreenSettings>();
        _cameraFitter = FindFirstObjectByType<BaseSpaceCameraFitter>();
        if (_viewportSettings != null && _cameraFitter != null)
            _viewportSettings.Committed += OnViewportCommitted;
        else
            Debug.LogWarning($"[{nameof(DecorationBinder)}] ViewportScreenSettings/BaseSpaceCameraFitter 없음 — 뷰포트를 줄여도 장식을 회수하지 않습니다.", this);
    }

    private void OnDestroy()
    {
        if (_system != null) _system.OwnedChanged -= Apply;
        if (_viewportSettings != null) _viewportSettings.Committed -= OnViewportCommitted;
    }

    private void OnViewportCommitted(RectInt viewport)
    {
        _savedViewport = viewport;
        _system.RecallPlaced(_isOutsideSavedViewport);
    }

    // 그림의 가로 중앙이 저장된 x이고, 아래 끝은 지면(뷰포트 아래 변)에 붙는다. 그래서 가로는 양 끝을, 세로는 높이만 본다.
    private bool IsOutsideSavedViewport(PlacedDecorationData placed)
    {
        // 지금 그리지 않는 장식(상점에 없는 id, 빈 프리팹)은 크기를 알 수 없어 그대로 둔다 — 그리는 쪽과 같은 판단.
        ShopItemDefinition item = FindItem(placed.itemId);
        if (item == null || item.decorationPrefab == null) return false;

        SpriteRenderer renderer = item.decorationPrefab.GetComponentInChildren<SpriteRenderer>();
        if (renderer == null || renderer.sprite == null) return false;

        Vector3 size = renderer.sprite.bounds.size;
        Vector3 scale = renderer.transform.lossyScale;
        float ppu = _cameraFitter.PixelsPerUnit;
        float halfWidthPx = size.x * Mathf.Abs(scale.x) * ppu * 0.5f;
        float heightPx = size.y * Mathf.Abs(scale.y) * ppu;

        return placed.x - halfWidthPx < _savedViewport.xMin
            || placed.x + halfWidthPx > _savedViewport.xMax
            || heightPx > _savedViewport.height;
    }

    private void Apply()
    {
        _buffer.Clear();
        IReadOnlyList<PlacedDecorationData> placed = _system.Placed;
        for (int i = 0; i < placed.Count; i++)
        {
            ShopItemDefinition item = FindItem(placed[i].itemId);
            if (item == null || item.decorationPrefab == null) continue;
            // 저장 목록의 칸 번호를 함께 싣는다. 건너뛴 칸이 있으면 레이어의 칸 번호와 달라지기 때문이다.
            _buffer.Add(new PlacedDecoration(item.decorationPrefab, placed[i].x, i));
        }
        _layer.SetDecorations(_buffer);
    }

    // 상품이 수십 개 수준이고 목록이 바뀔 때만 불리므로 선형 탐색으로 충분하다.
    private ShopItemDefinition FindItem(string id)
    {
        IReadOnlyList<ShopItemDefinition> items = _system.AvailableDecorations;
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i] != null && items[i].id == id) return items[i];
        }
        return null;
    }
}
