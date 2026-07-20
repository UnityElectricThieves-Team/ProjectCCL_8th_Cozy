using System;
using System.IO;
using UnityEngine;

/// <summary>
/// 게임 재화 '하트'의 지갑. 현재 보유량과 벌기/쓰기만 안다 — 하트가 어디서 들어오는지(친밀도·선물 등)는
/// 알지 않는다. 각 재화 획득 경로는 자기 환산을 마친 뒤 <see cref="Add"/>만 호출한다.
///
/// 보유량이 바뀔 때마다 즉시 파일에 기록한다. 하트는 스폰 기운처럼 수시로 변하는 값이 아니라
/// 가끔 바뀌는 값이라, <see cref="SaveScheduler"/>의 주기 저장으로 빈도를 묶을 이유가 없다
/// (스케줄러 주석의 "이산 이벤트는 발생 즉시 저장" 지침과 같은 판단).
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

        // 복원은 이벤트를 쏘지 않는다. 이 시점엔 구독자가 아직 없고(HeartSystem은 실행 순서 -100),
        // 표시 컴포넌트는 자기 OnEnable에서 CurrentHearts를 직접 읽어 초기값을 채운다.
        CurrentHearts = UserDataSaveIO.Load<HeartFileFormat>(GameDataPaths.Hearts).hearts;
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
        Save();
    }

    /// <summary>하트 소비. 잔액이 부족하면 아무것도 하지 않고 false, 성공 시 true.</summary>
    public bool TrySpend(int amount)
    {
        if (amount <= 0) return false;
        if (CurrentHearts < amount) return false;
        CurrentHearts -= amount;
        HeartsChanged?.Invoke(CurrentHearts);
        Save();
        return true;
    }

    /// <summary>
    /// 현재 보유량을 파일에 기록한다. 쓰기 실패(디스크 잠금 등)는 로그만 남기고 삼킨다 —
    /// 저장이 안 됐다고 하트 적립·소비 자체가 취소되면 게임이 더 이상하게 동작한다.
    /// </summary>
    private void Save()
    {
        try
        {
            UserDataSaveIO.Save(GameDataPaths.Hearts, new HeartFileFormat { hearts = CurrentHearts });
        }
        catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
        {
            Debug.LogError($"[{nameof(HeartSystem)}] 하트 저장 실패: {e.Message}", this);
        }
    }
}
