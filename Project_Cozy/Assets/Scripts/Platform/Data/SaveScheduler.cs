using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 등록된 IPeriodicSaveable들을 사용자 지정 주기마다 순회해 dirty인 것만 저장하는 스케줄러.
/// 앱 종료 시에는 FlushAll로 남은 dirty를 일괄 저장한다(정상 종료 시 손실 0 보장).
/// 종료 flush는 앱이 먼저 죽으면 안 되므로 반드시 동기로 완료한다.
///
/// 해금처럼 어쩌다 한 번 일어나는 이산 이벤트는 이 스케줄러를 거치지 말고 발생 즉시 저장할 것.
/// 이 스케줄러는 스폰 기운처럼 수시로 변하는 값의 저장 빈도를 묶는 용도다.
/// </summary>
public class SaveScheduler : MonoBehaviour
{
    [SerializeField] private float saveIntervalSeconds = 300f;

    private readonly List<IPeriodicSaveable> saveables = new List<IPeriodicSaveable>();
    private float elapsedSeconds;

    public void Register(IPeriodicSaveable saveable)
    {
        if (!saveables.Contains(saveable))
        {
            saveables.Add(saveable);
        }
    }

    public void Unregister(IPeriodicSaveable saveable)
    {
        saveables.Remove(saveable);
    }

    /// <summary>dirty인 대상을 전부 즉시 저장한다. 종료 외에도 강제 저장이 필요한 지점에서 호출 가능.</summary>
    public void FlushAll()
    {
        SaveDirty();
    }

    private void Update()
    {
        elapsedSeconds += Time.unscaledDeltaTime;
        if (elapsedSeconds < saveIntervalSeconds)
        {
            return;
        }
        elapsedSeconds = 0f;
        SaveDirty();
    }

    private void SaveDirty()
    {
        for (var i = 0; i < saveables.Count; i++)
        {
            if (saveables[i].IsDirty)
            {
                saveables[i].Save();
            }
        }
    }

    private void OnApplicationQuit()
    {
        FlushAll();
    }
}
