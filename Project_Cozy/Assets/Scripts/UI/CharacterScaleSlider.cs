using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 이 슬라이더의 값을 캐릭터 크기 배수(<see cref="ScaleMultiplierSettings"/>의 Character)에 그대로 반영한다.
/// 슬라이더에 직접 붙는 자기완결 컴포넌트 — 별도의 패널 컨트롤러 없이 혼자 동작한다.
/// 열릴 때 현재 배수로 핸들 위치를 맞춰, 슬라이더가 항상 현재 크기를 나타낸다.
/// </summary>
[RequireComponent(typeof(Slider))]
public class CharacterScaleSlider : MonoBehaviour
{
    [SerializeField] private ScaleMultiplierSettings _settings;

    private void Awake()
    {
        var slider = GetComponent<Slider>();
        if (_settings == null) return;

        slider.SetValueWithoutNotify(_settings.Character.Value); // 현재 크기로 핸들 맞춤(콜백 없이)
        slider.onValueChanged.AddListener(v => _settings.Character.Value = v);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (_settings == null)
            Debug.LogWarning($"[{nameof(CharacterScaleSlider)}] _settings가 비어 있음.", this);
    }
#endif
}
