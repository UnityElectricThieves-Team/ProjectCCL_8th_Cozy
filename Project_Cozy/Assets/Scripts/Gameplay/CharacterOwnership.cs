using UnityEngine;

/// <summary>
/// "어떤 캐릭터를 가졌고, 그중 누가 화면에 나와 있어야 하는가"의 주인. 지금은 시작 시 최초 캐릭터
/// 한 마리를 스폰하는 일만 한다.
///
/// <see cref="CharacterManager"/>와 나눈 이유: 그쪽은 "지금 살아 움직이는 캐릭터"(살아있는 목록,
/// 동시 존재 상한, 스폰 단일 지점, 유저 설정 적용)를 맡는다. 반면 이쪽은 재시작해도 남아야 하는 기록이다.
/// 둘은 수명이 다르다 — 캐릭터를 회수하면 살아있는 목록에서는 빠지지만 가졌다는 기록은 남는다.
/// 그래서 앞으로 들어올 저장·해금·배치·회수는 CharacterManager가 아니라 이 클래스에 둔다.
/// 실제 생성은 언제나 <see cref="CharacterManager.Spawn"/>을 거친다.
///
/// 실행 순서가 -99인 이유: CharacterManager(-100)의 Start가 씬에 놓인 캐릭터를 먼저 등록한 뒤에
/// 스폰해서, 분리 전과 같은 순서를 지키려는 것이다.
/// </summary>
[DefaultExecutionOrder(-99)]
[DisallowMultipleComponent]
public class CharacterOwnership : MonoBehaviour
{
    [Header("최초 캐릭터")]
    [SerializeField, Tooltip("시작 시 이 프리팹으로 한 마리 스폰한다. 씬에 놓인 캐릭터와 상관없이 스폰한다. 비우면 스폰하지 않는다.")]
    private GameObject _initialCharacterPrefab;

    [SerializeField, Tooltip("최초 캐릭터를 놓을 위치(월드). 바닥보다 위면 떨어져서 착지한다. " +
        "뷰포트 밖이어도 ViewportLivingAreaBinder가 안으로 끌어들이므로 정확할 필요는 없다.")]
    private Vector3 _initialSpawnPosition = new Vector3(0f, 1.08f, 0f);

    private void Start()
    {
        // 씬에 놓인 캐릭터(헤라·치즈)는 다른 캐릭터라 개수로 거르지 않는다.
        // 같은 프리팹을 씬에도 놓으면 두 마리가 되니, 최초 캐릭터는 씬에 놓지 않는다.
        if (_initialCharacterPrefab != null && CharacterManager.Instance != null)
            CharacterManager.Instance.Spawn(_initialCharacterPrefab, _initialSpawnPosition);
    }
}
