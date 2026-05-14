using UnityEngine;

public class PerformanceSettings : MonoBehaviour
{
    [Header("General")]
    [SerializeField] private bool disableVSync = true;

    [Header("Frame Rate")]
    [SerializeField] private int foregroundTargetFps = 60;
    [SerializeField] private int backgroundTargetFps = 30;

    private void Awake()
    {
        Application.runInBackground = true;

        if (disableVSync)
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
        var target = hasFocus ? foregroundTargetFps : backgroundTargetFps;
        Application.targetFrameRate = Mathf.Max(15, target);
    }
}
