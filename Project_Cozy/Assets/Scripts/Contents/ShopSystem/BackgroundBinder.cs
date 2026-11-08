using UnityEngine;

/// <summary>
/// <see cref="BackgroundSystem"/>의 활성 배경을 <see cref="BackgroundStage"/>에 이어 주는 얇은 바인더.
/// 활성 배경이 바뀌면 그 정의의 <see cref="ShopItemDefinition.backgroundPrefab"/>을 무대에 넣고, 없으면 null을 넣어 지운다.
///
/// 무대(<see cref="BackgroundStage"/>)는 상점을 모르고 배치만 책임진다. 상점 쪽 상태를 아는 것은 이 컴포넌트뿐이라,
/// 뷰포트 → 캐릭터 거주 영역을 잇는 <see cref="ViewportLivingAreaBinder"/>와 같은 자리다.
///
/// 부팅 복원은 <see cref="BackgroundSystem"/>이 이벤트를 쏘지 않으므로, Start에서 현재 값을 한 번 직접 읽는다.
/// 배경 프리팹 루트에 <see cref="BackgroundStage"/>와 함께 붙는다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(BackgroundStage))]
public sealed class BackgroundBinder : MonoBehaviour
{
    private BackgroundStage _stage;
    private BackgroundSystem _system;

    private void Awake() => _stage = GetComponent<BackgroundStage>();

    private void Start()
    {
        // BackgroundSystem은 실행 순서 -100이라 이 시점엔 Awake를 마쳤다.
        _system = BackgroundSystem.Instance;
        if (_system == null)
        {
            Debug.LogWarning($"[{nameof(BackgroundBinder)}] 씬에 {nameof(BackgroundSystem)}이 없어 배경을 연결하지 못했습니다.", this);
            enabled = false;
            return;
        }

        _system.ActiveBackgroundChanged += OnActiveBackgroundChanged;
        Apply();
    }

    private void OnDestroy()
    {
        if (_system != null) _system.ActiveBackgroundChanged -= OnActiveBackgroundChanged;
    }

    // 이벤트 인자(id)는 쓰지 않고 현재 상태를 다시 읽는다. 부팅 복원과 같은 경로를 타게 하기 위해서다.
    private void OnActiveBackgroundChanged(string _) => Apply();

    private void Apply()
    {
        var item = _system.ActiveBackground;
        _stage.SetPrefab(item != null ? item.backgroundPrefab : null);
    }
}
