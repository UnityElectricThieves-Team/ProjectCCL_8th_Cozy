/// <summary>
/// 누적 친밀도가 오른 만큼 하트를 준다. 기획은 친밀도 1당 하트 1이고, 단위와 환율은 <see cref="GameBalance"/>에 있다.
///
/// 부르는 쪽(<see cref="CharacterOwnership"/>)은 "저장돼 있던 친밀도"와 "새 친밀도"를 넘긴다. 기준이 저장 파일의 값이라
/// 재시작해도 지난 실행에서 준 하트를 다시 주지 않고, 화면에 없는 캐릭터의 몫도 같은 길로 받는다.
/// 이전에는 캐릭터마다 붙은 어댑터가 Start에서 기준을 다시 맞췄는데, 꺼진(귀환한) 캐릭터에서는 그 기준을 잡을 수 없었다.
/// 규약은 .claude/rules/unity/character-affinity.md.
///
/// 씬 컴포넌트가 아닌 static이라 붙이는 것을 잊어 하트가 조용히 안 나가는 일이 없다.
/// </summary>
public static class AffinityHeartReward
{
    private const int StepSize = GameBalance.AffinityPerHeartStep > 0 ? GameBalance.AffinityPerHeartStep : 1;

    /// <summary>친밀도가 <paramref name="before"/>에서 <paramref name="after"/>로 바뀌었을 때 줄 하트 수. 줄었거나 같으면 0.
    /// 단위를 새로 넘긴 횟수로 센다 — 단위 안에서 오른 나머지는 다음 변경 때 합쳐진다.</summary>
    public static int HeartsFor(int before, int after)
    {
        var steps = after / StepSize - before / StepSize;
        return steps > 0 ? steps * GameBalance.HeartsPerAffinityStep : 0;
    }

    /// <summary>친밀도가 바뀐 만큼 하트를 지급한다. 친밀도를 저장한 *뒤에* 부른다 — 사이에 프로세스가 죽으면
    /// 그 하트를 잃을 뿐 같은 하트를 두 번 받지는 않는다.</summary>
    public static void Grant(int before, int after)
    {
        var hearts = HeartsFor(before, after);
        if (hearts > 0) HeartSystem.Instance?.Add(hearts);
    }
}
