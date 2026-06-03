using TMPro;
using UnityEngine;

/// <summary>
/// <see cref="BaseCharacterController"/>의 현재 상태 이름과 친밀도를 캐릭터 옆 TextMeshPro 라벨에 두 줄로 표시.
/// 부모 계층에서 캐릭터를 자동 탐색하므로 prefab variant 안에 그대로 배치하면 동작.
/// 위치·스케일은 부모(ROOT) transform에 그대로 종속 — prefab에서 anchoredPosition·Pivot으로 배치를 결정한다.
/// </summary>
public sealed class CharacterStateLabel : MonoBehaviour
{
    [SerializeField] private BaseCharacterController _character;
    [SerializeField] private TMP_Text _label;

    private void Awake()
    {
        if (_label == null) _label = GetComponent<TMP_Text>();
        if (_character == null) _character = GetComponentInParent<BaseCharacterController>();
    }

    private void OnEnable()
    {
        if (_character == null || _label == null)
        {
            Debug.LogError($"[{nameof(CharacterStateLabel)}] BaseCharacterController 또는 TMP_Text 참조가 없습니다.", this);
            return;
        }
        _character.State.StateChanged += OnStateChanged;
        _character.Affinity.AffinityChanged += OnAffinityChanged;
        Refresh();
    }

    private void OnDisable()
    {
        if (_character == null) return;
        _character.State.StateChanged -= OnStateChanged;
        _character.Affinity.AffinityChanged -= OnAffinityChanged;
    }

    private void OnStateChanged(CharacterState _) => Refresh();
    private void OnAffinityChanged(int _) => Refresh();

    private void Refresh()
    {
        _label.text = $"State: {_character.State.CurrentStateName}\nAffinity: {_character.Affinity.Current}";
    }
}
