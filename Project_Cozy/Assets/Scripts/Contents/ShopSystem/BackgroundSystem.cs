using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// 배경 아이템의 런타임 상태를 들고 있는 씬 단일 시스템. "무엇을 샀는가"(구매 집합)와
/// "지금 어느 배경을 쓰는가"(활성 배경 하나)를 관리하고, 활성 배경이 바뀔 때 이벤트로 방송한다.
///
/// 이 시스템은 <b>상태와 이벤트만</b> 책임진다 — 실제로 배경을 화면에 그리는 렌더러는 아직 없다.
/// 나중에 배경을 그릴 컴포넌트가 <see cref="ActiveBackgroundChanged"/>를 구독해 sprite를 갈아끼우면 된다.
///
/// Figma 배경 규칙(기획):
/// - 배경은 <b>한 번에 하나만</b> 활성. 다른 배경을 쓰면 이전 배경은 자동으로 사용 해제된다(활성 id가 하나뿐이라 자연히 성립).
/// - 사용 취소하면 기본(활성 없음)으로 돌아간다.
///
/// 구매 집합과 활성 배경 모두 파일에 기록한다(<see cref="BackgroundFileFormat"/>).
/// 구매·사용·사용 취소가 일어날 때마다 즉시 저장하며, <see cref="SaveScheduler"/>를 거치지 않는다 —
/// 셋 다 가끔 일어나는 이산 이벤트라 저장 빈도를 묶을 이유가 없다(하트·장식과 같은 판단).
/// </summary>
[DefaultExecutionOrder(-100)]
public sealed class BackgroundSystem : MonoBehaviour
{
    public static BackgroundSystem Instance { get; private set; }

    [Tooltip("게임에 존재하는 모든 배경. 여기 늘어놓은 순서는 상점 진열 순서와 무관하다 — 진열 순서는 상점이 따로 정한다.")]
    [SerializeField] private ShopItemDefinition[] _availableBackgrounds;

    /// <summary>
    /// 게임에 존재하는 모든 배경 목록. 상점이 이걸 받아 자기 규칙대로 정렬해 진열한다.
    /// 카탈로그를 상점 패널이 아니라 이 시스템이 드는 이유는, 배경 상태를 들고 있는 쪽이
    /// "어떤 배경들이 있는가"도 알아야 하기 때문이다. 상점을 한 번도 열지 않아도 목록이 존재해야 한다.
    /// </summary>
    public IReadOnlyList<ShopItemDefinition> AvailableBackgrounds => _availableBackgrounds;

    private BackgroundFileFormat _data = new();

    /// <summary>구매 집합이 바뀌었을 때(새 배경을 샀을 때) 울린다. 슬롯들이 버튼 상태를 갱신하는 신호.</summary>
    public event Action OwnedChanged;

    /// <summary>활성 배경이 바뀌었을 때 현재 활성 배경 id로 울린다(없으면 빈 문자열). 렌더러가 구독할 지점.</summary>
    public event Action<string> ActiveBackgroundChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;

        // 복원은 이벤트를 쏘지 않는다. 이 시점엔 구독자가 아직 없고(실행 순서 -100),
        // 상점 패널은 자기 OnEnable에서 현재 상태를 직접 읽어 그린다(하트·장식과 같은 방식).
        _data = UserDataSaveIO.Load<BackgroundFileFormat>(GameDataPaths.Backgrounds);

        // 에디터 세이브는 사람이 열어 고칠 수 있는 평문 JSON이라, 필드가 null인 파일이 들어올 수 있다.
        _data.ownedIds ??= new HashSet<string>();

        // 소유하지 않은 배경이 활성으로 남아 있으면 지운다. 그 상태가 되면 해당 슬롯이 상점에 없어
        // 사용 취소를 누를 방법이 없고, 빠져나올 수 없는 상태가 된다. activeId가 null인 경우도 여기서 걸린다.
        if (!_data.ownedIds.Contains(_data.activeId)) _data.activeId = string.Empty;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>이 배경을 이미 샀는가.</summary>
    public bool IsOwned(string id) => _data.ownedIds.Contains(id);

    /// <summary>이 배경이 지금 사용 중인가.</summary>
    public bool IsActive(string id) => !string.IsNullOrEmpty(id) && _data.activeId == id;

    /// <summary>
    /// 배경을 구매한다. 이미 샀거나 잔액이 모자라면 아무 일도 없이 false.
    /// 성공하면 하트를 차감하고 구매 집합에 넣은 뒤 <see cref="OwnedChanged"/>를 울린다.
    /// </summary>
    public bool TryBuy(ShopItemDefinition item)
    {
        if (item == null || _data.ownedIds.Contains(item.id)) return false;
        if (HeartSystem.Instance == null || !HeartSystem.Instance.TrySpend(item.price)) return false;

        _data.ownedIds.Add(item.id);
        Save();
        OwnedChanged?.Invoke();
        return true;
    }

    /// <summary>이 배경을 사용 상태로 만든다(이전 활성 배경은 자동으로 해제된다). 안 산 배경이면 무시.</summary>
    public void Use(string id)
    {
        if (!IsOwned(id) || _data.activeId == id) return;
        _data.activeId = id;
        Save();
        ActiveBackgroundChanged?.Invoke(_data.activeId);
    }

    /// <summary>이 배경의 사용을 취소해 기본으로 되돌린다. 지금 활성이 아니면 무시.</summary>
    public void CancelUse(string id)
    {
        if (_data.activeId != id) return;
        _data.activeId = string.Empty;
        Save();
        ActiveBackgroundChanged?.Invoke(_data.activeId);
    }

    /// <summary>
    /// 현재 소유와 활성 배경을 파일에 기록한다. 쓰기 실패는 로그만 남기고 삼킨다 —
    /// 저장이 안 됐다고 구매나 사용 자체를 취소하면 이미 차감된 하트를 되돌릴 방법이 없어 더 이상해진다.
    /// </summary>
    private void Save()
    {
        try
        {
            UserDataSaveIO.Save(GameDataPaths.Backgrounds, _data);
        }
        catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
        {
            Debug.LogError($"[{nameof(BackgroundSystem)}] 배경 저장 실패: {e.Message}", this);
        }
    }
}
