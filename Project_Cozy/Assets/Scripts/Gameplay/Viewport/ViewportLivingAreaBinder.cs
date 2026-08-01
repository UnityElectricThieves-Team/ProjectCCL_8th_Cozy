using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 확정 뷰포트를 "캐릭터가 살 수 있는 월드 영역"으로 환산해 살아있는 캐릭터 전부에 걸어준다.
/// 이 컴포넌트가 있어야 캐릭터가 뷰포트 밖으로 걸어나가거나 드래그로 끌려나가지 않는다.
///
/// 뷰포트를 아는 것은 이쪽뿐이다 — 캐릭터는 월드 Rect만 받고 그것이 무엇에서 왔는지 모른다
/// (Character/CLAUDE.md의 "캐릭터는 화면 정책을 모른다" 원칙).
///
/// 거는 시점이 둘이다.
///   - <c>ViewportApplied</c> — 뷰포트가 확정될 때. 이때 이미 밖에 있던 캐릭터는 안으로 끌려온다.
///   - <c>CharacterManager.Registered</c> — 그 뒤에 등록된 캐릭터. ViewportApplied는 등록 시점에
///     다시 오지 않으므로, 이게 없으면 나중에 들어온 캐릭터만 제한 없이 돌아다닌다.
///
/// **편집 중 프리뷰에는 반응하지 않는다.** 저장·취소로 확정된 순간에만 적용한다 —
/// 핸들을 끄는 동안 캐릭터가 따라 끌려다니면 조작이 어렵고, 기획서도 조정 중에는 밖으로 나간
/// 대상을 그대로 두라고 정하고 있다(UserSettings.md §2.1.1).
/// </summary>
[DisallowMultipleComponent]
public class ViewportLivingAreaBinder : MonoBehaviour
{
    [Header("협력자 (미할당 시 자동 탐색)")]
    [SerializeField] private ViewportScreenSettings _viewportSettings;
    [SerializeField] private BaseSpaceCameraFitter _cameraFitter;

    private Rect _area;
    private bool _hasArea;

    private void Start()
    {
        if (_viewportSettings == null) _viewportSettings = FindFirstObjectByType<ViewportScreenSettings>();
        if (_cameraFitter == null)     _cameraFitter     = FindFirstObjectByType<BaseSpaceCameraFitter>();

        if (_viewportSettings == null || _cameraFitter == null)
        {
            Debug.LogWarning("[ViewportLivingAreaBinder] ViewportScreenSettings/BaseSpaceCameraFitter 없음 — 비활성. " +
                             "캐릭터가 뷰포트 밖으로 나갈 수 있습니다.");
            enabled = false;
            return;
        }

        _viewportSettings.ViewportApplied += OnViewportApplied;
        if (CharacterManager.Instance != null) CharacterManager.Instance.Registered += OnCharacterRegistered;

        // 초기 적용이 이미 끝난 뒤에 이 컴포넌트가 붙었을 수 있다(ViewportApplied는 다시 오지 않는다).
        if (_viewportSettings.IsReady) OnViewportApplied(_viewportSettings.Viewport);
    }

    private void OnDestroy()
    {
        if (_viewportSettings != null) _viewportSettings.ViewportApplied -= OnViewportApplied;
        if (CharacterManager.Instance != null) CharacterManager.Instance.Registered -= OnCharacterRegistered;
    }

    private void OnViewportApplied(RectInt viewportPx)
    {
        _area = _cameraFitter.BaseRectToWorld(viewportPx, _viewportSettings.BaseSpaceSize);
        _hasArea = true;

        if (CharacterManager.Instance == null) return;

        IReadOnlyList<GameObject> alive = CharacterManager.Instance.Alive;
        for (int i = 0; i < alive.Count; i++) Apply(alive[i]);
    }

    private void OnCharacterRegistered(GameObject character)
    {
        if (_hasArea) Apply(character);
    }

    private void Apply(GameObject character)
    {
        if (character == null) return;
        if (character.TryGetComponent(out BaseCharacterController controller))
            controller.SetLivingArea(_area);
    }
}
