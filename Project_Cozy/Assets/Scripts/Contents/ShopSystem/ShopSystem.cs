using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// 상점 '장식'의 소유 상태를 들고 있는 씬 단일 시스템. 무엇을 몇 개 샀는지와 어느 장식이 화면 어디에
/// 놓였는지를 관리하고 파일에 기록한다.
/// 배경 쪽의 <see cref="BackgroundSystem"/>과 대칭이며, 장식은 "사용/사용 취소"가 없어 더 단순하다.
///
/// 이 시스템이 생기기 전에는 장식 구매가 <see cref="HeartSystem.TrySpend"/>만 부르고 소유를 어디에도
/// 남기지 않았다. 하트가 저장되기 시작하면서 그 결함이 하트만 영구히 사라지는 문제로 드러났고,
/// 그 짝을 맞추는 것이 이 클래스의 존재 이유다 — 지불과 인도가 모두 파일에 남아야 거래가 성립한다.
///
/// 소유는 개수로 들고 있다(<see cref="ShopInventoryFileFormat"/> 주석 참고).
/// 회수할 때마다 그 장식의 회수 순번도 찍는다 — 인벤토리가 최근에 회수한 것부터 보여 주는 기준이다.
/// 구매가 일어날 때마다 즉시 저장한다. 하트와 같은 이유로 <see cref="SaveScheduler"/>를 거치지 않는다 —
/// 구매는 가끔 일어나는 이산 이벤트라 저장 빈도를 묶을 이유가 없다.
/// </summary>
[DefaultExecutionOrder(-100)]
public sealed class ShopSystem : MonoBehaviour
{
    public static ShopSystem Instance { get; private set; }

    [Tooltip("게임에 존재하는 모든 상품 목록. 장식은 이 목록의 decorations를 쓴다.")]
    [SerializeField] private ShopCatalog _catalog;

    /// <summary>
    /// 게임에 존재하는 모든 장식 목록. 상점이 이걸 받아 자기 규칙대로 정렬해 진열한다.
    /// 카탈로그를 상점 패널이 아니라 이 시스템이 드는 이유는, 소유 상태를 들고 있는 쪽이
    /// "어떤 장식들이 있는가"도 알아야 하기 때문이다(<see cref="BackgroundSystem"/>와 같은 구조).
    /// </summary>
    public IReadOnlyList<ShopItemDefinition> AvailableDecorations =>
        _catalog != null && _catalog.decorations != null ? _catalog.decorations : Array.Empty<ShopItemDefinition>();

    private ShopInventoryFileFormat _inventory = new();

    // 지금까지 찍은 회수 순번의 최댓값. 저장하지 않고 불러올 때 recallOrder에서 계산한다.
    private long _lastRecallOrder;

    /// <summary>
    /// 가진 개수나 놓인 목록이 바뀌었을 때 울린다(장식을 사서 놓았을 때, 놓인 장식을 회수·제거했을 때 등). 바뀐 뒤에 울리므로,
    /// 상점 슬롯(개수)·인벤토리(남은 개수)·장식 레이어(놓인 목록)가 같은 신호로 갱신한다.
    /// </summary>
    public event Action OwnedChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;

        // 복원은 이벤트를 쏘지 않는다. 이 시점엔 구독자가 아직 없고(실행 순서 -100),
        // 상점 패널은 자기 OnEnable에서 현재 상태를 직접 읽어 그린다(HeartSystem과 같은 방식).
        _inventory = UserDataSaveIO.Load<ShopInventoryFileFormat>(GameDataPaths.ShopInventory);

        // 에디터 세이브는 사람이 열어 고칠 수 있는 평문 JSON이라, 필드가 null인 파일이 들어올 수 있다.
        _inventory.ownedCounts ??= new Dictionary<string, int>();
        _inventory.placed ??= new List<PlacedDecorationData>();
        _inventory.recallOrder ??= new Dictionary<string, long>();

