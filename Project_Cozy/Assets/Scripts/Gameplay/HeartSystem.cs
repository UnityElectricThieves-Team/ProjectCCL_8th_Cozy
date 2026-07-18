using System;
using UnityEngine;

/// <summary>
/// 게임 재화 '하트'의 지갑. 현재 보유량과 벌기/쓰기만 안다 — 하트가 어디서 들어오는지(친밀도·선물 등)는
/// 알지 않는다. 각 재화 획득 경로는 자기 환산을 마친 뒤 <see cref="Add"/>만 호출한다.
///
/// 저장은 이 클래스가 하지 않는다. 미래 저장 시스템이 하트 데이터를 가져가고 되돌려 넣는다(지금은 미연결).
///
/// 씬 단일 인스턴스(Singleton). <see cref="CharacterManager"/>와 같은 패턴.
/// </summary>
[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
public class HeartSystem : MonoBehaviour
{
    public static HeartSystem Instance { get; private set; }

    /// <summary>현재 보유 하트. 0에서 시작.</summary>
    public int CurrentHearts { get; private set; }

    /// <summary>보유량이 변할 때마다 새 값으로 호출.</summary>
    public event Action<int> HeartsChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>하트 적립. 0 이하는 무시(적립은 실패 시나리오가 없어 반환값 없음).</summary>
    public void Add(int amount)
    {
        if (amount <= 0) return;
        CurrentHearts += amount;
        HeartsChanged?.Invoke(CurrentHearts);
    }

    /// <summary>하트 소비. 잔액이 부족하면 아무것도 하지 않고 false, 성공 시 true.</summary>
    public bool TrySpend(int amount)
    {
        if (amount <= 0) return false;
        if (CurrentHearts < amount) return false;
        CurrentHearts -= amount;
        HeartsChanged?.Invoke(CurrentHearts);
        return true;
    }
}
