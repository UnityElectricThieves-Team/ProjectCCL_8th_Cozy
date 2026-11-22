using UnityEngine;

/// <summary>
/// 설정 일반 탭의 [게임 초기화]. 게임 진행 내역을 처음 상태로 되돌린다 — 하트, 스폰 기운, 캐릭터 기록(해금·배치·친밀도),
/// 장식(산 기록·놓인 장식), 배경(산 기록·사용 중인 배경). 설정과 화면 크기(뷰포트)는 진행이 아니라 사용자 환경이라 남긴다.
///
/// 저장 파일을 지우고 씬을 다시 불러오는 대신 각 시스템이 자기 값을 비운다(<c>ResetProgress</c>).
/// 씬을 다시 불러오면 창을 바탕화면에 맞추는 초기화까지 다시 돌기 때문이다. 시스템마다 비우면 저장과 화면 갱신도
/// 평소 바뀔 때와 같은 이벤트 경로를 탄다.
///
/// Contents에 두는 이유: 대상이 Gameplay(하트·스폰 기운·캐릭터)와 Contents(장식·배경)에 걸쳐 있고,
/// Contents는 Gameplay를 참조하는 쪽이다.
/// </summary>
public static class GameProgressReset
{
    public static void ResetAll()
    {
        if (HeartSystem.Instance != null) HeartSystem.Instance.ResetProgress();

        // 스폰 포인트는 구름 오브젝트에 붙어 있고 싱글턴이 아니다. 초기화는 드문 일이라 그때 찾는다.
        var spawnPoint = Object.FindFirstObjectByType<SpawnPointManager>();
        if (spawnPoint != null) spawnPoint.ResetProgress();

        if (CharacterOwnership.Instance != null) CharacterOwnership.Instance.ResetProgress();
        if (ShopSystem.Instance != null) ShopSystem.Instance.ResetProgress();
        if (BackgroundSystem.Instance != null) BackgroundSystem.Instance.ResetProgress();
    }
}
