using TMPro;
using UnityEngine;

/// <summary>
/// <see cref="BaseCharacterController.Name"/>을 캐릭터 머리 위 TextMeshPro 라벨에 표시.
/// 부모 계층에서 캐릭터를 자동 탐색하므로 prefab 안에 그대로 배치하면 동작한다.
/// 이름은 스폰 시 1회 정해지고 바뀌지 않으므로 <see cref="CharacterStateLabel"/>과 달리
/// 이벤트 구독 없이 Start에서 한 번만 읽어 표시한다.
/// 위치·정렬 순서는 prefab의 transform / TMP 설정으로 결정한다.
/// </summary>
public sealed class CharacterNameLabel : MonoBehaviour
{
    [SerializeField] private BaseCharacterController _character;
    [SerializeField] private TMP_Text _label;

    private void Start()
    {
        if (_label == null) _label = GetComponent<TMP_Text>();
        if (_character == null) _character = GetComponentInParent<BaseCharacterController>();

        if (_character == null || _label == null)
        {
            Debug.LogError($"[{nameof(CharacterNameLabel)}] BaseCharacterController 또는 TMP_Text 참조가 없습니다.", this);
            return;
        }

        // Awake에서 이름이 할당되므로(모든 Awake → 모든 Start 순서 보장) 여기서 읽으면 항상 준비됨.
        _label.text = _character.Name;
    }
}
