using UnityEngine;

public class PerformanceSettings : MonoBehaviour
{
    [Header("General")]
    [SerializeField] private bool _disableVSync = true;

    [Header("Frame Rate")]
    [SerializeField] private int _foregroundTargetFps = 60;
    [SerializeField] private int _backgroundTargetFps = 30;

    private void Awake()
    {
        Application.runInBackground = true;

        if (_disableVSync)
        {
            QualitySettings.vSyncCount = 0;
        }

        ApplyTargetFps(Application.isFocused);
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        ApplyTargetFps(hasFocus);
    }

    private void ApplyTargetFps(bool hasFocus)
    {
        var target = hasFocus ? _foregroundTargetFps : _backgroundTargetFps;
        Application.targetFrameRate = Mathf.Max(15, target);
    }
}
