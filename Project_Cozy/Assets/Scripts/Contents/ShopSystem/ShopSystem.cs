using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// 상점 '장식'의 소유 상태를 들고 있는 씬 단일 시스템. 무엇을 몇 개 샀는지를 관리하고 파일에 기록한다.
/// 배경 쪽의 <see cref="BackgroundSystem"/>과 대칭이며, 장식은 "사용/사용 취소"가 없어 더 단순하다.
///
/// 이 시스템이 생기기 전에는 장식 구매가 <see cref="HeartSystem.TrySpend"/>만 부르고 소유를 어디에도
/// 남기지 않았다. 하트가 저장되기 시작하면서 그 결함이 하트만 영구히 사라지는 문제로 드러났고,
/// 그 짝을 맞추는 것이 이 클래스의 존재 이유다 — 지불과 인도가 모두 파일에 남아야 거래가 성립한다.
///
/// 소유는 개수로 들고 있다(<see cref="ShopInventoryFileFormat"/> 주석 참고).
/// 구매가 일어날 때마다 즉시 저장한다. 하트와 같은 이유로 <see cref="SaveScheduler"/>를 거치지 않는다 —
/// 구매는 가끔 일어나는 이산 이벤트라 저장 빈도를 묶을 이유가 없다.
/// </summary>
[DefaultExecutionOrder(-100)]
public sealed class ShopSystem : MonoBehaviour
{
    public static ShopSystem Instance { get; private set; }

    [Tooltip("게임에 존재하는 모든 장식. 여기 늘어놓은 순서는 상점 진열 순서와 무관하다 — 진열 순서는 상점이 따로 정한다.")]
    [SerializeField] private ShopItemDefinition[] _availableDecorations;

    /// <summary>
    /// 게임에 존재하는 모든 장식 목록. 상점이 이걸 받아 자기 규칙대로 정렬해 진열한다.
    /// 카탈로그를 상점 패널이 아니라 이 시스템이 드는 이유는, 소유 상태를 들고 있는 쪽이
    /// "어떤 장식들이 있는가"도 알아야 하기 때문이다(<see cref="BackgroundSystem"/>와 같은 구조).
    /// </summary>
    public IReadOnlyList<ShopItemDefinition> AvailableDecorations => _availableDecorations;

    private ShopInventoryFileFormat _inventory = new();

    /// <summary>소유가 바뀌었을 때(장식을 샀을 때) 울린다. 슬롯들이 표시를 갱신하는 신호.</summary>
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
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>이 장식을 몇 개 가지고 있는가. 산 적 없으면 0.</summary>
    public int GetCount(string id) => _inventory.ownedCounts.TryGetValue(id, out int count) ? count : 0;

    /// <summary>이 장식을 하나라도 가지고 있는가.</summary>
    public bool IsOwned(string id) => GetCount(id) > 0;

    /// <summary>
    /// 장식을 구매한다. 잔액이 모자라면 아무 일도 없이 false.
    /// 성공하면 하트를 차감하고 개수를 하나 늘린 뒤 저장하고 <see cref="OwnedChanged"/>를 울린다.
    /// </summary>
    public bool TryBuy(ShopItemDefinition item)
    {
        if (item == null) return false;
        if (HeartSystem.Instance == null || !HeartSystem.Instance.TrySpend(item.price)) return false;

        _inventory.ownedCounts[item.id] = GetCount(item.id) + 1;
        Save();
        OwnedChanged?.Invoke();
        return true;
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
