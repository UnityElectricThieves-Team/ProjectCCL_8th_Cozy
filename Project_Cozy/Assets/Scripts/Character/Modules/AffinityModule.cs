using System;
using UnityEngine;

/// <summary>
/// 친밀도 수치 + 이벤트만. <see cref="UnityEngine.Animator"/>/<see cref="UnityEngine.SpriteRenderer"/> 직접 참조 금지 —
/// 값이 바뀌었다는 사실만 <c>AffinityChanged</c>로 알리고, 그걸로 무엇을 할지는 구독자가 정한다.
/// 순수 C# <see cref="SerializableAttribute"/> 클래스 — <see cref="BaseCharacterController"/>가 <c>[SerializeField]</c>로 nested 보유.
///
/// 저장되는 친밀도는 누적값 하나뿐이고 줄어들지 않는다. 기획에 친밀도가 감소·소모되는 경우가 없다.
/// 소녀 변신 조건만은 누적값이 아니라 "게임을 켠 뒤, 또는 동물로 돌아온 뒤 쌓인 양"으로 본다 —
/// 저장하지 않는 실행 중 카운터라, 이것을 0으로 돌려도 누적 친밀도는 줄지 않는다.
///
/// 수치(쓰담·1분당 증가량, 변신 임계, 최대치)는 인스펙터가 아니라 <see cref="GameBalance"/>에 있다.
/// </summary>
[Serializable]
public sealed class AffinityModule
{
    private BaseCharacterController _owner;
    private int _cumulativeAffinity;

    // 게임을 켠 뒤, 또는 마지막으로 동물이 된 뒤 쌓인 친밀도. 저장하지 않는다(재시작하면 0 — 기획의 "게임 딱 켰을 때부터").
    private int _gainedSinceAnimal;

    // 깨어 있던 시간 중 아직 1분이 안 찬 나머지(초). 저장하지 않는다 — 재시작하면 새 1분부터 센다.
    private float _awakeSeconds;

    /// <summary>누적 친밀도. 줄어들지 않는다.</summary>
    public int CumulativeAffinity => _cumulativeAffinity;
    /// <summary>친밀도 최대치(하드 상한).</summary>
    public int Max => GameBalance.MaxAffinity;
    /// <summary>소녀 변신 가능 여부 — 게임을 켠 뒤, 또는 동물로 돌아온 뒤 쌓인 친밀도가 변신 임계 이상인가.</summary>
    public bool CanHumanTransform => _gainedSinceAnimal >= GameBalance.AffinityToTransform;

    /// <summary>친밀도 값이 변할 때마다 새 값으로 호출.</summary>
    public event Action<int> AffinityChanged;

    public void Bind(BaseCharacterController owner)
    {
        _owner = owner;
    }

    /// <summary>쓰담 1회당 누적.</summary>
    public void AddOnPet() => Add(GameBalance.AffinityPerPet);

    /// <summary>매 프레임 부른다. 깨어 있는 시간을 모아 1분이 찰 때마다 <see cref="GameBalance.AffinityPerAwakeMinute"/>만큼 올린다.
    /// 잠든 동안은 시간을 세지 않는다 — 기획의 "sleep 중 0". 깨어나면 모아 둔 나머지부터 이어서 센다.</summary>
    public void TickAwake(float dt, bool asleep)
    {
        if (asleep) return;

        _awakeSeconds += dt;
        if (_awakeSeconds < 60f) return;

        // 한 프레임이 1분을 넘기는 일은 없지만(멈췄다 풀린 경우 등) 그때도 분 수만큼 한 번에 준다.
        int minutes = (int)(_awakeSeconds / 60f);
        _awakeSeconds -= minutes * 60f;
        Add(minutes * GameBalance.AffinityPerAwakeMinute);
    }

    /// <summary>동물이 된 순간 부른다. 변신 조건을 처음부터 다시 센다 — 기획의 "고양이로 복귀한 뒤부터 카운트 시작".
    /// 누적 친밀도는 건드리지 않는다.</summary>
    public void RestartTransformCount() => _gainedSinceAnimal = 0;

    // 친밀도를 올리는 단 하나의 경로. 누적값은 하드 상한에서 멈춘다.
    private void Add(int amount)
    {
        var gain = Mathf.Max(0, amount);
        if (gain == 0) return;

        // 변신 카운터는 상한과 무관하게 센다. 상한에 닿은 캐릭터도 동물로 돌아온 뒤 다시 변신할 수 있어야 한다.
        // 임계를 넘으면 더 셀 필요가 없으므로 거기서 멈춰 넘침을 막는다.
        _gainedSinceAnimal = Mathf.Min(_gainedSinceAnimal + gain, GameBalance.AffinityToTransform);

        const int cap = GameBalance.MaxAffinity;
        if (_cumulativeAffinity >= cap) return;
        _cumulativeAffinity = (int)Math.Min((long)_cumulativeAffinity + gain, cap);

        AffinityChanged?.Invoke(_cumulativeAffinity);
    }

    /// <summary>
    /// 저장된 친밀도를 되돌린다. 스폰 직후 한 번만 부른다.
    /// <see cref="AffinityChanged"/>를 울리지 않는다 — 값이 바뀐 것이 아니라 되돌린 것이라, 울리면 기록 쪽이 같은 값을
    /// 다시 저장하고 표시 쪽을 괜히 다시 그린다. 구독자는 자기 Start에서 현재 값을 읽는다.
    /// </summary>
    public void Restore(int cumulativeAffinity)
    {
        _cumulativeAffinity = Mathf.Clamp(cumulativeAffinity, 0, GameBalance.MaxAffinity);
    }
}
