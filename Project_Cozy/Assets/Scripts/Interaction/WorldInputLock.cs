using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// "지금은 월드 오브젝트(캐릭터·구름 등)가 마우스 입력에 반응하면 안 된다"를 거는 공용 잠금.
/// 뷰포트 편집처럼 화면 전체를 다른 조작이 차지하는 모드가 건다.
///
/// 월드 마우스 입력은 한 곳이 아니라 여러 폴러(<see cref="InputInteractionManager"/>,
/// <see cref="DraggableObject2D"/>, <see cref="HoldClickEvent"/>)가 각자 읽는다. 모드 쪽이 그 컴포넌트들을
/// 찾아 하나씩 끄면 새 폴러가 생길 때마다 빠뜨리므로, 반대로 폴러들이 이 잠금 하나를 본다.
///
/// 잠금은 두 가지다. <see cref="Acquire"/>는 모든 월드 오브젝트를 막는 전체 잠금이고,
/// <see cref="AcquireAllowing"/>는 한 대상(과 그 자식)만 허용하고 나머지를 막는 잠금이다(튜토리얼의 "구름만", "치즈만").
/// 폴러는 자기 대상을 넘겨 <see cref="IsLockedFor"/>로 묻는다.
///
/// 소유자별로 걸고 푼다 — WindowManager의 창 동작 정지와 같은 의미론이다. 같은 owner가 두 번 걸거나
/// 두 번 풀어도 한 번으로 치고, 걸지 않은 owner로 풀어도 무해하며, 모든 소유자가 풀었을 때만 풀린다.
/// 같은 owner가 조건(허용 대상)을 바꾸려면 <see cref="Release"/> 뒤에 다시 건다 — 풀지 않고 다시 걸면 첫 조건이 그대로 남는다.
/// 건 쪽은 OnDisable에서도 반드시 자기 것을 푼다 — 남으면 캐릭터가 영영 반응하지 않는다.
/// </summary>
public static class WorldInputLock
{
    // 값이 null이면 전체 잠금, 아니면 그 Transform(과 자식)만 허용하는 잠금.
    private struct Entry
    {
        public bool HasAllowed;
        public Transform Allowed;
    }

    private static readonly Dictionary<object, Entry> _owners = new Dictionary<object, Entry>();

    /// <summary>허용 대상 없이 건 소유자(전체 잠금)가 하나라도 있는가.
    /// 일부 허용 잠금만 걸려 있으면 false다 — 대상별로 물을 때는 <see cref="IsLockedFor"/>를 쓴다.</summary>
    public static bool IsFullyLocked
    {
        get
        {
            foreach (var pair in _owners)
                if (!pair.Value.HasAllowed) return true;
            return false;
        }
    }

    /// <summary>이 대상이 지금 마우스 입력을 받으면 안 되는가. 전체 잠금이 있거나, 허용 대상을 둔 소유자 중
    /// 하나라도 이 대상을 허용하지 않으면 잠김. 월드 마우스 폴러는 새 press를 받기 전에 자기 대상으로 이걸 본다.</summary>
    public static bool IsLockedFor(Transform target)
    {
        foreach (var pair in _owners)
        {
            var entry = pair.Value;
            if (!entry.HasAllowed) return true;
            // 허용 대상이 파괴됐으면 아무것도 허용하지 않는 것으로 본다(IsChildOf는 파괴된 대상에서 예외를 낸다).
            if (target == null || entry.Allowed == null || !target.IsChildOf(entry.Allowed)) return true;
        }
        return false;
    }

    /// <summary>owner 이름으로 전체 잠금을 건다. 보통 호출하는 컴포넌트 자신(this)을 넘긴다.</summary>
    public static void Acquire(object owner)
    {
        if (!_owners.ContainsKey(owner)) _owners.Add(owner, new Entry());
    }

    /// <summary>owner 이름으로 allowed(와 그 자식)만 허용하는 잠금을 건다.
    /// 같은 owner가 이미 걸었으면 무시된다 — 조건을 바꾸려면 먼저 <see cref="Release"/>한다.</summary>
    public static void AcquireAllowing(object owner, Transform allowed)
    {
        if (!_owners.ContainsKey(owner)) _owners.Add(owner, new Entry { HasAllowed = true, Allowed = allowed });
    }

    /// <summary>owner가 건 잠금을 푼다. 다른 소유자가 남아 있으면 잠금은 유지된다.</summary>
    public static void Release(object owner) => _owners.Remove(owner);

    // 플레이 시작마다 비운다. 이 프로젝트는 도메인 리로드를 끈 채 플레이 모드에 들어가므로,
    // 비우지 않으면 지난 플레이에서 풀리지 않은 소유자가 다음 플레이까지 입력을 막는다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetState() => _owners.Clear();
}
