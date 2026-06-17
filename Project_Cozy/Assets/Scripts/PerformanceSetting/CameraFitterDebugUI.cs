// ============================================================
// CameraFitterDebugUI
//
// 브링업/디버그용 OnGUI 오버레이. CameraFitter의 화면 최상단 y(_maxY)를
// 런타임에 프리셋 버튼으로 바꾼다. 핵심 로직과 분리된 선택적 컴포넌트로,
// 릴리스에서는 빼거나 GameObject를 비활성화하면 된다.
//
// CameraFitter의 공개 API(MaxY / SetMaxY)만 사용한다.
// ============================================================
using UnityEngine;

public class CameraFitterDebugUI : MonoBehaviour
{
    [SerializeField] private CameraFitter _fitter;
    [SerializeField] private bool _show = true;
    [SerializeField] private float[] _presets = { 50f, 100f, 200f };

    private void Awake()
    {
        if (_fitter == null) _fitter = FindFirstObjectByType<CameraFitter>();
    }

    private void OnGUI()
    {
        if (!_show || _fitter == null) return;

        // WindowDebugOverlay(Rect(10, 10, ...))와 겹치지 않게 아래쪽에 배치.
        GUILayout.BeginArea(new Rect(10, 110, 260, 80));
        var box = new GUIStyle(GUI.skin.box) { fontSize = 12 };
        GUILayout.Box($"Max Y = {_fitter.MaxY:0.#}", box, GUILayout.Width(240));

        GUILayout.BeginHorizontal();
        foreach (var preset in _presets)
        {
            if (GUILayout.Button($"{preset:0.#}", GUILayout.Width(76)))
                _fitter.SetMaxY(preset);
        }
        GUILayout.EndHorizontal();
        GUILayout.EndArea();
    }
}
