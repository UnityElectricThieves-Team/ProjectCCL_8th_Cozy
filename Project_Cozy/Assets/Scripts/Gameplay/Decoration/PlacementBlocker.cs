using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// "여기에는 장식을 놓을 수 없다"는 UI 영역 표시. 기획의 "가장 우측 메뉴창은 아무것도 침범하지 못함"을 위해
/// 우측 메뉴창 오브젝트에 붙인다. 켜져 있는 동안 자기 RectTransform을 등록해 두고,
/// <see cref="DecorationPlacementController"/>가 설치 모드에 들어갈 때 이 목록을 읽는다.
///
/// 컨트롤러가 메뉴창(UI 타입)을 직접 찾지 않게 하려고 이렇게 뒤집었다 — Gameplay가 UI를 참조하지 않는다.
/// 금지 영역이 더 생기면 그 오브젝트에도 이 컴포넌트를 붙이면 된다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public sealed class PlacementBlocker : MonoBehaviour
{
    private static readonly List<RectTransform> _active = new();

    /// <summary>지금 켜져 있는 금지 영역들.</summary>
    public static IReadOnlyList<RectTransform> Active => _active;

    private void OnEnable() => _active.Add((RectTransform)transform);

    private void OnDisable() => _active.Remove((RectTransform)transform);

    // 도메인 리로드 없이 플레이 모드에 들어가므로 플레이 시작마다 비운다(WorldInputLock과 같은 이유).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetState() => _active.Clear();
}
