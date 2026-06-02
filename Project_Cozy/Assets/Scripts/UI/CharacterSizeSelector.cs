using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// 5개 버튼으로 <see cref="ScaleMultiplierSettings"/>의 Character 배수를 set. 추후 라디오바·슬라이더화 후보.
/// </summary>
public sealed class CharacterSizeSelector : MonoBehaviour
{
    [SerializeField] private ScaleMultiplierSettings _settings;
    [SerializeField] private Button[] _buttons;
    [SerializeField] private float[] _values = { 0.5f, 0.75f, 1.0f, 1.5f, 2.0f };

    private UnityAction[] _handlers;

    private void Awake()
    {
        if (_buttons == null || _values == null || _buttons.Length != _values.Length)
        {
            Debug.LogError($"[{nameof(CharacterSizeSelector)}] _buttons and _values must have the same length.", this);
            return;
        }
        _handlers = new UnityAction[_buttons.Length];
        for (int i = 0; i < _buttons.Length; i++)
        {
            if (_buttons[i] == null) continue;
            int captured = i;
            _handlers[i] = () =>
            {
                if (_settings != null) _settings.Character.Value = _values[captured];
            };
            _buttons[i].onClick.AddListener(_handlers[i]);
        }
    }

    private void OnDestroy()
    {
        if (_buttons == null || _handlers == null) return;
        for (int i = 0; i < _buttons.Length; i++)
            if (_buttons[i] != null && _handlers[i] != null)
                _buttons[i].onClick.RemoveListener(_handlers[i]);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (_buttons != null && _values != null && _buttons.Length != _values.Length)
            Debug.LogWarning($"[{nameof(CharacterSizeSelector)}] _buttons and _values length mismatch: {_buttons.Length} vs {_values.Length}.", this);
    }
#endif
}
