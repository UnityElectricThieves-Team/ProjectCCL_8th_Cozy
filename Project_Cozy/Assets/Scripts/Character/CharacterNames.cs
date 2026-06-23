using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 캐릭터에게 붙일 귀여운 동물 이름 풀(30개)과 할당/반납을 담당하는 정적 레지스트리.
///
/// - 스폰 시 <see cref="Acquire"/>로 "현재 사용 중이 아닌" 이름 1개를 무작위로 받아 점유한다.
/// - 캐릭터가 파괴되면 <see cref="Release"/>로 반납해 다시 후보가 되게 한다.
/// - 사용 중 집합은 정적 상태라, "도메인 리로드 끄기" 환경에서도 누수되지 않도록
///   <see cref="ResetState"/>가 매 플레이 시작 시 비운다.
///
/// Character/ 레이어에 두는 이유: 이름 할당은 캐릭터 본체(<see cref="BaseCharacterController"/>)가
/// 직접 호출하므로, Gameplay 쪽에 두면 Character→Gameplay 역참조가 생긴다.
/// </summary>
public static class CharacterNames
{
    // 귀여운 동물 이름 30개(중복 없음). 표현용 하드코딩 풀.
    private static readonly string[] Pool =
    {
        "Mocha", "Coco", "Bean", "Tofu", "Choco", "Hazel", "Milo", "Luna", "Bella", "Daisy",
        "Mango", "Peanut", "Olive", "Ginger", "Cloudy", "Cookie", "Cheese", "Latte", "Pumpkin", "Maple",
        "Biscuit", "Ruby", "Potato", "Sweetpea", "Chestnut", "Berry", "Cherry", "Grape", "Melon", "Marble",
    };

    private static readonly HashSet<string> _used = new HashSet<string>();
    // Acquire마다 재사용하는 임시 후보 목록(스폰은 드물어 비용 무시 가능, 알로케이션만 피함).
    private static readonly List<string> _free = new List<string>();

    /// <summary>
    /// 사용 중이 아닌 이름 1개를 무작위로 골라 점유하고 반환한다.
    /// 모두 점유된 경우(이론상 동시 30마리 초과, 캡이 10이라 실제로는 도달 불가)에는
    /// 경고 후 중복을 허용해 무작위 1개를 반환한다.
    /// </summary>
    public static string Acquire()
    {
        _free.Clear();
        foreach (var name in Pool)
            if (!_used.Contains(name)) _free.Add(name);

        if (_free.Count == 0)
        {
            Debug.LogWarning("[CharacterNames] 모든 이름이 사용 중입니다 — 중복을 허용해 할당합니다.");
            return Pool[Random.Range(0, Pool.Length)];
        }

        var pick = _free[Random.Range(0, _free.Count)];
        _used.Add(pick);
        return pick;
    }

    /// <summary>점유했던 이름을 반납한다(파괴 시 호출). null/빈 문자열·미점유 이름은 무시.</summary>
    public static void Release(string name)
    {
        if (!string.IsNullOrEmpty(name)) _used.Remove(name);
    }

    // 플레이 시작마다 정적 상태 초기화. 도메인 리로드를 꺼도 잔존하지 않게 한다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetState()
    {
        _used.Clear();
        _free.Clear();
    }
}
