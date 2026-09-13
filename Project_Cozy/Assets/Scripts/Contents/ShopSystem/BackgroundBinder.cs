using UnityEngine;

/// <summary>
/// <see cref="BackgroundSystem"/>의 활성 배경과 띠 높이를 <see cref="BackgroundStrip"/>에 이어 주는 얇은 바인더.
/// 활성 배경이 바뀌면 그 정의의 <see cref="ShopItemDefinition.backgroundSprite"/>를 띠에 넣고, 없으면 null을 넣어 숨긴다.
/// 높이는 시작할 때 한 번 넘긴다 — 런타임에 바뀌는 값이 아니다.
///
/// 띠(<see cref="BackgroundStrip"/>)는 상점을 모르고 기하만 책임진다. 상점 쪽 상태를 아는 것은 이 컴포넌트뿐이라,
/// 뷰포트 → 캐릭터 거주 영역을 잇는 <see cref="ViewportLivingAreaBinder"/>와 같은 자리다.
///
/// 부팅 복원은 <see cref="BackgroundSystem"/>이 이벤트를 쏘지 않으므로, Start에서 현재 값을 한 번 직접 읽는다.
/// 배경 프리팹 루트에 <see cref="BackgroundStrip"/>과 함께 붙는다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(BackgroundStrip))]
public sealed class BackgroundBinder : MonoBehaviour
{
    private BackgroundStrip _strip;
    private BackgroundSystem _system;

    private void Awake() => _strip = GetComponent<BackgroundStrip>();

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
        _strip.SetHeight(_system.HeightBasePx); // 높이의 소유자는 BackgroundSystem — 띠는 받기만 한다
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
        _strip.SetSprite(item != null ? item.backgroundSprite : null);
    }
}