        foreach (long order in _inventory.recallOrder.Values)
        {
            if (order > _lastRecallOrder) _lastRecallOrder = order;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>이 장식을 몇 개 가지고 있는가(놓인 것 + 인벤토리). 제거하면 준다. 산 적 없으면 0.</summary>
    public int GetCount(string id) => _inventory.ownedCounts.TryGetValue(id, out int count) ? count : 0;

    /// <summary>이 장식을 하나라도 가지고 있는가.</summary>
    public bool IsOwned(string id) => GetCount(id) > 0;

    /// <summary>
    /// 이 장식 중 아직 화면에 놓지 않은 개수(가진 개수 − 놓인 개수). 인벤토리가 보여 주는 개수다 —
    /// 기획(Figma 인벤토리)이 "이미 배치되어 있는 시설물은 인벤토리에 없음, 잔여 장식들만"이라고 정했다.
    /// </summary>
    public int GetUnplacedCount(string id)
    {
        int placed = 0;
        for (int i = 0; i < _inventory.placed.Count; i++)
        {
            if (_inventory.placed[i].itemId == id) placed++;
        }
        return Mathf.Max(0, GetCount(id) - placed);
    }

    /// <summary>
    /// 화면에 놓인 장식 목록(놓은 순서). 개수(<see cref="GetCount"/>)와는 별개다 — 개수는 가진 것 전체,
    /// 이 목록은 그중 화면에 놓인 것이다.
    /// </summary>
    public IReadOnlyList<PlacedDecorationData> Placed => _inventory.placed;

    /// <summary>
    /// 장식을 사서 baseX(베이스 공간 px, 그림의 가로 중앙) 자리에 놓는다. 설치 모드에서 자리를 확정한 순간에 부른다.
    /// 하트 차감, 개수 증가, 놓인 목록 추가, 저장을 한 번에 하고 마지막에 <see cref="OwnedChanged"/>를 울린다 —
    /// 지불과 인도가 함께 파일에 남아야 거래가 성립한다. 잔액이 모자라면 아무것도 바꾸지 않고 false.
    /// </summary>
    public bool TryBuyAndPlace(ShopItemDefinition item, float baseX)
    {
        if (item == null) return false;
        if (HeartSystem.Instance == null || !HeartSystem.Instance.TrySpend(item.price))
        {
            Debug.LogWarning($"[{nameof(ShopSystem)}] '{item.id}' 설치를 확정했지만 하트가 모자라 사지 못했습니다.", this);
            return false;
        }

        _inventory.ownedCounts[item.id] = GetCount(item.id) + 1;
        _inventory.placed.Add(new PlacedDecorationData { itemId = item.id, x = baseX });
        Save();
        OwnedChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// 놓인 장식 중 <paramref name="shouldRecall"/>이 true인 것을 인벤토리로 회수한다 — 놓인 목록에서 빼고 회수 순번을 찍어 저장한다.
    /// 가진 개수는 그대로라, 빠진 만큼 남은 개수(<see cref="GetUnplacedCount"/>)가 는다. 하트는 돌려주지 않는다(산 것을 거두는 것뿐이다).
    /// 회수한 것이 있으면 <see cref="OwnedChanged"/>를 울리고 개수를 돌려준다.
    /// </summary>
    public int RecallPlaced(Predicate<PlacedDecorationData> shouldRecall)
    {
        // RemoveAll은 빠지는 항목을 알려 주지 않아 회수 순번을 찍을 수 없다. 뒤에서부터 돌며 직접 뺀다.
        int recalled = 0;
        for (int i = _inventory.placed.Count - 1; i >= 0; i--)
        {
            var placed = _inventory.placed[i];
            if (!shouldRecall(placed)) continue;

            _inventory.placed.RemoveAt(i);
            MarkRecalled(placed.itemId);
            recalled++;
        }
        if (recalled == 0) return 0;

        Save();
        OwnedChanged?.Invoke();
        return recalled;
    }

    /// <summary>
    /// 놓인 목록의 칸 번호 placedIndex에 있는 장식 하나를 인벤토리로 회수한다. 회수 모드에서 장식을 고른 순간에 부른다.
    /// 회수 순번을 찍고 저장한 뒤 <see cref="OwnedChanged"/>를 울린다. 하트는 돌려주지 않는다. 범위 밖이면 아무것도 바꾸지 않고 false.
    /// </summary>
    public bool RecallPlacedAt(int placedIndex)
    {
        if (placedIndex < 0 || placedIndex >= _inventory.placed.Count) return false;

        string id = _inventory.placed[placedIndex].itemId;
        _inventory.placed.RemoveAt(placedIndex);
        MarkRecalled(id);
        Save();
        OwnedChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// 놓인 목록의 칸 번호 placedIndex에 있는 장식 하나를 영구히 없앤다. 제거 모드에서 놓인 장식을 고른 순간에 부른다.
    /// 놓인 목록에서 빼고 가진 개수를 하나 줄인 뒤 저장하고 <see cref="OwnedChanged"/>를 울린다. 하트는 돌려주지 않는다(기획).
    /// 범위 밖이면 아무것도 바꾸지 않고 false.
    /// </summary>
    public bool RemovePlacedAt(int placedIndex)
    {
        if (placedIndex < 0 || placedIndex >= _inventory.placed.Count) return false;

        string id = _inventory.placed[placedIndex].itemId;
        _inventory.placed.RemoveAt(placedIndex);
        DecreaseCount(id, 1);
        Save();
        OwnedChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// 인벤토리(아직 놓지 않은 것)에서 이 장식을 count개 영구히 없앤다. 제거 모드에서 인벤토리 슬롯을 고른 순간에 부른다.
    /// 1 ≤ count ≤ <see cref="GetUnplacedCount"/>가 아니면 아무것도 바꾸지 않고 false. 하트는 돌려주지 않는다.
    /// </summary>
    public bool RemoveUnplaced(string id, int count)
    {
        if (count < 1 || count > GetUnplacedCount(id)) return false;

        DecreaseCount(id, count);
        Save();
        OwnedChanged?.Invoke();
        return true;
    }

    // 0 이하가 되면 키를 지운다. 손으로 고친 파일처럼 놓인 수가 가진 수보다 많아도 음수가 남지 않게 한다.
    private void DecreaseCount(string id, int amount)
    {
        int left = GetCount(id) - amount;
        if (left > 0) _inventory.ownedCounts[id] = left;
        else _inventory.ownedCounts.Remove(id);
    }

    /// <summary>
    /// 이 장식을 마지막으로 회수한 순번. 클수록 최근이다. 회수한 적이 없으면(옛 파일 포함) -1.
    /// 인벤토리가 "가장 최근에 회수한 것부터 앞"으로 정렬하는 데 쓴다.
    /// </summary>
    public long GetRecallOrder(string id) => _inventory.recallOrder.TryGetValue(id, out long order) ? order : -1;

    private void MarkRecalled(string id)
    {
        _lastRecallOrder++;
        _inventory.recallOrder[id] = _lastRecallOrder;
    }

    /// <summary>
    /// 게임 초기화(<see cref="GameProgressReset"/>). 산 기록과 화면에 놓인 장식을 모두 지우고 저장한다.
    /// 놓인 장식은 <see cref="OwnedChanged"/>를 듣는 바인더가 화면에서 걷어 낸다.
    /// </summary>
    public void ResetProgress()
    {
        _inventory.ownedCounts.Clear();
        _inventory.placed.Clear();
        _inventory.recallOrder.Clear();
        _lastRecallOrder = 0;
        Save();
        OwnedChanged?.Invoke();
    }

    /// <summary>
    /// 현재 소유를 파일에 기록한다. 쓰기 실패는 로그만 남기고 삼킨다 —
    /// 저장이 안 됐다고 구매 자체를 취소하면 이미 차감된 하트를 되돌릴 방법이 없어 더 이상해진다.
    /// </summary>
    private void Save()
    {
        try
        {
            UserDataSaveIO.Save(GameDataPaths.ShopInventory, _inventory);
        }
        catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
        {
            Debug.LogError($"[{nameof(ShopSystem)}] 장식 소유 저장 실패: {e.Message}", this);
        }
    }
}
