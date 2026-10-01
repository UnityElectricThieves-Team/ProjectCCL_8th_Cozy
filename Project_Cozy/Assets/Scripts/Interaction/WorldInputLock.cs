using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// "지금은 월드 오브젝트(캐릭터·별 등)가 마우스 입력에 반응하면 안 된다"를 거는 공용 잠금.
/// 뷰포트 편집처럼 화면 전체를 다른 조작이 차지하는 모드가 건다.
///
/// 월드 마우스 입력은 한 곳이 아니라 여러 폴러(<see cref="InputInteractionManager"/>,
/// <see cref="DraggableObject2D"/>, <see cref="HoldClickEvent"/>)가 각자 읽는다. 모드 쪽이 그 컴포넌트들을
/// 찾아 하나씩 끄면 새 폴러가 생길 때마다 빠뜨리므로, 반대로 폴러들이 이 잠금 하나를 본다.
///
/// 소유자별로 걸고 푼다 — WindowManager의 창 동작 정지와 같은 의미론이다. 같은 owner가 두 번 걸거나
/// 두 번 풀어도 한 번으로 치고, 걸지 않은 owner로 풀어도 무해하며, 모든 소유자가 풀었을 때만 풀린다.
/// 건 쪽은 OnDisable에서도 반드시 자기 것을 푼다 — 남으면 캐릭터가 영영 반응하지 않는다.
/// </summary>
public static class WorldInputLock
{
    private static readonly HashSet<object> _owners = new HashSet<object>();

    /// <summary>하나 이상의 소유자가 잠금을 걸고 있는가. 월드 마우스 폴러는 새 입력을 받기 전에 이걸 본다.</summary>
    public static bool IsLocked => _owners.Count > 0;

    /// <summary>owner 이름으로 잠금을 건다. 보통 호출하는 컴포넌트 자신(this)을 넘긴다.</summary>
    public static void Acquire(object owner) => _owners.Add(owner);

    /// <summary>owner가 건 잠금을 푼다. 다른 소유자가 남아 있으면 잠금은 유지된다.</summary>
    public static void Release(object owner) => _owners.Remove(owner);

    // 플레이 시작마다 비운다. 이 프로젝트는 도메인 리로드를 끈 채 플레이 모드에 들어가므로,
    // 비우지 않으면 지난 플레이에서 풀리지 않은 소유자가 다음 플레이까지 입력을 막는다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetState() => _owners.Clear();
}
