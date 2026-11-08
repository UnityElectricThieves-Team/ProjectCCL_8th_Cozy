/// <summary>
/// 게임 밸런스 수치를 한 곳에 모은다. 값마다 출시 값(기획)과 테스트 값을 함께 두고,
/// 에디터와 Development Build에서는 테스트 값을, 그 밖의 빌드에서는 출시 값을 쓴다.
///
/// 출시 값의 정본은 Figma다. 테스트 값은 개발 중에 빨리 확인하려고 일부러 키운(줄인) 값이라 기획과 다르다.
/// 인스펙터나 프리팹에 두지 않는 이유: 프리팹 값이 코드 기본값을 덮어써 기획 값이 어디 있는지 흩어지고,
/// 빌드 종류에 따라 값을 바꿀 수도 없기 때문이다.
///
/// 에디터에서 출시 값으로 확인하려면 Player Settings의 Scripting Define Symbols에 COZY_RELEASE_BALANCE를 넣는다.
/// </summary>
public static class GameBalance
{
#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && !COZY_RELEASE_BALANCE
    /// <summary>지금 테스트 값을 쓰는 중인가.</summary>
    public const bool UsesTestValues = true;
#else
    public const bool UsesTestValues = false;
#endif

    // ===== 친밀도 =====

    /// <summary>캐릭터를 좌클릭(쓰담)할 때마다 오르는 친밀도. 기획 +1.</summary>
    public const int AffinityPerPet = UsesTestValues ? 25 : 1;

    /// <summary>깨어 있는 동안 1분마다 오르는 친밀도. 기획 +2.</summary>
    public const int AffinityPerAwakeMinute = UsesTestValues ? 20 : 2;

    /// <summary>게임을 켠 뒤, 또는 동물이 된 뒤 이만큼 쌓이면 소녀로 변신할 수 있다. 기획 100.
    /// 테스트도 같은 값이다 — 변신 조건 자체를 시험하려면 기획 값이 낫다.</summary>
    public const int AffinityToTransform = 100;

    /// <summary>누적 친밀도 최대치. 기획 1억.</summary>
    public const int MaxAffinity = 100_000_000;

    // ===== 하트 =====

    /// <summary>친밀도가 이만큼 오를 때마다 <see cref="HeartsPerAffinityStep"/>개의 하트를 준다. 기획은 친밀도 1당 하트 1.</summary>
    public const int AffinityPerHeartStep = 1;
    public const int HeartsPerAffinityStep = 1;

    /// <summary>보유 하트 최대치. 기획 10억. 테스트는 상한에서 멈추는지 바로 보이면서 모든 상품을 살 수 있는 크기다.</summary>
    public const int MaxHearts = UsesTestValues ? 2_000 : 1_000_000_000;

    // ===== 변신 =====

    /// <summary>소녀로 변신한 뒤 이 시간(초)이 지나면 스스로 동물로 돌아간다. 기획 2시간.</summary>
    public const float GirlFormSeconds = UsesTestValues ? 60f : 7200f;

    // ===== 캐릭터 행동 =====

    /// <summary>대기 상태에서 다음 행동(걷기·특수 대기)을 고르기까지 기다리는 시간의 최소·최대(초). 기획 10~12초.
    /// 테스트는 동작을 빨리 보려고 줄였다.</summary>
    public const float IdleSecondsMin = UsesTestValues ? 5f : 10f;
    public const float IdleSecondsMax = UsesTestValues ? 6f : 12f;

    /// <summary>캐릭터를 좌클릭한 채 이 시간(초)이 지나면 잡힌다. 그 전에 떼면 쓰담으로 끝난다. 기획 2초.
    /// 테스트는 잡기까지가 답답하지 않게 줄였다. 2초가 맞는지는 기획에 다시 묻는 중이다(2026-11-08).</summary>
    public const float GrabHoldSeconds = UsesTestValues ? 0.25f : 2f;

    /// <summary>이 시간(초) 동안 아무 입력이 없으면 잠든다. 기획 30분. 테스트는 30분을 기다려서는 수면을 시험할 수 없어
    /// 2분으로 줄였다 — 대가로 에디터에서 2분만 손을 떼도 캐릭터가 잠든다.</summary>
    public const float SleepAfterIdleSeconds = UsesTestValues ? 120f : 1800f;

    // ===== 해금 =====

    /// <summary>
    /// 테스트 때 쓰는 캐릭터 해금 기운. 해금 순서(도감 정의의 필요 기운이 낮은 순)의 n번째 캐릭터가 n번째 값을 쓴다.
    /// 캐릭터가 이 목록보다 많으면 남는 캐릭터는 마지막 값을 쓴다. 출시 때는 null이고 도감 정의의 값을 그대로 쓴다
    /// (기획 20 / 1,000 / 3,000 / 7,000 / 15,000).
    /// </summary>
    public static readonly int[] TestUnlockEnergy = UsesTestValues ? new[] { 20, 100, 150, 200, 250 } : null;
}
