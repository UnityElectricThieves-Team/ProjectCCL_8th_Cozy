using System;
using System.Collections.Generic;
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
/// 저장은 아직 미연결(세션 휘발) — 재화·친밀도와 같은 상태다. [[project_pending_affinity_save_api]] 패턴을 따른다.
/// </summary>
[DefaultExecutionOrder(-100)]
public sealed class BackgroundSystem : MonoBehaviour
{
    public static BackgroundSystem Instance { get; private set; }

    private readonly HashSet<string> _owned = new();
    private string _activeId = string.Empty; // 빈 문자열 = 활성 배경 없음(기본)

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
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>이 배경을 이미 샀는가.</summary>
    public bool IsOwned(string id) => _owned.Contains(id);

    /// <summary>이 배경이 지금 사용 중인가.</summary>
    public bool IsActive(string id) => !string.IsNullOrEmpty(id) && _activeId == id;

    /// <summary>
    /// 배경을 구매한다. 이미 샀거나 잔액이 모자라면 아무 일도 없이 false.
    /// 성공하면 하트를 차감하고 구매 집합에 넣은 뒤 <see cref="OwnedChanged"/>를 울린다.
    /// </summary>
    public bool TryBuy(ShopItemDefinition item)
    {
        if (item == null || _owned.Contains(item.id)) return false;
        if (HeartSystem.Instance == null || !HeartSystem.Instance.TrySpend(item.price)) return false;

        _owned.Add(item.id);
        OwnedChanged?.Invoke();
        return true;
    }

    /// <summary>이 배경을 사용 상태로 만든다(이전 활성 배경은 자동으로 해제된다). 안 산 배경이면 무시.</summary>
    public void Use(string id)
    {
        if (!IsOwned(id) || _activeId == id) return;
        _activeId = id;
        ActiveBackgroundChanged?.Invoke(_activeId);
    }

    /// <summary>이 배경의 사용을 취소해 기본으로 되돌린다. 지금 활성이 아니면 무시.</summary>
    public void CancelUse(string id)
    {
        if (_activeId != id) return;
        _activeId = string.Empty;
        ActiveBackgroundChanged?.Invoke(_activeId);
    }
}
