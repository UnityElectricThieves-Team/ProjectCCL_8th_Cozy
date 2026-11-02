using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 유저 설정의 UI 크기를 화면에 건다. 설정을 들고 있지도 저장하지도 않는다 — <see cref="SettingsManager"/>를 읽어 적용만 한다.
///
/// 키우는 방법은 <b>대상 RectTransform의 localScale</b>이다(패널들의 공통 부모, 메뉴 바).
/// 캔버스의 CanvasScaler는 건드리지 않는다. 기준 해상도 폭이 월드의 배율과 묶여 있어서(viewport-coordinates.md),
/// 그걸 바꾸면 UI만이 아니라 월드와 UI의 맞물림까지 깨진다.
/// 대상마다 자기 피벗을 기준으로 커진다 — 패널 부모는 화면 가운데, 메뉴 바는 오른쪽 아래 모서리.
///
/// 키운 뒤에는 화면 밖으로 밀려난 패널을 안으로 당긴다(<see cref="DraggablePanel.ClampIntoCanvas"/>).
/// 그래픽 탭은 미리보기가 바로 보여야 하므로 저장값이 아니라 지금 값을 읽는다.
///
/// UIRoot에 붙이고, 인스펙터에서 대상(Panels, MenuBar)을 넣는다.
/// </summary>
[DisallowMultipleComponent]
public sealed class UIScaleApplier : MonoBehaviour
{
    [Tooltip("UI 크기를 걸 대상. 패널들의 공통 부모와 메뉴 바.")]
    [SerializeField] private RectTransform[] _targets;

    private SettingsManager _settings;
    private DraggablePanel[] _draggables;
    private float _applied = 1f;

    private void Start()
    {
        _settings = SettingsManager.Instance;
        if (_settings == null)
        {
            Debug.LogWarning($"[{nameof(UIScaleApplier)}] SettingsManager 없음 — UI 크기 설정을 적용하지 않습니다.", this);
            return;
        }

        // 패널은 씬에 미리 놓여 있고 실행 중에 늘지 않아 한 번만 모은다. 꺼진 패널도 포함한다.
        var found = new List<DraggablePanel>();
        if (_targets != null)
        {
            foreach (RectTransform target in _targets)
            {
                if (target != null) found.AddRange(target.GetComponentsInChildren<DraggablePanel>(true));
            }
        }
        _draggables = found.ToArray();

        Apply();
        _settings.GraphicsChanged += Apply;
    }

    private void OnDestroy()
    {
        if (_settings != null) _settings.GraphicsChanged -= Apply;
    }

    // GraphicsChanged는 다른 그래픽 값이 바뀔 때도 울리므로, 배율이 같으면 아무 일도 하지 않는다.
    private void Apply()
    {
        float scale = GraphicsScaleSteps.UI.ToScale(_settings.UIScaleStep);
        if (Mathf.Approximately(scale, _applied)) return;
        _applied = scale;

        if (_targets != null)
        {
            foreach (RectTransform target in _targets)
            {
                if (target != null) target.localScale = new Vector3(scale, scale, 1f);
            }
        }
        foreach (DraggablePanel draggable in _draggables) draggable.ClampIntoCanvas();
    }
}
