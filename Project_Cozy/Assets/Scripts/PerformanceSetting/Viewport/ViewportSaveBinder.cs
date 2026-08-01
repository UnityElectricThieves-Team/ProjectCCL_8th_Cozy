using UnityEngine;

/// <summary>
/// 뷰포트를 파일에 저장하고 불러오는 유일한 지점.
///
/// <see cref="ViewportScreenSettings"/>는 "영속화를 모른다"는 계약을 갖고 ViewportSaved 이벤트를
/// 노출한다. 이 컴포넌트가 그 계약의 구현체다 — 정책 쪽에 파일 경로 지식이 새지 않게 분리해 둔다.
///
/// **창 rect는 저장하지 않는다.** 작업 영역에서 언제든 다시 유도할 수 있는 파생값이라 저장하면
/// 원본과 어긋날 위험만 생긴다(저장된 창이 지금 모니터보다 커서 조작 불가가 되는 식). 사용자가
/// 직접 정하는 원본은 뷰포트뿐이고, 그것만 저장한다.
/// 근거: Docs/Development/StaticWindowMigrationPlan.md.
///
/// 저장 시점은 값이 확정될 때 즉시 — HeartSystem·ShopSystem과 같은 골격이다.
/// SaveScheduler는 구현체가 0개라 등록하면 조용히 저장이 안 되므로 쓰지 않는다.
/// </summary>
[DisallowMultipleComponent]
public class ViewportSaveBinder : MonoBehaviour
{
    [SerializeField, Tooltip("비우면 같은 오브젝트에서 찾고, 없으면 씬에서 찾는다.")]
    private ViewportScreenSettings _settings;

    private void Awake()
    {
        if (_settings == null) _settings = GetComponent<ViewportScreenSettings>();
        if (_settings == null) _settings = FindFirstObjectByType<ViewportScreenSettings>();
        if (_settings == null)
        {
            Debug.LogError("[ViewportSaveBinder] ViewportScreenSettings 없음 — 뷰포트가 저장·복원되지 않습니다.");
            enabled = false;
            return;
        }

        Load();
        _settings.ViewportSaved += OnViewportSaved;
    }

    private void OnDestroy()
    {
        if (_settings != null) _settings.ViewportSaved -= OnViewportSaved;
    }

    /// <summary>
    /// Awake에서 부른다. ViewportScreenSettings.Start()는 코루틴이라 모든 Awake 뒤에 돌므로,
    /// 여기서 넣은 값을 그쪽이 베이스 공간에 맞춰 클램프한 뒤 적용한다.
    /// (아직 IsReady 전이라 SetViewport는 값만 담아둔다 — 그게 의도된 경로다.)
    /// </summary>
    private void Load()
    {
        ViewportFileFormat data = UserDataSaveIO.Load<ViewportFileFormat>(GameDataPaths.Viewport);

        // 크기가 0이면 첫 실행이거나 파일이 손상된 경우다(UserDataSaveIO가 손상 시 빈 값을 준다).
        // 주입하지 않고 넘기면 Start가 "베이스 공간 전체"라는 기본값으로 채운다 — 두 경우가 같은
        // 안전한 경로를 타게 된다.
        if (data.width <= 0 || data.height <= 0) return;

        _settings.SetViewport(new RectInt(data.x, data.y, data.width, data.height));
    }

    private void OnViewportSaved(RectInt viewport)
    {
        UserDataSaveIO.Save(GameDataPaths.Viewport, new ViewportFileFormat
        {
            x = viewport.x,
            y = viewport.y,
            width = viewport.width,
            height = viewport.height,
        });
    }
}
